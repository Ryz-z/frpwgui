using System;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;

namespace FrpWin
{
    internal static class Program
    {
        private const string MutexName = "FrpWinManagerSingleInstance";
        private const string ActivateEventName = "FrpWinManagerActivateEvent";

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int nStdHandle);
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        private const int STD_OUTPUT_HANDLE = -11;

        /// <summary>
        /// 保证命令行输出可见：只有当标准输出还没有被重定向/继承时才去附加父进程控制台，
        /// 否则会把调用方重定向的文件句柄覆盖掉。
        /// </summary>
        private static void EnsureConsole()
        {
            try
            {
                IntPtr h = GetStdHandle(STD_OUTPUT_HANDLE);
                if (h == IntPtr.Zero || h == INVALID_HANDLE_VALUE)
                {
                    AttachConsole(ATTACH_PARENT_PROCESS);
                }

                // 统一用 UTF-8 输出，否则中文日志在控制台里会显示成乱码
                try { Console.OutputEncoding = new System.Text.UTF8Encoding(false); }
                catch { }
            }
            catch { }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            // ---- 以 Windows 服务方式运行：FrpWin.exe --service frps|frpc ----
            if (args != null && args.Length >= 2 &&
                string.Equals(args[0], "--service", StringComparison.OrdinalIgnoreCase))
            {
                RunAsService(args[1]);
                return;
            }

            // ---- 命令行生成配置文件：FrpWin.exe --gen-config ----
            if (args != null && args.Length >= 1 &&
                string.Equals(args[0], "--gen-config", StringComparison.OrdinalIgnoreCase))
            {
                Environment.Exit(GenerateConfig());
                return;
            }

            // ---- 命令行前台运行：FrpWin.exe --run frps|frpc ----
            if (args != null && args.Length >= 2 &&
                string.Equals(args[0], "--run", StringComparison.OrdinalIgnoreCase))
            {
                Environment.Exit(RunForeground(args[1]));
                return;
            }

            // ---- 界面布局自检：FrpWin.exe --dump-layout ----
            if (args != null && args.Length >= 1 &&
                string.Equals(args[0], "--dump-layout", StringComparison.OrdinalIgnoreCase))
            {
                Environment.Exit(DumpLayout());
                return;
            }

            // ---- 界面渲染成图片：FrpWin.exe --screenshot <png> [标签页序号] ----
            if (args != null && args.Length >= 2 &&
                string.Equals(args[0], "--screenshot", StringComparison.OrdinalIgnoreCase))
            {
                int tab = args.Length >= 3 ? int.Parse(args[2]) : 1;
                Environment.Exit(TakeScreenshot(args[1], tab));
                return;
            }

            // ---- 图形界面（带单实例保护）----
            bool createdNew;
            var mutex = new Mutex(true, MutexName, out createdNew);
            bool ownIt = createdNew;

            if (!createdNew)
            {
                // 上一个实例可能是异常退出后遗弃了互斥体，这里尝试接管
                try { ownIt = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { ownIt = true; }
            }

            if (!ownIt)
            {
                // 已经有实例在运行：请它把窗口显示出来，然后自己立刻退出
                SignalExistingInstance();
                return;
            }

            // 这两个调用必须在创建任何窗口/控件之前完成
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName))
            {
                var form = new MainForm();

                var watcher = new Thread(() =>
                {
                    while (true)
                    {
                        try
                        {
                            if (!activateEvent.WaitOne()) break;
                            if (form.IsDisposed || form.Disposing) break;
                            form.BeginInvoke(new Action(form.ActivateFromOtherInstance));
                        }
                        catch { break; }
                    }
                });
                watcher.IsBackground = true;
                watcher.Start();

                try
                {
                    Application.Run(form);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("程序发生未处理的错误：\r\n\r\n" + ex, "FrpWin 错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            GC.KeepAlive(mutex);
        }

        /// <summary>
        /// 由 Windows 服务控制管理器启动。如果被用户直接双击 / 命令行运行，
        /// 这里给出友好提示并退出，而不是让 .NET 弹出一个会卡住进程的对话框。
        /// Environment.UserInteractive 为 true 就说明不是 services.exe 拉起来的。
        /// </summary>
        private static void RunAsService(string role)
        {
            if (Environment.UserInteractive)
            {
                string tip = "FrpWin 服务宿主不能直接在命令行下运行。\r\n\r\n" +
                             "请通过 Windows 服务控制管理器启动它（服务名：FrpWinServer / FrpWinClient），" +
                             "或者直接双击 FrpWin.exe 使用图形界面来启停 frp。";

                EnsureConsole();
                Console.WriteLine(tip);
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetTempPath(), "FrpWin-service.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + role + " : " + tip.Replace("\r\n", " ") + Environment.NewLine);
                }
                catch { }
                Environment.Exit(1);
                return;
            }

            try
            {
                ServiceBase.Run(new FrpService(role));
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetTempPath(), "FrpWin-service.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + role + " 服务启动失败: " + ex + Environment.NewLine);
                }
                catch { }
                Environment.Exit(1);
            }
        }

        private static void SignalExistingInstance()        {
            try
            {
                EventWaitHandle ev;
                if (EventWaitHandle.TryOpenExisting(ActivateEventName, out ev))
                {
                    using (ev) { ev.Set(); }
                }
            }
            catch { }
        }

        /// <summary>
        /// 命令行前台运行 frps / frpc（不开图形界面），日志同时输出到控制台和服务日志文件。
        /// 与图形界面的“启动”按钮、以及 Windows 服务走的是同一套 FrpRunner 逻辑。
        /// </summary>
        private static int RunForeground(string role)
        {
            bool isServer = string.Equals(role, "frps", StringComparison.OrdinalIgnoreCase);
            string tag = isServer ? "frps" : "frpc";
            string exe = isServer ? AppPaths.FrpsExe : AppPaths.FrpcExe;
            string cfg = isServer ? AppPaths.FrpsConfig : AppPaths.FrpcConfig;

            EnsureConsole();

            if (!File.Exists(exe))
            {
                Console.WriteLine("找不到程序文件：" + exe);
                return 1;
            }

            if (!File.Exists(cfg))
            {
                try
                {
                    var settings = ConfigStore.Load();
                    string p1, p2;
                    ConfigStore.WriteToml(settings, out p1, out p2);
                    Console.WriteLine("配置文件不存在，已自动生成：" + cfg);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("配置文件不存在且自动生成失败：" + ex.Message);
                    return 1;
                }
            }

            using (var runner = new FrpRunner())
            {
                runner.Line += line => { try { Console.WriteLine(line); } catch { } };
                try
                {
                    runner.Start(tag, exe, cfg, AppPaths.InstallDir, AppPaths.ServiceLogFile(tag));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("启动 " + tag + " 失败：" + ex.Message);
                    return 1;
                }

                Console.WriteLine(tag + " 已启动 (PID " + runner.ProcessId + ")，配置文件：" + cfg);
                Console.WriteLine("按 Ctrl+C 结束。日志文件：" + AppPaths.ServiceLogFile(tag));

                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    try { runner.Stop(); } catch { }
                };

                runner.WaitForExit(-1);
            }

            return 0;
        }

        /// <summary>
        /// 界面布局自检：把窗口摆到屏幕外真实布局一次，然后打印所有控件的实际位置尺寸。
        /// 自动化测试据此判断按钮文字有没有被截断、按钮条有没有盖住表格。
        /// </summary>
        /// <summary>
        /// 把界面渲染成 PNG（窗口摆到屏幕外，不影响用户），用于人工/自动检查界面效果。
        /// </summary>
        private static int TakeScreenshot(string path, int tabIndex)
        {
            EnsureConsole();
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var form = new MainForm();
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new System.Drawing.Point(-4000, -4000);
                form.Show();
                Application.DoEvents();

                form.SelectTabForTest(tabIndex);
                System.Threading.Thread.Sleep(400);
                Application.DoEvents();

                using (var bmp = new System.Drawing.Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }

                Console.WriteLine("已保存截图: " + path + "  (" + form.Width + "x" + form.Height + ")");

                form.Close();
                form.Dispose();
                Application.DoEvents();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("截图失败: " + ex);
                return 1;
            }
        }

        private static int DumpLayout()
        {
            EnsureConsole();
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var form = new MainForm();
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new System.Drawing.Point(-4000, -4000);
                form.Show();
                Application.DoEvents();

                Console.WriteLine(form.DumpLayoutReport());

                form.Close();
                form.Dispose();
                Application.DoEvents();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("布局自检失败: " + ex);
                return 1;
            }
        }

        private static int GenerateConfig()        {
            try
            {
                var settings = ConfigStore.Load();
                string frpsPath, frpcPath;
                ConfigStore.WriteToml(settings, out frpsPath, out frpcPath);
                ConfigStore.Save(settings);

                EnsureConsole();
                Console.WriteLine("已生成服务端配置: " + frpsPath);
                Console.WriteLine("已生成客户端配置: " + frpcPath);
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    EnsureConsole();
                    Console.WriteLine("生成配置失败: " + ex.Message);
                }
                catch { }
                return 1;
            }
        }
    }
}

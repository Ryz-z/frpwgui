using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace FrpWin
{
    public sealed partial class MainForm : Form
    {
        private readonly AppSettings _settings;
        private readonly FrpRunner _serverRunner = new FrpRunner();
        private readonly FrpRunner _clientRunner = new FrpRunner();
        private readonly Timer _statusTimer = new Timer();

        // ---------- 服务端控件 ----------
        private TextBox _sBindAddr, _sBindPort, _sKcpPort, _sHttpPort, _sHttpsPort,
                        _sSubDomain, _sToken, _sDashPort, _sDashUser, _sDashPass;
        private ComboBox _sLogLevel;
        private TextBox _sLog;
        private Label _sStatusLabel, _sProcLabel;
        private Button _sBtnStart, _sBtnStop, _sBtnSvcInstall, _sBtnSvcRemove;

        // ---------- 客户端控件 ----------
        private TextBox _cAddr, _cPort, _cToken, _cAdminPort, _cAdminUser, _cAdminPass;
        private CheckBox _cTls, _cLoginFail;
        private ComboBox _cLogLevel;
        private DataGridView _gridProxies, _gridVisitors;
        private BindingList<ProxyItem> _proxies;
        private BindingList<VisitorItem> _visitors;
        private TextBox _cLog;
        private Label _cStatusLabel, _cProcLabel;
        private Button _cBtnStart, _cBtnStop, _cBtnSvcInstall, _cBtnSvcRemove;

        // ---------- 其它 ----------
        private NotifyIcon _tray;
        private CheckBox _chkTray;
        private bool _reallyExit;

        private static readonly string[] LogLevels = { "trace", "debug", "info", "warn", "error" };
        private static readonly string[] ProxyTypes = { "tcp", "udp", "http", "https", "stcp", "xtcp" };
        private static readonly string[] VisitorTypes = { "stcp", "xtcp" };

        public MainForm()
        {
            _settings = ConfigStore.Load();

            SuspendLayout();
            Text = "FrpWin 内网穿透管理器  v1.0  (frp 0.71.0 · Windows x64)";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 700);
            ClientSize = new Size(1180, 980);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }
            try { Icon = Icon.ExtractAssociatedIcon(AppPaths.SelfExe); } catch { }

            var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 8) };
            tabs.TabPages.Add(BuildServerTab());
            tabs.TabPages.Add(BuildClientTab());
            tabs.TabPages.Add(BuildAboutTab());
            Controls.Add(tabs);

            var status = new StatusStrip();
            var spring = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _chkTray = new CheckBox { Text = "关闭窗口时最小化到托盘", AutoSize = true };
            var host = new ToolStripControlHost(_chkTray) { AutoSize = true };
            status.Items.Add(spring);
            status.Items.Add(host);
            Controls.Add(status);

            _statusLabelCommon = spring;

            ResumeLayout(true);

            BuildTray();
            LoadSettingsIntoUi();
            HookRunnerLogs();

            _statusTimer.Interval = 2500;
            _statusTimer.Tick += (s, e) => RefreshStatus();
            _statusTimer.Start();

            FormClosing += OnFormClosing;
            RefreshStatus();

            AppendLog(_sLog, "FrpWin 已就绪。");
            AppendLog(_sLog, "程序目录: " + AppPaths.InstallDir);
            AppendLog(_sLog, "数据目录: " + AppPaths.DataDir + "  (配置文件与日志都在这里)");
            if (ServiceManager.IsAdministrator)
                AppendLog(_sLog, "当前已具有管理员权限，可以直接安装/卸载 Windows 服务。");
            else
                AppendLog(_sLog, "当前为普通用户权限；安装 Windows 服务时会提示以管理员身份重启。");
        }

        private ToolStripStatusLabel _statusLabelCommon;

        // =====================================================================
        //  服务端标签页
        // =====================================================================
        private TabPage BuildServerTab()
        {
            var page = new TabPage("服务端（frps · 部署在有公网的服务器）") { BackColor = SystemColors.Control, Padding = new Padding(8) };

            _sBindAddr = new TextBox();
            _sBindPort = new TextBox();
            _sKcpPort = new TextBox();
            _sHttpPort = new TextBox();
            _sHttpsPort = new TextBox();
            _sSubDomain = new TextBox();
            _sToken = new TextBox();
            _sDashPort = new TextBox();
            _sDashUser = new TextBox();
            _sDashPass = new TextBox();
            _sLogLevel = MakeCombo(LogLevels);
            _sLog = MakeLogBox();

            var form = new FormTable();
            form.Add("监听地址 bindAddr", _sBindAddr);
            form.Add("监听端口 bindPort", _sBindPort);
            form.Add("KCP 端口 kcpBindPort", _sKcpPort);
            form.Add("HTTP 虚拟主机端口", _sHttpPort);
            form.Add("HTTPS 虚拟主机端口", _sHttpsPort);
            form.Add("泛域名 subDomainHost", _sSubDomain);
            form.Add("认证令牌 token", _sToken);
            form.Add("Dashboard 端口", _sDashPort);
            form.Add("Dashboard 用户名", _sDashUser);
            form.Add("Dashboard 密码", _sDashPass);
            form.Add("日志级别 log.level", _sLogLevel);

            var hint = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(760, 0),
                ForeColor = Color.DimGray,
                Margin = new Padding(3, 2, 3, 8),
                Text = "说明：端口填 0 表示关闭该功能。token 必须与客户端完全一致；" +
                       "Dashboard 端口大于 0 时可用浏览器打开 http://服务器IP:端口 查看实时连接状态。"
            };
            form.AddSpan(hint);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(10, 4, 10, 8), WrapContents = true };
            _sBtnStart = MakeButton("保存并启动服务端", 150, true);
            _sBtnStop = MakeButton("停止服务端", 100, false);
            _sBtnSvcInstall = MakeButton("安装为 Windows 服务", 155, false);
            _sBtnSvcRemove = MakeButton("卸载 Windows 服务", 155, false);
            var sBtnOpenCfg = MakeButton("查看配置文件", 110, false);
            var sBtnOpenDir = MakeButton("打开数据目录", 110, false);
            var sBtnFirewall = MakeButton("放行防火墙端口", 130, false);
            _sBtnStart.Click += (s, e) => StartServer();
            _sBtnStop.Click += (s, e) => StopServer();
            _sBtnSvcInstall.Click += (s, e) => InstallService("frps", _sLog);
            _sBtnSvcRemove.Click += (s, e) => RemoveService("frps", _sLog);
            sBtnOpenCfg.Click += (s, e) => OpenFile(AppPaths.FrpsConfig);
            sBtnOpenDir.Click += (s, e) => OpenFolder(AppPaths.DataDir);
            sBtnFirewall.Click += (s, e) => OpenFirewallPorts();

            buttons.Controls.AddRange(new Control[] {
                _sBtnStart, _sBtnStop, _sBtnSvcInstall, _sBtnSvcRemove,
                sBtnOpenCfg, sBtnOpenDir, sBtnFirewall });

            _sStatusLabel = MakeStatusLabel();
            _sProcLabel = MakeStatusLabel();
            var info = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(10, 0, 10, 0), WrapContents = true };
            info.Controls.Add(_sStatusLabel);
            info.Controls.Add(_sProcLabel);

            page.Controls.Add(BuildPageLayout(form.Panel, buttons, info, _sLog));
            return page;
        }

        // =====================================================================
        //  关于 / 帮助标签页
        // =====================================================================
        private TabPage BuildAboutTab()
        {
            var page = new TabPage("使用说明 / 关于") { BackColor = SystemColors.Control, Padding = new Padding(8) };

            var box = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                BackColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 9.5F),
                Text = AboutText()
            };
            page.Controls.Add(box);
            return page;
        }

        private static string AboutText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("FrpWin —— Windows 专用 frp 内网穿透套装");
            sb.AppendLine("======================================================");
            sb.AppendLine();
            sb.AppendLine("【包含内容】");
            sb.AppendLine("  frps.exe    服务端程序，运行在具有公网 IP 的机器上（frp 官方 v0.71.0 源码编译，x64）");
            sb.AppendLine("  frpc.exe    客户端程序，运行在需要被访问的内网机器上");
            sb.AppendLine("  FrpWin.exe  本图形管理器，用于配置、启停、安装 Windows 服务");
            sb.AppendLine();
            sb.AppendLine("【五分钟上手】");
            sb.AppendLine("  第一步（服务器，有公网 IP 的机器）：");
            sb.AppendLine("    1. 打开 FrpWin，切到“服务端”标签页；");
            sb.AppendLine("    2. 确认监听端口（默认 7000）、设置一个 token（认证令牌）；");
            sb.AppendLine("    3. 点“保存并启动服务端”；");
            sb.AppendLine("    4. 点“放行防火墙端口”，把 7000 端口在防火墙中放行；");
            sb.AppendLine("    5. 如需长期运行，点“安装为 Windows 服务”，开机自动启动。");
            sb.AppendLine();
            sb.AppendLine("  第二步（内网机器）：");
            sb.AppendLine("    1. 打开 FrpWin，切到“客户端”标签页；");
            sb.AppendLine("    2. 填写服务器公网 IP 和端口，token 要与服务端完全一致；");
            sb.AppendLine("    3. 在下方代理表格里添加一条，例如：");
            sb.AppendLine("         名称 = 远程桌面   类型 = tcp   本地IP = 127.0.0.1");
            sb.AppendLine("         本地端口 = 3389   远程端口 = 13389");
            sb.AppendLine("    4. 点“保存并启动客户端”。");
            sb.AppendLine();
            sb.AppendLine("  第三步：在任何地方用“远程桌面”连接 服务器公网IP:13389 即可。");
            sb.AppendLine();
            sb.AppendLine("【代理类型说明】");
            sb.AppendLine("  tcp      通用 TCP 转发（远程桌面、SSH、数据库、游戏联机等）");
            sb.AppendLine("  udp      通用 UDP 转发（DNS、部分游戏）");
            sb.AppendLine("  http     网站穿透，用域名访问，需要服务端配置 vhostHTTPPort");
            sb.AppendLine("  https    加密网站穿透，需要服务端配置 vhostHTTPSPort");
            sb.AppendLine("  stcp     安全点对点，不暴露公网端口，需要访客端配合（访客表格）");
            sb.AppendLine("  xtcp     点对点直连，速度快，NAT 类型合适时流量不经服务器中转");
            sb.AppendLine();
            sb.AppendLine("【端口与安全建议】");
            sb.AppendLine("  · 一定要设置足够复杂的 token，否则任何人都能连上你的服务端；");
            sb.AppendLine("  · frps 的 7000 端口务必在防火墙里只对可信来源开放；");
            sb.AppendLine("  · Dashboard 的默认密码 admin 请务必修改。");
            sb.AppendLine();
            sb.AppendLine("【数据位置】");
            sb.AppendLine("  " + AppPaths.DataDir);
            sb.AppendLine("  其中 frps.toml / frpc.toml 是自动生成的 frp 配置文件，");
            sb.AppendLine("  logs 目录下是运行日志，ui-settings.xml 保存本界面的设置。");
            sb.AppendLine();
            sb.AppendLine("【开源协议】");
            sb.AppendLine("  frp 由 fatedier 开发，遵循 Apache-2.0 协议，项目地址 https://github.com/fatedier/frp");
            sb.AppendLine("  本管理器为在其之上编写的 Windows 图形外壳，同样遵循 Apache-2.0 协议。");
            return sb.ToString();
        }

        // =====================================================================
        //  布局辅助
        // =====================================================================
        private sealed class FormTable
        {
            private readonly TableLayoutPanel _t;
            public FormTable()
            {
                _t = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(12, 12, 12, 6)
                };
                _t.ColumnCount = 2;
                _t.ColumnStyles.Clear();
                _t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 235F));
                _t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            }
            public TableLayoutPanel Panel { get { return _t; } }

            public void Add(string label, Control c)
            {
                _t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _t.RowCount = _t.RowStyles.Count;
                int r = _t.RowCount - 1;

                var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 8, 3) };
                c.Anchor = AnchorStyles.Left;
                if (c is TextBox || c is ComboBox) c.Width = 280;

                _t.Controls.Add(l, 0, r);
                _t.Controls.Add(c, 1, r);
            }

            public void AddSpan(Control c)
            {
                _t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _t.RowCount = _t.RowStyles.Count;
                int r = _t.RowCount - 1;
                c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                _t.Controls.Add(c, 0, r);
                _t.SetColumnSpan(c, 2);
            }
        }

        private static Label FieldLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 8, 3) };
        }

        /// <summary>
        /// 两列字段布局：一行放两组「标签 + 输入框」，用来把连接设置的高度压掉一半。
        /// </summary>
        private sealed class FormTable2
        {
            private readonly TableLayoutPanel _t;

            public FormTable2()
            {
                _t = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(12, 12, 12, 6)
                };
                _t.ColumnStyles.Clear();
                _t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
                _t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                _t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
                _t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                _t.ColumnCount = _t.ColumnStyles.Count;
            }

            public TableLayoutPanel Panel { get { return _t; } }

            public void Add(string l1, Control c1, string l2, Control c2)
            {
                _t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _t.RowCount = _t.RowStyles.Count;
                int r = _t.RowCount - 1;

                _t.Controls.Add(FieldLabel(l1), 0, r);
                Prepare(c1);
                _t.Controls.Add(c1, 1, r);

                _t.Controls.Add(FieldLabel(l2), 2, r);
                if (c2 != null)
                {
                    Prepare(c2);
                    _t.Controls.Add(c2, 3, r);
                }
            }

            public void AddSpan(Control c)
            {
                _t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _t.RowCount = _t.RowStyles.Count;
                int r = _t.RowCount - 1;
                c.Anchor = AnchorStyles.Left;
                _t.Controls.Add(c, 0, r);
                _t.SetColumnSpan(c, 4);
            }

            private static void Prepare(Control c)
            {
                c.Anchor = AnchorStyles.Left;
                if (c is TextBox || c is ComboBox) c.Width = 190;
            }
        }

        /// <summary>
        /// GroupBox 开了 AutoSize 之后，会把子控件的“首选高度”算大一圈，
        /// 白白多占几十个像素。这里按子控件真实布局出来的高度把外框收紧，
        /// 省下来的高度全部留给下面的表格。
        /// </summary>
        private static void TightenGroupBoxes(Control root)
        {
            foreach (Control c in root.Controls)
            {
                var box = c as GroupBox;
                if (box != null && box.AutoSize)
                {
                    Control topChild = null;
                    foreach (Control ch in box.Controls)
                    {
                        if (ch.Dock == DockStyle.Top) { topChild = ch; break; }
                    }
                    if (topChild != null)
                    {
                        int chrome = box.Height - box.ClientSize.Height;   // 标题栏等外框高度
                        int want = topChild.Bottom + box.Padding.Bottom + chrome;
                        if (want > 0 && want < box.Height)
                        {
                            box.AutoSize = false;
                            box.Height = want;
                        }
                    }
                }
                if (c.HasChildren) TightenGroupBoxes(c);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            TightenGroupBoxes(this);
        }

        private static Control BuildPageLayout(Control settingsContent, Control buttons, Control info, TextBox log)        {
            var scroll = new Panel { Name = "scrollArea", Dock = DockStyle.Fill, AutoScroll = true, BackColor = SystemColors.Control };
            scroll.Controls.Add(settingsContent);

            var top = new TableLayoutPanel { Name = "pageTop", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            top.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            top.Controls.Add(scroll, 0, 0);
            top.Controls.Add(buttons, 0, 1);
            top.Controls.Add(info, 0, 2);

            var logBox = new GroupBox { Text = "运行日志（实时）", Dock = DockStyle.Fill, Padding = new Padding(6) };
            log.Dock = DockStyle.Fill;
            logBox.Controls.Add(log);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105F));
            root.Controls.Add(top, 0, 0);
            root.Controls.Add(logBox, 0, 1);
            return root;
        }

        private static TextBox MakeLogBox()
        {
            return new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BackColor = Color.FromArgb(250, 250, 250),
                Font = new Font("Consolas", 8.5F),
                BorderStyle = BorderStyle.None
            };
        }

        private static ComboBox MakeCombo(string[] items)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
            c.Items.AddRange(items);
            c.SelectedIndex = 0;
            return c;
        }

        private static Button MakeButton(string text, int minWidth, bool primary)
        {
            // 用 AutoSize 而不是固定宽度，否则不同 DPI / 字体下按钮文字会被截断
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(minWidth, 32),
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(3, 3, 6, 3),
                UseVisualStyleBackColor = true
            };
            if (primary) b.Font = new Font(b.Font, FontStyle.Bold);
            return b;
        }

        private static Label MakeStatusLabel()
        {
            return new Label { AutoSize = true, Margin = new Padding(3, 4, 18, 3), Text = "" };
        }

        // =====================================================================
        //  托盘
        // =====================================================================
        private void BuildTray()
        {
            _tray = new NotifyIcon();
            try { _tray.Icon = Icon.ExtractAssociatedIcon(AppPaths.SelfExe); } catch { _tray.Icon = SystemIcons.Application; }
            _tray.Text = "FrpWin 内网穿透管理器";
            _tray.Visible = false;

            var menu = new ContextMenuStrip();
            menu.Items.Add("显示主界面", null, (s, e) => RestoreFromTray());
            menu.Items.Add("打开数据目录", null, (s, e) => OpenFolder(AppPaths.DataDir));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) =>
            {
                _reallyExit = true;
                Close();
            });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            _tray.Visible = false;
        }

        /// <summary>第二个实例启动时调用，把已经运行的窗口拉到前台。</summary>
        public void ActivateFromOtherInstance()
        {
            try
            {
                RestoreFromTray();
                BringToFront();
                TopMost = true;
                TopMost = false;
                Activate();
            }
            catch { }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_reallyExit && _chkTray.Checked && (_serverRunner.IsRunning || _clientRunner.IsRunning))
            {
                e.Cancel = true;
                Hide();
                _tray.Visible = true;
                _tray.ShowBalloonTip(2000, "FrpWin 仍在后台运行",
                    "frp 进程继续运行中。双击托盘图标可重新打开窗口。", ToolTipIcon.Info);
                return;
            }

            _statusTimer.Stop();
            _serverRunner.Dispose();
            _clientRunner.Dispose();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        }

        // =====================================================================
        //  通用动作
        // =====================================================================
        private void HookRunnerLogs()
        {
            _serverRunner.Line += line => Post(_sLog, line);
            _clientRunner.Line += line => Post(_cLog, line);
        }

        private void Post(TextBox box, string line)
        {
            if (box == null || box.IsDisposed) return;
            try
            {
                if (box.InvokeRequired) box.BeginInvoke(new Action<TextBox, string>(Post), box, line);
                else AppendLog(box, line);
            }
            catch { }
        }

        internal static void AppendLog(TextBox box, string line)
        {
            if (box == null) return;
            try
            {
                if (box.Lines.Length > 1200)
                {
                    var keep = new string[600];
                    Array.Copy(box.Lines, box.Lines.Length - 600, keep, 0, 600);
                    box.Lines = keep;
                }
                box.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + line + Environment.NewLine);
                box.SelectionStart = box.TextLength;
                box.ScrollToCaret();
            }
            catch { }
        }

        private static void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) { Warn("无法打开目录：" + ex.Message); }
        }

        private static void OpenFile(string path)
        {
            try
            {
                if (!File.Exists(path)) { Warn("文件还不存在，请先保存配置：\r\n" + path); return; }
                Process.Start(new ProcessStartInfo("notepad.exe", "\"" + path + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) { Warn("无法打开文件：" + ex.Message); }
        }

        private void OpenFirewallPorts()
        {
            var ports = new List<int>();
            ports.Add(ParsePortOr(_sBindPort.Text, 7000));
            int kcp = ParsePortOr(_sKcpPort.Text, 0); if (kcp > 0) ports.Add(kcp);
            int http = ParsePortOr(_sHttpPort.Text, 0); if (http > 0) ports.Add(http);
            int https = ParsePortOr(_sHttpsPort.Text, 0); if (https > 0) ports.Add(https);
            int dash = ParsePortOr(_sDashPort.Text, 0); if (dash > 0) ports.Add(dash);

            var rules = new List<string>();
            foreach (var p in ports)
            {
                rules.Add("advfirewall firewall add rule name=\"FrpWin TCP " + p + "\" dir=in action=allow protocol=TCP localport=" + p);
            }
            foreach (var p in ports)
            {
                rules.Add("advfirewall firewall add rule name=\"FrpWin UDP " + p + "\" dir=in action=allow protocol=UDP localport=" + p);
            }

            if (!EnsureAdmin()) return;

            int ok = 0, fail = 0;
            foreach (var rule in rules)
            {
                try
                {
                    var psi = new ProcessStartInfo("netsh.exe", rule)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        p.StandardOutput.ReadToEnd();
                        p.StandardError.ReadToEnd();
                        p.WaitForExit(15000);
                        if (p.ExitCode == 0) ok++; else fail++;
                    }
                }
                catch { fail++; }
            }

            AppendLog(_sLog, "防火墙规则处理完成：成功 " + ok + " 条，失败 " + fail + " 条。端口：" + string.Join(", ", ports.ConvertAll(x => x.ToString()).ToArray()));
            Info("已在 Windows 防火墙中放行以下端口：\r\n\r\n" + string.Join(", ", ports.ConvertAll(x => x.ToString()).ToArray()) +
                 "\r\n\r\n成功 " + ok + " 条规则" + (fail > 0 ? "，失败 " + fail + " 条（可能已存在同名规则）" : "") + "。");
        }

        private bool EnsureAdmin()
        {
            if (ServiceManager.IsAdministrator) return true;

            var r = MessageBox.Show(
                "该操作需要管理员权限。\r\n\r\n是否以管理员身份重新启动 FrpWin？\r\n（重启后请重新执行刚才的操作）",
                "需要管理员权限", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r == DialogResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(AppPaths.SelfExe) { UseShellExecute = true, Verb = "runas" });
                    _reallyExit = true;
                    Close();
                }
                catch { Warn("无法请求管理员权限，请手工右键 FrpWin.exe 选择“以管理员身份运行”。"); }
            }
            return false;
        }

        private void InstallService(string role, TextBox log)
        {
            if (!EnsureAdmin()) return;
            if (!SaveAll(role)) return;

            string msg;
            bool ok;
            msg = ServiceManager.Install(role, out ok);
            AppendLog(log, msg);
            if (!ok) Warn(msg); else Info(msg);
            RefreshStatus();
        }

        private void RemoveService(string role, TextBox log)
        {
            if (!EnsureAdmin()) return;

            string msg;
            bool ok;
            msg = ServiceManager.Uninstall(role, out ok);
            AppendLog(log, msg);
            if (!ok) Warn(msg); else Info(msg);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            string srvName = ServiceManager.ServerServiceName;
            string cliName = ServiceManager.ClientServiceName;

            string srvSvc = ServiceManager.QueryState(srvName);
            string cliSvc = ServiceManager.QueryState(cliName);

            bool srvRun = _serverRunner.IsRunning;
            bool cliRun = _clientRunner.IsRunning;

            _sStatusLabel.Text = "服务状态：" + srvSvc + "   |   界面进程：" + (srvRun ? "运行中 (PID " + _serverRunner.ProcessId + ")" : "未运行");
            _sStatusLabel.ForeColor = srvRun || srvSvc == "RUNNING" ? Color.FromArgb(0, 120, 0) : Color.DimGray;

            _cStatusLabel.Text = "服务状态：" + cliSvc + "   |   界面进程：" + (cliRun ? "运行中 (PID " + _clientRunner.ProcessId + ")" : "未运行");
            _cStatusLabel.ForeColor = cliRun || cliSvc == "RUNNING" ? Color.FromArgb(0, 120, 0) : Color.DimGray;

            _sBtnStart.Enabled = !srvRun;
            _sBtnStop.Enabled = srvRun;
            _cBtnStart.Enabled = !cliRun;
            _cBtnStop.Enabled = cliRun;

            if (_statusLabelCommon != null)
            {
                _statusLabelCommon.Text = "frps 服务：" + srvSvc + "    frpc 服务：" + cliSvc +
                                          "    数据目录：" + AppPaths.DataDir;
            }

            _sProcLabel.Text = File.Exists(AppPaths.FrpsExe) ? "" : "⚠ 未找到 frps.exe，请重新安装。";
            _cProcLabel.Text = File.Exists(AppPaths.FrpcExe) ? "" : "⚠ 未找到 frpc.exe，请重新安装。";
            _sProcLabel.ForeColor = Color.Firebrick;
            _cProcLabel.ForeColor = Color.Firebrick;
        }

        // =====================================================================
        //  校验辅助
        // =====================================================================
        private static int ParsePortOr(string text, int fallback)
        {
            int v;
            return int.TryParse((text ?? "").Trim(), out v) ? v : fallback;
        }

        private static bool ReadPort(TextBox box, string name, int min, int max, out int value)
        {
            value = 0;
            string raw = (box.Text ?? "").Trim();
            if (!int.TryParse(raw, out value))
            {
                Warn(name + " 必须是数字，当前填写的是：“" + raw + "”");
                box.Focus();
                box.SelectAll();
                return false;
            }
            if (value < min || value > max)
            {
                Warn(name + " 必须在 " + min + " ~ " + max + " 之间，当前为 " + value + "。");
                box.Focus();
                box.SelectAll();
                return false;
            }
            return true;
        }

        internal static void Warn(string msg)
        {
            MessageBox.Show(msg, "FrpWin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        internal static void Info(string msg)
        {
            MessageBox.Show(msg, "FrpWin", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // =====================================================================
        //  界面自检：把所有控件的实际布局尺寸打印出来，
        //  供自动化测试断言“按钮文字没被截断”“按钮条没盖住表格”。
        // =====================================================================
        public string DumpLayoutReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("FORM|" + ClientSize.Width + "|" + ClientSize.Height);
            DumpControls(this, this, sb);
            return sb.ToString();
        }

        /// <summary>供自检使用：切换到指定标签页。</summary>
        public void SelectTabForTest(int index)
        {
            foreach (Control c in Controls)
            {
                var tabs = c as TabControl;
                if (tabs != null && index >= 0 && index < tabs.TabPages.Count)
                {
                    tabs.SelectedIndex = index;
                    return;
                }
            }
        }

        private static void DumpControls(Control root, Control parent, StringBuilder sb)
        {
            foreach (Control c in parent.Controls)
            {
                Rectangle r = root.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
                string text = (c.Text ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
                string name = string.IsNullOrEmpty(c.Name) ? "-" : c.Name;

                if (c is Button)
                {
                    // 按钮文字需要的宽度 vs 实际宽度，判断有没有被截断
                    int need = TextRenderer.MeasureText(text, c.Font).Width + c.Padding.Horizontal;
                    sb.AppendLine("BTN|" + name + "|" + text + "|" + r.Left + "|" + r.Top + "|" + r.Width + "|" + r.Height + "|" + need);
                }
                else if ((c is Label || c is CheckBox) && text.Length > 0)
                {
                    // 实际高度 > 单行高度 说明标签文字被挤成了两行
                    int single = TextRenderer.MeasureText("测", c.Font).Height + c.Padding.Vertical + 4;
                    sb.AppendLine("LBL|" + text + "|" + r.Width + "|" + r.Height + "|" + single);
                }
                else if (!string.IsNullOrEmpty(c.Name))
                {
                    // 字段：CTL|Type|Name|Left|Top|Width|Height
                    sb.AppendLine("CTL|" + c.GetType().Name + "|" + name + "|" + r.Left + "|" + r.Top + "|" + r.Width + "|" + r.Height);
                }

                if (c is DataGridView)
                {
                    // 表格：表头高度 / 行高 是否够放下当前字体的文字（防止文字被纵向截断）
                    var gv = (DataGridView)c;
                    sb.AppendLine("GRD|" + name
                        + "|" + gv.ColumnHeadersHeight
                        + "|" + gv.RowTemplate.Height
                        + "|" + LineHeight(gv.Font)
                        + "|" + gv.Font.Name + " " + gv.Font.SizeInPoints);
                }

                if (c.HasChildren) DumpControls(root, c, sb);
            }
        }
    }
}

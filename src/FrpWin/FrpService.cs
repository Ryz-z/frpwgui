using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;

namespace FrpWin
{
    /// <summary>
    /// Windows 服务宿主：以 Windows 服务身份运行时，由它把 frps.exe / frpc.exe
    /// 作为子进程拉起并守护，输出写入 ProgramData\FrpWin\logs。
    /// </summary>
    internal sealed class FrpService : ServiceBase
    {
        private readonly string _role;
        private FrpRunner _runner;

        public FrpService(string role)
        {
            _role = string.Equals(role, "frps", StringComparison.OrdinalIgnoreCase) ? "frps" : "frpc";
            ServiceName = ServiceManager.ServiceNameFor(_role);
            CanStop = true;
            CanShutdown = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            string exe = _role == "frps" ? AppPaths.FrpsExe : AppPaths.FrpcExe;
            string cfg = _role == "frps" ? AppPaths.FrpsConfig : AppPaths.FrpcConfig;

            if (!File.Exists(exe))
                throw new FileNotFoundException("服务无法启动，缺少程序文件：" + exe);

            // 配置文件丢失时，用界面保存过的设置自动补一份，避免服务起不来
            if (!File.Exists(cfg))
            {
                try
                {
                    var settings = ConfigStore.Load();
                    string p1, p2;
                    ConfigStore.WriteToml(settings, out p1, out p2);
                    EventLog.WriteEntry(ServiceName, "配置文件不存在，已根据界面设置自动生成：" + cfg,
                        EventLogEntryType.Warning);
                }
                catch (Exception ex)
                {
                    throw new FileNotFoundException("服务无法启动，缺少配置文件且自动生成失败：" + cfg, ex);
                }
            }

            _runner = new FrpRunner();
            _runner.Start(_role, exe, cfg, AppPaths.InstallDir, AppPaths.ServiceLogFile(_role));
            EventLog.WriteEntry(ServiceName, "已启动 " + Path.GetFileName(exe) + "，配置：" + cfg, EventLogEntryType.Information);
        }

        protected override void OnStop()
        {
            if (_runner != null)
            {
                _runner.Stop();
                _runner.Dispose();
                _runner = null;
            }
            EventLog.WriteEntry(ServiceName, "服务已停止。", EventLogEntryType.Information);
        }

        protected override void OnShutdown()
        {
            OnStop();
            base.OnShutdown();
        }
    }
}

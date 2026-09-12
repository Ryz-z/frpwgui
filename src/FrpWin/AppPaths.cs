using System;
using System.IO;
using System.Reflection;

namespace FrpWin
{
    /// <summary>集中管理程序目录、数据目录与各类文件路径。</summary>
    internal static class AppPaths
    {
        /// <summary>程序安装目录（FrpWin.exe 所在目录）。</summary>
        public static string InstallDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); }
        }

        public static string SelfExe
        {
            get { return Assembly.GetEntryAssembly().Location; }
        }

        public static string FrpsExe { get { return Path.Combine(InstallDir, "frps.exe"); } }
        public static string FrpcExe { get { return Path.Combine(InstallDir, "frpc.exe"); } }

        private static string _dataDir;

        /// <summary>数据目录：优先 C:\ProgramData\FrpWin，不可写时回退到当前用户目录。</summary>
        public static string DataDir
        {
            get
            {
                if (_dataDir != null) return _dataDir;

                string machine = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FrpWin");
                if (TryEnsureDir(machine)) { _dataDir = machine; return _dataDir; }

                string user = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrpWin");
                TryEnsureDir(user);
                _dataDir = user;
                return _dataDir;
            }
        }

        private static bool TryEnsureDir(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                // 真正验证可写性
                string probe = Path.Combine(path, ".write-probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        public static string SettingsFile { get { return Path.Combine(DataDir, "ui-settings.xml"); } }
        public static string FrpsConfig { get { return Path.Combine(DataDir, "frps.toml"); } }
        public static string FrpcConfig { get { return Path.Combine(DataDir, "frpc.toml"); } }

        public static string LogDir
        {
            get
            {
                string d = Path.Combine(DataDir, "logs");
                try { Directory.CreateDirectory(d); } catch { }
                return d;
            }
        }

        public static string ServiceLogFile(string role) { return Path.Combine(LogDir, role + ".service.log"); }
    }
}

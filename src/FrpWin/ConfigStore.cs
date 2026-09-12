using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace FrpWin
{
    /// <summary>界面设置的读写，以及把设置渲染成 frp 的 TOML 配置文件。</summary>
    internal static class ConfigStore
    {
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(AppPaths.SettingsFile))
                {
                    var ser = new XmlSerializer(typeof(AppSettings));
                    using (var fs = File.OpenRead(AppPaths.SettingsFile))
                    {
                        var s = (AppSettings)ser.Deserialize(fs);
                        if (s != null)
                        {
                            if (s.Server == null) s.Server = new ServerSettings();
                            if (s.Client == null) s.Client = new ClientSettings();
                            if (s.Client.Proxies == null) s.Client.Proxies = new System.Collections.Generic.List<ProxyItem>();
                            if (s.Client.Visitors == null) s.Client.Visitors = new System.Collections.Generic.List<VisitorItem>();
                            return s;
                        }
                    }
                }
            }
            catch { /* 设置损坏时回退到默认值 */ }

            return CreateDefault();
        }

        private static AppSettings CreateDefault()
        {
            var s = new AppSettings();
            s.Server.Token = RandomToken();

            s.Client.ServerAddr = "127.0.0.1";
            s.Client.ServerPort = 7000;
            s.Client.Token = s.Server.Token;
            s.Client.Proxies.Add(new ProxyItem
            {
                Name = "远程桌面",
                Type = "tcp",
                LocalIP = "127.0.0.1",
                LocalPort = 3389,
                RemotePort = 13389
            });
            return s;
        }

        public static string RandomToken()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var rnd = new Random(Guid.NewGuid().GetHashCode());
            var sb = new StringBuilder(16);
            for (int i = 0; i < 16; i++) sb.Append(chars[rnd.Next(chars.Length)]);
            return sb.ToString();
        }

        public static void Save(AppSettings s)
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            var ser = new XmlSerializer(typeof(AppSettings));
            using (var fs = File.Create(AppPaths.SettingsFile))
            {
                ser.Serialize(fs, s);
            }
        }

        /// <summary>写出 frps.toml 与 frpc.toml，返回两个路径。</summary>
        public static void WriteToml(AppSettings s, out string frpsPath, out string frpcPath)
        {
            Directory.CreateDirectory(AppPaths.DataDir);

            frpsPath = AppPaths.FrpsConfig;
            frpcPath = AppPaths.FrpcConfig;

            File.WriteAllText(frpsPath, Toml.BuildFrps(s.Server), new UTF8Encoding(false));
            File.WriteAllText(frpcPath, Toml.BuildFrpc(s.Client), new UTF8Encoding(false));
        }
    }
}

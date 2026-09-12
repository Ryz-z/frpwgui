using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace FrpWin
{
    /// <summary>frpc 代理条目。</summary>
    public class ProxyItem
    {
        public string Name { get; set; }
        public string Type { get; set; }          // tcp / udp / http / https / stcp / xtcp
        public string LocalIP { get; set; }
        public int LocalPort { get; set; }
        public int RemotePort { get; set; }
        public string CustomDomains { get; set; } // 逗号分隔（http/https）
        public string Subdomain { get; set; }     // http/https
        public string SecretKey { get; set; }     // stcp/xtcp
        public bool UseEncryption { get; set; }
        public bool UseCompression { get; set; }

        public ProxyItem()
        {
            Name = "proxy1";
            Type = "tcp";
            LocalIP = "127.0.0.1";
            LocalPort = 80;
            RemotePort = 6000;
            CustomDomains = "";
            Subdomain = "";
            SecretKey = "";
        }

        public ProxyItem Clone()
        {
            return (ProxyItem)MemberwiseClone();
        }
    }

    /// <summary>frpc 访问者条目（用于点对点 / 密钥穿透）。</summary>
    public class VisitorItem
    {
        public string Name { get; set; }
        public string Type { get; set; }          // stcp / xtcp
        public string ServerName { get; set; }
        public string SecretKey { get; set; }
        public string BindAddr { get; set; }
        public int BindPort { get; set; }

        public VisitorItem()
        {
            Name = "visitor1";
            Type = "stcp";
            ServerName = "";
            SecretKey = "";
            BindAddr = "127.0.0.1";
            BindPort = 6000;
        }

        public VisitorItem Clone()
        {
            return (VisitorItem)MemberwiseClone();
        }
    }

    /// <summary>服务端（frps）设置。</summary>
    public class ServerSettings
    {
        public string BindAddr { get; set; }
        public int BindPort { get; set; }
        public int KcpBindPort { get; set; }      // 0 表示关闭
        public int VhostHttpPort { get; set; }    // 0 表示关闭
        public int VhostHttpsPort { get; set; }   // 0 表示关闭
        public string SubDomainHost { get; set; }
        public string Token { get; set; }
        public int DashboardPort { get; set; }    // 0 表示关闭 Dashboard
        public string DashboardUser { get; set; }
        public string DashboardPassword { get; set; }
        public string LogLevel { get; set; }
        public int LogMaxDays { get; set; }

        public ServerSettings()
        {
            BindAddr = "0.0.0.0";
            BindPort = 7000;
            KcpBindPort = 0;
            VhostHttpPort = 0;
            VhostHttpsPort = 0;
            SubDomainHost = "";
            Token = "";
            DashboardPort = 7500;
            DashboardUser = "admin";
            DashboardPassword = "admin";
            LogLevel = "info";
            LogMaxDays = 3;
        }
    }

    /// <summary>客户端（frpc）设置。</summary>
    public class ClientSettings
    {
        public string ServerAddr { get; set; }
        public int ServerPort { get; set; }
        public string Token { get; set; }
        public bool TlsEnable { get; set; }
        public string LogLevel { get; set; }
        public bool LoginFailExit { get; set; }
        public int AdminPort { get; set; }        // frpc 自带管理界面，0 表示关闭
        public string AdminUser { get; set; }
        public string AdminPassword { get; set; }

        [XmlArrayItem("Proxy")]
        public List<ProxyItem> Proxies { get; set; }

        [XmlArrayItem("Visitor")]
        public List<VisitorItem> Visitors { get; set; }

        public ClientSettings()
        {
            ServerAddr = "127.0.0.1";
            ServerPort = 7000;
            Token = "";
            TlsEnable = true;
            LogLevel = "info";
            LoginFailExit = false;
            AdminPort = 0;
            AdminUser = "admin";
            AdminPassword = "admin";
            Proxies = new List<ProxyItem>();
            Visitors = new List<VisitorItem>();
        }
    }

    /// <summary>全部界面设置，序列化到 ui-settings.xml。</summary>
    [XmlRoot("FrpWinSettings")]
    public class AppSettings
    {
        public ServerSettings Server { get; set; }
        public ClientSettings Client { get; set; }

        public AppSettings()
        {
            Server = new ServerSettings();
            Client = new ClientSettings();
        }
    }
}

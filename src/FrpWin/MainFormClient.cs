using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FrpWin
{
    public sealed partial class MainForm
    {
        // =====================================================================
        //  客户端标签页
        // =====================================================================
        private TabPage BuildClientTab()
        {
            var page = new TabPage("客户端（frpc · 部署在内网机器上）") { BackColor = SystemColors.Control, Padding = new Padding(8) };

            _cAddr = new TextBox();
            _cPort = new TextBox();
            _cToken = new TextBox();
            _cTls = new CheckBox { Text = "与服务端之间的连接启用 TLS 加密（推荐勾选）", AutoSize = true };
            _cLoginFail = new CheckBox { Text = "登录失败时自动退出（不勾选则一直重试）", AutoSize = true };
            _cLogLevel = MakeCombo(LogLevels);
            _cAdminPort = new TextBox();
            _cAdminUser = new TextBox();
            _cAdminPass = new TextBox();

            // 连接设置用两列布局，把纵向空间让给下面的两个表格
            _cLoginFail.Margin = new Padding(24, 3, 3, 3);
            var checks = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Margin = new Padding(0)
            };
            checks.Controls.Add(_cTls);
            checks.Controls.Add(_cLoginFail);
            checks.Name = "checksPanel";

            var conn = new FormTable2();
            conn.Add("服务器地址", _cAddr, "服务器端口", _cPort);
            conn.Add("认证令牌", _cToken, "日志级别", _cLogLevel);
            conn.AddSpan(checks);
            conn.Add("管理界面端口", _cAdminPort, "", null);

            var connBox = new GroupBox
            {
                Text = "① 连接设置", Name = "connBox",
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8)
            };
            conn.Panel.Name = "connTable";
            connBox.Controls.Add(conn.Panel);

            // ---------------- 代理表格 ----------------
            _gridProxies = MakeGrid();
            _gridProxies.Name = "gridProxies";
            _gridProxies.Columns.Add(MakeText("名称", "Name", 90));
            _gridProxies.Columns.Add(MakeTypeColumn("类型", "Type", ProxyTypes, 80));
            _gridProxies.Columns.Add(MakeText("本地IP", "LocalIP", 95));
            _gridProxies.Columns.Add(MakeText("本地端口", "LocalPort", 70));
            _gridProxies.Columns.Add(MakeText("远程端口", "RemotePort", 70));
            _gridProxies.Columns.Add(MakeText("自定义域名", "CustomDomains", 150));
            _gridProxies.Columns.Add(MakeText("子域名", "Subdomain", 80));
            _gridProxies.Columns.Add(MakeText("密钥", "SecretKey", 85));
            _gridProxies.Columns.Add(MakeCheck("加密", "UseEncryption", 45));
            _gridProxies.Columns.Add(MakeCheck("压缩", "UseCompression", 45));

            var proxyBar = MakeToolBar();
            proxyBar.Name = "proxyBar";
            var pAddTcp = MakeButton("＋ 添加 TCP/UDP 转发", 150, false);
            var pAddWeb = MakeButton("＋ 添加网站穿透", 120, false);
            var pAddP2P = MakeButton("＋ 添加点对点(stcp)", 140, false);
            var pDel = MakeButton("－ 删除选中行", 100, false);
            var pCopy = MakeButton("复制选中行", 95, false);
            pAddTcp.Click += (s, e) => AddProxy("tcp");
            pAddWeb.Click += (s, e) => AddProxy("http");
            pAddP2P.Click += (s, e) => AddProxy("stcp");
            pDel.Click += (s, e) => RemoveSelected(_gridProxies);
            pCopy.Click += (s, e) => DuplicateSelected(_gridProxies);
            proxyBar.Controls.AddRange(new Control[] { pAddTcp, pAddWeb, pAddP2P, pDel, pCopy });

            var proxyBox = new GroupBox { Name = "proxyBox", Text = "② 代理规则（要穿透哪些服务）", Dock = DockStyle.Fill, Padding = new Padding(6) };
            proxyBox.Controls.Add(StackBarOverGrid(proxyBar, _gridProxies));

            // ---------------- 访问者表格 ----------------
            _gridVisitors = MakeGrid();
            _gridVisitors.Name = "gridVisitors";
            _gridVisitors.Columns.Add(MakeText("名称", "Name", 110));
            _gridVisitors.Columns.Add(MakeTypeColumn("类型", "Type", VisitorTypes, 70));
            _gridVisitors.Columns.Add(MakeText("对应代理名", "ServerName", 150));
            _gridVisitors.Columns.Add(MakeText("密钥", "SecretKey", 100));
            _gridVisitors.Columns.Add(MakeText("绑定地址", "BindAddr", 110));
            _gridVisitors.Columns.Add(MakeText("绑定端口", "BindPort", 100));

            var visBar = MakeToolBar();
            visBar.Name = "visBar";
            var vAdd = MakeButton("＋ 添加访问者", 110, false);
            var vDel = MakeButton("－ 删除选中行", 100, false);
            vAdd.Click += (s, e) => AddVisitor();
            vDel.Click += (s, e) => RemoveSelected(_gridVisitors);
            visBar.Controls.AddRange(new Control[] { vAdd, vDel });

            var visBox = new GroupBox
            {
                Name = "visBox",
                Text = "③ 访问者（仅 stcp / xtcp 点对点穿透时需要，访问方填写）",
                Dock = DockStyle.Fill,
                Padding = new Padding(6)
            };
            visBox.Controls.Add(StackBarOverGrid(visBar, _gridVisitors));

            var settings = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 4
            };
            settings.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 224F));   // ② 代理规则
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 195F));   // ③ 访问者
            settings.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            settings.Controls.Add(connBox, 0, 0);
            settings.Controls.Add(proxyBox, 0, 1);
            settings.Controls.Add(visBox, 0, 2);

            var advBtn = MakeButton("高级：本地管理界面账号", 170, false);
            advBtn.Click += (s, e) => EditAdminAccount();
            var hint = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(1040, 0),
                ForeColor = Color.DimGray,
                Margin = new Padding(6, 8, 3, 8),
                Text = "提示：远程端口需在服务端放行；http/https 代理要填域名（多个用逗号隔开），stcp/xtcp 只填密钥。"
            };
            var advRow = new FlowLayoutPanel { Name = "advRow", Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            advRow.Controls.Add(advBtn);
            advRow.Controls.Add(hint);
            settings.Controls.Add(advRow, 0, 3);

            // ---------------- 按钮与日志 ----------------
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(10, 4, 10, 8), WrapContents = true };
            _cBtnStart = MakeButton("保存并启动客户端", 150, true);
            _cBtnStop = MakeButton("停止客户端", 100, false);
            _cBtnSvcInstall = MakeButton("安装为 Windows 服务", 155, false);
            _cBtnSvcRemove = MakeButton("卸载 Windows 服务", 155, false);
            var cBtnOpenCfg = MakeButton("查看配置文件", 110, false);
            var cBtnOpenDir = MakeButton("打开数据目录", 110, false);
            _cBtnStart.Click += (s, e) => StartClient();
            _cBtnStop.Click += (s, e) => StopClient();
            _cBtnSvcInstall.Click += (s, e) => InstallService("frpc", _cLog);
            _cBtnSvcRemove.Click += (s, e) => RemoveService("frpc", _cLog);
            cBtnOpenCfg.Click += (s, e) => OpenFile(AppPaths.FrpcConfig);
            cBtnOpenDir.Click += (s, e) => OpenFolder(AppPaths.DataDir);
            buttons.Controls.AddRange(new Control[] {
                _cBtnStart, _cBtnStop, _cBtnSvcInstall, _cBtnSvcRemove, cBtnOpenCfg, cBtnOpenDir });

            _cStatusLabel = MakeStatusLabel();
            _cProcLabel = MakeStatusLabel();
            var info = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(10, 0, 10, 0), WrapContents = true };
            info.Controls.Add(_cStatusLabel);
            info.Controls.Add(_cProcLabel);

            _cLog = MakeLogBox();
            page.Controls.Add(BuildPageLayout(settings, buttons, info, _cLog));
            return page;
        }

        /// <summary>表格上方的按钮条。</summary>
        private static FlowLayoutPanel MakeToolBar()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Margin = new Padding(0),
                Padding = new Padding(0, 2, 0, 4)
            };
        }

        /// <summary>
        /// 把按钮条和表格上下摆好。
        /// 这里必须用 TableLayoutPanel 明确分行，不能靠 Dock + BringToFront：
        /// BringToFront 会把 Dock=Top 的按钮条在 z 序里提到最前面，导致停靠顺序反转，
        /// 表格会先占满整个区域，按钮条再盖在表格表头上，第一行就被压掉一半。
        /// </summary>
        private static TableLayoutPanel StackBarOverGrid(Control bar, DataGridView grid)
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            // 注意：RowCount 赋值不会自动填充 RowStyles，必须用 Add 而不是索引器
            t.RowStyles.Clear();
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            t.RowCount = t.RowStyles.Count;

            grid.Dock = DockStyle.Fill;
            grid.Margin = new Padding(0);

            t.Controls.Add(bar, 0, 0);
            t.Controls.Add(grid, 0, 1);
            return t;
        }

        /// <summary>一行文字在当前字体下的实际像素高度。</summary>
        internal static int LineHeight(Font font)
        {
            return TextRenderer.MeasureText("测试Ag", font).Height;
        }

        private DataGridView MakeGrid()
        {
            // 关键：表头高度和行高都必须按“当前字体”算出来。
            // 之前表头用的是 DisableResizing + 默认高度，那个高度是按控件构造时的
            // 系统默认字体算的；等控件被加到窗体上、继承了 9pt 微软雅黑之后，
            // 字体变高了，冻结的表头高度却不会跟着变，于是表头文字被从下往上截掉一半。
            int line = LineHeight(this.Font);

            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = true,
                EditMode = DataGridViewEditMode.EditOnEnter,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                // AutoSize = 高度跟着字体自动算，同时用户依然不能拖动改变
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                // 列按权重铺满整个宽度，右侧不留空白；窗口太窄时自动出现横向滚动条
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = this.Font,
                RowTemplate = { Height = line + 9 }
            };
            g.DataError += (s, e) => { e.ThrowException = false; };

            // 字体将来若因 DPI/主题变化，行高同步跟着变
            g.FontChanged += (s, e) => { g.RowTemplate.Height = LineHeight(g.Font) + 9; };
            return g;
        }

        private static DataGridViewTextBoxColumn MakeText(string header, string prop, int weight)
        {
            return new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                DataPropertyName = prop,
                FillWeight = weight,
                MinimumWidth = weight,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
        }

        private static DataGridViewCheckBoxColumn MakeCheck(string header, string prop, int weight)
        {
            return new DataGridViewCheckBoxColumn
            {
                HeaderText = header,
                DataPropertyName = prop,
                FillWeight = weight,
                MinimumWidth = weight,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
        }

        private static DataGridViewComboBoxColumn MakeTypeColumn(string header, string prop, string[] values, int weight)
        {
            var c = new DataGridViewComboBoxColumn
            {
                HeaderText = header,
                DataPropertyName = prop,
                FillWeight = weight,
                MinimumWidth = weight,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            c.Items.AddRange(values);
            return c;
        }

        private void AddProxy(string type)
        {
            if (_proxies == null) return;
            int n = 1;
            string name;
            do { name = "proxy" + n++; } while (ContainsProxyName(name));

            var p = new ProxyItem { Name = name, Type = type, LocalIP = "127.0.0.1", LocalPort = 80, RemotePort = NextFreeRemotePort() };

            if (type == "http" || type == "https") { p.CustomDomains = "www.example.com"; p.RemotePort = 0; }
            if (type == "stcp" || type == "xtcp") { p.SecretKey = ConfigStore.RandomToken(); p.RemotePort = 0; }

            _proxies.Add(p);
            SelectLast(_gridProxies);
        }

        private bool ContainsProxyName(string name)
        {
            foreach (var p in _proxies)
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private int NextFreeRemotePort()
        {
            int port = 6000;
            var used = new HashSet<int>();
            foreach (var p in _proxies) used.Add(p.RemotePort);
            while (used.Contains(port) && port < 65000) port++;
            return port;
        }

        private void AddVisitor()
        {
            if (_visitors == null) return;
            int n = 1;
            string name;
            do { name = "visitor" + n++; } while (ContainsVisitorName(name));

            _visitors.Add(new VisitorItem { Name = name, Type = "stcp", SecretKey = ConfigStore.RandomToken(), BindPort = 6000 });
            SelectLast(_gridVisitors);
        }

        private bool ContainsVisitorName(string name)
        {
            foreach (var v in _visitors)
                if (string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void SelectLast(DataGridView g)
        {
            if (g.Rows.Count == 0) return;
            g.ClearSelection();
            int i = g.Rows.Count - 1;
            g.Rows[i].Selected = true;
            g.CurrentCell = g.Rows[i].Cells[0];
            g.BeginEdit(true);
        }

        /// <summary>
        /// 把 DataGridView 里正在编辑、还没提交的单元格内容落到绑定对象上，
        /// 否则用户刚改完就点“保存并启动”会保存成旧值。
        /// </summary>
        private static void CommitGridEdits(DataGridView g)
        {
            if (g == null) return;
            try
            {
                g.EndEdit();
                if (g.DataSource != null && g.BindingContext != null)
                {
                    var cm = g.BindingContext[g.DataSource] as CurrencyManager;
                    if (cm != null) cm.EndCurrentEdit();
                }
            }
            catch { }
        }

        private static void RemoveSelected(DataGridView g)
        {
            var list = g.DataSource as System.Collections.IList;
            if (list == null) return;

            // 先提交正在编辑的单元格，否则删除后旧内容会被回写
            g.EndEdit();

            var idx = new List<int>();
            foreach (DataGridViewCell cell in g.SelectedCells)
                if (!idx.Contains(cell.RowIndex)) idx.Add(cell.RowIndex);
            idx.Sort();
            idx.Reverse();

            foreach (int i in idx)
            {
                if (i >= 0 && i < list.Count) list.RemoveAt(i);
            }
        }

        private static void DuplicateSelected(DataGridView g)
        {
            var list = g.DataSource as System.Collections.IList;
            if (list == null) return;
            g.EndEdit();

            int i = g.CurrentCell != null ? g.CurrentCell.RowIndex : -1;
            if (i < 0 || i >= list.Count) return;

            var src = list[i];
            var clone = src is ProxyItem ? (object)((ProxyItem)src).Clone() : null;
            if (clone == null) return;

            var p = (ProxyItem)clone;
            p.Name = p.Name + "_copy";
            int n = 1;
            while (ContainsName(list, p.Name)) { p.Name = p.Name + n++; }
            list.Add(p);
        }

        private static bool ContainsName(System.Collections.IList list, string name)
        {
            foreach (var o in list)
            {
                var p = o as ProxyItem;
                if (p != null && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void EditAdminAccount()
        {
            using (var dlg = new AdminAccountDialog(_cAdminUser.Text, _cAdminPass.Text))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _cAdminUser.Text = dlg.UserName;
                    _cAdminPass.Text = dlg.Password;
                }
            }
        }

        // =====================================================================
        //  载入 / 保存
        // =====================================================================
        private void LoadSettingsIntoUi()
        {
            var s = _settings.Server;
            _sBindAddr.Text = s.BindAddr;
            _sBindPort.Text = s.BindPort.ToString();
            _sKcpPort.Text = s.KcpBindPort.ToString();
            _sHttpPort.Text = s.VhostHttpPort.ToString();
            _sHttpsPort.Text = s.VhostHttpsPort.ToString();
            _sSubDomain.Text = s.SubDomainHost;
            _sToken.Text = s.Token;
            _sDashPort.Text = s.DashboardPort.ToString();
            _sDashUser.Text = s.DashboardUser;
            _sDashPass.Text = s.DashboardPassword;
            SelectLevel(_sLogLevel, s.LogLevel);

            var c = _settings.Client;
            _cAddr.Text = c.ServerAddr;
            _cPort.Text = c.ServerPort.ToString();
            _cToken.Text = c.Token;
            _cTls.Checked = c.TlsEnable;
            _cLoginFail.Checked = c.LoginFailExit;
            SelectLevel(_cLogLevel, c.LogLevel);
            _cAdminPort.Text = c.AdminPort.ToString();
            _cAdminUser.Text = c.AdminUser;
            _cAdminPass.Text = c.AdminPassword;

            _proxies = new BindingList<ProxyItem>(c.Proxies);
            _gridProxies.DataSource = _proxies;
            _visitors = new BindingList<VisitorItem>(c.Visitors);
            _gridVisitors.DataSource = _visitors;

            _chkTray.Checked = true;
        }

        private static void SelectLevel(ComboBox box, string level)
        {
            int i = Array.IndexOf(LogLevels, (level ?? "").Trim().ToLowerInvariant());
            box.SelectedIndex = i >= 0 ? i : 2;
        }

        private bool SaveServerSettings()
        {
            var s = _settings.Server;
            s.BindAddr = string.IsNullOrWhiteSpace(_sBindAddr.Text) ? "0.0.0.0" : _sBindAddr.Text.Trim();

            int v;
            if (!ReadPort(_sBindPort, "监听端口 bindPort", 1, 65535, out v)) return false;
            s.BindPort = v;
            if (!ReadPort(_sKcpPort, "KCP 端口（不需要就填 0）", 0, 65535, out v)) return false;
            s.KcpBindPort = v;
            if (!ReadPort(_sHttpPort, "HTTP 虚拟主机端口（不需要就填 0）", 0, 65535, out v)) return false;
            s.VhostHttpPort = v;
            if (!ReadPort(_sHttpsPort, "HTTPS 虚拟主机端口（不需要就填 0）", 0, 65535, out v)) return false;
            s.VhostHttpsPort = v;
            if (!ReadPort(_sDashPort, "Dashboard 端口（不需要就填 0）", 0, 65535, out v)) return false;
            s.DashboardPort = v;

            s.SubDomainHost = _sSubDomain.Text.Trim();
            s.Token = _sToken.Text.Trim();
            s.DashboardUser = _sDashUser.Text.Trim();
            s.DashboardPassword = _sDashPass.Text;
            s.LogLevel = Convert.ToString(_sLogLevel.SelectedItem);
            return true;
        }

        private bool SaveClientSettings()
        {
            var c = _settings.Client;
            c.ServerAddr = _cAddr.Text.Trim();
            if (string.IsNullOrEmpty(c.ServerAddr))
            {
                Warn("请填写服务器地址（服务端的公网 IP 或域名）。");
                _cAddr.Focus();
                return false;
            }

            int v;
            if (!ReadPort(_cPort, "服务器端口 serverPort", 1, 65535, out v)) return false;
            c.ServerPort = v;
            if (!ReadPort(_cAdminPort, "本地管理界面端口（不需要就填 0）", 0, 65535, out v)) return false;
            c.AdminPort = v;

            c.Token = _cToken.Text.Trim();
            c.TlsEnable = _cTls.Checked;
            c.LoginFailExit = _cLoginFail.Checked;
            c.LogLevel = Convert.ToString(_cLogLevel.SelectedItem);
            c.AdminUser = _cAdminUser.Text;
            c.AdminPassword = _cAdminPass.Text;

            _gridProxies.EndEdit();
            _gridVisitors.EndEdit();
            CommitGridEdits(_gridProxies);
            CommitGridEdits(_gridVisitors);

            // 校验代理名称与端口
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in c.Proxies)
            {
                if (p == null) continue;
                p.Name = (p.Name ?? "").Trim();
                if (p.Name.Length == 0) { Warn("代理规则里有名称为空的行，请填写名称。"); return false; }
                if (!names.Add(p.Name)) { Warn("代理名称重复：“" + p.Name + "”，名称必须唯一。"); return false; }
                if (p.LocalPort <= 0 || p.LocalPort > 65535)
                {
                    Warn("代理“" + p.Name + "”的本地端口不合法（" + p.LocalPort + "）。");
                    return false;
                }
                string t = (p.Type ?? "tcp").ToLowerInvariant();
                if ((t == "tcp" || t == "udp") && (p.RemotePort <= 0 || p.RemotePort > 65535))
                {
                    Warn("代理“" + p.Name + "”是 " + t + " 类型，必须填写 1~65535 的远程端口。");
                    return false;
                }
                if ((t == "http" || t == "https") &&
                    string.IsNullOrWhiteSpace(p.CustomDomains) && string.IsNullOrWhiteSpace(p.Subdomain))
                {
                    Warn("代理“" + p.Name + "”是 " + t + " 类型，必须填写自定义域名或子域名。");
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(p.LocalIP)) p.LocalIP = p.LocalIP.Trim();
                if (!string.IsNullOrWhiteSpace(p.CustomDomains)) p.CustomDomains = p.CustomDomains.Trim();
                if (!string.IsNullOrWhiteSpace(p.Subdomain)) p.Subdomain = p.Subdomain.Trim();
                if (!string.IsNullOrWhiteSpace(p.SecretKey)) p.SecretKey = p.SecretKey.Trim();
            }

            foreach (var vis in c.Visitors)
            {
                if (vis == null) continue;
                vis.Name = (vis.Name ?? "").Trim();
                vis.ServerName = (vis.ServerName ?? "").Trim();
                if (vis.Name.Length == 0)
                {
                    Warn("访问者表格里有名称为空的行，请填写名称或删除该行。");
                    return false;
                }
                if (vis.ServerName.Length == 0)
                {
                    Warn("访问者“" + vis.Name + "”必须填写对应的代理名 serverName。");
                    return false;
                }
                if (vis.BindPort <= 0 || vis.BindPort > 65535)
                {
                    Warn("访问者“" + vis.Name + "”的本地绑定端口不合法（" + vis.BindPort + "）。");
                    return false;
                }
            }

            return true;
        }

        /// <summary>保存设置并重新生成两个 TOML 配置文件。</summary>
        private bool SaveAll(string roleHint)
        {
            if (!SaveServerSettings()) return false;
            if (!SaveClientSettings()) return false;

            try
            {
                ConfigStore.Save(_settings);
                string f1, f2;
                ConfigStore.WriteToml(_settings, out f1, out f2);
                AppendLog(_sLog, "配置已保存：" + f1);
                AppendLog(_cLog, "配置已保存：" + f2);
                return true;
            }
            catch (Exception ex)
            {
                Warn("保存配置失败：\r\n" + ex.Message);
                return false;
            }
        }

        // =====================================================================
        //  启动 / 停止
        // =====================================================================
        private void StartServer()
        {
            if (!SaveAll("frps")) return;

            if (!File.Exists(AppPaths.FrpsExe))
            {
                Warn("找不到 frps.exe：\r\n" + AppPaths.FrpsExe + "\r\n\r\n请重新运行安装程序。");
                return;
            }

            if (_serverRunner.IsRunning)
            {
                Info("服务端已经在运行中。");
                return;
            }

            try
            {
                _serverRunner.Start("frps", AppPaths.FrpsExe, AppPaths.FrpsConfig, AppPaths.InstallDir, null);
                AppendLog(_sLog, "已启动 frps（PID " + _serverRunner.ProcessId + "）。");
                AppendLog(_sLog, "若这是一台有公网 IP 的服务器，请记得用“放行防火墙端口”开放 " + _sBindPort.Text + " 端口。");
            }
            catch (Exception ex)
            {
                Warn("启动 frps 失败：\r\n" + ex.Message);
            }
            RefreshStatus();
        }

        private void StopServer()
        {
            if (!_serverRunner.IsRunning) { Info("服务端当前没有在运行。"); return; }
            _serverRunner.Stop();
            AppendLog(_sLog, "已停止 frps。");
            RefreshStatus();
        }

        private void StartClient()
        {
            if (!SaveAll("frpc")) return;

            if (!File.Exists(AppPaths.FrpcExe))
            {
                Warn("找不到 frpc.exe：\r\n" + AppPaths.FrpcExe + "\r\n\r\n请重新运行安装程序。");
                return;
            }

            if (_clientRunner.IsRunning)
            {
                Info("客户端已经在运行中，如需应用新配置请先停止再启动。");
                return;
            }

            try
            {
                _clientRunner.Start("frpc", AppPaths.FrpcExe, AppPaths.FrpcConfig, AppPaths.InstallDir, null);
                AppendLog(_cLog, "已启动 frpc（PID " + _clientRunner.ProcessId + "）。");
            }
            catch (Exception ex)
            {
                Warn("启动 frpc 失败：\r\n" + ex.Message);
            }
            RefreshStatus();
        }

        private void StopClient()
        {
            if (!_clientRunner.IsRunning) { Info("客户端当前没有在运行。"); return; }
            _clientRunner.Stop();
            AppendLog(_cLog, "已停止 frpc。");
            RefreshStatus();
        }
    }

    /// <summary>修改 frpc 本地管理界面的账号密码。</summary>
    internal sealed class AdminAccountDialog : Form
    {
        private readonly TextBox _user = new TextBox { Width = 200 };
        private readonly TextBox _pass = new TextBox { Width = 200, UseSystemPasswordChar = true };

        public string UserName { get { return _user.Text; } }
        public string Password { get { return _pass.Text; } }

        public AdminAccountDialog(string user, string pass)
        {
            Text = "本地管理界面账号";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(340, 130);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            _user.Text = user;
            _pass.Text = pass;

            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(12) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(new Label { Text = "用户名", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            t.Controls.Add(_user, 1, 0);
            t.Controls.Add(new Label { Text = "密码", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            t.Controls.Add(_pass, 1, 1);

            var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Width = 80 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 80 };
            var flow = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right };
            flow.Controls.Add(ok);
            flow.Controls.Add(cancel);
            t.Controls.Add(flow, 1, 2);

            Controls.Add(t);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}

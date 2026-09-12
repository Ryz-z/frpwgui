# ============================================================================
#  验证 FrpWin 图形管理器生成的 TOML 配置能被 frp 官方程序正确解析
#  覆盖全部代理类型：tcp / udp / http / https / stcp / xtcp + visitors
# ============================================================================
$ErrorActionPreference = 'Continue'
. (Join-Path $PSScriptRoot 'config.ps1')

$APP  = $GuiExe
$BIN  = $FrpBinDir
$DATA = Join-Path $env:ProgramData 'FrpWin'
$FRPS = Join-Path $DATA 'frps.toml'
$FRPC = Join-Path $DATA 'frpc.toml'
$SET  = Join-Path $DATA 'ui-settings.xml'

if (-not (Test-Path $APP)) { throw "找不到 $APP，请先运行 .\scripts\build.ps1 -SkipInstaller" }

$pass = 0; $fail = 0
function Check($name, $ok, $detail = '') {
    if ($ok) { Write-Host "  [PASS] $name  $detail" -ForegroundColor Green; $script:pass++ }
    else     { Write-Host "  [FAIL] $name  $detail" -ForegroundColor Red;   $script:fail++ }
}

Write-Host "=== 1. 写入一份覆盖所有代理类型的界面设置 ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $DATA | Out-Null
Remove-Item $SET -Force -ErrorAction SilentlyContinue

$xml = @'
<?xml version="1.0" encoding="utf-8"?>
<FrpWinSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Server>
    <BindAddr>0.0.0.0</BindAddr>
    <BindPort>17000</BindPort>
    <KcpBindPort>17001</KcpBindPort>
    <VhostHttpPort>17080</VhostHttpPort>
    <VhostHttpsPort>17443</VhostHttpsPort>
    <SubDomainHost>test.example.com</SubDomainHost>
    <Token>Tok3n-测试-!@#$%^&amp;*()</Token>
    <DashboardPort>17500</DashboardPort>
    <DashboardUser>admin</DashboardUser>
    <DashboardPassword>Passw0rd!</DashboardPassword>
    <LogLevel>debug</LogLevel>
    <LogMaxDays>7</LogMaxDays>
  </Server>
  <Client>
    <ServerAddr>127.0.0.1</ServerAddr>
    <ServerPort>17000</ServerPort>
    <Token>Tok3n-测试-!@#$%^&amp;*()</Token>
    <TlsEnable>true</TlsEnable>
    <LogLevel>debug</LogLevel>
    <LoginFailExit>false</LoginFailExit>
    <AdminPort>17400</AdminPort>
    <AdminUser>admin</AdminUser>
    <AdminPassword>Passw0rd!</AdminPassword>
    <Proxies>
      <Proxy><Name>p-tcp</Name><Type>tcp</Type><LocalIP>127.0.0.1</LocalIP><LocalPort>3389</LocalPort><RemotePort>13389</RemotePort><CustomDomains></CustomDomains><Subdomain></Subdomain><SecretKey></SecretKey><UseEncryption>true</UseEncryption><UseCompression>true</UseCompression></Proxy>
      <Proxy><Name>p-udp</Name><Type>udp</Type><LocalIP>127.0.0.1</LocalIP><LocalPort>5353</LocalPort><RemotePort>15353</RemotePort><CustomDomains></CustomDomains><Subdomain></Subdomain><SecretKey></SecretKey><UseEncryption>false</UseEncryption><UseCompression>false</UseCompression></Proxy>
      <Proxy><Name>p-http</Name><Type>http</Type><LocalIP>127.0.0.1</LocalIP><LocalPort>80</LocalPort><RemotePort>0</RemotePort><CustomDomains>www.example.com, example.com</CustomDomains><Subdomain>web01</Subdomain><SecretKey></SecretKey><UseEncryption>false</UseEncryption><UseCompression>false</UseCompression></Proxy>
      <Proxy><Name>p-https</Name><Type>https</Type><LocalIP>127.0.0.1</LocalIP><LocalPort>443</LocalPort><RemotePort>0</RemotePort><CustomDomains>secure.example.com</CustomDomains><Subdomain></Subdomain><SecretKey></SecretKey><UseEncryption>false</UseEncryption><UseCompression>false</UseCompression></Proxy>
      <Proxy><Name>p-stcp</Name><Type>stcp</Type><LocalIP>127.0.0.1</LocalIP><LocalPort>22</LocalPort><RemotePort>0</RemotePort><CustomDomains></CustomDomains><Subdomain></Subdomain><SecretKey>secret-key-stcp</SecretKey><UseEncryption>false</UseEncryption><UseCompression>false</UseCompression></Proxy>
      <Proxy><Name>p-xtcp</Name><Type>xtcp</Type><LocalIP>127.0.0.1</LocalIP><LocalPort>22</LocalPort><RemotePort>0</RemotePort><CustomDomains></CustomDomains><Subdomain></Subdomain><SecretKey>secret-key-xtcp</SecretKey><UseEncryption>false</UseEncryption><UseCompression>false</UseCompression></Proxy>
    </Proxies>
    <Visitors>
      <Visitor><Name>v-stcp</Name><Type>stcp</Type><ServerName>p-stcp</ServerName><SecretKey>secret-key-stcp</SecretKey><BindAddr>127.0.0.1</BindAddr><BindPort>19000</BindPort></Visitor>
      <Visitor><Name>v-xtcp</Name><Type>xtcp</Type><ServerName>p-xtcp</ServerName><SecretKey>secret-key-xtcp</SecretKey><BindAddr>127.0.0.1</BindAddr><BindPort>19001</BindPort></Visitor>
    </Visitors>
  </Client>
</FrpWinSettings>
'@
[System.IO.File]::WriteAllText($SET, $xml, (New-Object System.Text.UTF8Encoding $false))
Check "界面设置文件已写入" (Test-Path $SET)

Write-Host "`n=== 2. 调用 FrpWin.exe --gen-config 生成 TOML ===" -ForegroundColor Cyan
Remove-Item $FRPS, $FRPC -Force -ErrorAction SilentlyContinue
$g = Start-Process -FilePath $APP -ArgumentList '--gen-config' -Wait -PassThru
Check "gen-config 退出码为 0" ($g.ExitCode -eq 0) "exit=$($g.ExitCode)"
Check "已生成 frps.toml" (Test-Path $FRPS)
Check "已生成 frpc.toml" (Test-Path $FRPC)

Write-Host "`n=== 3. 用 frp 官方 verify 命令校验 ===" -ForegroundColor Cyan
$v1 = & "$BIN\frps.exe" verify -c "$FRPS" 2>&1
$c1 = $LASTEXITCODE
Write-Host "  frps verify: $($v1 -join ' | ')"
Check "frps 接受生成的配置" ($c1 -eq 0) "exit=$c1"

$v2 = & "$BIN\frpc.exe" verify -c "$FRPC" 2>&1
$c2 = $LASTEXITCODE
Write-Host "  frpc verify: $($v2 -join ' | ')"
Check "frpc 接受生成的配置" ($c2 -eq 0) "exit=$c2"

Write-Host "`n=== 4. 校验关键内容 ===" -ForegroundColor Cyan
$frpsTxt = Get-Content $FRPS -Raw
$frpcTxt = Get-Content $FRPC -Raw
Check "frps 含 bindPort" ($frpsTxt -match 'bindPort = 17000')
Check "frps 含 kcpBindPort" ($frpsTxt -match 'kcpBindPort = 17001')
Check "frps 含 vhostHTTPPort" ($frpsTxt -match 'vhostHTTPPort = 17080')
Check "frps 含 vhostHTTPSPort" ($frpsTxt -match 'vhostHTTPSPort = 17443')
Check "frps 含 subDomainHost" ($frpsTxt -match 'subDomainHost = "test\.example\.com"')
Check "frps 含 Dashboard 配置" ($frpsTxt -match 'webServer\.port = 17500')
Check "token 特殊字符被正确转义" ($frpcTxt -match 'auth\.token = "Tok3n')
Check "frpc 含 6 条代理" (([regex]::Matches($frpcTxt, '\[\[proxies\]\]')).Count -eq 6) "实际 $(([regex]::Matches($frpcTxt,'\[\[proxies\]\]')).Count) 条"
Check "frpc 含 2 条访问者" (([regex]::Matches($frpcTxt, '\[\[visitors\]\]')).Count -eq 2) "实际 $(([regex]::Matches($frpcTxt,'\[\[visitors\]\]')).Count) 条"
Check "http 代理含 customDomains" ($frpcTxt -match 'customDomains = \["www\.example\.com", "example\.com"\]')
Check "stcp 代理含 secretKey" ($frpcTxt -match 'secretKey = "secret-key-stcp"')
Check "tcp 代理含加密压缩开关" ($frpcTxt -match 'transport\.useEncryption = true' -and $frpcTxt -match 'transport\.useCompression = true')
Check "visitor 含 serverName/bindPort" ($frpcTxt -match 'serverName = "p-stcp"' -and $frpcTxt -match 'bindPort = 19000')

Write-Host "`n=== 5. 真实启动一次，确认服务端能跑起来 ===" -ForegroundColor Cyan
$f = Start-Process -FilePath "$BIN\frps.exe" -ArgumentList "-c", "`"$FRPS`"" -PassThru -NoNewWindow `
     -RedirectStandardOutput "$env:TEMP\frpwin-verify.out" -RedirectStandardError "$env:TEMP\frpwin-verify.err"
Start-Sleep -Seconds 3
$alive = -not $f.HasExited
Check "服务端使用 GUI 生成的配置成功启动" $alive "PID=$($f.Id)"
if (-not $alive) { Get-Content "$env:TEMP\frpwin-verify.out","$env:TEMP\frpwin-verify.err" -ErrorAction SilentlyContinue | Select-Object -First 10 }
if ($alive) { $f.Kill() }
Start-Sleep -Milliseconds 500

Write-Host "`n======================================" -ForegroundColor Cyan
Write-Host "  通过: $pass   失败: $fail"
Write-Host "======================================" -ForegroundColor Cyan
if ($fail -gt 0) { exit 1 } else { exit 0 }

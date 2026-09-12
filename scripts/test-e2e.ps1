# ============================================================================
#  端到端联调测试：本机同时跑 frps + frpc，验证 TCP 隧道真正打通
#  （不依赖 FrpWin.exe，直接测第三方 frp 二进制）
# ============================================================================
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'config.ps1')

$BIN = $FrpBinDir
$T   = Join-Path $BuildDir 'e2e'
$LOCAL_PORT  = 18080
$REMOTE_PORT = 18000
$FRP_PORT    = 17000
$DASH_PORT   = 17500
$TOKEN       = 'e2e-token-123456'

foreach ($f in @('frps.exe', 'frpc.exe')) {
    if (-not (Test-Path (Join-Path $BIN $f))) { throw "缺少 $f，请先运行 .\scripts\fetch-frp.ps1" }
}

Remove-Item $T -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $T | Out-Null

$frpsToml = @"
bindAddr = "0.0.0.0"
bindPort = $FRP_PORT
auth.method = "token"
auth.token = "$TOKEN"
webServer.addr = "127.0.0.1"
webServer.port = $DASH_PORT
webServer.user = "admin"
webServer.password = "admin123"
log.to = "console"
log.level = "info"
"@
[System.IO.File]::WriteAllText("$T\frps.toml", $frpsToml, (New-Object System.Text.UTF8Encoding $false))

$frpcToml = @"
serverAddr = "127.0.0.1"
serverPort = $FRP_PORT
auth.method = "token"
auth.token = "$TOKEN"
log.to = "console"
log.level = "info"

[[proxies]]
name = "e2e-tcp"
type = "tcp"
localIP = "127.0.0.1"
localPort = $LOCAL_PORT
remotePort = $REMOTE_PORT
"@
[System.IO.File]::WriteAllText("$T\frpc.toml", $frpcToml, (New-Object System.Text.UTF8Encoding $false))

Write-Host "=== 1. 启动内网目标服务 (127.0.0.1:$LOCAL_PORT) ==="
$targetJob = Start-Job -ScriptBlock {
    param($port)
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, $port)
    $listener.Start()
    while ($true) {
        try {
            $client = $listener.AcceptTcpClient()
            $stream = $client.GetStream()
            $buf = New-Object byte[] 4096
            try { $null = $stream.Read($buf, 0, $buf.Length) } catch { }
            $body = 'HELLO-FRP-OK'
            $resp = "HTTP/1.1 200 OK`r`nContent-Type: text/plain`r`nContent-Length: $($body.Length)`r`nConnection: close`r`n`r`n$body"
            $bytes = [System.Text.Encoding]::ASCII.GetBytes($resp)
            $stream.Write($bytes, 0, $bytes.Length)
            $stream.Flush()
            $client.Close()
        } catch { }
    }
} -ArgumentList $LOCAL_PORT
Start-Sleep -Seconds 2

Write-Host "=== 2. 启动 frps (控制端口 $FRP_PORT, 面板 $DASH_PORT) ==="
$frpsProc = Start-Process -FilePath "$BIN\frps.exe" -ArgumentList "-c", "`"$T\frps.toml`"" `
    -WorkingDirectory $T -PassThru -NoNewWindow -RedirectStandardOutput "$T\frps.out" -RedirectStandardError "$T\frps.err"
Start-Sleep -Seconds 3
Write-Host "   frps PID = $($frpsProc.Id)  running = $(-not $frpsProc.HasExited)"

Write-Host "=== 3. 启动 frpc ==="
$frpcProc = Start-Process -FilePath "$BIN\frpc.exe" -ArgumentList "-c", "`"$T\frpc.toml`"" `
    -WorkingDirectory $T -PassThru -NoNewWindow -RedirectStandardOutput "$T\frpc.out" -RedirectStandardError "$T\frpc.err"
Start-Sleep -Seconds 5
Write-Host "   frpc PID = $($frpcProc.Id)  running = $(-not $frpcProc.HasExited)"

$pass = 0; $fail = 0
function Check($name, $ok, $detail) {
    if ($ok) { Write-Host "  [PASS] $name  $detail" -ForegroundColor Green; $script:pass++ }
    else     { Write-Host "  [FAIL] $name  $detail" -ForegroundColor Red;   $script:fail++ }
}

Write-Host "`n=== 4. 验证结果 ==="
try {
    $r = Invoke-WebRequest -Uri "http://127.0.0.1:$LOCAL_PORT/" -TimeoutSec 8 -UseBasicParsing
    Check "直连内网服务" ($r.Content -eq 'HELLO-FRP-OK') "Content=$($r.Content)"
} catch { Check "直连内网服务" $false $_.Exception.Message }

try {
    $r = Invoke-WebRequest -Uri "http://127.0.0.1:$REMOTE_PORT/" -TimeoutSec 10 -UseBasicParsing
    Check "通过 frp 隧道访问" ($r.Content -eq 'HELLO-FRP-OK') "Content=$($r.Content)"
} catch { Check "通过 frp 隧道访问" $false $_.Exception.Message }

try {
    $body = (curl.exe -s -L -u "admin:admin123" "http://127.0.0.1:$DASH_PORT/") -join "`n"
    Check "frps Dashboard 页面" ($body -match 'id="app"' -and $body -match 'frp server') "$($body.Length) 字节, 含 Vue 挂载点"
} catch { Check "frps Dashboard 页面" $false $_.Exception.Message }

try {
    $pair = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("admin:admin123"))
    $r = Invoke-WebRequest -Uri "http://127.0.0.1:$DASH_PORT/api/proxy/tcp" -Headers @{ Authorization = "Basic $pair" } -TimeoutSec 10 -UseBasicParsing
    Check "Dashboard API /api/proxy/tcp" ($r.StatusCode -eq 200) "HTTP $($r.StatusCode)"
} catch { Check "Dashboard API /api/proxy/tcp" $false $_.Exception.Message }

Write-Host "=== 5. 验证认证保护（错误 token 必须连不上） ==="
$badCfg = (Get-Content "$T\frpc.toml" -Raw) -replace [regex]::Escape($TOKEN), 'wrong-token-xxx'
[System.IO.File]::WriteAllText("$T\frpc-bad.toml", $badCfg, (New-Object System.Text.UTF8Encoding $false))
$frpcBad = Start-Process -FilePath "$BIN\frpc.exe" -ArgumentList "-c", "`"$T\frpc-bad.toml`"" `
    -WorkingDirectory $T -PassThru -NoNewWindow -RedirectStandardOutput "$T\frpcbad.out" -RedirectStandardError "$T\frpcbad.err"
Start-Sleep -Seconds 4
$serverLog = Get-Content "$T\frps.out" -Raw -ErrorAction SilentlyContinue
Check "错误 token 被服务端拒绝" ($serverLog -match "doesn't match token|token in login doesn't match") "服务端日志有认证失败记录"
if ($frpcBad -and -not $frpcBad.HasExited) { $frpcBad.Kill() }

Write-Host "`n=== 6. 清理 ==="
foreach ($p in @($frpsProc, $frpcProc, $frpcBad)) {
    if ($p -and -not $p.HasExited) { try { $p.Kill() } catch { } }
}
Stop-Job $targetJob -ErrorAction SilentlyContinue
Remove-Job $targetJob -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

Write-Host "`n======================================"
Write-Host "  通过: $pass   失败: $fail"
Write-Host "======================================"
if ($fail -gt 0) { exit 1 } else { exit 0 }

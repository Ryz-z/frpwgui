# ============================================================================
#  安装包实机验证：静默安装 → 检查文件/快捷方式/注册表 → 启动程序 → 卸载
#  以 /CURRENTUSER（非管理员）模式安装，避免触发 UAC
# ============================================================================
$ErrorActionPreference = 'Continue'
. (Join-Path $PSScriptRoot 'config.ps1')

$SETUP   = Join-Path $DistDir 'setup.exe'
$TESTDIR = Join-Path $BuildDir 'testinstall'
$DESKTOP = [Environment]::GetFolderPath('Desktop')
$LNK     = Join-Path $DESKTOP 'FrpWin 内网穿透套装.lnk'
$GROUPDIR= Join-Path ([Environment]::GetFolderPath('Programs')) 'FrpWin'

if (-not (Test-Path $SETUP)) { throw "找不到 $SETUP，请先运行 .\scripts\build.ps1" }

$pass = 0; $fail = 0
function Check($name, $ok, $detail = '') {
    if ($ok) { Write-Host "  [PASS] $name  $detail" -ForegroundColor Green; $script:pass++ }
    else     { Write-Host "  [FAIL] $name  $detail" -ForegroundColor Red;   $script:fail++ }
}

Write-Host "=== 0. 环境准备 ===" -ForegroundColor Cyan
Remove-Item $TESTDIR -Recurse -Force -ErrorAction SilentlyContinue
if (Test-Path $LNK) { Remove-Item $LNK -Force }
Write-Host "  安装包: $SETUP ($([math]::Round((Get-Item $SETUP).Length/1MB,1)) MB)"
Write-Host "  测试目录: $TESTDIR"
Write-Host "  桌面快捷方式: $LNK"

Write-Host "`n=== 1. 静默安装 (/CURRENTUSER) ===" -ForegroundColor Cyan
$p = Start-Process -FilePath $SETUP -ArgumentList @(
    '/CURRENTUSER','/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',
    '/MERGETASKS="desktopicon"',
    "/DIR=`"$TESTDIR`"",
    "/LOG=`"$(Join-Path $BuildDir 'install.log')`""
) -Wait -PassThru
Write-Host "  安装程序退出码: $($p.ExitCode)"
Check "安装程序退出码为 0" ($p.ExitCode -eq 0) "exit=$($p.ExitCode)"

Write-Host "`n=== 2. 检查安装的文件 ===" -ForegroundColor Cyan
foreach ($f in @('FrpWin.exe','frps.exe','frpc.exe','使用说明.txt','unins000.exe')) {
    $full = Join-Path $TESTDIR $f
    $exists = Test-Path $full
    $size = if ($exists) { "{0:N1} KB" -f ((Get-Item $full).Length/1KB) } else { '-' }
    Check "文件 $f" $exists $size
}

Write-Host "`n=== 3. 检查桌面快捷方式 ===" -ForegroundColor Cyan
Check "桌面快捷方式存在" (Test-Path $LNK) $LNK
if (Test-Path $LNK) {
    $sh = New-Object -ComObject WScript.Shell
    $sc = $sh.CreateShortcut($LNK)
    Write-Host "     TargetPath  : $($sc.TargetPath)"
    Write-Host "     WorkingDir  : $($sc.WorkingDirectory)"
    Write-Host "     IconLocation: $($sc.IconLocation)"
    Check "快捷方式指向 FrpWin.exe" ($sc.TargetPath -ieq (Join-Path $TESTDIR 'FrpWin.exe'))
    Check "快捷方式有图标" ($sc.IconLocation -match 'FrpWin.exe')
}

Write-Host "`n=== 4. 检查开始菜单 ===" -ForegroundColor Cyan
Check "开始菜单组存在" (Test-Path $GROUPDIR) $GROUPDIR
if (Test-Path $GROUPDIR) { Get-ChildItem $GROUPDIR | ForEach-Object { Write-Host "     $($_.Name)" } }

Write-Host "`n=== 5. 检查卸载注册表项 ===" -ForegroundColor Cyan
$found = $null
foreach ($r in @('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
                 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall')) {
    if (Test-Path $r) {
        $found = Get-ChildItem $r -ErrorAction SilentlyContinue |
                 Where-Object { $_.GetValue('DisplayName') -like '*FrpWin*' } | Select-Object -First 1
        if ($found) { Write-Host "     位于: $($found.Name)"; break }
    }
}
Check "已注册卸载信息" ($null -ne $found)
if ($found) {
    Write-Host "     DisplayName    : $($found.GetValue('DisplayName'))"
    Write-Host "     DisplayVersion : $($found.GetValue('DisplayVersion'))"
    Write-Host "     UninstallString: $($found.GetValue('UninstallString'))"
}

Write-Host "`n=== 6. 检查数据目录 ===" -ForegroundColor Cyan
$dataDir = Join-Path $env:ProgramData 'FrpWin'
Check "数据目录已创建" (Test-Path $dataDir) $dataDir
if (Test-Path $dataDir) { Get-ChildItem $dataDir | ForEach-Object { Write-Host "     $($_.Name)" } }

Write-Host "`n=== 7. 启动图形管理器 ===" -ForegroundColor Cyan
$app = Start-Process -FilePath (Join-Path $TESTDIR 'FrpWin.exe') -PassThru
Start-Sleep -Seconds 6
$app.Refresh()
$alive = -not $app.HasExited
Check "FrpWin.exe 能正常启动并保持运行" $alive "PID=$($app.Id)"
if (-not $alive) { Write-Host "     退出码: $($app.ExitCode)" }
if ($alive) {
    $title = $app.MainWindowTitle
    Write-Host "     主窗口标题: $title"
    Check "主窗口已创建" ($title -like '*FrpWin*') $title
}

# 第二个实例应立即退出，并把已有窗口拉到前台
$app2 = Start-Process -FilePath (Join-Path $TESTDIR 'FrpWin.exe') -PassThru
$exited = $app2.WaitForExit(8000)
Start-Sleep -Milliseconds 300
Check "单实例保护生效" $exited

if ($alive) { $app.Kill(); Start-Sleep -Seconds 1 }
if (-not $app2.HasExited) { $app2.Kill() }

Write-Host "`n=== 8. 执行卸载 ===" -ForegroundColor Cyan
$unins = Join-Path $TESTDIR 'unins000.exe'
if (Test-Path $unins) {
    $u = Start-Process -FilePath $unins -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -PassThru
    $u.WaitForExit(120000) | Out-Null
    Write-Host "  卸载程序退出码: $($u.ExitCode)"
    Start-Sleep -Seconds 3
    Check "安装目录已清理" (-not (Test-Path (Join-Path $TESTDIR 'FrpWin.exe')))
    Check "桌面快捷方式已删除" (-not (Test-Path $LNK))
    Check "开始菜单已删除" (-not (Test-Path $GROUPDIR))
} else {
    Check "找到卸载程序 unins000.exe" $false
}

Write-Host "`n======================================" -ForegroundColor Cyan
Write-Host "  通过: $pass   失败: $fail"
Write-Host "======================================" -ForegroundColor Cyan
if ($fail -gt 0) { exit 1 } else { exit 0 }

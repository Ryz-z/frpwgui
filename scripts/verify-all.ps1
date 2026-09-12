# ============================================================================
#  全量验证：重新构建 + 跑完全部测试
#  每个套件都在独立子进程里跑，退出码可靠；
#  脚本语法错误会被提前发现，不会被误判为通过。
#
#  用法： .\scripts\verify-all.ps1
# ============================================================================
$ErrorActionPreference = 'Continue'
. (Join-Path $PSScriptRoot 'config.ps1')

$results = @()

function Test-ScriptSyntax($path) {
    $err = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$null, [ref]$err)
    return @($err).Count
}

function RunSuite($name, $script, $scriptArgs = @()) {
    Write-Host "`n`n##########  $name  ##########" -ForegroundColor Yellow

    $syn = Test-ScriptSyntax $script
    if ($syn -gt 0) {
        Write-Host "  脚本语法错误 $syn 处，跳过执行" -ForegroundColor Red
        $script:results += [pscustomobject]@{ Suite = $name; ExitCode = 99; Seconds = 0; Note = '语法错误' }
        return
    }

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $out = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script @scriptArgs 2>&1
    $code = $LASTEXITCODE
    $sw.Stop()

    $out | ForEach-Object { Write-Host $_ }

    $script:results += [pscustomobject]@{
        Suite = $name; ExitCode = $code; Seconds = [int]$sw.Elapsed.TotalSeconds; Note = ''
    }
}

# 1. 编译图形管理器
Write-Host "##########  编译 FrpWin.exe  ##########" -ForegroundColor Yellow
& (Join-Path $PSScriptRoot 'build.ps1') -SkipInstaller
if ($LASTEXITCODE -ne 0) { Write-Host "编译失败" -ForegroundColor Red; exit 1 }

# 2. 各测试套件
RunSuite "端到端隧道 test-e2e"       (Join-Path $PSScriptRoot 'test-e2e.ps1')
RunSuite "界面布局 test-layout"      (Join-Path $PSScriptRoot 'test-layout.ps1')
RunSuite "配置生成 test-config"      (Join-Path $PSScriptRoot 'test-config.ps1')
RunSuite "前台运行 test-runmode"     (Join-Path $PSScriptRoot 'test-runmode.ps1')

# 3. 打包 + 安装卸载（需要 Inno Setup）
if (Find-ISCC) {
    RunSuite "打包构建 build.ps1"    (Join-Path $PSScriptRoot 'build.ps1')
    RunSuite "安装卸载 test-installer" (Join-Path $PSScriptRoot 'test-installer.ps1')
} else {
    Write-Host "`n未找到 Inno Setup，跳过打包与安装测试" -ForegroundColor DarkYellow
}

# 4. 汇总
Write-Host "`n`n==================================================" -ForegroundColor Cyan
Write-Host "              验证汇总" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
foreach ($r in $results) {
    $mark = if ($r.ExitCode -eq 0) { '通过' } else { '失败' }
    $color = if ($r.ExitCode -eq 0) { 'Green' } else { 'Red' }
    Write-Host ("  {0,-26} {1,-6} {2,4}s  {3}" -f $r.Suite, $mark, $r.Seconds, $r.Note) -ForegroundColor $color
}
$failed = @($results | Where-Object { $_.ExitCode -ne 0 }).Count
Write-Host "--------------------------------------------------" -ForegroundColor Cyan
if ($failed -eq 0) { Write-Host "  全部通过 ✔" -ForegroundColor Green }
else { Write-Host "  有 $failed 项失败 ✘" -ForegroundColor Red }

if (Test-Path $DistDir) {
    Write-Host "`n========== dist 产物 ==========" -ForegroundColor Cyan
    Get-ChildItem $DistDir -Recurse -File | ForEach-Object {
        Write-Host ("  {0,-46} {1,9:N2} MB" -f $_.FullName.Substring($DistDir.Length + 1), ($_.Length / 1MB))
    }
}

Write-Host "`n========== 残留进程检查 ==========" -ForegroundColor Cyan
$r = Get-Process FrpWin, frps, frpc -ErrorAction SilentlyContinue
if ($r) { $r | Select-Object Id, ProcessName | Format-Table -AutoSize } else { Write-Host "  无残留 ✔" }

exit $failed

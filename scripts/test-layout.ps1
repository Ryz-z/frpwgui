# ============================================================================
#  界面布局回归测试
#
#  针对开发过程中真实出现过的缺陷：
#   1) “② 代理规则 / ③ 访问者” 的按钮条盖住表格表头，第一行只显示一半
#   2) 按钮用固定宽度，在高 DPI 下文字被横向截断
#   3) 访问者表格太矮，表头 + 两行都放不下
#   4) 默认窗口下内容溢出，必须滚动才能看到 ③
#   5) 表头/行高按控件构造时的字体算死，继承 9pt 字体后表头文字被纵向截掉一半
#
#  依赖 FrpWin.exe 内置的两个自检开关：
#    --dump-layout              打印每个控件的真实位置和尺寸
#    --screenshot <png> [页]    把界面渲染成 PNG
# ============================================================================
$ErrorActionPreference = 'Continue'
. (Join-Path $PSScriptRoot 'config.ps1')

$APP = $GuiExe
$OUT = Join-Path $BuildDir 'layout.txt'

if (-not (Test-Path $APP)) { throw "找不到 $APP，请先运行 .\scripts\build.ps1 -SkipInstaller" }
New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null

$pass = 0; $fail = 0
function Check($name, $ok, $detail = '') {
    if ($ok) { Write-Host "  [PASS] $name  $detail" -ForegroundColor Green; $script:pass++ }
    else     { Write-Host "  [FAIL] $name  $detail" -ForegroundColor Red;   $script:fail++ }
}

Write-Host "=== 1. 运行布局自检 ===" -ForegroundColor Cyan
$p = Start-Process -FilePath $APP -ArgumentList '--dump-layout' -PassThru -Wait -RedirectStandardOutput $OUT
Check "布局自检进程退出码为 0" ($p.ExitCode -eq 0) "exit=$($p.ExitCode)"
$lines = Get-Content $OUT -Encoding UTF8
if ($lines | Where-Object { $_ -match '布局自检失败' }) { Write-Host ($lines | Select-String '布局自检失败') -ForegroundColor Red }

function Get-Ctl($name) {
    $l = $lines | Where-Object { $_ -match "^CTL\|.*\|$name\|" } | Select-Object -Last 1
    if (-not $l) { return $null }
    $f = $l -split '\|'
    # 字段：CTL|Type|Name|Left|Top|Width|Height
    return [pscustomobject]@{
        Type   = $f[1]; Name = $f[2]
        Left   = [int]$f[3]; Top = [int]$f[4]
        Width  = [int]$f[5]; Height = [int]$f[6]
        Right  = [int]$f[3] + [int]$f[5]
        Bottom = [int]$f[4] + [int]$f[6]
    }
}

function Get-Grd($name) {
    $l = $lines | Where-Object { $_ -match "^GRD\|$name\|" } | Select-Object -Last 1
    if (-not $l) { return $null }
    $f = $l -split '\|'
    # 字段：GRD|Name|ColumnHeadersHeight|RowHeight|TextHeight|Font
    return [pscustomobject]@{
        Name = $f[1]; HeaderH = [int]$f[2]; RowH = [int]$f[3]
        TextH = [int]$f[4]; Font = $f[5]
    }
}

Write-Host "`n=== 2. 取控件实际位置 ===" -ForegroundColor Cyan
$proxyBar  = Get-Ctl 'proxyBar'
$gridP     = Get-Ctl 'gridProxies'
$visBar    = Get-Ctl 'visBar'
$gridV     = Get-Ctl 'gridVisitors'
$advRow    = Get-Ctl 'advRow'
$mP        = Get-Grd 'gridProxies'
$mV        = Get-Grd 'gridVisitors'
$sf        = ($lines | Where-Object { $_ -match '\|scrollArea\|' } | Select-Object -Last 1) -split '\|'
$viewBottom = [int]$sf[4] + [int]$sf[6]

foreach ($c in @($proxyBar, $gridP, $visBar, $gridV, $advRow)) {
    if ($c) { Write-Host ("     {0,-14} Top={1,4} 底={2,4} 左={3,4} 宽={4,4} 高={5,4}" -f $c.Name, $c.Top, $c.Bottom, $c.Left, $c.Width, $c.Height) }
    else    { Write-Host "     控件缺失" -ForegroundColor Red }
}
Write-Host "     客户端可视区底部 = $viewBottom"
Check "关键控件都拿到了" ($proxyBar -and $gridP -and $visBar -and $gridV -and $advRow)
Check "两个表格的度量都拿到了" ($mP -and $mV)

Write-Host "`n=== 3. 按钮条不能盖住表格（表头/第一行必须完整可见） ===" -ForegroundColor Cyan
Check "代理规则：表格从按钮条下方开始" ($gridP.Top -ge $proxyBar.Bottom) "bar底=$($proxyBar.Bottom) 表格顶=$($gridP.Top)"
Check "访问者：表格从按钮条下方开始"   ($gridV.Top -ge $visBar.Bottom)  "bar底=$($visBar.Bottom) 表格顶=$($gridV.Top)"

Write-Host "`n=== 4. 表头高度、行高必须放得下当前字体的文字（否则会被纵向截掉） ===" -ForegroundColor Cyan
foreach ($m in @($mP, $mV)) {
    Check "$($m.Name) 表头高度够（>= 文字高+6）" ($m.HeaderH -ge ($m.TextH + 6)) "表头=$($m.HeaderH) 文字高=$($m.TextH) 字体=$($m.Font)"
    Check "$($m.Name) 行高够（>= 文字高+4）"     ($m.RowH -ge ($m.TextH + 4))    "行高=$($m.RowH) 文字高=$($m.TextH)"
}

Write-Host "`n=== 5. 表格高度至少能显示 表头 + 2 行数据 ===" -ForegroundColor Cyan
foreach ($pair in @(@($gridP, $mP), @($gridV, $mV))) {
    $g = $pair[0]; $m = $pair[1]
    $need = $m.HeaderH + 2 * $m.RowH
    $rows = [math]::Floor(($g.Height - $m.HeaderH) / $m.RowH)
    Check "$($g.Name) 能显示表头+2行" ($g.Height -ge $need) "高=$($g.Height) 需要=$need 实际可显示 $rows 行"
}

Write-Host "`n=== 6. 默认窗口下不需要滚动就能看到全部内容 ===" -ForegroundColor Cyan
Check "③ 访问者和高级按钮都在可视区内" ($advRow.Bottom -le $viewBottom) "内容底=$($advRow.Bottom) 可视区底=$viewBottom"
Check "访问者表格完整可见" ($gridV.Bottom -le $viewBottom) "表格底=$($gridV.Bottom)"

Write-Host "`n=== 7. 按钮文字不能被横向截断 ===" -ForegroundColor Cyan
$btns = $lines | Where-Object { $_ -match '^BTN\|' }
Check "读到按钮数量 > 15" ($btns.Count -gt 15) "共 $($btns.Count) 个"
$clipped = @()
foreach ($b in $btns) {
    $f = $b -split '\|'
    # 字段：BTN|Name|Text|Left|Top|Width|Height|NeedWidth
    $w = [int]$f[5]; $need = [int]$f[7]; $txt = $f[2]
    if ($need -gt $w) { $clipped += "$txt (需要${need} 实际${w})" }
}
Check "所有按钮文字都不被截断" ($clipped.Count -eq 0) $(if ($clipped.Count) { $clipped -join '; ' } else { "共检查 $($btns.Count) 个按钮" })

Write-Host "`n=== 8. 字段标签不能被挤成两行 ===" -ForegroundColor Cyan
$wrapped = @()
foreach ($l in ($lines | Where-Object { $_ -match '^LBL\|' })) {
    $f = $l -split '\|'
    $txt = $f[1]; $h = [int]$f[3]; $single = [int]$f[4]
    if ($txt.Length -le 40 -and $h -gt $single) { $wrapped += $txt }
}
Check "所有字段标签都是单行" ($wrapped.Count -eq 0) $(if ($wrapped.Count) { $wrapped -join '; ' } else { "共检查 $(($lines | Where-Object { $_ -match '^LBL\|' }).Count) 个标签" })

Write-Host "`n=== 9. 表格列铺满宽度，右侧不留大片空白 ===" -ForegroundColor Cyan
Check "代理表格宽度合理" ($gridP.Width -gt 600) "宽=$($gridP.Width)px"

Write-Host "`n======================================" -ForegroundColor Cyan
Write-Host "  通过: $pass   失败: $fail"
Write-Host "======================================" -ForegroundColor Cyan
if ($fail -gt 0) { exit 1 } else { exit 0 }

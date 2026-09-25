#Requires -Version 5.1
<#
  生成应用图标 assets\app.ico（7 个尺寸，256 用 PNG 压缩，其余 32 位 DIB）

  用法（在项目根目录下跑）：
    .\tools\make-icon.ps1                                   # 画工具自带的图案（不依赖任何外部图片）
    .\tools\make-icon.ps1 -Source assets\icon-source.png     # 或者用自己的图（自动取中心正方形）
    .\tools\make-icon.ps1 -Source art.png -CropX 415 -CropY 100 -Side 800
    .\tools\make-icon.ps1 -Source art.png -Preview out.png   # 额外导出一张预览条，方便眼看效果

  说明：-Design（默认）画出的是本工具自己的图案 —— 一个被"擦掉"一角的圆环，
  表示"去掉描边"。**不使用任何游戏素材或官方 logo**；想换成自己的图就用 -Source。
#>
param(
    [string]$Source = '',
    [switch]$Design,
    [string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\app.ico'),
    [string]$ArtOut = '',
    [int]$CropX = -1,
    [int]$CropY = -1,
    [int]$Side  = 0,
    [string]$Preview = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
if (-not $Design -and -not $Source) { $Design = $true }

if ($Design) {
    # ── 内置图案（纯代码绘制，无外部素材）─────────────────────
    $Side = 1024
    $master = New-Object System.Drawing.Bitmap $Side, $Side
    $g = [System.Drawing.Graphics]::FromImage($master)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $rr = [int]($Side * 0.22); $d = $rr * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($Side - $d, 0, $d, $d, 270, 90)
    $path.AddArc($Side - $d, $Side - $d, $d, $d, 0, 90)
    $path.AddArc(0, $Side - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $full = New-Object System.Drawing.Rectangle 0, 0, $Side, $Side
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $full,
        [System.Drawing.Color]::FromArgb(255, 30, 42, 104),
        [System.Drawing.Color]::FromArgb(255, 126, 92, 255),
        45)
    $g.FillPath($bg, $path); $bg.Dispose()

    # 对角柔光：左上亮、右下透明
    $sheen = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $full,
        [System.Drawing.Color]::FromArgb(64, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(0, 255, 255, 255),
        60)
    $g.FillPath($sheen, $path); $sheen.Dispose()
    $path.Dispose()

    # 主图案：一圈描边，右下角被"擦"成逐渐消失的三点
    $ringR = $Side * 0.30
    $penW  = [single]($Side * 0.078)
    $pr = New-Object System.Drawing.Rectangle (
        [int]($Side / 2 - $ringR)), ([int]($Side / 2 - $ringR)), ([int]($ringR * 2)), ([int]($ringR * 2))

    $solid = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $penW
    $solid.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $solid.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($solid, $pr, 118, 298)          # 实线弧：故意留出右下 56°~118° 的缺口
    $solid.Dispose()

    $alpha = 200
    foreach ($a in 62, 78, 94) {               # 缺口里三点，越往前走越淡
        $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb($alpha, 255, 255, 255)), ([single]($penW * 0.60))
        $p.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $p.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawArc($p, $pr, $a, 9)
        $p.Dispose()
        $alpha -= 62
    }
    $g.Dispose()

    if (-not $ArtOut) { $ArtOut = Join-Path $root 'assets\icon-source.png' }
    $artDir = Split-Path -Parent $ArtOut
    if ($artDir -and -not (Test-Path -LiteralPath $artDir)) { New-Item -ItemType Directory -Path $artDir -Force | Out-Null }
    $master.Save($ArtOut, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host ("母图已生成（内置图案）：{0}  {1}x{1}" -f $ArtOut, $Side)
} else {
    if (-not (Test-Path -LiteralPath $Source)) { throw "找不到源图：$Source" }
    $img = [System.Drawing.Image]::FromFile($Source)
    try {
        $w = $img.Width; $h = $img.Height
        if ($Side -le 0) { $Side = [Math]::Min($w, $h) }
        if ($CropX -lt 0) { $CropX = [int](($w - $Side) / 2) }
        if ($CropY -lt 0) { $CropY = [int](($h - $Side) / 2) }
        if ($CropX + $Side -gt $w -or $CropY + $Side -gt $h) { throw "裁剪区域超出原图（原图 ${w}x${h}）" }

        $rect = New-Object System.Drawing.Rectangle $CropX, $CropY, $Side, $Side
        $master = New-Object System.Drawing.Bitmap $Side, $Side
        $g = [System.Drawing.Graphics]::FromImage($master)
        $g.DrawImage($img, (New-Object System.Drawing.Rectangle 0, 0, $Side, $Side), $rect, [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()
    } finally { $img.Dispose() }

    Write-Host ("源图 {0}x{1} -> 裁剪 ({2},{3}) {4}x{4}" -f $w, $h, $CropX, $CropY, $Side)
}

# ── 生成各尺寸 ──────────────────────────────────────────────
$sizes = @(256, 128, 64, 48, 32, 24, 16)
$entries = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.DrawImage($master, 0, 0, $s, $s)
    $g.Dispose()

    if ($s -eq 256) {
        # 256 用 PNG 压缩存（体积小、支持完整 alpha）
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $data = $ms.ToArray()
    } else {
        # 小尺寸用 32 位 DIB + AND 掩码：兼容性最好
        $ms = New-Object System.IO.MemoryStream
        $bw = New-Object System.IO.BinaryWriter $ms
        $bw.Write([int]40); $bw.Write([int]$s); $bw.Write([int]($s * 2))
        $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]0); $bw.Write([int]($s * $s * 4))
        $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
        for ($y = $s - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $s; $x++) {
                $c = $bmp.GetPixel($x, $y)
                $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
            }
        }
        $rowB = [int][Math]::Ceiling($s / 8.0); $pad = (4 - ($rowB % 4)) % 4
        $bw.Write((New-Object byte[] (($rowB + $pad) * $s)))
        $bw.Flush(); $data = $ms.ToArray()
    }
    $entries += [pscustomobject]@{ W = $s; Data = $data }
    $bmp.Dispose()
}

# ── 组装 .ico ───────────────────────────────────────────────
$fs = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$entries.Count)
$off = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $wb = if ($e.W -ge 256) { 0 } else { $e.W }
    $bw.Write([byte]$wb); $bw.Write([byte]$wb); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([int16]1); $bw.Write([int16]32)
    $bw.Write([int]$e.Data.Length); $bw.Write([int]$off)
    $off += $e.Data.Length
}
foreach ($e in $entries) { $bw.Write($e.Data) }
$bw.Flush()
[System.IO.File]::WriteAllBytes($Out, $fs.ToArray())
$master.Dispose()
Write-Host ("已生成 {0}  {1:N0} 字节（{2} 个尺寸）" -f $Out, (Get-Item -LiteralPath $Out).Length, $entries.Count)

# ── 可选：导出一条预览，方便肉眼确认小尺寸不糊 ──────────────
if ($Preview) {
    $sheet = New-Object System.Drawing.Bitmap 600, 320
    $g = [System.Drawing.Graphics]::FromImage($sheet)
    $g.Clear([System.Drawing.Color]::FromArgb(245, 246, 248))
    $x = 16
    foreach ($s in @(256, 64, 48, 32, 16)) {
        $i = New-Object System.Drawing.Icon $Out, $s, $s
        $b = $i.ToBitmap()
        $g.DrawImage($b, $x, 20, $s, $s)
        $b.Dispose(); $i.Dispose()
        $x += $s + 24
    }
    $g.Dispose()
    $sheet.Save($Preview, [System.Drawing.Imaging.ImageFormat]::Png)
    $sheet.Dispose()
    Write-Host "预览: $Preview"
}

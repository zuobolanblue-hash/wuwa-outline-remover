# build.ps1 — 编译 WuwaOutlineTool.exe
# 完全离线：只用 Windows 自带的 .NET Framework 编译器（csc.exe），无需 dotnet SDK / NuGet / 联网
#
#   .\build.ps1                          编译并覆盖 WuwaOutlineTool.exe
#   .\build.ps1 -OutFile xx\new.exe      编译到别的路径（工具正开着时用这个，不打扰它）
#   .\build.ps1 -KeepRunning             即使工具正在运行也不去关它（可能会报 CS0016）
param(
    [string]$OutFile = 'WuwaOutlineTool.exe',
    [switch]$KeepRunning
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$srcDir = Join-Path $here 'src'
$assetDir = Join-Path $here 'assets'
$toolDir = Join-Path $here 'tools'

$csc = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $csc) { throw '找不到 csc.exe（需要 .NET Framework 4.x，Windows 自带）' }

# ── 统一补 UTF-8 BOM ─────────────────────────────────────────────────
# Windows PowerShell 5.1 会把「无 BOM 的 UTF-8」当 GBK 读，带中文的脚本会直接报语法错
# （记事本另存、某些编辑器、git 检出都可能把 BOM 弄丢），编译前先兜一次。
$psFiles = @(Get-ChildItem -LiteralPath $toolDir -Filter '*.ps1' -File -ErrorAction SilentlyContinue)
$psFiles += @(Get-Item -LiteralPath $here -Filter 'build.ps1' -ErrorAction SilentlyContinue)
foreach ($ps in $psFiles) {
    try {
        $bytes = [System.IO.File]::ReadAllBytes($ps.FullName)
        $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        if (-not $hasBom) {
            $text = [System.IO.File]::ReadAllText($ps.FullName, [System.Text.Encoding]::UTF8)
            [System.IO.File]::WriteAllText($ps.FullName, $text, (New-Object System.Text.UTF8Encoding($true)))
            Write-Host ("已补 UTF-8 BOM: " + $ps.Name) -ForegroundColor DarkYellow
        }
    } catch { }
}

$out = if ([System.IO.Path]::IsPathRooted($OutFile)) { $OutFile } else { Join-Path $here $OutFile }
$outDir = Split-Path -Parent $out
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

# ── 旧实例占着 exe 时 csc 会报 CS0016，先请它退出 ────────────────────────
if (-not $KeepRunning) {
    $outFull = [System.IO.Path]::GetFullPath($out)
    $running = @(Get-Process -Name 'WuwaOutlineTool' -ErrorAction SilentlyContinue | Where-Object {
        try { [System.IO.Path]::GetFullPath($_.Path) -eq $outFull } catch { $false }
    })
    if ($running.Count -gt 0) {
        Write-Host ("检测到目标 exe 正在运行（PID {0}），先关闭它再编译…" -f (($running | ForEach-Object { $_.Id }) -join ','))
        foreach ($p in $running) { try { [void]$p.CloseMainWindow() } catch { } }
        Start-Sleep -Milliseconds 900
        foreach ($p in $running) {
            try {
                if (-not $p.HasExited -and -not $p.WaitForExit(2500)) { $p.Kill(); [void]$p.WaitForExit(2500) }
            } catch { }
        }
    } else {
        $others = @(Get-Process -Name 'WuwaOutlineTool' -ErrorAction SilentlyContinue)
        if ($others.Count -gt 0) {
            Write-Host ("注：另有 {0} 个工具实例在运行，本次编译输出到别的路径，未打扰它们。" -f $others.Count) -ForegroundColor DarkGray
        }
    }
}

$sources = @(
    (Join-Path $srcDir 'WuwaOutlineTool.cs'),   # 数据类 + Core 逻辑 + CLI 入口
    (Join-Path $srcDir 'Ui.cs'),                # 主题 + 自绘控件 + 主窗口
    (Join-Path $srcDir 'Lang.cs'),              # 中英双语
    (Join-Path $srcDir 'LangData.cs')           # 由 tools\gen-lang.ps1 从 i18n\lang.en.tsv 生成
)

& (Join-Path $toolDir 'gen-lang.ps1') | Out-Host    # i18n\lang.en.tsv -> src\LangData.cs

$log = Join-Path ([System.IO.Path]::GetTempPath()) ('wuwa-build-' + [guid]::NewGuid().ToString('N') + '.txt')
$ico = Join-Path $assetDir 'app.ico'
$iconArg = @()
if (Test-Path -LiteralPath $ico) { $iconArg = @("/win32icon:$ico") }
& $csc /nologo /target:winexe /platform:anycpu /codepage:65001 /optimize+ /warn:4 `
    "/out:$out" `
    "/win32manifest:$(Join-Path $assetDir 'app.manifest')" `
    $iconArg `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    $sources 2>&1 | Out-File -LiteralPath $log -Encoding utf8

$code = $LASTEXITCODE
$diag = @(Get-Content -LiteralPath $log -ErrorAction SilentlyContinue)
Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue

if ($code -ne 0) {
    Write-Host '--- 编译器输出 ---' -ForegroundColor Red
    $diag | ForEach-Object { Write-Host $_ }
    throw "编译失败，退出码 $code"
}

# 只提示和本次改动相关的警告，别把满屏噪音倒出来
$warn = @($diag | Where-Object { $_ -match 'warning CS' })
if ($warn.Count -gt 0) {
    Write-Host ("{0} 条编译警告：" -f $warn.Count) -ForegroundColor DarkYellow
    $warn | ForEach-Object { Write-Host ('  ' + $_) -ForegroundColor DarkGray }
}

$fi = Get-Item -LiteralPath $out
Write-Host ("已生成: {0}  ({1} KB)" -f $fi.FullName, [Math]::Round($fi.Length / 1KB, 1)) -ForegroundColor Green

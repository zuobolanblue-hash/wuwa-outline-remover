#Requires -Version 5.1
<#
  去描边工具 · 离线验证（不开游戏就能跑完的部分）
  ================================================================
  第 0 级：工具自检 + 真实目录只读体检（不写任何文件）
  第 1 级：把一个 mod 整个复制到沙盒里，在副本上打补丁 → 查字节 → 还原 → 逐字节比对
          （证明"只改该改的字节、还原无损、重复打不叠加"，且完全不动游戏目录）

  用法（在项目任意位置都能跑，脚本自己认路径）：
    .\tools\wuwa-test.ps1                       跑第 0 + 1 级
    .\tools\wuwa-test.ps1 -Keep                 跑完不删沙盒（想自己翻文件时用）
    .\tools\wuwa-test.ps1 -Mods 'D:\...\Mods'   指定真实 Mods 目录（不指定就自动找常见位置）
    .\tools\wuwa-test.ps1 -Only 0               只跑第 0 级（最快，纯只读）

  退出码：0 全过 / 1 有失败项 / 2 环境问题
#>
[CmdletBinding()]
param(
    [string]$Mods = '',
    [string]$Exe  = '',
    [string]$Sandbox = '',
    [int]$Only = 0,
    [switch]$Keep
)

$ErrorActionPreference = 'Stop'
$fail = 0
$pass = 0

$root = Split-Path -Parent $PSScriptRoot                 # tools 的上一层 = 项目根目录
if (-not $Exe)     { $Exe     = Join-Path $root 'WuwaOutlineTool.exe' }
if (-not $Sandbox) { $Sandbox = Join-Path $root '_test\ladder' }
if (-not $Mods) {
    $Mods = @(
        'C:\XXMI\WWMI\Mods',
        'D:\XXMI\WWMI\Mods',
        (Join-Path $env:USERPROFILE 'XXMI\WWMI\Mods')
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if ($Mods -and -not (Test-Path -LiteralPath $Mods)) {
    Write-Host "提示：没找到 Mods 目录（$Mods）—— 第 0 级的真实目录部分跳过，第 1 级改用合成样本" -ForegroundColor Yellow
    $Mods = ''
}
$hasMods = [bool]($Mods -and (Test-Path -LiteralPath $Mods))
if (-not (Test-Path -LiteralPath $Exe)) { throw "找不到 exe：$Exe —— 请先跑 .\build.ps1 编译" }

function Ok([string]$m)   { $script:pass++; Write-Host ('  [PASS] ' + $m) -ForegroundColor Green }
function Bad([string]$m)  { $script:fail++; Write-Host ('  [FAIL] ' + $m) -ForegroundColor Red }
function Info([string]$m) { Write-Host ('         ' + $m) -ForegroundColor DarkGray }
function Head([string]$m) { Write-Host ''; Write-Host $m -ForegroundColor Cyan }

# CLI 输出用管道收（PowerShell 里直接 $o = & exe ... 拿不到 GUI 子系统程序的输出）
function RunTool {
    param([string[]]$ToolArgs)
    $tmp = Join-Path $env:TEMP ('wuwa-cli-' + [guid]::NewGuid().ToString('N') + '.txt')
    # 固定要中文输出：不带 -Lang 时命令行会跟随 settings.json 里的 lang=（用户在界面切过），
    # 而本脚本是按固定文案解析结果的，不能随用户偏好变
    if (-not ($ToolArgs -contains '-Lang')) { $ToolArgs = $ToolArgs + @('-Lang', 'zh') }
    & $Exe @ToolArgs 2>&1 | Out-File -LiteralPath $tmp -Encoding utf8
    $t = ''
    if (Test-Path -LiteralPath $tmp) { $t = [System.IO.File]::ReadAllText($tmp, [System.Text.Encoding]::UTF8) }
    Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
    return $t
}

function Get-Sha1([string]$p) { return (Get-FileHash -LiteralPath $p -Algorithm SHA1).Hash }

# 返回 @(R零, G零, B相同, A相同)
function Compare-Channels([string]$a, [string]$b) {
    $x = [System.IO.File]::ReadAllBytes($a)
    $y = [System.IO.File]::ReadAllBytes($b)
    if ($x.Length -ne $y.Length) { return @($false, $false, $false, $false) }
    $rZero = $true; $gZero = $true; $bSame = $true; $aSame = $true
    for ($i = 0; $i + 3 -lt $x.Length; $i += 4) {
        if ($x[$i]     -ne 0) { $rZero = $false }
        if ($x[$i + 1] -ne 0) { $gZero = $false }
        if ($x[$i + 2] -ne $y[$i + 2]) { $bSame = $false }
        if ($x[$i + 3] -ne $y[$i + 3]) { $aSame = $false }
        if (-not ($rZero -or $gZero -or $bSame -or $aSame)) { break }
    }
    return @($rZero, $gZero, $bSame, $aSame)
}

if (-not (Test-Path -LiteralPath $Exe)) { Write-Host "找不到工具：$Exe" -ForegroundColor Red; exit 2 }

# ═══════════════════════════ 第 0 级 ═══════════════════════════
Head '第 0 级 · 工具自检 + 真实目录只读体检（不写任何文件）'

$t = RunTool @('--selftest')
if ($t -match 'SELFTEST OK') { Ok '工具自检 19 项全过（SELFTEST OK）' }
else { Bad ('工具自检没通过：' + ($t -split "`r?`n" | Where-Object { $_ -match 'FAIL' } | Select-Object -First 3)) }

if (-not $hasMods) {
    Write-Host "真实 Mods 目录不存在：$Mods（第 0 级后半跳过）" -ForegroundColor Yellow
} else {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $t = RunTool @('--cli', '-Path', $Mods, '-Status')
    $sw.Stop()
    $line = ($t -split "`r?`n" | Where-Object { $_ -match '共 \d+ 个 Color.buf' } | Select-Object -First 1)
    if ($line) { Ok ('只读扫描完成（{0:N1} 秒）：{1}' -f $sw.Elapsed.TotalSeconds, $line.Trim()) }
    else { Bad '只读扫描没有正常输出' }

    $patched = @(($t -split "`r?`n") | Where-Object { $_ -match '已去描边' })
    Info ('其中已去描边 ' + $patched.Count + ' 个' + $(if ($patched.Count) { '：' + (($patched | ForEach-Object { ($_ -split '\s{2,}')[0].Trim() }) -join '、') } else { '' }))

    $t = RunTool @('--cli', '-Path', $Mods, '-Verify')
    $vline = ($t -split "`r?`n" | Where-Object { $_ -match '体检完成' } | Select-Object -First 1)
    if ($vline) {
        $issues = 0
        if ($vline -match '异常 (\d+)') { $issues = [int]$Matches[1] }
        if ($issues -eq 0) { Ok ('体检无异常：' + $vline.Trim()) } else { Bad ('体检有 ' + $issues + ' 个异常，先别进第 1 级') }
    } else { Bad '体检没有正常输出' }
}

if ($Only -lt 1) {
    Head '第 1 级 · 沙盒副本字节级验证（不碰游戏目录）'

    # 选样本：优先拿一个真实的小 mod；没有真实目录（新机器 / CI）就自己合成一个
    Info '正在挑一个合适的 mod 做样本…'
    $pick = $null
    $modDir = ''
    if ($hasMods) {
        $cands = Get-ChildItem -LiteralPath $Mods -Recurse -File -Force |
                 Where-Object { $_.Name -eq 'Color.buf' -and $_.Length -ge 8192 } |
                 Sort-Object Length
        foreach ($c in $cands) {
            $bytes = [System.IO.File]::ReadAllBytes($c.FullName)
            $hasOutline = $false
            for ($i = 0; $i + 3 -lt $bytes.Length; $i += 4) {
                if ($bytes[$i] -ne 0 -or $bytes[$i + 1] -ne 0) { $hasOutline = $true; break }
            }
            if ($hasOutline) { $pick = $c; break }
        }
        if ($pick) {
            $meshes = $pick.Directory
            while ($meshes -and $meshes.Name -ne 'Meshes') { $meshes = $meshes.Parent }
            $modDir = if ($meshes) { $meshes.Parent.FullName } else { $pick.Directory.Parent.FullName }
            Info ('样本（真实 mod）：' + $modDir)
        }
    }
    if (-not $modDir) {
        # 合成样本：2526 顶点，R=220 G=140（确实带描边）B=255 A=255
        $stage = Join-Path (Split-Path $Sandbox -Parent) 'SynthMod'
        if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
        $sd = Join-Path $stage 'Meshes'
        New-Item -ItemType Directory -Path $sd -Force | Out-Null
        $verts = 2526
        $seed = New-Object byte[] ($verts * 4)
        for ($i = 0; $i -lt $verts; $i++) {
            $seed[$i * 4] = 220; $seed[$i * 4 + 1] = 140; $seed[$i * 4 + 2] = 255; $seed[$i * 4 + 3] = 255
        }
        [System.IO.File]::WriteAllBytes((Join-Path $sd 'Color.buf'), $seed)
        $pick = Get-Item -LiteralPath (Join-Path $sd 'Color.buf')
        $modDir = $stage
        Info '没有可用的真实 mod —— 改用合成样本（2526 顶点）'
    }
    Info ('Color.buf {0:N0} 字节（{1:N0} 顶点）' -f $pick.Length, ($pick.Length / 4))

    if (Test-Path -LiteralPath $Sandbox) { Remove-Item -LiteralPath $Sandbox -Recurse -Force }
    New-Item -ItemType Directory -Path $Sandbox -Force | Out-Null
    $copy = Join-Path $Sandbox (Split-Path $modDir -Leaf)
    Copy-Item -LiteralPath $modDir -Destination $copy -Recurse -Force

    # PowerShell 5.1 没有 Path.GetRelativePath，手算
    $parentOfMod = Split-Path $modDir -Parent
    $relBuf = $pick.FullName.Substring($parentOfMod.Length).TrimStart('\')
    $buf = Join-Path $copy $relBuf
    if (-not (Test-Path -LiteralPath $buf)) {
        $buf = (Get-ChildItem -LiteralPath $copy -Recurse -File -Filter 'Color.buf' | Select-Object -First 1).FullName
    }
    $sha0 = Get-Sha1 $buf
    Info ('副本已就位：' + $copy)

    # 1) 打补丁
    $t = RunTool @('--cli', '-Path', $copy)
    if ($t -match '已去描边|已完成|成功') { Ok '打补丁命令正常返回' } else { Info ('打补丁输出：' + ($t -replace "`r?`n", ' / ')) }
    $sha1 = Get-Sha1 $buf
    if ($sha1 -ne $sha0) { Ok 'Color.buf 确实被改动了' } else { Bad 'Color.buf 没变化，补丁没生效' }

    # 2) 通道校验：R/G 归零，B/A 逐字节不变
    $origBak = @(Get-ChildItem -LiteralPath $copy -Recurse -File | Where-Object { $_.Name -match '\.orig\.[0-9a-fA-F]{8}\.bak$' }) | Select-Object -First 1
    if (-not $origBak) { Bad '没找到原始档 .orig.<hash8>.bak' }
    else {
        Ok ('自动留档：' + $origBak.Name)
        $ch = Compare-Channels $buf $origBak.FullName
        if ($ch[0]) { Ok 'R 通道（描边遮罩）已全部归零' } else { Bad 'R 通道还有非零值' }
        if ($ch[1]) { Ok 'G 通道（描边粗细）已全部归零' } else { Bad 'G 通道还有非零值' }
        if ($ch[2]) { Ok 'B 通道（皮肤遮罩）逐字节未动' } else { Bad 'B 通道被改了 —— 这是绝不能碰的' }
        if ($ch[3]) { Ok 'A 通道逐字节未动（Alpha=keep 模式的预期）' } else { Bad 'A 通道被改了' }
        if ((Get-Sha1 $origBak.FullName) -eq $sha0) { Ok '原始档内容 = 打补丁前的原文件' } else { Bad '原始档和原文件不一致' }
    }

    # 3) 重复打补丁：应幂等
    $t2 = RunTool @('--cli', '-Path', $copy)
    $sha2 = Get-Sha1 $buf
    if ($sha2 -eq $sha1) { Ok '重复打补丁不改变文件（幂等）' } else { Bad '重复打补丁又改了文件' }
    $bakCount = @(Get-ChildItem -LiteralPath $copy -Recurse -File | Where-Object { $_.Name -match '\.orig\.[0-9a-fA-F]{8}\.bak$' }).Count
    if ($bakCount -eq 1) { Ok '重复打补丁没有叠加多余的原始档（仍然 1 个）' } else { Bad ('原始档变成 ' + $bakCount + ' 个，说明重复留档了') }

    # 4) 还原：必须逐字节无损
    $t = RunTool @('--cli', '-Path', $copy, '-Restore')
    $sha3 = Get-Sha1 $buf
    if ($sha3 -eq $sha0) { Ok '还原后与原始文件逐字节一致（SHA1 相同）' } else { Bad '还原后和原文件不一致 —— 这是最严重的问题' }
    $t = RunTool @('--cli', '-Path', $copy, '-Status')
    if ($t -match '原版') { Ok '还原后状态回到「原版」' } else { Bad '还原后状态没回到原版' }

    if ($Keep) { Info ('沙盒保留在：' + $Sandbox) }
    else { Remove-Item -LiteralPath $Sandbox -Recurse -Force -ErrorAction SilentlyContinue; Info '沙盒已清理' }
}

# ═══════════════════════════ 汇总 ═══════════════════════════
Head '汇总'
Write-Host ("  通过 {0} 项，失败 {1} 项" -f $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
if ($fail -eq 0) {
    Write-Host '  离线部分全过 → 可以进游戏做第 2 级（单角色单变体）' -ForegroundColor Green
    exit 0
} else {
    Write-Host '  有失败项 → 先别改真实目录，把上面的 [FAIL] 发我' -ForegroundColor Red
    exit 1
}

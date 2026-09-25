#Requires -Version 5.1
<#
  鸣潮 Mods · 顶点色「兜底快照」
  ================================================================
  和 WuwaOutlineTool 互相独立：只负责备份 / 校验 / 还原「工具可能改到的文件」，
  不看工具的清单、不依赖工具的存档，所以工具的存档全丢了也能靠它回滚。

  覆盖范围（= WuwaOutlineTool 的改动半径，别的一律不碰）：
    Color.buf                       ← 被打补丁的文件本体
    *.bak / *.BAK / *.bak_nooutline ← 本工具的原始档 + Wuwa Mod Fixer 的备份
    *.nooutline.json                ← 本工具的清单

  用法：
    .\tools\wuwa-snapshot.ps1                新建一份快照（首次、以及每次大幅改动前跑）
    .\tools\wuwa-snapshot.ps1 -List          列出已有快照
    .\tools\wuwa-snapshot.ps1 -Verify        和最新快照逐字节比对（只读，不动任何文件）
    .\tools\wuwa-snapshot.ps1 -Verify -WithSidecars   连存档/清单一起比对
    .\tools\wuwa-snapshot.ps1 -RestoreAll    用最新快照整体覆盖回去（兜底最后手段）
    .\tools\wuwa-snapshot.ps1 -RestoreAll -Exact      顺带删掉快照之后新产生的清单/原始档
                                                （绝不删任何 Color.buf，也不删 mod）
    -Mods / -Store 不传就自动找：Mods 找常见安装位置，Store 用项目下的 _snapshots\
  退出码：一致 0 / 有差异 1 / 出错 2
#>
[CmdletBinding()]
param(
    [string]$Mods = '',
    [string]$Store = '',
    [switch]$List,
    [switch]$Verify,
    [switch]$RestoreAll,
    [switch]$WithSidecars,
    [switch]$Exact
)

$ErrorActionPreference = 'Stop'
$sw = [System.Diagnostics.Stopwatch]::StartNew()

$root = Split-Path -Parent $PSScriptRoot                 # tools 的上一层 = 项目根目录
if (-not $Store) { $Store = Join-Path $root '_snapshots' }
if (-not $Mods) {
    $Mods = @(
        'C:\XXMI\WWMI\Mods',
        'D:\XXMI\WWMI\Mods',
        (Join-Path $env:USERPROFILE 'XXMI\WWMI\Mods')
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

function Say([string]$m) { Write-Host $m }

# ---- 取「改动半径」内的文件（按文件名判定，不解析内容） -------------------
function Get-ScopeFiles {
    param([string]$Root, [switch]$ColorOnly)
    $all = Get-ChildItem -LiteralPath $Root -Recurse -File -Force -ErrorAction SilentlyContinue
    $hit = New-Object System.Collections.Generic.List[string]
    foreach ($f in $all) {
        $n = $f.Name
        if ($n -eq 'Color.buf') { $hit.Add($f.FullName); continue }
        if ($ColorOnly) { continue }
        if ($n -like '*.nooutline.json' -or $n -like '*.bak' -or $n -like '*.BAK' -or $n -like '*.bak_nooutline') {
            $hit.Add($f.FullName)
        }
    }
    return $hit
}

function Get-LatestSnap {
    param([string]$Store)
    if (-not (Test-Path -LiteralPath $Store)) { return $null }
    $d = Get-ChildItem -LiteralPath $Store -Directory -ErrorAction SilentlyContinue |
         Sort-Object Name -Descending | Select-Object -First 1
    if ($null -eq $d) { return $null }
    return $d.FullName
}

function Get-Manifest {
    param([string]$SnapDir)
    $p = Join-Path $SnapDir 'MANIFEST.tsv'
    if (-not (Test-Path -LiteralPath $p)) { throw "快照里找不到 MANIFEST.tsv：$SnapDir" }
    $map = @{}
    foreach ($line in [System.IO.File]::ReadAllLines($p, [System.Text.Encoding]::UTF8)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split "`t"
        if ($parts.Count -lt 3) { continue }
        $map[$parts[2]] = [pscustomobject]@{ Hash = $parts[0]; Size = [int64]$parts[1]; Rel = $parts[2] }
    }
    return $map
}

function Get-Hash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA1).Hash
}

function Get-Rel([string]$Root, [string]$Full) {
    $r = $Root.TrimEnd('\')
    if ($Full.StartsWith($r, [StringComparison]::OrdinalIgnoreCase)) {
        return $Full.Substring($r.Length).TrimStart('\')
    }
    return $Full
}

# ══════════════════════════════ -List ══════════════════════════════
if ($List) {
    $d = Get-LatestSnap $Store
    if (-not $d) { Say "还没有任何快照（$Store）"; exit 2 }
    Get-ChildItem -LiteralPath $Store -Directory | Sort-Object Name -Descending | ForEach-Object {
        $m = Join-Path $_.FullName 'MANIFEST.tsv'
        $cnt = 0; $sz = 0
        if (Test-Path -LiteralPath $m) {
            foreach ($l in [System.IO.File]::ReadAllLines($m, [System.Text.Encoding]::UTF8)) {
                if ([string]::IsNullOrWhiteSpace($l)) { continue }
                $p = $l -split "`t"; if ($p.Count -lt 3) { continue }
                $cnt++; $sz += [int64]$p[1]
            }
        }
        $mark = if ($_.FullName -eq $d) { ' ← 最新' } else { '' }
        Say ("{0}   {1,5} 个文件   {2,9:N1} MB{3}" -f $_.Name, $cnt, ($sz / 1MB), $mark)
    }
    exit 0
}

# ══════════════════════════════ -Verify ══════════════════════════════
if ($Verify) {
    $snap = Get-LatestSnap $Store
    if (-not $snap) { Say "还没有任何快照，先跑一次 .\wuwa-snapshot.ps1"; exit 2 }
    Say "快照：$snap"
    $man = Get-Manifest $snap
    Say ("清单里 {0} 个文件，开始重新校验…" -f $man.Count)

    $colorOnly = -not $WithSidecars
    $now = Get-ScopeFiles -Root $Mods -ColorOnly:$colorOnly

    $same = 0; $diff = New-Object System.Collections.Generic.List[string]
    $missing = New-Object System.Collections.Generic.List[string]
    $seen = New-Object 'System.Collections.Generic.HashSet[string]'

    foreach ($f in $now) {
        $rel = Get-Rel $Mods $f
        [void]$seen.Add($rel)
        if (-not $man.ContainsKey($rel)) {
            if ([System.IO.Path]::GetFileName($f) -eq 'Color.buf') {
                $diff.Add("$rel  (快照里没有 → 新增的 mod 或新文件)")
            } else {
                $diff.Add("$rel  (快照里没有 → 新增存档/清单)")
            }
            continue
        }
        $h = Get-Hash $f
        if ($h -eq $man[$rel].Hash) { $same++ } else { $diff.Add("$rel  (内容不同)") }
    }
    foreach ($rel in $man.Keys) {
        if ($rel -like '*\Color.buf' -or $rel -eq 'Color.buf') {
            if (-not $seen.Contains($rel)) { $missing.Add("$rel  (文件不在了)") }
        }
    }

    Say ''
    Say ("一致 {0} 个；不同 {1} 个；缺失 {2} 个" -f $same, $diff.Count, $missing.Count)
    if ($diff.Count -gt 0) {
        Say '--- 有差异（最多列 20 条）---'
        $diff | Select-Object -First 20 | ForEach-Object { Say ('  ' + $_) }
    }
    if ($missing.Count -gt 0) {
        Say '--- 缺失（最多列 20 条）---'
        $missing | Select-Object -First 20 | ForEach-Object { Say ('  ' + $_) }
    }
    Say ("耗时 {0:N1} 秒" -f $sw.Elapsed.TotalSeconds)
    if ($diff.Count -eq 0 -and $missing.Count -eq 0) { Say 'VERIFY OK：和快照逐字节一致'; exit 0 }
    Say 'VERIFY DIFF：与快照不一致（上面每一条都能单独查）'
    exit 1
}

# ══════════════════════════════ -RestoreAll ══════════════════════════════
if ($RestoreAll) {
    $snap = Get-LatestSnap $Store
    if (-not $snap) { Say "还没有任何快照，没法还原"; exit 2 }
    Say "用这个快照整体还原：$snap"
    if (-not (Test-Path -LiteralPath $Mods)) { Say "Mods 目录不存在：$Mods"; exit 2 }

    $copied = 0; $removed = 0
    $inSnap = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($f in (Get-ChildItem -LiteralPath $snap -Recurse -File -Force)) {
        $rel = Get-Rel $snap $f.FullName
        if ($rel -eq 'MANIFEST.tsv' -or $rel -eq 'META.txt') { continue }
        [void]$inSnap.Add($rel)
        $dst = Join-Path $Mods $rel
        $dir = Split-Path -Parent $dst
        if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        Copy-Item -LiteralPath $f.FullName -Destination $dst -Force
        $copied++
    }
    Say "已覆盖 $copied 个文件"

    if ($Exact) {
        # 只删"工具自己的产物"：清单 和 .orig.<hash8>.bak 原始档。
        # 绝不删 Color.buf，也绝不删 mod 里的其它东西。
        foreach ($f in (Get-ChildItem -LiteralPath $Mods -Recurse -File -Force -ErrorAction SilentlyContinue)) {
            $n = $f.Name
            $isSidecar = ($n -like '*.nooutline.json') -or ($n -match '\.orig\.[0-9a-fA-F]{8}\.bak$')
            if (-not $isSidecar) { continue }
            $rel = Get-Rel $Mods $f.FullName
            if (-not $inSnap.Contains($rel)) { Remove-Item -LiteralPath $f.FullName -Force; $removed++ }
        }
        Say "已删除快照之后新产生的清单/原始档 $removed 个"
    }

    Say ''
    Say '还原完成。建议接着跑一次自检比对：'
    Say ("  .\wuwa-snapshot.ps1 -Verify")
    exit 0
}

# ══════════════════════════════ 默认：新建快照 ══════════════════════════════
if (-not (Test-Path -LiteralPath $Mods)) { Say "Mods 目录不存在：$Mods"; exit 2 }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$dir = Join-Path $Store $stamp
New-Item -ItemType Directory -Path $dir -Force | Out-Null

Say "扫描 $Mods …"
$files = Get-ScopeFiles -Root $Mods
Say ("纳入范围 {0} 个文件（Color.buf + 存档 + 清单）" -f $files.Count)

$lines = New-Object System.Collections.Generic.List[string]
$total = [int64]0
$i = 0
foreach ($f in $files) {
    $i++
    $rel = Get-Rel $Mods $f
    $len = (Get-Item -LiteralPath $f).Length
    $h = Get-Hash $f
    $total += $len
    $lines.Add("$h`t$len`t$rel")
    $dst = Join-Path $dir $rel
    $d2 = Split-Path -Parent $dst
    if (-not (Test-Path -LiteralPath $d2)) { New-Item -ItemType Directory -Path $d2 -Force | Out-Null }
    Copy-Item -LiteralPath $f -Destination $dst -Force
    if ($i % 200 -eq 0) { Write-Host ("  …{0}/{1}" -f $i, $files.Count) }
}

$manPath = Join-Path $dir 'MANIFEST.tsv'
[System.IO.File]::WriteAllLines($manPath, $lines, (New-Object System.Text.UTF8Encoding($true)))

$meta = @(
    "创建时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    "Mods 目录: $Mods"
    "文件数: $($files.Count)"
    "总大小: $([math]::Round($total / 1MB, 1)) MB"
    "SHA1 清单: MANIFEST.tsv"
)
[System.IO.File]::WriteAllLines((Join-Path $dir 'META.txt'), $meta, (New-Object System.Text.UTF8Encoding($true)))

Say ''
Say "快照完成：$dir"
Say ("{0} 个文件，{1:N1} MB，耗时 {2:N1} 秒" -f $files.Count, ($total / 1MB), $sw.Elapsed.TotalSeconds)
Say ''
Say '以后随时可以：'
Say '  .\wuwa-snapshot.ps1 -Verify      只读比对（改完/还原完用来确认）'
Say '  .\wuwa-snapshot.ps1 -RestoreAll  整体回滚（兜底）'
exit 0

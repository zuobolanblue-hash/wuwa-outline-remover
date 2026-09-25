# wrap-i18n.ps1 —— 把所有含中文的字符串字面量包成 Lang.T("…")
#
#   .\tools\wrap-i18n.ps1                 处理 src\Ui.cs 与 src\WuwaOutlineTool.cs
#   .\tools\wrap-i18n.ps1 -DryRun         只报告会改多少处，不写文件
#
# 可重复运行（已经包过的会跳过）。注释行、Lang.cs / LangData.cs 不动。
param(
    [switch]$DryRun,
    [string[]]$Files = @('Ui.cs', 'WuwaOutlineTool.cs')
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot                  # tools 的上一层 = 项目根目录
$srcDir = Join-Path $root 'src'

# 这些位置不能包：case 标签 / const 常量
$linePattern = [regex]'"((?:[^"\\]|\\.)*)"'
$total = 0

foreach ($name in $Files) {
    $path = Join-Path $srcDir $name
    if (-not (Test-Path -LiteralPath $path)) { throw "找不到：$path" }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $bom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $nl = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $lines = $text -split [regex]::Escape($nl)
    $count = 0

    for ($li = 0; $li -lt $lines.Count; $li++) {
        $line = $lines[$li]
        $trim = $line.TrimStart()
        if ($trim.StartsWith('//') -or $trim.StartsWith('*') -or $trim.StartsWith('/*')) { continue }
        # 只保护「case 标签本身/const 常量」带中文的极端情况；
        # case 标签一般是 ASCII，行里其它中文值照常要包。
        if ($trim -match '^\s*case\s+"[^"]*[\u4e00-\u9fa5]') { continue }
        if ($trim -match '\bconst\s+string\b' -and $line -match '[\u4e00-\u9fa5]') { continue }

        $ms = @($linePattern.Matches($line))
        # 从后往前插，索引才不会乱
        for ($mi = $ms.Count - 1; $mi -ge 0; $mi--) {
            $m = $ms[$mi]
            if ($m.Groups[1].Value -notmatch '[\u4e00-\u9fa5\u3000-\u303f\uff00-\uffef]') { continue }
            $before = $line.Substring(0, $m.Index)
            if ($before.EndsWith('Lang.T(')) { continue }
            $line = $before + 'Lang.T(' + $m.Value + ')' + $line.Substring($m.Index + $m.Length)
            $count++
        }
        $lines[$li] = $line
    }

    Write-Host ("{0}: 包装 {1} 处" -f $name, $count)
    $total += $count
    if (-not $DryRun) {
        $out = ($lines -join "`r`n")
        [System.IO.File]::WriteAllText($path, $out, (New-Object System.Text.UTF8Encoding($bom)))
    }
}
Write-Host ("合计 {0} 处{1}" -f $total, $(if ($DryRun) { '（演练，未写盘）' } else { '' }))

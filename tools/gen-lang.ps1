# gen-lang.ps1 —— 把 i18n\lang.en.tsv 生成 src\LangData.cs（build.ps1 会自动调用，不用手工跑）
#
#   .\tools\gen-lang.ps1                             用默认路径
#   .\tools\gen-lang.ps1 -Tsv x.tsv -Out y.cs        指定别的输入 / 输出
#
# TSV 格式：一行一条，`中文原文 <TAB> English`，用 \r \n \t \\ 表示换行/制表/反斜杠。
# 生成时把每一行做 C# 字符串转义（\ -> \\，" -> \"），使运行期读到的正是"转义后的原文"，
# 交给 Lang.Unescape 还原，保证和源码里的字面量一一对上。
param(
    [string]$Tsv = '',
    [string]$Out = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot                 # tools 的上一层 = 项目根目录
if (-not $Tsv) { $Tsv = Join-Path $root 'i18n\lang.en.tsv' }
if (-not $Out) { $Out = Join-Path $root 'src\LangData.cs' }
$outDir = Split-Path -Parent $Out
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

if (-not (Test-Path -LiteralPath $Tsv)) { throw "找不到翻译表：$Tsv" }
$rows = [System.IO.File]::ReadAllLines($Tsv, [System.Text.Encoding]::UTF8)

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('// LangData.cs —— 由 gen-lang.ps1 从 lang.en.tsv 自动生成，请勿手工编辑')
[void]$sb.AppendLine('// 中文原文就是键；查不到就回退成中文。')
[void]$sb.AppendLine('namespace WuwaOutline')
[void]$sb.AppendLine('{')
[void]$sb.AppendLine('    internal static class LangData')
[void]$sb.AppendLine('    {')
[void]$sb.AppendLine('        public static readonly string[] Rows = new string[] {')

$n = 0
foreach ($row in $rows) {
    if ([string]::IsNullOrWhiteSpace($row)) { continue }
    if ($row -notmatch "`t") { throw ("第 " + ($n + 1) + " 行没有 TAB 分隔：$row") }
    $esc = $row.Replace('\', '\\').Replace('"', '\"')
    [void]$sb.AppendLine('            "' + $esc + '",')
    $n++
}
[void]$sb.AppendLine('        };')
[void]$sb.AppendLine('    }')
[void]$sb.AppendLine('}')

[System.IO.File]::WriteAllText($Out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($true)))
Write-Host ("LangData.cs 已生成：{0} 条翻译 -> {1}" -f $n, $Out)

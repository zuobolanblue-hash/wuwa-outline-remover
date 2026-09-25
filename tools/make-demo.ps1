#Requires -Version 5.1
<#
  make-demo.ps1 —— 造一套"假 mod"，用来在没有游戏、也不想碰真实 Mods 的情况下试工具
  ================================================================================
  生成结构（就是 WWMI mod 的样子，只是网格数据是合成的）：

    <Out>\_MANAGED_\group_1\01 Demo Character A\mod.ini + Meshes\Color.buf
    <Out>\_MANAGED_\group_1\02 Demo Character B\...
    <Out>\_MANAGED_\group_2\03 Demo Character C\...

  每个 Color.buf 都是"带描边"的状态（R=200 G=128：描边遮罩 + 描边粗细都不是 0），
  所以工具扫描后会显示成原版待处理；点一次「开始去描边」就能看到它的完整流程
  （留档 → 改字节 → 状态变绿 → 备份页出现还原点）。

  用法：
    .\tools\make-demo.ps1                     默认建在项目下的 _test\demo-mods（已在 .gitignore 里）
    .\tools\make-demo.ps1 -Out D:\demo-mods   指定别的位置
    .\WuwaOutlineTool.exe --scan D:\demo-mods 直接开界面扫它

  真实游戏里的 mod 永远不会被这个脚本碰到。
#>
[CmdletBinding()]
param(
    [string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) '_test\demo-mods'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$specs = @(
    [pscustomobject]@{ Group = 'group_1'; Name = '01 Demo Character A'; Verts = 2526 },
    [pscustomobject]@{ Group = 'group_1'; Name = '02 Demo Character B'; Verts = 4096 },
    [pscustomobject]@{ Group = 'group_2'; Name = '03 Demo Character C'; Verts = 1024 }
)

if (Test-Path -LiteralPath $Out) {
    if (-not $Force) { throw "$Out 已存在。想覆盖就加 -Force（只会重写这个目录）" }
    Remove-Item -LiteralPath $Out -Recurse -Force
}

$modIni = @"
; 演示用的最小 mod.ini：声明了顶点缓冲，所以顶点色方案对它有效
[TextureOverrideDemoBody]
hash = 0266ab77
handling = skip
ib = ResourceIndexBuffer
vb0 = ResourcePositionBuffer
vb1 = ResourceVectorBuffer
vb2 = ResourceTexcoordBuffer
vb3 = ResourceColorBuffer
drawindexed = auto
"@

foreach ($s in $specs) {
    $meshes = Join-Path $Out ("_MANAGED_\{0}\{1}\Meshes" -f $s.Group, $s.Name)
    New-Item -ItemType Directory -Path $meshes -Force | Out-Null

    # R=200（描边遮罩）G=128（描边粗细）B=255（皮肤遮罩，永远不该被改）A=255（头发描边）
    $buf = New-Object byte[] ($s.Verts * 4)
    for ($i = 0; $i -lt $s.Verts; $i++) {
        $buf[$i * 4]     = 200
        $buf[$i * 4 + 1] = 128
        $buf[$i * 4 + 2] = 255
        $buf[$i * 4 + 3] = 255
    }
    [System.IO.File]::WriteAllBytes((Join-Path $meshes 'Color.buf'), $buf)
    [System.IO.File]::WriteAllText((Join-Path $meshes '..\mod.ini'), $modIni, (New-Object System.Text.UTF8Encoding($false)))

    Write-Host ("  造好：{0}  ({1:N0} 顶点, {2:N0} 字节)" -f $s.Name, $s.Verts, $buf.Length)
}

Write-Host ''
Write-Host ("演示 Mods 已生成：{0}" -f $Out) -ForegroundColor Green
Write-Host ("接着跑：.\WuwaOutlineTool.exe --scan `"{0}`"" -f $Out) -ForegroundColor DarkGray

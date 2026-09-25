# 更新日志（Changelog）

格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [1.0.0] - 2026-09-25

第一个正式版本。核心思路：**描边粗细写在 mod 自己的顶点色里，所以按数据改** ——
不认角色、不依赖 shader hash、不随游戏版本失效。

### Added

- **去描边**：把 `Meshes\Color.buf` 每个顶点的 R（Outline Mask）、G（Outline Thickness）清零。
  B（Skin Mask）永不改动。可选 `Alpha=0` 连头发那圈也去掉（A=255 作为备选取值）。
- **自动留档**：改动前把原始文件存成 `Color.buf.orig.<hash8>.bak`，并写清单 `Meshes\.nooutline.json`，
  存档放在**每个 mod 自己的文件夹里**，所以重命名、挪位置、被 mod manager 改成 `DISABLED xxx` 都不会丢。
- **一键还原**：还原到打补丁前 / 还原到最早版本 / 还原选中的某一版；还原后与原文件逐字节一致。
- **兼容 Wuwa Mod Fixer**：识别并只读复用它的 `.BAK`，永不修改或删除。
- **体检（只读）**：`-Verify` 检查记录完整性、原始档是否丢失、有无无主产物。
- **清理**：`-Clean` 删本工具在这个 mod 里的存档与清单；`-PurgeOrphans` 清"mod 已被删/被换"后留下的零散产物。
- **图形界面**：扁平风格、DPI 自适应（125% / 150% 不糊不溢出）、状态列配色与人话悬浮解释、
  双击某行直接跳到它的还原点列表。
- **中英文切换**：界面右上角 `中文 / EN` 一键切换，结果记进 `settings.json`；CLI 用 `-Lang en|zh`。
  翻译表在 `i18n\lang.en.tsv`，记事本改完重新编译即可，漏翻自动回退中文。
- **命令行**：`-Status / -History / -Verify / -Restore / -Clean / -PurgeOrphans / -Alpha / -DryRun / -Deep`。
- **离线自证**：`--selftest`（19 项）+ `tools\wuwa-test.ps1`（沙盒副本上逐字节验证：只改该改的字节、
  还原无损、重复运行幂等）；没有游戏目录时自动改用合成样本，新机器和 CI 都能跑。
- **兜底快照**：`tools\wuwa-snapshot.ps1` 独立于本工具存档，只备份工具的改动半径，可整体回滚。

### Notes

- 只对**装了皮肤 mod 的角色**生效；纯原版角色的描边来自游戏本体 shader，本工具管不到。
- 只声明贴图的"仅换贴图"型 mod 画的是游戏原版网格，改了没效果 —— 工具会识别并标成
  `未使用顶点色(改了无效)`，跳过不写盘。
- 编译只需要 Windows 自带的 .NET Framework 编译器（`csc.exe`），无需 SDK、无需联网。

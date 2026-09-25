---
name: Bug 报告
about: 工具没按预期工作（改完没效果、还原不了、界面不对等）
title: '[Bug] '
labels: bug
assignees: ''
---

## 环境

* 工具版本：`WuwaOutlineTool.exe --selftest` 第一行，或界面右下角 `v…`
* Windows 版本：
* 屏幕缩放（100% / 125% / 150%）：（界面相关的 bug 请务必填）
* Mods 目录大概长什么样（`_MANAGED_` 还是散装、用没用 mod manager）：

## 你做了什么

1. 我选了哪个目录：
2. 点了什么（扫描 / 开始去描边 / 某个还原按钮）：
3. Alpha 选项选的哪个（保持不动 / 去掉头发描边 / A=255）：

## 期望 vs 实际

* 期望：
* 实际：

## 已经跑过的只读检查（请把输出贴上）

> 这两步**只读，不会改任何文件**，但能说明九成的问题

```
.\WuwaOutlineTool.exe --cli -Path "<你的目录>" -Verify
.\WuwaOutlineTool.exe --selftest
```

```text
（把输出贴在这里）
```

## 日志

界面 CONSOLE 面板最后 20 行，或项目目录下 `WuwaOutlineTool.log` 的最后 20 行：

```text
（贴在这里）
```

## 截图（界面问题必填）

拖进来即可。

## 补充

* 装过 Wuwa Mod Fixer 吗？
* 是不是"只换贴图"型的 mod（`mod.ini` 里只有贴图、没有 `ib=` / `vb*=`）？
* 游戏里有没有按 **F10** 重载 WWMI？

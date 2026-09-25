# 贡献指南（Contributing）

先说结论：**任何能证明"更安全 / 更准 / 更好用"的改动都欢迎**。这个工具动的是玩家辛苦收集的 mod 文件，
所以第一原则是**不许弄坏别人的东西**。

## 一、提 issue 之前

1. 先看 README 的「常见问题」——「改完没效果」「头发还有一圈」基本都在那儿有答案。
2. 用 `--cli -Path "<目录>" -Verify` 跑一次只读体检，把输出贴上（**只读，不会改任何文件**）。
3. 顺手把 `WuwaOutlineTool.log` 的最后 20 行贴上。

## 二、开发环境

不需要装任何 SDK：

* Windows 10/11 自带 .NET Framework 4.x 的 `csc.exe`
* PowerShell 5.1（系统自带）
* 编辑器随意，但请确保 `.ps1` 存成 **UTF-8 带 BOM**
  （Windows PowerShell 5.1 会把无 BOM 的 UTF-8 当 GBK 读，带中文的脚本会直接语法报错；
  万一忘了，跑一次 `build.ps1` 会自动补回来）

```powershell
# 编译（产物：根目录的 WuwaOutlineTool.exe）
.\build.ps1

# 工具开着时编译到别处，不打扰正在运行的实例
.\build.ps1 -OutFile _staging\new.exe
```

### 代码约束（重要）

源码按 **C# 5** 写，因为用的是系统自带的 csc（`/langversion` 默认就是 5）：

* 不能用字符串插值 `$"..."`、`?.`、表达式体成员、自动属性初始化器
* 用 `delegate { }` 而不是 lambda（同项目风格）
* 面向用户的文案一律写成 `Lang.T("中文原文")`，中文原文就是翻译表的键

## 三、怎么加一条翻译

不需要动 C#：

1. 打开 `i18n\lang.en.tsv`，追加一行：`中文原文<TAB>English`
   （换行写 `\r\n`、制表写 `\t`、反斜杠写 `\\`，因为一条必须占一行）
2. 跑 `.\build.ps1`（会自动执行 `tools\gen-lang.ps1` 生成 `src\LangData.cs` 编进 exe）
3. 界面上点 `EN` 看效果。查不到的条目会原样显示中文，不会空白也不会崩

## 四、改完必须自己验

```powershell
# 核心逻辑自检（19 项，造临时数据，不碰游戏目录）
.\WuwaOutlineTool.exe --selftest

# 离线验证阶梯：自检 + 真实目录只读体检 + 沙盒副本逐字节验证
.\tools\wuwa-test.ps1

# 没有游戏目录也能跑（用合成样本）
.\tools\wuwa-test.ps1 -Mods 'D:\__none__'
```

**只要改到了字节级逻辑（R/G/B/A 通道、存档、清单、还原），PR 里必须附上 `wuwa-test.ps1` 全过的输出。**
涉及界面改动请附截图（中英各一张更好）。

## 五、提交 PR

* 一个 PR 只做一件事，别把格式化跟功能混在一起
* commit message 建议用 `type(scope): 说明` 的形式，例如
  `fix(ui): 回滚页提示文字压住列表卡片`、`feat(i18n): 增加英文字幕`
* PR 模板里有清单，照着勾
* 如果你手上有真实 mod 目录，**请只在自己的副本上试**，不要提交任何来自游戏的原始文件、
  也不要提交第三方 mod 的内容（`_test\` / `_snapshots\` 已在 `.gitignore` 里）

## 六、不要做的事

* 不要引入任何需要联网的构建步骤、包管理器或第三方二进制
* 不要提交游戏素材、官方 logo、第三方 mod 文件（图标请用 `tools\make-icon.ps1 -Design` 生成，
  或你**自己拥有版权**的图：`tools\make-icon.ps1 -Source 你的图.png`）
* 不要动 `B`（Skin Mask）通道 —— 那是皮肤遮罩，改了角色会变色
* 不要把"备份/还原"换成"直接覆盖"—— 任何破坏可还原性的改动都不会被合并

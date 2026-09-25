// Ui.cs —— 现代化界面（扁平风格、圆角卡片、大按钮、状态配色）
// 与 WuwaOutlineTool.cs 里的 Core 共用同一套逻辑与磁盘状态格式。

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WuwaOutline
{
    // ───────────────────────── 主题 ─────────────────────────
    internal static class Theme
    {
        public static float Scale = 1f;
        public static int S(int px) { return (int)Math.Round(px * Scale); }

        public static readonly Color Bg = Color.FromArgb(241, 243, 247);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(226, 230, 236);
        public static readonly Color BorderStrong = Color.FromArgb(206, 212, 220);
        public static readonly Color Text = Color.FromArgb(28, 32, 38);
        public static readonly Color SubText = Color.FromArgb(112, 120, 132);
        public static readonly Color Accent = Color.FromArgb(45, 108, 235);
        public static readonly Color AccentHover = Color.FromArgb(30, 88, 210);
        public static readonly Color AccentSoft = Color.FromArgb(233, 240, 254);
        public static readonly Color Green = Color.FromArgb(24, 145, 88);
        public static readonly Color Orange = Color.FromArgb(198, 122, 22);
        public static readonly Color Red = Color.FromArgb(198, 58, 58);
        public static readonly Color ConsoleBg = Color.FromArgb(26, 28, 33);
        public static readonly Color ConsoleText = Color.FromArgb(214, 219, 226);

        public static readonly Font Body;
        public static readonly Font BodyBold;
        public static readonly Font Title;
        public static readonly Font TitleSub;
        public static readonly Font Small;
        public static readonly Font Mono;
        public static readonly Font Button;

        static Theme()
        {
            string family = "Microsoft YaHei UI";
            Body = new Font(family, 10F);
            BodyBold = new Font(family, 10F, FontStyle.Bold);
            Title = new Font(family, 15F, FontStyle.Bold);
            TitleSub = new Font(family, 9.5F);
            Small = new Font(family, 9F);
            Button = new Font(family, 10F);
            Mono = new Font("Consolas", 9.5F);
        }

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = Math.Max(2, radius * 2);
            if (d > r.Width) d = Math.Max(2, r.Width);
            if (d > r.Height) d = Math.Max(2, r.Height);
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // ───────────────────────── 自绘按钮 ─────────────────────────
    internal class FlatButton : Button
    {
        public bool Accent;
        public bool Small;
        public int Radius = 8;
        private bool _hover, _down;

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Theme.Card;
            Font = Theme.Button;
            Height = Theme.S(38);
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            MouseEnter += delegate { _hover = true; Invalidate(); };
            MouseLeave += delegate { _hover = false; _down = false; Invalidate(); };
            MouseDown += delegate { _down = true; Invalidate(); };
            MouseUp += delegate { _down = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill, border, text;

            if (!Enabled)
            {
                fill = Color.FromArgb(238, 240, 244);
                border = Color.FromArgb(228, 231, 236);
                text = Color.FromArgb(168, 174, 183);
            }
            else if (Accent)
            {
                fill = (_hover || _down) ? Theme.AccentHover : Theme.Accent;
                border = fill;
                text = Color.White;
            }
            else
            {
                fill = _down ? Color.FromArgb(229, 234, 241) : (_hover ? Color.FromArgb(246, 248, 251) : Theme.Card);
                border = _hover ? Theme.BorderStrong : Theme.Border;
                text = Theme.Text;
            }

            using (var path = Theme.Rounded(r, Radius))
            using (var b = new SolidBrush(fill))
                g.FillPath(b, path);
            using (var path = Theme.Rounded(r, Radius))
            using (var pen = new Pen(border))
                g.DrawPath(pen, path);

            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, Text, Font, r, text, flags);
        }
    }

    // 分段选择按钮（当页签用）
    internal class TabPill : FlatButton
    {
        public bool Selected;
        public TabPill() { Radius = 9; }

        protected override void OnPaint(PaintEventArgs pe)
        {
            if (!Selected) { base.OnPaint(pe); return; }

            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Rounded(r, Radius))
            using (var b = new SolidBrush(Theme.Accent))
                g.FillPath(b, path);
            TextRenderer.DrawText(g, Text, Font, r, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // 圆角卡片容器
    internal class CardPanel : Panel
    {
        public int Radius = 12;
        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Rounded(r, Radius))
            using (var b = new SolidBrush(Theme.Card))
                g.FillPath(b, path);
            using (var path = Theme.Rounded(r, Radius))
            using (var pen = new Pen(Theme.Border))
                g.DrawPath(pen, path);
        }
    }

    // 双缓冲表格（避免滚动闪烁）
    internal class FlatGrid : DataGridView
    {
        public FlatGrid() { DoubleBuffered = true; }
    }

    // ───────────────────────── 主窗口 ─────────────────────────
    internal class MainForm : Form
    {
        private CardPanel headerCard, optsCard, gridCard, listCard, consoleCard, tabCard;
        private Panel contentHost, pageApply, pageRollback, statusPanel;
        private ToolTip tip;
        private TextBox txtRoot;
        private CardPanel pathBox;
        private FlatButton btnBrowse, btnScan, btnApply;
        private TabPill tabApply, tabRollback;
        private ComboBox cboAlpha;
        private CheckBox chkSelectedOnly, chkDryRun, chkDeep;
        private FlatGrid grid;
        private Label emptyHint;
        private ListView lvPoints;
        private FlatButton btnHistory, btnRestoreSel, btnRestoreLatest, btnRestoreEarliest;
        private FlatButton btnClean, btnVerify, btnPurge, btnClearLog;
        private RichTextBox txtLog;
        private Label lblTitle, lblSub, lblDir, lblAlpha, lblAlphaHint, lblConsole, lblRollbackHint;
        private Label lblStatusDot, lblStatus, lblCount, lblVer;

        private readonly List<Entry> _entries = new List<Entry>();
        private Entry _pointsEntry;   // 回滚页当前列的是哪个 mod 的还原点
        private TabPill pillZh, pillEn;   // 中 / EN 切换
        private int _tab;                 // 当前页签，重建界面时用来还原
        private int _cntTotal = -1, _cntIssues, _cntNotes;   // 扫描统计（换语言时按新语言重排）
        private double _cntSecs;
        private volatile bool _busy;

        private string SettingsPath
        {
            get { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "settings.json"); }
        }

        public MainForm()
        {
            // 构造函数里控件还没有句柄，DeviceDpi 会返回 96；直接从桌面 DC 取真实 DPI
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) Theme.Scale = g.DpiX / 96f; } catch { }
            if (Theme.Scale <= 0.1f || Theme.Scale > 4f) Theme.Scale = 1f;

            Text = Lang.T("鸣潮 去描边工具");
            BackColor = Theme.Bg;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(Theme.S(900), Theme.S(620));
            ClientSize = new Size(Theme.S(1240), Theme.S(780));
            DoubleBuffered = true;

            // 标题栏 / 任务栏图标：显式取 exe 自带的那个（WinForms 不设 Icon 时会用
            // 它自己的默认图标，光给 exe 嵌图标是不够的 —— 标题栏还是那片红黄蓝方块）。
            try { Icon = new System.Drawing.Icon(Application.ExecutablePath, SystemInformation.IconSize); }
            catch
            {
                try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
                catch { }
            }

            BuildAll();

            Core.Log = AppendLog;
            AppendLog(Lang.T("鸣潮 去描边工具 v") + Core.ToolVer + Lang.T("  ·  顶点色方案（R=描边遮罩  G=描边厚度  B=皮肤遮罩不改  A=头发厚度）"));
            AppendLog(Lang.T("与 Wuwa Mod Fixer 的 .BAK 备份互通（只读使用）。改完回游戏按 F10 重载 WWMI。"));

            LoadSettings();
            Load += delegate { DoLayout(); };
            Resize += delegate { DoLayout(); };
            FormClosing += delegate { SaveSettings(); };
        }

        // ── 构建 / 重建 ───────────────────────────────────────
        // 单独抽出来：切换中英时整份重建，保证一处文案都不漏。
        private void BuildAll()
        {
            Text = Lang.T("鸣潮 去描边工具");   // 标题栏也跟着语言走（重建时同样生效）
            BuildHeader();
            BuildSegments();
            BuildOptions();
            BuildPages();
            BuildConsole();
            BuildStatus();

            // 把各容器挂到窗体上（位置统一由 DoLayout 计算）
            Controls.Add(headerCard);
            Controls.Add(tabCard);
            Controls.Add(optsCard);
            Controls.Add(contentHost);
            Controls.Add(consoleCard);
            Controls.Add(statusPanel);
            SwitchTab(_tab);
            AttachTips();
        }

        // ── 构建 ──────────────────────────────────────────────
        private void BuildHeader()
        {
            headerCard = new CardPanel();
            lblTitle = new Label { Text = Lang.T("鸣潮 去描边工具"), Font = Theme.Title, ForeColor = Theme.Text, AutoSize = true, BackColor = Color.Transparent };
            lblSub = new Label { Text = Lang.T("给皮肤 mod 去掉角色卡通描边  ·  原始数据自动留档，可随时还原  ·  改完回游戏按 F10 重载"), Font = Theme.TitleSub, ForeColor = Theme.SubText, AutoSize = true, BackColor = Color.Transparent };
            lblDir = new Label { Text = Lang.T("MOD 目录"), Font = Theme.Small, ForeColor = Theme.SubText, AutoSize = true, BackColor = Color.Transparent };

            pathBox = new CardPanel { Radius = 8 };
            txtRoot = new TextBox { BorderStyle = BorderStyle.None, Font = Theme.Body, BackColor = Theme.Card, ForeColor = Theme.Text };
            txtRoot.Text = Core.DetectDefaultMods();      // 常见安装位置里有就用，没有就留空（靠提示文字引导）
            SetCue(txtRoot, Lang.T("点『浏览…』选你的 Mods 目录"));
            pathBox.Controls.Add(txtRoot);

            const int rowH = 38;
            btnBrowse = new FlatButton { Text = Lang.T("浏览…"), Width = Theme.S(92), Height = Theme.S(rowH) };
            btnScan = new FlatButton { Text = Lang.T("扫描"), Width = Theme.S(92), Height = Theme.S(rowH) };
            btnApply = new FlatButton { Text = Lang.T("开始去描边"), Accent = true, Width = Theme.S(168), Height = Theme.S(rowH), Radius = 10 };
            btnApply.Font = Theme.BodyBold;

            btnBrowse.Click += delegate
            {
                var dlg = new FolderBrowserDialog();
                dlg.Description = Lang.T("选择要处理的 mod 目录（可指到某一组，也可指到整个 Mods）");
                if (Directory.Exists(txtRoot.Text)) dlg.SelectedPath = txtRoot.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) { SetRoot(dlg.SelectedPath); SaveSettings(); }
            };
            btnScan.Click += delegate { ScanAsync(); };
            btnApply.Click += delegate { ApplyAsync(); };

            // 中 / EN 切换（放在顶栏右上角，和页签同样的胶囊样式）
            pillZh = new TabPill { Text = "中文", Width = Theme.S(66), Height = Theme.S(30), Selected = !Lang.En };
            pillEn = new TabPill { Text = "EN", Width = Theme.S(56), Height = Theme.S(30), Selected = Lang.En };
            pillZh.Font = Theme.BodyBold;
            pillEn.Font = Theme.BodyBold;
            pillZh.Click += delegate { SetLang(false); };
            pillEn.Click += delegate { SetLang(true); };

            headerCard.Controls.AddRange(new Control[] { lblTitle, lblSub, lblDir, pathBox, btnBrowse, btnScan, btnApply, pillZh, pillEn });
        }

        private void BuildSegments()
        {
            // 页签放进和其它区域同宽的卡片里，左右边缘与上下卡片对齐
            tabCard = new CardPanel { Radius = 10 };
            tabApply = new TabPill { Text = Lang.T("去描边"), Width = Theme.S(150), Height = Theme.S(40), Selected = true };
            tabRollback = new TabPill { Text = Lang.T("备份 / 回滚"), Width = Theme.S(150), Height = Theme.S(40) };
            tabApply.Click += delegate { SwitchTab(0); };
            tabRollback.Click += delegate { SwitchTab(1); };
            tabCard.Controls.Add(tabApply);
            tabCard.Controls.Add(tabRollback);
        }

        private void BuildOptions()
        {
            optsCard = new CardPanel();
            lblAlpha = new Label { Text = Lang.T("头发描边怎么处理"), Font = Theme.Small, ForeColor = Theme.SubText, AutoSize = true, BackColor = Color.Transparent };
            cboAlpha = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Theme.Body, FlatStyle = FlatStyle.Flat };
            cboAlpha.Items.AddRange(new object[] { Lang.T("保持不动（推荐先这样）"), Lang.T("去掉头发描边（A=0）"), Lang.T("A=255（另一种，一般不用）") });
            cboAlpha.SelectedIndex = 0;
            lblAlphaHint = new Label { Text = Lang.T("身体的描边去掉了、头发还留一圈 → 选第二项再来一次"), Font = Theme.Small, ForeColor = Theme.SubText, AutoSize = true, BackColor = Color.Transparent };
            chkSelectedOnly = new CheckBox { Text = Lang.T("只处理表格里选中的行"), Font = Theme.Body, ForeColor = Theme.Text, AutoSize = true, BackColor = Color.Transparent };
            chkDryRun = new CheckBox { Text = Lang.T("演练：只显示结果，不改文件"), Font = Theme.Body, ForeColor = Theme.Text, AutoSize = true, BackColor = Color.Transparent };
            chkDeep = new CheckBox { Text = Lang.T("大文件也精确统计顶点数（慢）"), Font = Theme.Body, ForeColor = Theme.Text, AutoSize = true, BackColor = Color.Transparent };
            optsCard.Controls.AddRange(new Control[] { lblAlpha, cboAlpha, lblAlphaHint, chkSelectedOnly, chkDryRun, chkDeep });
        }

        private void BuildPages()
        {
            contentHost = new Panel { BackColor = Theme.Bg };

            // 去描边页
            pageApply = new Panel { BackColor = Theme.Bg };
            gridCard = new CardPanel();
            grid = new FlatGrid
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                AllowUserToResizeRows = false,
                BorderStyle = BorderStyle.None,
                BackgroundColor = Theme.Card,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(238, 241, 245),
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                ScrollBars = ScrollBars.Both
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.SubText;
            grid.ColumnHeadersDefaultCellStyle.Font = Theme.BodyBold;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(Theme.S(10), 0, 0, 0);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = Theme.S(40);
            grid.RowTemplate.Height = Theme.S(34);
            grid.DefaultCellStyle.Font = Theme.Body;
            grid.DefaultCellStyle.ForeColor = Theme.Text;
            grid.DefaultCellStyle.BackColor = Theme.Card;
            grid.DefaultCellStyle.SelectionBackColor = Theme.AccentSoft;
            grid.DefaultCellStyle.SelectionForeColor = Theme.Text;
            grid.DefaultCellStyle.Padding = new Padding(Theme.S(10), 0, Theme.S(6), 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 253, 254);

            grid.Columns.Add(Col("Mod", Lang.T("皮肤 / mod"), 220, true, Lang.T("mod 文件夹名，就是 mod manager 里显示的那个名字")));
            grid.Columns.Add(Col("Verts", Lang.T("顶点数"), 84, false, Lang.T("这个模型有多少个顶点（Color.buf 字节数 ÷ 4）")));
            grid.Columns.Add(Col("StateText", Lang.T("状态"), 205, false, Lang.T("绿=已去描边；灰=原版未处理；橙=需注意（内容被改过 / 本来就没有描边数据 / mod 没使用顶点色）")));
            grid.Columns.Add(Col("GOnText", Lang.T("带描边顶点"), 100, false, Lang.T("原本有多少个顶点带着描边数据。0 = 本来就没有描边，无需处理")));
            grid.Columns.Add(Col("ManAlpha", "Alpha", 78, false, Lang.T("这份文件打补丁时用的头发处理参数；- 表示还没处理过")));
            grid.Columns.Add(Col("OrigLabel", Lang.T("原始档"), 150, false, Lang.T("可还原回去的原始文件来源：mine = 本工具存档，legacy = 旧版备份，fixer = Wuwa Mod Fixer 的 .BAK")));
            grid.Columns.Add(Col("BakText", Lang.T("我的备份"), 100, false, Lang.T("本工具在这个 mod 里占用的备份空间。点「清理本工具存档」可释放（之后就不能还原了）")));
            grid.Columns.Add(Col("FixerCount", Lang.T("Fixer 备份"), 92, false, Lang.T("Wuwa Mod Fixer 在这个 mod 里留下的 .BAK 数量（本工具只读引用，永不修改）")));

            // 悬浮到「状态」格上给出人话解释
            grid.CellToolTipTextNeeded += delegate(object s, DataGridViewCellToolTipTextNeededEventArgs ev)
            {
                if (ev.RowIndex < 0 || ev.ColumnIndex < 0) return;
                var entry = grid.Rows[ev.RowIndex].Tag as Entry;
                if (entry == null) return;
                string col = grid.Columns[ev.ColumnIndex].DataPropertyName;
                string t = null;
                if (col == "StateText")
                {
                    if (!entry.ColorUsed)
                    {
                        t = Lang.T("这个 mod 的 ini 里没有声明自己的 Color.buf（只声明了贴图，没有 ib= / vb0..vb4= 这类顶点缓冲绑定）。") +
                            Lang.T("\r\n说明它画的是**游戏原版网格**，所以它自己的顶点色根本不会被加载 —— 清 R/G 不会有任何效果，工具会跳过它。") +
                            Lang.T("\r\n游戏里看到的描边是原版模型的描边，要处理得靠 shader 级的去描边 mod。");
                    }
                    else switch (entry.State)
                    {
                        case "patched": t = Lang.T("已经去描边了。原始数据保存在这个 mod 的「我的备份」里，随时可以还原。"); break;
                        case "pristine": t = Lang.T("内容和已记录的原始档一致 —— 也就是说：目前是原版状态（已还原或从未改动）。"); break;
                        case "modified": t = Lang.T("这个文件被外部改动过（作者更新了 mod，或别的工具改过）。再点「开始去描边」会以现在的内容为新基准重打，并另存一份新的原始档。"); break;
                        default:
                            t = entry.StateText.StartsWith(Lang.T("空顶点色")) || entry.StateText.Contains(Lang.T("本就无描边"))
                                ? Lang.T("这份模型本来就没有描边数据（多半是武器 / 发饰 / 特效网格），工具会跳过它，不做任何改动。")
                                : Lang.T("还没处理过。点「开始去描边」会清掉描边，并自动在这个 mod 里留一份原始档。");
                            break;
                    }
                    if (entry.StateText.Contains(Lang.T("无原始档"))) t += Lang.T("\r\n注意：找不到原始档，还原不了（可能是备份被删了）。");
                    if (entry.StateText.Contains(Lang.T("有Fixer"))) t += Lang.T("\r\n这个 mod 里还有 Wuwa Mod Fixer 的备份，也能作为还原点使用。");
                }
                else if (col == "GOnText") { t = Lang.T("原本带着描边数据的顶点数：") + (entry.GOn < 0 ? Lang.T("（大文件未统计，可勾选「大文件也精确统计顶点数」）") : entry.GOn.ToString()); }
                else if (col == "Verts") { t = Lang.T("顶点数：") + entry.Verts + Lang.T("（约 ") + Core.FormatSize(new FileInfo(entry.BufPath).Length) + Lang.T("）"); }
                if (t != null) ev.ToolTipText = t;
            };

            grid.CellFormatting += delegate(object s, DataGridViewCellFormattingEventArgs ev)
            {
                if (ev.RowIndex < 0 || ev.ColumnIndex < 0) return;
                if (grid.Columns[ev.ColumnIndex].DataPropertyName != "StateText") return;
                var e = ev.RowIndex < _entries.Count ? _entries[ev.RowIndex] : null;
                Entry entry = null;
                if (grid.Rows[ev.RowIndex].Tag is Entry) entry = (Entry)grid.Rows[ev.RowIndex].Tag; else entry = e;
                if (entry == null) return;
                Color c = Theme.Text;
                string st = entry.StateText;
                if (!entry.ColorUsed) c = Theme.Orange;
                else if (st.StartsWith(Lang.T("已去描边"))) c = Theme.Green;
                else if (entry.State == "modified") c = Theme.Orange;
                else if (st.StartsWith(Lang.T("空顶点色")) || st.Contains(Lang.T("本就无描边"))) c = Theme.Orange;
                else if (entry.State == "untracked") c = Theme.SubText;
                ev.CellStyle.ForeColor = c;
                ev.CellStyle.Font = Theme.BodyBold;
            };
            grid.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs ev)
            {
                if (ev.RowIndex >= 0) { SwitchTab(1); ShowHistory(); }
            };

            emptyHint = new Label
            {
                Text = Lang.T("还没有扫描结果\r\n\r\n点右上角「扫描」列出所有皮肤 mod，然后点「开始去描边」"),
                Font = Theme.Body,
                ForeColor = Theme.SubText,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Theme.Card
            };

            gridCard.Controls.Add(grid);
            gridCard.Controls.Add(emptyHint);
            pageApply.Controls.Add(gridCard);

            // 回滚页
            pageRollback = new Panel { BackColor = Theme.Bg };
            btnHistory = new FlatButton { Text = Lang.T("查看选中 mod 的还原点"), Width = Theme.S(196) };
            btnRestoreSel = new FlatButton { Text = Lang.T("选中行 → 还原到选中版本"), Width = Theme.S(206) };
            btnRestoreLatest = new FlatButton { Text = Lang.T("全部 → 还原成打补丁前"), Width = Theme.S(190) };
            btnRestoreEarliest = new FlatButton { Text = Lang.T("全部 → 还原到最早版本"), Width = Theme.S(190) };
            btnVerify = new FlatButton { Text = Lang.T("体检（只读）"), Width = Theme.S(120) };
            btnClean = new FlatButton { Text = Lang.T("删除我的备份与清单"), Width = Theme.S(186) };
            btnPurge = new FlatButton { Text = Lang.T("清理无主备份（mod 已删）"), Width = Theme.S(206) };

            btnHistory.Click += delegate { ShowHistory(); };
            btnRestoreSel.Click += delegate { RestoreSelectedPoint(); };
            btnRestoreLatest.Click += delegate { RestoreAsync("latest"); };
            btnRestoreEarliest.Click += delegate { RestoreAsync("earliest"); };
            btnVerify.Click += delegate { VerifyAsync(); };
            btnClean.Click += delegate { CleanAsync(); };
            btnPurge.Click += delegate { PurgeAsync(); };

            listCard = new CardPanel();
            lvPoints = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BorderStyle = BorderStyle.None,
                Font = Theme.Body,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            lvPoints.Columns.Add(Lang.T("版本"), Theme.S(190));
            lvPoints.Columns.Add(Lang.T("来源"), Theme.S(130));
            lvPoints.Columns.Add(Lang.T("文件"), Theme.S(330));
            lvPoints.Columns.Add(Lang.T("时间"), Theme.S(170));
            lvPoints.Columns.Add(Lang.T("大小"), Theme.S(100));
            lvPoints.Columns.Add(Lang.T("说明"), Theme.S(150));
            listCard.Controls.Add(lvPoints);

            lblRollbackHint = new Label
            {
                Text = Lang.T("备份就在每个 mod 自己的 Meshes 里：.nooutline.json（清单）+ Color.buf.orig.<哈希>.bak（原始文件），重命名 / 挪位置都不会丢") + Environment.NewLine +
                       Lang.T("先在上面选中一个 mod（或双击它），这里会列出它全部可还原的版本"),
                Font = Theme.Small,
                ForeColor = Theme.SubText,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            pageRollback.Controls.AddRange(new Control[] { btnHistory, btnRestoreSel, btnRestoreLatest, btnRestoreEarliest, btnVerify, btnClean, btnPurge, lblRollbackHint, listCard });

            contentHost.Controls.Add(pageRollback);
            contentHost.Controls.Add(pageApply);
        }

        private void BuildConsole()
        {
            consoleCard = new CardPanel();
            lblConsole = new Label { Text = "CONSOLE", Font = Theme.Small, ForeColor = Theme.SubText, AutoSize = true, BackColor = Color.Transparent };
            btnClearLog = new FlatButton { Text = Lang.T("清空"), Width = Theme.S(72), Height = Theme.S(28), Radius = 7, Small = true };
            btnClearLog.Click += delegate { txtLog.Clear(); };
            txtLog = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.ConsoleBg,
                ForeColor = Theme.ConsoleText,
                Font = Theme.Mono,
                WordWrap = false,
                DetectUrls = false
            };
            consoleCard.Controls.AddRange(new Control[] { lblConsole, btnClearLog, txtLog });
        }

        private void BuildStatus()
        {
            statusPanel = new Panel { BackColor = Theme.Card };
            statusPanel.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawLine(pen, 0, 0, statusPanel.Width, 0);
            };
            lblStatusDot = new Label { Text = "●", Font = Theme.Small, ForeColor = Theme.Green, AutoSize = true, BackColor = Color.Transparent };
            lblStatus = new Label { Text = "READY", Font = Theme.Small, ForeColor = Theme.Text, AutoSize = true, BackColor = Color.Transparent };
            lblCount = new Label { Text = "", Font = Theme.Small, ForeColor = Theme.SubText, AutoSize = true, BackColor = Color.Transparent };
            lblVer = new Label { Text = "v" + Core.ToolVer, Font = Theme.Small, ForeColor = Theme.SubText, AutoSize = true, BackColor = Color.Transparent };
            statusPanel.Controls.AddRange(new Control[] { lblStatusDot, lblStatus, lblCount, lblVer });
        }

        private static DataGridViewTextBoxColumn Col(string prop, string header, int width, bool fill = false, string tip = null)
        {
            var c = new DataGridViewTextBoxColumn();
            c.DataPropertyName = prop;
            c.HeaderText = header;
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
            c.ToolTipText = tip;
            if (fill)
            {
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                c.MinimumWidth = Theme.S(width);
                c.FillWeight = 100;
            }
            else
            {
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                c.Width = Theme.S(width);
            }
            return c;
        }

        // ── 悬浮提示：把每个控件"改什么、不动什么"讲清楚 ──────────
        private void AttachTips()
        {
            tip = new ToolTip();
            tip.AutoPopDelay = 20000;
            tip.InitialDelay = 350;
            tip.ReshowDelay = 120;
            tip.ShowAlways = true;
            tip.UseFading = true;

            tip.SetToolTip(lblSub, Lang.T("原理：描边的粗细写在模型自己的顶点色里（R=描边遮罩，G=描边粗细）。\r\n本工具只把这两个通道清零，不认角色、不依赖 shader hash，所以出新角色或游戏大版本更新都不用改。\r\n只对装了皮肤 mod 的角色生效；纯原版角色要靠 shader 层的去边 mod。"));

            tip.SetToolTip(txtRoot, Lang.T("要处理的目录。可以指到某一组（如 _MANAGED_\\group_6，就只处理这一组），\r\n也可以指到整个 Mods（所有组、含停用的都会处理）。\r\n工具会把该目录下所有层级的子文件夹翻一遍，找每个 mod 的 Meshes\\Color.buf。"));
            tip.SetToolTip(btnBrowse, Lang.T("选择要处理的 mod 目录"));
            tip.SetToolTip(btnScan, Lang.T("只读取，不改任何文件。\r\n列出目录下所有皮肤 mod 的当前状态、顶点数、备份情况。"));
            tip.SetToolTip(btnApply, Lang.T("对范围内所有文件执行去描边。\r\n已经处理过的会自动跳过（不会重复备份、不会叠加修改）。\r\n处理前会自动在各自的 mod 文件夹里留一份原始档。"));

            tip.SetToolTip(tabApply, Lang.T("去描边页：扫描、查看状态、执行去描边"));
            tip.SetToolTip(tabRollback, Lang.T("备份 / 回滚页：查看可还原的版本、还原、体检、清理"));

            tip.SetToolTip(cboAlpha, Lang.T("头发那圈描边怎么处理：\r\n· 保持不动（默认）：只清 R/G，A 通道原样保留\r\n· 去掉头发描边（A=0）：身体干净了但头发还有一圈时用这个\r\n· A=255：另一种取值，上面两个效果都不对时再试"));
            tip.SetToolTip(chkSelectedOnly, Lang.T("勾上后，只对表格里选中的那些行操作，其它文件一律不碰。\r\n适合先拿一两个角色试水。"));
            tip.SetToolTip(chkDryRun, Lang.T("演练模式：只把「会做什么」打印到下面的日志里，不写任何文件。\r\n想先看清楚会改哪些 mod 就勾这个。"));
            tip.SetToolTip(chkDeep, Lang.T("默认只统计小文件的「带描边顶点数」，大文件那一列显示 -（为了快）。\r\n需要精确数字时勾上，代价是扫描慢一些。"));

            tip.SetToolTip(grid, Lang.T("表格：每一行是一个 mod 的 Color.buf。\r\n悬浮在「状态」格上会用人话解释这一行的现状。\r\n双击某一行可直接跳到「备份 / 回滚」页看它的还原点。"));
            tip.SetToolTip(emptyHint, "");
            tip.SetToolTip(lvPoints, Lang.T("还原点列表：每一行是一份可以还原回去的原始文件。\r\n· 版本 mine:xxxx = 本工具存档；legacy = 旧版备份；fixer:… = Wuwa Mod Fixer 的 .BAK\r\n· 标了「当前基准」的那份，就是本工具「打补丁前」的状态\r\n选中一行后点「选中行 → 还原到选中版本」，就会用它覆盖当前文件。"));
            tip.SetToolTip(lblRollbackHint, Lang.T("还原点跟着 mod 文件夹走：重命名、挪位置之后依然认得。"));

            tip.SetToolTip(btnHistory, Lang.T("列出表格里选中那个 mod 的所有可还原版本"));
            tip.SetToolTip(btnRestoreSel, Lang.T("把表格里选中的行，还原成列表里选中的那个版本"));
            tip.SetToolTip(btnRestoreLatest, Lang.T("把这个范围内所有文件，还原成「本工具改动之前」的状态（最新那份原始档）"));
            tip.SetToolTip(btnRestoreEarliest, Lang.T("退回到最早的那份备份。注意：可能比 Wuwa Mod Fixer 修复之前还旧，一般不要用，除非你明确想要那个更旧的状态。"));
            tip.SetToolTip(btnVerify, Lang.T("只读检查：记录是否完整、原始档有没有丢、有没有无主备份。不改任何文件。"));
            tip.SetToolTip(btnClean, Lang.T("删掉本工具在这个 mod 里留下的备份和清单，释放空间。\r\n注意：删掉之后就再也还原不回去了。不会碰 Wuwa Mod Fixer 的 .BAK。"));
            tip.SetToolTip(btnPurge, Lang.T("清理「对应 mod 已经被删除或替换」后剩下的零碎备份与清单。不会碰 Wuwa Mod Fixer 的 .BAK。"));

            tip.SetToolTip(btnClearLog, Lang.T("清空下面的日志面板（磁盘上的 WuwaOutlineTool.log 不受影响）"));
            tip.SetToolTip(txtLog, Lang.T("运行日志。同样会写入本工具目录下的 WuwaOutlineTool.log"));
            tip.SetToolTip(lblCount, Lang.T("扫描结果的统计：总数 / 异常 / 提示 / 耗时"));
        }

        // ── 排版 ──────────────────────────────────────────────
        private void DoLayout()
        {
            if (headerCard == null || ClientSize.Width < Theme.S(200)) return;

            int pad = Theme.S(16), gap = Theme.S(12);
            int w = ClientSize.Width, h = ClientSize.Height;
            int headerH = Theme.S(132), segH = Theme.S(60), optsH = Theme.S(84);
            int statusH = Theme.S(34);
            int consoleH = Math.Max(Theme.S(130), (int)(h * 0.23));

            int y = pad;
            headerCard.Bounds = new Rectangle(pad, y, w - pad * 2, headerH); y += headerH + gap;
            tabCard.Bounds = new Rectangle(pad, y, w - pad * 2, segH); y += segH + gap;
            optsCard.Bounds = new Rectangle(pad, y, w - pad * 2, optsH); y += optsH + gap;

            int statusTop = h - statusH;
            int consoleTop = statusTop - consoleH - gap;
            consoleCard.Bounds = new Rectangle(pad, consoleTop, w - pad * 2, consoleH);
            contentHost.Bounds = new Rectangle(pad, y, w - pad * 2, Math.Max(Theme.S(100), consoleTop - gap - y));
            statusPanel.Bounds = new Rectangle(0, statusTop, w, statusH);

            // 顶栏内部：输入框和三个按钮同高同基线，严格对齐
            lblTitle.Location = new Point(Theme.S(22), Theme.S(16));
            lblSub.Location = new Point(Theme.S(23), Theme.S(48));
            lblDir.Location = new Point(Theme.S(22), Theme.S(92));

            // 语言切换：顶栏右上角，与标题同一行
            if (pillEn != null && pillZh != null)
            {
                pillEn.Location = new Point(headerCard.Width - Theme.S(22) - pillEn.Width, Theme.S(14));
                pillZh.Location = new Point(pillEn.Left - Theme.S(8) - pillZh.Width, Theme.S(14));
            }

            const int rowH = 38;
            int btnY = Theme.S(82);
            btnApply.Location = new Point(headerCard.Width - Theme.S(22) - btnApply.Width, btnY);
            btnScan.Location = new Point(btnApply.Left - Theme.S(10) - btnScan.Width, btnY);
            btnBrowse.Location = new Point(btnScan.Left - Theme.S(10) - btnBrowse.Width, btnY);
            btnApply.Height = btnScan.Height = btnBrowse.Height = Theme.S(rowH);

            int pathLeft = lblDir.Right + Theme.S(14);
            int pathRight = btnBrowse.Left - Theme.S(14);
            pathBox.Bounds = new Rectangle(pathLeft, btnY, Math.Max(Theme.S(120), pathRight - pathLeft), Theme.S(rowH));
            txtRoot.Bounds = new Rectangle(Theme.S(13), (Theme.S(rowH) - Theme.S(20)) / 2, Math.Max(Theme.S(60), pathBox.Width - Theme.S(26)), Theme.S(20));
            // 启动时文本框还是默认宽度、装不下整条路径，会被横向滚到末尾；
            // 布局定下来（宽度已确定）之后把视图拨回开头，否则开头几个字符是缺的
            if (txtRoot.TextLength > 0 && txtRoot.SelectionStart != 0)
            {
                txtRoot.SelectionStart = 0;
                txtRoot.SelectionLength = 0;
            }

            // 页签卡片内部：左边距与卡片留白一致
            tabApply.Location = new Point(Theme.S(12), Theme.S(10));
            tabRollback.Location = new Point(tabApply.Right + Theme.S(8), Theme.S(10));

            // 选项卡片内部：Alpha 一行、勾选一行，避免文字互相压住
            int rowA = Theme.S(14), rowB = Theme.S(48);
            lblAlpha.Location = new Point(Theme.S(22), rowA + Theme.S(7));
            cboAlpha.Location = new Point(lblAlpha.Right + Theme.S(10), rowA);
            cboAlpha.Width = Theme.S(190);
            cboAlpha.Height = Theme.S(30);
            lblAlphaHint.Location = new Point(cboAlpha.Right + Theme.S(14), rowA + Theme.S(8));
            chkSelectedOnly.Location = new Point(Theme.S(22), rowB);
            chkDryRun.Location = new Point(chkSelectedOnly.Right + Theme.S(26), rowB);
            chkDeep.Location = new Point(chkDryRun.Right + Theme.S(26), rowB);

            // 页面
            pageApply.Bounds = new Rectangle(0, 0, contentHost.Width, contentHost.Height);
            pageRollback.Bounds = pageApply.Bounds;
            gridCard.Bounds = new Rectangle(0, 0, pageApply.Width, pageApply.Height);
            grid.Bounds = new Rectangle(Theme.S(12), Theme.S(12), Math.Max(Theme.S(80), gridCard.Width - Theme.S(24)), Math.Max(Theme.S(60), gridCard.Height - Theme.S(24)));
            emptyHint.Bounds = grid.Bounds;

            int by = Theme.S(6), bh = Theme.S(38);
            btnHistory.Location = new Point(0, by);
            btnRestoreSel.Location = new Point(btnHistory.Right + Theme.S(8), by);
            btnRestoreLatest.Location = new Point(btnRestoreSel.Right + Theme.S(8), by);
            btnRestoreEarliest.Location = new Point(btnRestoreLatest.Right + Theme.S(8), by);
            btnHistory.Height = btnRestoreSel.Height = btnRestoreLatest.Height = btnRestoreEarliest.Height = bh;

            int by2 = by + bh + Theme.S(10);
            btnVerify.Location = new Point(0, by2);
            btnClean.Location = new Point(btnVerify.Right + Theme.S(8), by2);
            btnPurge.Location = new Point(btnClean.Right + Theme.S(8), by2);
            btnVerify.Height = btnClean.Height = btnPurge.Height = bh;

            // 说明文字：按它自己的实际高度让位，绝不压到下面的卡片上；
            // 左边距和列表内容对齐（lvPoints 在卡片里缩进 12）
            int hintTop = by2 + bh + Theme.S(12);
            lblRollbackHint.Location = new Point(Theme.S(12), hintTop);
            int hintH = Math.Max(Theme.S(30), Math.Max(lblRollbackHint.Height, lblRollbackHint.PreferredHeight));
            int listTop = hintTop + hintH + Theme.S(14);
            listCard.Bounds = new Rectangle(0, listTop, pageRollback.Width, Math.Max(Theme.S(80), pageRollback.Height - listTop));
            lvPoints.Bounds = new Rectangle(Theme.S(12), Theme.S(12), Math.Max(Theme.S(80), listCard.Width - Theme.S(24)), Math.Max(Theme.S(50), listCard.Height - Theme.S(24)));

            // 控制台
            lblConsole.Location = new Point(Theme.S(20), Theme.S(14));
            btnClearLog.Location = new Point(consoleCard.Width - Theme.S(20) - btnClearLog.Width, Theme.S(10));
            txtLog.Bounds = new Rectangle(Theme.S(16), Theme.S(42), Math.Max(Theme.S(80), consoleCard.Width - Theme.S(32)), Math.Max(Theme.S(40), consoleCard.Height - Theme.S(58)));

            // 状态栏
            lblStatusDot.Location = new Point(Theme.S(18), Theme.S(9));
            lblStatus.Location = new Point(Theme.S(36), Theme.S(9));
            lblCount.Location = new Point(Theme.S(120), Theme.S(9));
            lblVer.Location = new Point(statusPanel.Width - Theme.S(18) - lblVer.Width, Theme.S(9));
        }

        private void SwitchTab(int index)
        {
            _tab = index;
            tabApply.Selected = index == 0;
            tabRollback.Selected = index == 1;
            tabApply.Invalidate();
            tabRollback.Invalidate();
            pageApply.Visible = index == 0;
            pageRollback.Visible = index == 1;
        }

        // ── 设置持久化 ────────────────────────────────────────
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var line in File.ReadAllLines(SettingsPath))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;
                    string k = parts[0].Trim(), v = parts[1].Trim();
                    if (k == "root" && Directory.Exists(v)) SetRoot(v);
                    if (k == "alpha")
                    {
                        int idx;
                        if (int.TryParse(v, out idx)) cboAlpha.SelectedIndex = Math.Max(0, Math.Min(2, idx));
                    }
                    if (k == "deep") chkDeep.Checked = v == "1";
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                File.WriteAllLines(SettingsPath, new[]
                {
                    "root=" + txtRoot.Text,
                    "alpha=" + cboAlpha.SelectedIndex.ToString(),
                    "deep=" + (chkDeep.Checked ? "1" : "0"),
                    "lang=" + Lang.Code
                }, Encoding.UTF8);
            }
            catch { }
        }

        // ── 中英切换 ──────────────────────────────────────────
        // 整份界面重建，保证一处文案都不漏；扫描结果保留（_entries 不动，只重新绑一次表格）。
        private void SetLang(bool en)
        {
            if (Lang.En == en) return;
            Lang.En = en;
            SaveSettings();
            RebuildUi();
            AppendLog(Lang.T("界面语言已切换为：") + Lang.Code);
        }

        private void RebuildUi()
        {
            string root = txtRoot.Text.Trim();
            string log = txtLog.Text;
            int alphaIdx = cboAlpha.SelectedIndex;
            bool deep = chkDeep.Checked;
            var keep = SelectedPaths();

            SuspendLayout();
            var olds = new List<Control>();
            foreach (Control c in Controls) olds.Add(c);
            Controls.Clear();
            foreach (var c in olds) { try { c.Dispose(); } catch { } }
            BuildAll();
            ResumeLayout(true);

            SetRoot(root);
            if (alphaIdx >= 0) cboAlpha.SelectedIndex = alphaIdx;
            chkDeep.Checked = deep;
            foreach (var e in _entries) Core.SetStateText(e);   // 状态列文案跟着换语言
            BindEntries();
            SelectByPaths(keep);
            ReloadPoints();
            if (_cntTotal >= 0)
                lblCount.Text = string.Format(Lang.T("共 {0} 个  ·  异常 {1}  ·  提示 {2}  ·  {3:0.0}s"),
                    _cntTotal, _cntIssues, _cntNotes, _cntSecs);
            if (log.Length > 0) txtLog.Text = log;              // 日志面板内容别丢
            PerformLayout();
            DoLayout();
        }

        // ── 日志 / 状态 ───────────────────────────────────────
        // 文本框空着的时候显示灰色提示（WinForms 没有原生 placeholder，得发 EM_SETCUEBANNER）
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, string lParam);

        private static void SetCue(TextBox box, string text)
        {
            try { if (box != null && box.IsHandleCreated) SendMessage(box.Handle, 0x1501, 1, text); }
            catch { }
        }

        // 目录为空 / 不存在时给出人话提示，返回 false 表示别继续
        private bool EnsureRoot(string root)
        {
            if (root == null || root.Length == 0)
            {
                MessageBox.Show(this, Lang.T("还没选 MOD 目录：点上面的『浏览…』选一下（一般是 XXMI 目录下的 Mods 文件夹）"), Lang.T("提示"));
                return false;
            }
            if (!Directory.Exists(root))
            {
                MessageBox.Show(this, Lang.T("目录不存在：") + root, Lang.T("提示"));
                return false;
            }
            return true;
        }

        // 设置目录并取消全选（避免一进来整条路径被高亮）
        private void SetRoot(string p)
        {
            txtRoot.Text = p;
            txtRoot.SelectionStart = txtRoot.TextLength;
            txtRoot.SelectionLength = 0;
        }

        private void AppendLog(string msg)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(AppendLog), msg); return; }
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + msg + Environment.NewLine);
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
            try
            {
                File.AppendAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "WuwaOutlineTool.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + msg + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { }
        }

        private void SetBusy(bool busy, string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action<bool, string>(SetBusy), busy, text); return; }
            _busy = busy;
            btnScan.Enabled = btnApply.Enabled = btnBrowse.Enabled = !busy;
            btnRestoreSel.Enabled = btnRestoreLatest.Enabled = btnRestoreEarliest.Enabled = !busy;
            btnClean.Enabled = btnVerify.Enabled = btnPurge.Enabled = btnHistory.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            lblStatusDot.ForeColor = busy ? Theme.Orange : Theme.Green;
            lblStatus.Text = busy ? text : "READY";
            lblTitle.Text = busy ? Lang.T("鸣潮 去描边工具  ·  ") + text : Lang.T("鸣潮 去描边工具");
        }

        private string AlphaValue
        {
            get
            {
                if (cboAlpha.SelectedIndex == 1) return "0";
                if (cboAlpha.SelectedIndex == 2) return "255";
                return "keep";
            }
        }

        private List<Entry> SelectedEntries()
        {
            var list = new List<Entry>();
            foreach (DataGridViewRow r in grid.SelectedRows)
            {
                var e = r.Tag as Entry;
                if (e != null) list.Add(e);
            }
            return list;
        }

        private List<Entry> EntriesToProcess()
        {
            if (chkSelectedOnly.Checked)
            {
                var sel = SelectedEntries();
                if (sel.Count == 0) { MessageBox.Show(this, Lang.T("没有选中任何行。"), Lang.T("提示")); return null; }
                return sel;
            }
            return new List<Entry>(_entries);
        }

        // ── 操作后重新读盘 ────────────────────────────────────
        // 以前只有「扫描」会读盘，所以打完补丁/还原之后，表格里的
        // 状态 / 带描边 / 原始档 / 我的备份 还是操作前的旧值，
        // 看着就像"我处理过了但备份没显示"。这里让每次写盘操作完成后都重读一次。

        private string[] SelectedPaths()
        {
            var list = new List<string>();
            foreach (DataGridViewRow r in grid.SelectedRows)
            {
                var e = r.Tag as Entry;
                if (e != null) list.Add(e.BufPath);
            }
            return list.ToArray();
        }

        private void SelectByPaths(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            var want = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
            grid.ClearSelection();
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                var e = grid.Rows[i].Tag as Entry;
                if (e != null && want.Contains(e.BufPath)) grid.Rows[i].Selected = true;
            }
        }

        private List<Entry> ReloadEntries(string root, bool deep)
        {
            var fresh = Core.Discover(root);
            foreach (var e in fresh)
            {
                try { Core.Load(e, deep); }
                catch { }
            }
            return fresh;
        }

        private void AdoptEntries(List<Entry> fresh, string[] keepPaths)
        {
            _entries.Clear();
            _entries.AddRange(fresh);
            BindEntries();
            SelectByPaths(keepPaths);
            ReloadPoints();
        }

        // 还原点列表是"某一个 mod"的，表格重读之后它也必须跟着重读，
        // 否则会停在旧状态（甚至错误地显示"没有可用还原点"）。
        private void ReloadPoints()
        {
            string keepLabel = null;
            if (lvPoints.SelectedItems.Count > 0)
            {
                var kp = lvPoints.SelectedItems[0].Tag as RestorePoint;
                if (kp != null) keepLabel = kp.Label;
            }
            var sel = SelectedEntries();
            if (sel.Count == 0) { lvPoints.Items.Clear(); _pointsEntry = null; return; }
            _pointsEntry = sel[0];
            FillPoints(_pointsEntry, keepLabel);
        }

        // ── 扫描 ──────────────────────────────────────────────
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("--scan", StringComparison.OrdinalIgnoreCase))
                {
                    if (Directory.Exists(args[i + 1])) { SetRoot(args[i + 1]); ScanAsync(); }
                    break;
                }
            }
        }

        private void ScanAsync()
        {
            if (_busy) return;
            string root = txtRoot.Text.Trim();
            if (!EnsureRoot(root)) return;
            SaveSettings();
            bool deep = chkDeep.Checked;
            SetBusy(true, Lang.T("扫描中…"));
            Task.Factory.StartNew(delegate
            {
                try
                {
                    AppendLog(Lang.T("开始扫描：") + root);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var list = Core.Discover(root);
                    AppendLog(Lang.T("发现 ") + list.Count + Lang.T(" 个 Color.buf，正在读取状态…"));
                    int done = 0;
                    foreach (var e in list)
                    {
                        try { Core.Load(e, deep); }
                        catch (Exception ex) { AppendLog(Lang.T("读取失败：") + e.BufPath + " —— " + ex.Message); }
                        done++;
                        if (done % 50 == 0) AppendLog(Lang.T("  已处理 ") + done + "/" + list.Count);
                    }
                    sw.Stop();
                    BeginInvoke(new Action(delegate
                    {
                        _entries.Clear();
                        _entries.AddRange(list);
                        BindEntries();
                        ReloadPoints();
                        int issues = 0, notes = 0;
                        foreach (var e in list) { bool i, n; Core.Verify(e, out i, out n); if (i) issues++; else if (n) notes++; }
                        lblCount.Text = string.Format(Lang.T("共 {0} 个  ·  异常 {1}  ·  提示 {2}  ·  {3:0.0}s"), list.Count, issues, notes, sw.Elapsed.TotalSeconds);
                        _cntTotal = list.Count; _cntIssues = issues; _cntNotes = notes; _cntSecs = sw.Elapsed.TotalSeconds;
                        AppendLog(string.Format(Lang.T("扫描完成：{0} 个 Color.buf，异常 {1}，提示 {2}，耗时 {3:0.0} 秒。"), list.Count, issues, notes, sw.Elapsed.TotalSeconds));
                    }));
                }
                catch (Exception ex) { AppendLog(Lang.T("扫描出错：") + ex.Message); }
                finally { SetBusy(false, ""); }
            });
        }

        private void BindEntries()
        {
            var src = new BindingSource();
            src.DataSource = _entries.Select(MakeRow).ToList();
            grid.DataSource = src;
            for (int i = 0; i < _entries.Count && i < grid.Rows.Count; i++) grid.Rows[i].Tag = _entries[i];
            emptyHint.Visible = _entries.Count == 0;
            grid.Visible = _entries.Count > 0;
        }

        private static object MakeRow(Entry e)
        {
            return new
            {
                Mod = e.ModName,
                Verts = e.Verts,
                StateText = e.StateText,
                GOnText = e.GOn < 0 ? "-" : e.GOn.ToString(),
                ManAlpha = e.ManAlpha ?? "-",
                OrigLabel = e.Orig == null ? Lang.T("无") : e.Orig.Label,
                BakText = Core.FormatSize(e.MyBakSize),
                FixerCount = e.FixerCount
            };
        }

        // ── 操作 ──────────────────────────────────────────────
        private void ApplyAsync()
        {
            if (_busy) return;
            var list = EntriesToProcess();
            if (list == null) return;
            if (list.Count == 0) { MessageBox.Show(this, Lang.T("请先扫描。"), Lang.T("提示")); return; }
            string alpha = AlphaValue; bool dry = chkDryRun.Checked; bool deep = chkDeep.Checked;
            string root = txtRoot.Text.Trim();
            string[] keep = SelectedPaths();
            SetBusy(true, dry ? Lang.T("演练中…") : Lang.T("去描边中…"));
            Task.Factory.StartNew(delegate
            {
                int changed = 0, skipped = 0;
                try
                {
                    AppendLog(string.Format(Lang.T("开始去描边：{0} 个文件，Alpha={1}{2}"), list.Count, alpha, dry ? Lang.T("（演练，不写盘）") : ""));
                    foreach (var e in list)
                    {
                        string r = Core.Apply(e, alpha, dry, deep);
                        if (r.StartsWith(Lang.T("已去描边"))) changed++; else skipped++;
                        if (r != Lang.T("已是最新，跳过")) AppendLog("  [" + e.ModName + "] " + r);
                    }
                    AppendLog(string.Format(Lang.T("完成：改动 {0} 个，跳过 {1} 个。回游戏按 F10 重载 WWMI。"), changed, skipped));
                }
                catch (Exception ex) { AppendLog(Lang.T("出错：") + ex.Message); }
                finally { SetBusy(false, ""); }
                if (!dry)
                {
                    try
                    {
                        // 重新读盘：让表格里的状态 / 原始档 / 我的备份 立刻反映刚才的改动
                        var fresh = ReloadEntries(root, deep);
                        BeginInvoke(new Action(delegate { AdoptEntries(fresh, keep); }));
                    }
                    catch (Exception ex) { AppendLog(Lang.T("刷新列表失败：") + ex.Message); }
                }
            });
        }

        private void RestoreAsync(string selector)
        {
            if (_busy) return;
            List<Entry> list;
            if (selector == "selected")
            {
                var sel = SelectedEntries();
                list = sel.Count > 0 ? sel : EntriesToProcess();
                if (list == null || list.Count == 0) { MessageBox.Show(this, Lang.T("请先选中要还原的 mod。"), Lang.T("提示")); return; }
            }
            else
            {
                list = EntriesToProcess();
                if (list == null || list.Count == 0) return;
            }
            bool dry = chkDryRun.Checked;
            bool deep = chkDeep.Checked;
            string root = txtRoot.Text.Trim();
            string[] keep = SelectedPaths();
            SetBusy(true, Lang.T("还原中…"));
            Task.Factory.StartNew(delegate
            {
                try
                {
                    AppendLog(string.Format(Lang.T("开始还原：{0} 个文件，选择={1}{2}"), list.Count, selector, dry ? Lang.T("（演练）") : ""));
                    foreach (var e in list) AppendLog("  [" + e.ModName + "] " + Core.Restore(e, selector, dry, false));
                }
                catch (Exception ex) { AppendLog(Lang.T("出错：") + ex.Message); }
                finally { SetBusy(false, ""); }
                if (!dry)
                {
                    try
                    {
                        var fresh = ReloadEntries(root, deep);
                        BeginInvoke(new Action(delegate { AdoptEntries(fresh, keep); }));
                    }
                    catch (Exception ex) { AppendLog(Lang.T("刷新列表失败：") + ex.Message); }
                }
            });
        }

        private void CleanAsync()
        {
            if (_busy) return;
            var list = EntriesToProcess();
            if (list == null || list.Count == 0) return;
            bool dry = chkDryRun.Checked;
            bool deep = chkDeep.Checked;
            string root = txtRoot.Text.Trim();
            string[] keep = SelectedPaths();
            SetBusy(true, Lang.T("清理中…"));
            Task.Factory.StartNew(delegate
            {
                try
                {
                    foreach (var e in list)
                    {
                        string r = Core.Clean(e, dry);
                        if (r != Lang.T("无可清理项")) AppendLog("  [" + e.ModName + "] " + r);
                    }
                }
                catch (Exception ex) { AppendLog(Lang.T("出错：") + ex.Message); }
                finally { SetBusy(false, ""); }
                if (!dry)
                {
                    try
                    {
                        var fresh = ReloadEntries(root, deep);
                        BeginInvoke(new Action(delegate { AdoptEntries(fresh, keep); }));
                    }
                    catch (Exception ex) { AppendLog(Lang.T("刷新列表失败：") + ex.Message); }
                }
            });
        }

        private void VerifyAsync()
        {
            if (_busy) return;
            string root = txtRoot.Text.Trim();
            if (!EnsureRoot(root)) return;
            bool deep = chkDeep.Checked;
            SetBusy(true, Lang.T("体检中…"));
            Task.Factory.StartNew(delegate
            {
                try
                {
                    var list = Core.Discover(root);
                    foreach (var e in list) { try { Core.Load(e, deep); } catch { } }
                    int issues = 0, notes = 0, clean = 0;
                    foreach (var e in list)
                    {
                        bool i, n; string msg = Core.Verify(e, out i, out n);
                        if (i) { issues++; AppendLog(Lang.T("  [异常] ") + e.ModName + Lang.T("：") + msg); }
                        else if (n) { notes++; AppendLog(Lang.T("  [提示] ") + e.ModName + Lang.T("：") + msg); }
                        else clean++;
                    }
                    var orphans = Core.FindOrphans(root);
                    AppendLog(string.Format(Lang.T("体检完成：{0} 个 Color.buf —— 无问题 {1}；提示 {2}；异常 {3}；孤儿存档 {4}"), list.Count, clean, notes, issues, orphans.Count));
                    foreach (var o in orphans.Take(20)) AppendLog(Lang.T("   孤儿：") + o);
                    if (orphans.Count > 20) AppendLog(Lang.T("   …还有 ") + (orphans.Count - 20) + Lang.T(" 个，点「清理孤儿存档」可全部清理"));
                }
                catch (Exception ex) { AppendLog(Lang.T("体检出错：") + ex.Message); }
                finally { SetBusy(false, ""); }
            });
        }

        private void PurgeAsync()
        {
            if (_busy) return;
            string root = txtRoot.Text.Trim();
            if (!EnsureRoot(root)) return;
            bool dry = chkDryRun.Checked;
            bool deep = chkDeep.Checked;
            string[] keep = SelectedPaths();
            SetBusy(true, Lang.T("清理孤儿存档…"));
            Task.Factory.StartNew(delegate
            {
                try
                {
                    var orphans = Core.FindOrphans(root);
                    if (orphans.Count == 0) { AppendLog(Lang.T("没有孤儿存档：本工具产物都能对上各自的 Color.buf。")); }
                    else
                    {
                        long freed = 0;
                        foreach (var o in orphans)
                        {
                            try
                            {
                                long sz = new FileInfo(o).Length;
                                if (!dry) File.Delete(o);
                                freed += sz;
                                AppendLog((dry ? Lang.T("[演练] 将删除 ") : Lang.T("已删除 ")) + o);
                            }
                            catch (Exception ex) { AppendLog(Lang.T("删除失败：") + o + " —— " + ex.Message); }
                        }
                        AppendLog(string.Format(Lang.T("{0}孤儿存档 {1} 个，{2}"), dry ? Lang.T("演练：") : Lang.T("已清理"), orphans.Count, Core.FormatSize(freed)));
                    }
                }
                catch (Exception ex) { AppendLog(Lang.T("出错：") + ex.Message); }
                finally { SetBusy(false, ""); }
                if (!dry)
                {
                    try
                    {
                        var fresh = ReloadEntries(root, deep);
                        BeginInvoke(new Action(delegate { AdoptEntries(fresh, keep); }));
                    }
                    catch (Exception ex) { AppendLog(Lang.T("刷新列表失败：") + ex.Message); }
                }
            });
        }

        private void ShowHistory()
        {
            var sel = SelectedEntries();
            if (sel.Count == 0) { AppendLog(Lang.T("请先在「去描边」页选中一个 mod（或双击某一行）。")); return; }
            _pointsEntry = sel[0];
            FillPoints(_pointsEntry, null);
            if (_pointsEntry.Points.Count == 0)
                AppendLog("[" + _pointsEntry.ModName + Lang.T("] 没有可用还原点：这个 mod 还没有被本工具留档过（原版 mod，或者原始档被删了）。"));
            else
                AppendLog("[" + _pointsEntry.ModName + Lang.T("] 还原点 ") + _pointsEntry.Points.Count + Lang.T(" 个 —— 「<= 当前基准」那份就是打补丁前的内容"));
        }

        // 把某个 mod 的还原点填进列表；keepLabel 用来在刷新后保住原来选中的那一行
        private void FillPoints(Entry e, string keepLabel)
        {
            lvPoints.Items.Clear();
            if (e == null || e.Points.Count == 0) return;
            foreach (var p in e.Points.OrderBy(x => x.Created))
            {
                string src = p.Kind == "mine" ? Lang.T("本工具存档") : (p.Kind == "legacy" ? Lang.T("旧版备份") : "Wuwa Mod Fixer");
                string note = (e.ManPoint != null && string.Equals(p.File, e.ManPoint.File, StringComparison.OrdinalIgnoreCase)) ? Lang.T("<= 当前基准") : "";
                var it = new ListViewItem(new[]
                {
                    p.Label, src, p.File,
                    p.Created.ToString("yyyy-MM-dd HH:mm:ss"),
                    Core.FormatSize(p.Size), note
                });
                it.Tag = p;
                lvPoints.Items.Add(it);
                if (keepLabel != null && string.Equals(p.Label, keepLabel, StringComparison.OrdinalIgnoreCase)) it.Selected = true;
            }
        }

        // 「选中行 → 还原到选中版本」：用回滚列表里选中的那一份去覆盖。
        // 以前这里把字符串 "selected" 当版本号传给底层，底层只认
        // latest / earliest / mine:xxxx / 文件名，于是永远报"没有匹配的还原点"。
        private void RestoreSelectedPoint()
        {
            if (_busy) return;
            var rows = SelectedEntries();
            if (rows.Count == 0) { MessageBox.Show(this, Lang.T("请先在「去描边」页选中一个 mod（或双击它跳到本页）。"), Lang.T("提示")); return; }

            if (lvPoints.SelectedItems.Count == 0)
            {
                _pointsEntry = rows[0];
                FillPoints(_pointsEntry, null);
                if (lvPoints.Items.Count == 0)
                    MessageBox.Show(this, "[" + _pointsEntry.ModName + Lang.T("] 没有可用还原点：它的原始数据没有被留档过。"), Lang.T("提示"));
                else
                    MessageBox.Show(this, Lang.T("已经列出这个 mod 的还原点，请在下面的列表里选一个版本，再点这个按钮。"), Lang.T("提示"));
                return;
            }

            var p = lvPoints.SelectedItems[0].Tag as RestorePoint;
            if (p == null) { MessageBox.Show(this, Lang.T("没读到选中的版本，请重新选一次。"), Lang.T("提示")); return; }
            if (rows.Count > 1)
                AppendLog(Lang.T("注意：还原点是单个 mod 的，本次只还原表格里第一条选中的 mod。要整组还原请用「全部 → 还原成打补丁前」。"));

            RestorePointAsync(rows[0], p);
        }

        private void RestorePointAsync(Entry e, RestorePoint p)
        {
            bool dry = chkDryRun.Checked;
            bool deep = chkDeep.Checked;
            string root = txtRoot.Text.Trim();
            string[] keep = SelectedPaths();
            SetBusy(true, Lang.T("还原中…"));
            Task.Factory.StartNew(delegate
            {
                try
                {
                    AppendLog(string.Format(Lang.T("还原到指定版本：{0} —— 用 {1}（{2}）{3}"),
                        e.ModName, p.Label, p.File, dry ? Lang.T("【演练】") : ""));
                    AppendLog("  " + Core.Restore(e, p.Label, dry, false));
                }
                catch (Exception ex) { AppendLog(Lang.T("出错：") + ex.Message); }
                finally { SetBusy(false, ""); }
                if (!dry)
                {
                    try
                    {
                        var fresh = ReloadEntries(root, deep);
                        BeginInvoke(new Action(delegate { AdoptEntries(fresh, keep); }));
                    }
                    catch (Exception ex) { AppendLog(Lang.T("刷新列表失败：") + ex.Message); }
                }
            });
        }
    }
}

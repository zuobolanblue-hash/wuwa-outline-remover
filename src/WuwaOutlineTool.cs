// WuwaOutlineTool.cs  —  鸣潮 WWMI 皮肤 mod 去描边工具（GUI + CLI 双模）
// 与 PowerShell 版 wuwa-no-outline-patch.ps1 v3.2 共用同一套磁盘状态格式，两边可互操作。
//
// 图形界面：直接双击运行
// 命令行（便于脚本化 / 排错）：
//   WuwaOutlineTool.exe --cli -Path "<目录>" -Status
//   WuwaOutlineTool.exe --cli -Path "<目录>" -History
//   WuwaOutlineTool.exe --cli -Path "<目录>" -Verify
//   WuwaOutlineTool.exe --cli -Path "<目录>"                 (去描边，Alpha 保留)
//   WuwaOutlineTool.exe --cli -Path "<目录>" -Alpha 0        (头发还留描边时试)
//   WuwaOutlineTool.exe --cli -Path "<目录>" -Restore        (还原到打补丁前)
//   WuwaOutlineTool.exe --cli -Path "<目录>" -RestoreTo earliest
//   WuwaOutlineTool.exe --cli -Path "<目录>" -Clean
//   WuwaOutlineTool.exe --cli -Path "<目录>" -PurgeOrphans
//   WuwaOutlineTool.exe --selftest
//
// 状态（与 PS 版一致）：
//   <mod>\Meshes\.nooutline.json               原始哈希/补丁哈希/Alpha/时间
//   <mod>\Meshes\Color.buf.orig.<hash8>.bak    按内容哈希存档的原始文件
//   另识别：<mod>\Meshes\Color.buf.bak_nooutline（旧版备份）
//           <mod>\Meshes\Color.buf_2026-06-28 10-06-49.BAK（Wuwa Mod Fixer 的备份，只读使用）

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WuwaOutline
{
    // ───────────────────────── 数据结构 ─────────────────────────
    internal class RestorePoint
    {
        public string Kind;      // mine / legacy / fixer
        public string Label;
        public string File;
        public DateTime Created;
        public long Size;
    }

    internal class Entry
    {
        public string BufPath;
        public string ModName;
        public string Format;
        public int Stride;
        // mod 的 ini 到底有没有声明这个 Color.buf（“只换贴图”型 mod 不会声明，
        // 它画的是游戏原版网格，顶点色方案对它无效）
        public bool ColorUsed = true;

        public byte[] Cur;
        public string CurHash;
        public long Verts;
        public long GOn = -1;
        public bool RgZero;
        public bool AllZero;

        public List<RestorePoint> Points = new List<RestorePoint>();
        public RestorePoint ManPoint;
        public RestorePoint Orig;
        public string OrigHash;
        public string ManOriginalFile;
        public string ManPatchedHash;
        public string ManAlpha;
        public bool HasManifest;
        public bool PatchedByUs;

        public string State;
        public string StateText;
        public long MyBakSize;
        public int FixerCount;
        public int MineCount;

        public string DisplayMod { get { return ModName; } }
    }

    // ───────────────────────── 核心逻辑 ─────────────────────────
    internal static class Core
    {
        public const string ToolName = "wuwa-outline-tool";
        public const string ToolVer = "1.0";
        public const string ManifestName = ".nooutline.json";
        public const string LegacySuffix = ".bak_nooutline";
        public const long CountVerticesLimit = 400000;
        public const long IniSizeLimit = 8L * 1024 * 1024;

        public static Action<string> Log = delegate { };

        // 常见 XXMI / WWMI 安装位置：只在"用户没指定过目录"时拿来当默认值
        public static readonly string[] ModsCandidates = new string[]
        {
            @"C:\XXMI\WWMI\Mods",
            @"D:\XXMI\WWMI\Mods",
            @"E:\XXMI\WWMI\Mods",
            @"C:\Games\XXMI\WWMI\Mods"
        };

        /// <summary>找一个存在的 XXMI Mods 目录；一个都没有就返回空串（让用户自己点『浏览…』）。</summary>
        public static string DetectDefaultMods()
        {
            foreach (string c in ModsCandidates)
            {
                try { if (Directory.Exists(c)) return c; }
                catch { }
            }
            try
            {
                string p = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"XXMI\WWMI\Mods");
                if (Directory.Exists(p)) return p;
            }
            catch { }
            return "";
        }

        private static void L(string format, params object[] a)
        {
            Log(string.Format(CultureInfo.InvariantCulture, format, a));
        }

        // ── 小工具 ──
        public static string Sha1(byte[] b)
        {
            using (var sha = SHA1.Create())
            {
                var h = sha.ComputeHash(b);
                var sb = new StringBuilder(h.Length * 2);
                foreach (var x in h) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }

        public static void Analyze(byte[] b, out long gOn, out bool rgZero, out bool allZero)
        {
            long g = 0; bool z = true; bool az = true;
            for (int i = 0; i + 3 < b.Length; i += 4)
            {
                if (b[i] != 0 || b[i + 1] != 0) z = false;
                if (b[i + 1] > 0) g++;
                if (b[i] != 0 || b[i + 1] != 0 || b[i + 2] != 0 || b[i + 3] != 0) az = false;
            }
            gOn = g; rgZero = z; allZero = az;
        }

        public static void Patch(byte[] b, int alpha)
        {
            for (int i = 0; i + 3 < b.Length; i += 4)
            {
                b[i] = 0; b[i + 1] = 0;
                if (alpha >= 0) b[i + 3] = (byte)alpha;
            }
        }

        public static bool IsBackupName(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains(".bak") || n.Contains("_backup");
        }

        // ── 清单（与 PS 版 JSON 字段完全一致）──
        public static Dictionary<string, string> ReadManifest(string bufPath)
        {
            string p = Path.Combine(Path.GetDirectoryName(bufPath), ManifestName);
            if (!File.Exists(p)) return null;
            try
            {
                string text = File.ReadAllText(p, Encoding.UTF8);
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match m in Regex.Matches(text, "\"([A-Za-z_][A-Za-z0-9_]*)\"\\s*:\\s*(?:\"((?:[^\"\\\\]|\\\\.)*)\"|(-?[0-9]+(?:\\.[0-9]+)?))"))
                {
                    string key = m.Groups[1].Value;
                    string val = m.Groups[2].Success ? Regex.Unescape(m.Groups[2].Value) : m.Groups[3].Value;
                    dict[key] = val;
                }
                return dict;
            }
            catch { return null; }
        }

        public static string JsonEscape(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u" + ((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        public static void WriteManifest(string bufPath, string origFile, string origHash,
                                         string patchedHash, string alpha, long verts)
        {
            string p = Path.Combine(Path.GetDirectoryName(bufPath), ManifestName);
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"tool\": \"" + ToolName + "\",");
            sb.AppendLine("  \"version\": \"" + ToolVer + "\",");
            sb.AppendLine("  \"patchedAt\": \"" + DateTime.Now.ToString("s") + "\",");
            sb.AppendLine("  \"originalFile\": \"" + JsonEscape(origFile) + "\",");
            sb.AppendLine("  \"originalHash\": \"" + JsonEscape(origHash) + "\",");
            sb.AppendLine("  \"patchedHash\": \"" + patchedHash + "\",");
            sb.AppendLine("  \"alpha\": \"" + alpha + "\",");
            sb.AppendLine("  \"vertices\": " + verts.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine(Lang.T("  \"note\": \"R=0,G=0；B 保留；A 按 alpha 设置\""));
            sb.AppendLine("}");
            File.WriteAllText(p, sb.ToString(), new UTF8Encoding(true));
        }

        public static void RemoveManifest(string bufPath)
        {
            string p = Path.Combine(Path.GetDirectoryName(bufPath), ManifestName);
            if (File.Exists(p)) File.Delete(p);
        }

        // ── 枚举（自己走目录，遇到无权访问的目录跳过而不是整棵树报错）──
        public static IEnumerable<string> Walk(string root, string pattern)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                string d = stack.Pop();
                string[] files, dirs;
                try { files = Directory.GetFiles(d, pattern); }
                catch { files = new string[0]; }
                try { dirs = Directory.GetDirectories(d); }
                catch { dirs = new string[0]; }
                foreach (var f in files) yield return f;
                foreach (var s in dirs) stack.Push(s);
            }
        }

        // ── 目标发现：优先读 ini 的 [ResourceColorBuffer]，兜底按文件名 ──
        public static List<Entry> Discover(string root)
        {
            ColorUseCache.Clear();   // ini 可能被作者更新过，每次扫描重新判定
            var map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            var order = new List<Entry>();

            foreach (var ini in Walk(root, "*.ini"))
            {
                if (!string.Equals(Path.GetExtension(ini), ".ini", StringComparison.OrdinalIgnoreCase)) continue;
                if (IsBackupName(Path.GetFileName(ini))) continue;
                try { if (new FileInfo(ini).Length > IniSizeLimit) continue; } catch { continue; }

                string rel = null, fmt = null; int stride = 0; bool inSection = false;
                try
                {
                    foreach (var raw in File.ReadLines(ini))
                    {
                        string t = raw.Trim();
                        if (!inSection)
                        {
                            if (t == "[ResourceColorBuffer]") inSection = true;
                            continue;
                        }
                        if (t.StartsWith("[")) break;
                        if (t.StartsWith("filename", StringComparison.OrdinalIgnoreCase))
                        {
                            int eq = t.IndexOf('=');
                            if (eq > 0) rel = t.Substring(eq + 1).Trim().Trim('"');
                        }
                        else if (t.StartsWith("format", StringComparison.OrdinalIgnoreCase))
                        {
                            int eq = t.IndexOf('=');
                            if (eq > 0) fmt = t.Substring(eq + 1).Trim();
                        }
                        else if (t.StartsWith("stride", StringComparison.OrdinalIgnoreCase))
                        {
                            int eq = t.IndexOf('=');
                            int v;
                            if (eq > 0 && int.TryParse(t.Substring(eq + 1).Trim(), out v)) stride = v;
                        }
                    }
                }
                catch { continue; }

                if (rel == null) continue;
                string cand = Path.Combine(Path.GetDirectoryName(ini), rel.Replace('/', '\\'));
                if (!File.Exists(cand)) continue;
                cand = Path.GetFullPath(cand);
                if (map.ContainsKey(cand)) continue;
                var e = new Entry { BufPath = cand, Format = fmt, Stride = stride };
                map[cand] = e; order.Add(e);
            }

            foreach (var f in Walk(root, "Color.buf"))
            {
                if (!string.Equals(Path.GetFileName(f), "Color.buf", StringComparison.Ordinal)) continue;
                string full = Path.GetFullPath(f);
                if (map.ContainsKey(full)) continue;
                var e = new Entry { BufPath = full };
                map[full] = e; order.Add(e);
            }

            foreach (var e in order) e.ModName = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(e.BufPath)));
            return order;
        }

        // ── 还原点 ──
        public static List<RestorePoint> GetPoints(string bufPath)
        {
            var pts = new List<RestorePoint>();
            string dir = Path.GetDirectoryName(bufPath);
            string leaf = Path.GetFileName(bufPath);
            if (!Directory.Exists(dir)) return pts;

            string[] files;
            try { files = Directory.GetFiles(dir); } catch { return pts; }

            string rx = "^" + Regex.Escape(leaf) + "_(\\d{4}-\\d{2}-\\d{2} \\d{2}-\\d{2}-\\d{2}(?:\\.\\d{3})?)\\.BAK$";
            foreach (var f in files)
            {
                string name = Path.GetFileName(f);
                if (name.StartsWith(leaf + ".orig.", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                {
                    var m = Regex.Match(name, "\\.orig\\.([0-9a-fA-F]{8})\\.bak$", RegexOptions.IgnoreCase);
                    var fi = new FileInfo(f);
                    pts.Add(new RestorePoint
                    {
                        Kind = "mine",
                        Label = "mine:" + (m.Success ? m.Groups[1].Value.ToLowerInvariant() : "?"),
                        File = name,
                        Created = fi.LastWriteTime,
                        Size = fi.Length
                    });
                }
                else if (string.Equals(name, leaf + LegacySuffix, StringComparison.OrdinalIgnoreCase))
                {
                    var fi = new FileInfo(f);
                    pts.Add(new RestorePoint { Kind = "legacy", Label = "legacy", File = name, Created = fi.LastWriteTime, Size = fi.Length });
                }
                else
                {
                    var m = Regex.Match(name, rx, RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var fi = new FileInfo(f);
                        DateTime when = fi.LastWriteTime;
                        DateTime parsed;
                        if (DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) when = parsed;
                        pts.Add(new RestorePoint { Kind = "fixer", Label = "fixer:" + m.Groups[1].Value, File = name, Created = when, Size = fi.Length });
                    }
                }
            }
            return pts;
        }

        public static byte[] ReadPoint(Entry e, RestorePoint p)
        {
            return File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(e.BufPath), p.File));
        }

        public static RestorePoint SelectPoint(Entry e, string selector)
        {
            if (e.Points.Count == 0) return null;
            string sel = (selector ?? "latest").ToLowerInvariant();

            if (sel == "latest")
            {
                if (e.ManPoint != null) return e.ManPoint;
                foreach (var kind in new[] { "mine", "legacy", "fixer" })
                {
                    var hit = e.Points.Where(p => p.Kind == kind).OrderByDescending(p => p.Created).FirstOrDefault();
                    if (hit != null) return hit;
                }
                return e.Points[0];
            }
            if (sel == "earliest") return e.Points.OrderBy(p => p.Created).First();

            var exact = e.Points.FirstOrDefault(p => p.Label.ToLowerInvariant() == sel || p.File.ToLowerInvariant() == sel);
            if (exact != null) return exact;
            return e.Points.FirstOrDefault(p => p.Label.ToLowerInvariant().StartsWith(sel));
        }

        // ── 读取并判定状态 ──
        public static void Load(Entry e, bool deep)
        {
            e.Cur = File.ReadAllBytes(e.BufPath);
            e.Verts = e.Cur.Length / 4;
            e.CurHash = Sha1(e.Cur);

            var man = ReadManifest(e.BufPath);
            e.HasManifest = man != null;
            e.ManOriginalFile = man != null && man.ContainsKey("originalFile") ? man["originalFile"] : null;
            e.ManPatchedHash = man != null && man.ContainsKey("patchedHash") ? man["patchedHash"] : null;
            e.ManAlpha = man != null && man.ContainsKey("alpha") ? man["alpha"] : null;

            e.Points = GetPoints(e.BufPath);
            e.ManPoint = e.ManOriginalFile == null ? null : e.Points.FirstOrDefault(p => string.Equals(p.File, e.ManOriginalFile, StringComparison.OrdinalIgnoreCase));
            e.Orig = e.ManPoint ?? SelectPoint(e, "latest");
            e.OrigHash = e.Orig == null ? null : Sha1(ReadPoint(e, e.Orig));
            e.PatchedByUs = e.ManPatchedHash != null && string.Equals(e.ManPatchedHash, e.CurHash, StringComparison.OrdinalIgnoreCase);

            e.MineCount = e.Points.Count(p => p.Kind == "mine" || p.Kind == "legacy");
            e.FixerCount = e.Points.Count(p => p.Kind == "fixer");

            var files = new string[0];
            try { files = Directory.GetFiles(Path.GetDirectoryName(e.BufPath)); } catch { }
            string leaf = Path.GetFileName(e.BufPath);
            e.MyBakSize = files.Where(f =>
            {
                string n = Path.GetFileName(f);
                return n.StartsWith(leaf + ".orig.", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(n, leaf + LegacySuffix, StringComparison.OrdinalIgnoreCase);
            }).Sum(f => new FileInfo(f).Length);

            bool rgZero = false, allZero = false; long gOn = -1;
            if (!e.PatchedByUs)
            {
                if (deep || e.Verts <= CountVerticesLimit)
                    Analyze(e.Cur, out gOn, out rgZero, out allZero);
                else
                {
                    // 只判断 R/G 是否全零（足够定状态），顶点计数留空
                    bool z = true, az = true;
                    for (int i = 0; i + 3 < e.Cur.Length; i += 4)
                    {
                        if (e.Cur[i] != 0 || e.Cur[i + 1] != 0) z = false;
                        if (e.Cur[i] != 0 || e.Cur[i + 1] != 0 || e.Cur[i + 2] != 0 || e.Cur[i + 3] != 0) az = false;
                        if (!z && !az) break;
                    }
                    rgZero = z; allZero = az;
                }
            }
            else { rgZero = true; allZero = false; gOn = 0; }
            e.RgZero = rgZero; e.AllZero = allZero; e.GOn = gOn;

            if (e.OrigHash != null && string.Equals(e.OrigHash, e.CurHash, StringComparison.OrdinalIgnoreCase)) e.State = "pristine";
            else if (e.PatchedByUs) e.State = "patched";
            else if (e.RgZero) e.State = "patched";
            else if (e.MineCount > 0) e.State = "modified";
            else e.State = "untracked";

            // "只换贴图"型 mod：ini 里没有声明 Color.buf，改它不会有任何效果
            e.ColorUsed = DetectColorUsed(e.BufPath);
            SetStateText(e);
        }

        // 状态列的人话文案。单独抽出来，切换中英时不用重读文件就能重新生成。
        public static void SetStateText(Entry e)
        {
            switch (e.State)
            {
                case "pristine": e.StateText = Lang.T("原版未改"); return;
                case "untracked": e.StateText = Lang.T("原版(未记录)"); break;
                case "modified": e.StateText = Lang.T("内容不认识(有原始档)"); return;
                default: e.StateText = Lang.T("已去描边"); break;
            }
            if (e.State == "patched" && e.Orig == null)
            {
                if (e.AllZero) e.StateText = Lang.T("空顶点色(无需处理)");
                else if (e.HasManifest || e.MineCount > 0) e.StateText = Lang.T("已去描边(无原始档)");
                else e.StateText = Lang.T("R/G 全零(本就无描边)");
            }
            if (e.State == "untracked" && e.FixerCount > 0) e.StateText = Lang.T("原版(有Fixer备份)");
            if (!e.ColorUsed) e.StateText = Lang.T("未使用顶点色(改了无效)");
        }

        private static bool IsEmptyColorState(Entry e)
        {
            return e.State == "patched" && e.Orig == null && e.AllZero;
        }
        private static bool IsNoOutlineState(Entry e)
        {
            return e.State == "patched" && e.Orig == null && !e.AllZero && !e.HasManifest && e.MineCount == 0;
        }

        // ── 这个 mod 的 ini 到底用不用自己的 Meshes\Color.buf？ ──
        // 有些 mod（例如 group_6\02 某角色 ww0000）是"只换贴图"型：它的 ini 只声明
        // ResourceTextureN，完全不声明 ib=/vb0..vb4=，也不声明 Resource*Buffer。
        // 这类 mod 画的是**游戏原版网格**，所以它自己的 Color.buf 根本不会被加载，
        // 改它 R/G 一点效果都没有——必须明确告诉用户，而不是假装处理成功。
        // 判据：从"缓冲区文件自己所在的目录"往上最多三级，**任何一处**提到 "color" 就算在用。
        // 两个坑都要避开：
        //  1) 不能从 Meshes 的上一级开始 —— 有些 mod 把 Color 缓冲直接放成 mod 文件夹里的
        //     <GUID>.assets（group_32\01 某角色 ww0000 官模），那一级会落到 group_32\
        //  2) 不能"第一个含 ini 的目录说了算" —— 例如
        //     ...\Lupa KMS Regensburg v1.3\Lupa0\Meshes\1.ini 不含 color，
        //     真正声明的 mod.ini 在再上一级（Lupa0\mod.ini）
        // 三级内一个 ini 都没找到时按"在用"处理（不敢下结论就别下结论）。
        private static readonly Dictionary<string, bool> ColorUseCache =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public static bool DetectColorUsed(string bufPath)
        {
            string dir = Path.GetDirectoryName(bufPath);
            bool cached;
            if (dir != null && ColorUseCache.TryGetValue(dir, out cached)) return cached;

            bool used = false, sawIni = false;
            string d = dir;
            for (int up = 0; up < 3 && d != null; up++)
            {
                string[] inis;
                try { inis = Directory.GetFiles(d, "*.ini"); }
                catch { inis = new string[0]; }
                if (inis.Length > 0)
                {
                    sawIni = true;
                    foreach (var ini in inis)
                    {
                        try
                        {
                            if (File.ReadAllText(ini).IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0) { used = true; break; }
                        }
                        catch { }
                    }
                    if (used) break;
                }
                string upDir = Path.GetDirectoryName(d);
                if (upDir == null || upDir == d) break;
                d = upDir;
            }
            bool result = sawIni ? used : true;
            if (dir != null) ColorUseCache[dir] = result;
            return result;
        }

        // ── 应用 ──
        public static string Apply(Entry e, string alpha, bool dryRun, bool deep)
        {
            if (!e.ColorUsed)
            {
                L(Lang.T("跳过 -> {0}（mod 的 ini 没有声明 Color.buf，它画的是游戏原版网格，顶点色方案对它无效）"), e.BufPath);
                return Lang.T("跳过：mod 未使用顶点色，改了无效");
            }
            if (e.State == "patched" && e.ManAlpha == alpha && e.PatchedByUs) return Lang.T("已是最新，跳过");
            if (IsEmptyColorState(e)) return Lang.T("无需处理(空顶点色)");
            if (IsNoOutlineState(e)) return Lang.T("无需处理(本就无描边)");

            int alphaValue = -1;
            if (alpha != "keep") alphaValue = int.Parse(alpha, CultureInfo.InvariantCulture);

            byte[] baseBytes; byte[] toArchive; string hashToArchive; string baseSrc;
            if (e.State == "patched")
            {
                if (e.Orig != null)
                {
                    baseBytes = ReadPoint(e, e.Orig);
                    toArchive = baseBytes; hashToArchive = e.OrigHash;
                    baseSrc = Lang.T("还原点(") + e.Orig.Label + ")";
                }
                else
                {
                    baseBytes = e.Cur; toArchive = null; hashToArchive = null;
                    baseSrc = Lang.T("当前文件（无原始档，仅调整 Alpha）");
                }
            }
            else
            {
                baseBytes = e.Cur; toArchive = e.Cur; hashToArchive = e.CurHash;
                baseSrc = e.State == "pristine" ? Lang.T("当前文件(=已记录原版)") : Lang.T("当前文件(新版原版)");
            }

            string origFile = null;
            if (toArchive != null)
            {
                origFile = Path.GetFileName(e.BufPath) + ".orig." + hashToArchive.Substring(0, 8) + ".bak";
                string full = Path.Combine(Path.GetDirectoryName(e.BufPath), origFile);
                if (File.Exists(full)) L(Lang.T("      已有同内容原始档，跳过存档 -> {0}"), origFile);
                else
                {
                    if (!dryRun) File.WriteAllBytes(full, toArchive);
                    L(Lang.T("      原始档已存档 -> {0}"), origFile);
                }
            }
            else L(Lang.T("      警告：已去描边但找不到原始档，只能就地调整 Alpha，之后无法还原"));

            var patch = new byte[baseBytes.Length];
            Buffer.BlockCopy(baseBytes, 0, patch, 0, baseBytes.Length);
            Patch(patch, alphaValue);

            if (dryRun)
            {
                L(Lang.T("[演练] {0}（顶点 {1}；基准 {2}；Alpha={3}）"), e.BufPath, e.Verts, baseSrc, alpha);
                return Lang.T("演练：将去描边(Alpha=") + alpha + ")";
            }

            File.WriteAllBytes(e.BufPath, patch);
            WriteManifest(e.BufPath, origFile, hashToArchive, Sha1(patch), alpha, e.Verts);
            L(Lang.T("已去描边 -> {0}（顶点 {1}；基准 {2}；Alpha={3}）"), e.BufPath, e.Verts, baseSrc, alpha);
            return Lang.T("已去描边(Alpha=") + alpha + ")";
        }

        // ── 还原 ──
        public static string Restore(Entry e, string selector, bool dryRun, bool cleanAfter)
        {
            var pick = SelectPoint(e, selector);
            if (pick == null) { L(Lang.T("无法还原（找不到还原点「{0}」）：{1}"), selector, e.BufPath); return Lang.T("还原失败：找不到还原点「") + selector + Lang.T("」"); }
            if (dryRun) { L(Lang.T("[演练] 将用 {0} 还原 {1}"), pick.File, e.BufPath); return Lang.T("演练：将还原到 ") + pick.Label; }

            File.WriteAllBytes(e.BufPath, ReadPoint(e, pick));
            RemoveManifest(e.BufPath);
            L(Lang.T("已还原 -> {0}（还原点 {1}）"), e.BufPath, pick.Label);

            if (cleanAfter)
            {
                long freed = 0;
                foreach (var f in MyArtifacts(e)) { freed += new FileInfo(f).Length; File.Delete(f); }
                if (freed > 0) L(Lang.T("      顺手清理本脚本存档：释放 {0} KB"), freed / 1024);
            }
            return Lang.T("已还原(") + pick.Label + ")";
        }

        // ── 清理 ──
        public static IEnumerable<string> MyArtifacts(Entry e)
        {
            string dir = Path.GetDirectoryName(e.BufPath);
            string leaf = Path.GetFileName(e.BufPath);
            return Directory.GetFiles(dir).Where(f =>
            {
                string n = Path.GetFileName(f);
                return n.StartsWith(leaf + ".orig.", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(n, leaf + LegacySuffix, StringComparison.OrdinalIgnoreCase);
            });
        }

        public static string Clean(Entry e, bool dryRun)
        {
            var mine = MyArtifacts(e).ToList();
            string man = Path.Combine(Path.GetDirectoryName(e.BufPath), ManifestName);
            bool hasMan = File.Exists(man);
            if (mine.Count == 0 && !hasMan) return Lang.T("无可清理项");
            if (dryRun) return Lang.T("演练：将清理 ") + mine.Count + Lang.T(" 个存档 + 清单");

            long freed = 0;
            foreach (var f in mine) { freed += new FileInfo(f).Length; File.Delete(f); }
            if (hasMan) File.Delete(man);
            L(Lang.T("已清理本脚本存档：{0} 个，释放 {1} KB（Fixer 的 .BAK 未动）"), mine.Count, freed / 1024);
            return Lang.T("已清理 ") + mine.Count + Lang.T(" 个存档");
        }

        // ── 体检 ──
        public static string Verify(Entry e, out bool isIssue, out bool isNote)
        {
            isIssue = false; isNote = false;
            var issues = new List<string>();
            var notes = new List<string>();

            if (e.HasManifest && e.ManOriginalFile != null &&
                !File.Exists(Path.Combine(Path.GetDirectoryName(e.BufPath), e.ManOriginalFile)))
                issues.Add(Lang.T("原始档丢失，无法还原"));
            if (e.HasManifest && e.MineCount == 0) issues.Add(Lang.T("清单在、存档不在"));
            if (!e.HasManifest && e.MineCount > 0) notes.Add(Lang.T("有存档、无清单（半状态，仍可还原）"));
            if (e.State == "modified") notes.Add(Lang.T("内容被外部改过（会以新内容为基准重打）"));
            if (e.State == "patched" && !e.HasManifest && e.MineCount == 0)
                notes.Add(Lang.T("无描边数据且无来源记录（若为角色本体则记录已丢失，无法还原）"));
            if (!e.ColorUsed)
            {
                if (e.MineCount > 0) notes.Add(Lang.T("mod 的 ini 没声明 Color.buf，改动不会有任何效果（建议还原掉，避免误以为生效）"));
                else notes.Add(Lang.T("mod 的 ini 没声明 Color.buf，顶点色方案对它无效（会在游戏里保留原版描边）"));
            }

            if (issues.Count > 0) { isIssue = true; return string.Join(Lang.T("；"), issues.ToArray()); }
            if (notes.Count > 0) { isNote = true; return string.Join(Lang.T("；"), notes.ToArray()); }
            return null;
        }

        // ── 孤儿产物（mod 被删除/替换后留下的零碎文件）──
        public static List<string> FindOrphans(string root)
        {
            var result = new List<string>();
            foreach (var f in Walk(root, "*.bak"))
            {
                string name = Path.GetFileName(f);
                var m = Regex.Match(name, "\\.orig\\.[0-9a-fA-F]{8}\\.bak$", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                string buf = Path.Combine(Path.GetDirectoryName(f), name.Substring(0, m.Index));
                if (!File.Exists(buf)) result.Add(f);
            }
            foreach (var f in Walk(root, "*.json"))
            {
                if (!string.Equals(Path.GetFileName(f), ManifestName, StringComparison.OrdinalIgnoreCase)) continue;
                string dir = Path.GetDirectoryName(f);
                string bufLeaf = null;
                var man = ReadManifest(Path.Combine(dir, "Color.buf"));
                if (man != null && man.ContainsKey("originalFile"))
                {
                    var m = Regex.Match(man["originalFile"], "^(.*)\\.orig\\.[0-9a-fA-F]{8}\\.bak$", RegexOptions.IgnoreCase);
                    if (m.Success) bufLeaf = m.Groups[1].Value;
                }
                string buf = Path.Combine(dir, bufLeaf ?? "Color.buf");
                if (!File.Exists(buf)) result.Add(f);
            }
            return result;
        }

        public static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return (bytes / 1024.0 / 1024 / 1024).ToString("0.00") + " GB";
            if (bytes >= 1024L * 1024) return (bytes / 1024.0 / 1024).ToString("0.0") + " MB";
            return (bytes / 1024).ToString("0") + " KB";
        }
    }

    // ───────────────────────── 入口 / CLI ─────────────────────────
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;
        private const int STD_OUTPUT_HANDLE = -11;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [STAThread]
        private static int Main(string[] args)
        {
            // 语言：命令行 -Lang en|zh 优先，否则读 settings.json 里的 lang=
            string langArg = GetArg(args, "-Lang");
            if (langArg == null) langArg = GetArg(args, "--lang");
            try
            {
                Lang.InitFrom(langArg, Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "settings.json"));
            }
            catch { }

            if (args.Length > 0 && args[0].Equals("--selftest", StringComparison.OrdinalIgnoreCase))
                return SelfTest();

            bool guiScan = args.Any(a => a.Equals("--scan", StringComparison.OrdinalIgnoreCase));
            bool cli = !guiScan && args.Length > 0 && args.Any(a => a.StartsWith("-", StringComparison.Ordinal));
            if (cli)
            {
                // 输出走"管道 / > 文件"时，AttachConsole 会把重定向的 stdout 抢走
                // （cmd 下 `> report.txt` 得到空文件）；而进程没有控制台时 .NET 的
                // Console.Out 又会退化成黑洞（PowerShell 里 `$o = & exe ...` 什么都拿不到）。
                // 所以：先留住原 stdout 句柄，挂上父控制台拿到它的输出代码页，
                // 再把 Console.Out 换成"写回原句柄、按父控制台代码页"的 writer。
                // 交互式 / 管道 / 重定向三种情况的编码就此一致（正常机器上是 GBK，UTF-8 控制台下是 UTF-8）。
                IntPtr origOut = GetStdHandle(STD_OUTPUT_HANDLE);
                bool redirected = Console.IsOutputRedirected;
                try { AttachConsole(ATTACH_PARENT_PROCESS); } catch { }
                if (redirected)
                {
                    try
                    {
                        var handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(origOut, false);
                        var stream = new FileStream(handle, FileAccess.Write);
                        Console.SetOut(new StreamWriter(stream, Console.OutputEncoding) { AutoFlush = true });
                    }
                    catch { }
                }
                return RunCli(args);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        private static string GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static bool HasFlag(string[] args, string name)
        {
            return args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private static int RunCli(string[] args)
        {
            string logFile = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "WuwaOutlineTool.log");
            Core.Log = delegate(string s)
            {
                Console.WriteLine(s);
                try
                {
                    File.AppendAllText(logFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + s + Environment.NewLine, new UTF8Encoding(false));
                }
                catch { }
            };
            string root = GetArg(args, "-Path");
            if (root == null) root = GetArg(args, "--path");
            if (root == null) { Console.WriteLine(Lang.T("用法见 --help")); return 2; }
            if (!Directory.Exists(root)) { Console.WriteLine(Lang.T("目录不存在：") + root); return 2; }

            string alpha = GetArg(args, "-Alpha") ?? "keep";
            bool dry = HasFlag(args, "-DryRun");
            bool deep = HasFlag(args, "-Deep");

            if (HasFlag(args, "-PurgeOrphans"))
            {
                var orphans = Core.FindOrphans(root);
                if (orphans.Count == 0) { Console.WriteLine(Lang.T("没有孤儿存档：本工具产物都能对上各自的 Color.buf。")); return 0; }
                long freed = 0;
                foreach (var o in orphans)
                {
                    long sz = new FileInfo(o).Length;
                    if (!dry) File.Delete(o);
                    freed += sz;
                    Console.WriteLine((dry ? Lang.T("[演练] 将删除 ") : Lang.T("已删除 ")) + o);
                }
                Console.WriteLine(string.Format(Lang.T("{0}孤儿存档 {1} 个，{2}"), dry ? Lang.T("演练：") : Lang.T("已清理"), orphans.Count, Core.FormatSize(freed)));
                return 0;
            }

            var list = Core.Discover(root);
            if (list.Count == 0) { Console.WriteLine(Lang.T("在 ") + root + Lang.T(" 下没找到 Color.buf（也读不到 [ResourceColorBuffer]）")); return 2; }
            foreach (var e in list) { try { Core.Load(e, deep); } catch (Exception ex) { Console.WriteLine(Lang.T("读取失败：") + e.BufPath + " —— " + ex.Message); } }

            if (HasFlag(args, "-Status"))
            {
                Console.WriteLine(string.Format("{0,-46} {1,9} {2,-22} {3,10} {4,-6} {5}", Lang.T("皮肤"), Lang.T("顶点数"), Lang.T("状态"), Lang.T("带描边"), "Alpha", Lang.T("原始档")));
                foreach (var e in list)
                    Console.WriteLine(string.Format("{0,-46} {1,9} {2,-22} {3,10} {4,-6} {5}",
                        Trim(e.ModName, 46), e.Verts, e.StateText, e.GOn < 0 ? "-" : e.GOn.ToString(), e.ManAlpha ?? "-", e.Orig == null ? Lang.T("无") : e.Orig.Label));
                var orphans = Core.FindOrphans(root);
                Console.WriteLine(string.Format(Lang.T("共 {0} 个 Color.buf；孤儿存档 {1} 个。"), list.Count, orphans.Count));
                return 0;
            }

            if (HasFlag(args, "-History"))
            {
                foreach (var e in list)
                {
                    if (e.Points.Count == 0) continue;
                    Console.WriteLine("== " + e.ModName + " ==");
                    foreach (var p in e.Points.OrderBy(x => x.Created))
                        Console.WriteLine(string.Format("   {0,-28} {1,10} {2:yyyy-MM-dd HH:mm:ss}  {3}", p.Label, Core.FormatSize(p.Size), p.Created, p.File));
                }
                return 0;
            }

            if (HasFlag(args, "-Verify"))
            {
                int issues = 0, notes = 0, clean = 0;
                foreach (var e in list)
                {
                    bool i, n; string msg = Core.Verify(e, out i, out n);
                    if (i) { issues++; Console.WriteLine(Lang.T("  [异常] ") + e.ModName + Lang.T("：") + msg); }
                    else if (n) { notes++; Console.WriteLine(Lang.T("  [提示] ") + e.ModName + Lang.T("：") + msg); }
                    else clean++;
                }
                var orphans = Core.FindOrphans(root);
                Console.WriteLine(string.Format(Lang.T("体检完成：{0} 个 Color.buf —— 无问题 {1}；提示 {2}；异常 {3}"), list.Count, clean, notes, issues));
                if (issues == 0) Console.WriteLine(Lang.T("异常为 0：所有记录都对得上，还原链完整。"));
                if (orphans.Count > 0) { Console.WriteLine(Lang.T("孤儿存档 ") + orphans.Count + Lang.T(" 个（-PurgeOrphans 清理）：")); foreach (var o in orphans) Console.WriteLine("   " + o); }
                else Console.WriteLine(Lang.T("没有孤儿存档。"));
                return 0;
            }

            int changed = 0;
            foreach (var e in list)
            {
                string r;
                if (HasFlag(args, "-Clean") && !HasFlag(args, "-Restore")) r = Core.Clean(e, dry);
                else if (HasFlag(args, "-Restore"))
                {
                    string sel = GetArg(args, "-RestoreTo") ?? "latest";
                    r = Core.Restore(e, sel, dry, HasFlag(args, "-Clean"));
                }
                else r = Core.Apply(e, alpha, dry, deep);
                if (r != Lang.T("已是最新，跳过") && r != Lang.T("无可清理项")) Console.WriteLine("[" + e.ModName + "] " + r);
                if (r.StartsWith(Lang.T("已去描边")) || r.StartsWith(Lang.T("已还原")) || r.StartsWith(Lang.T("已清理"))) changed++;
            }
            Console.WriteLine(string.Format(Lang.T("完成：本次改动 {0} 个文件。"), changed));
            return 0;
        }

        private static string Trim(string s, int n)
        {
            if (s == null) return "";
            return s.Length <= n ? s : s.Substring(0, n - 1) + "…";
        }

        // ── 自检：不依赖游戏，纯造数据验证核心逻辑 ──
        private static int SelfTest()
        {
            int fails = 0;
            Action<bool, string> check = delegate(bool ok, string what)
            {
                Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what);
                if (!ok) fails++;
            };
            string root = Path.Combine(Path.GetTempPath(), "wuwa-outline-selftest-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            string meshes = Path.Combine(root, "TestMod", "Meshes");
            Directory.CreateDirectory(meshes);
            File.WriteAllText(Path.Combine(root, "TestMod", "mod.ini"),
                "[ResourceColorBuffer]\r\nformat = DXGI_FORMAT_R8G8B8A8_UNORM\r\nstride = 4\r\nfilename = Meshes/Color.buf\r\n");

            // 造 2000 个顶点：R/G/B/A 都有变化
            var data = new byte[2000 * 4];
            var rnd = new Random(1234);
            for (int i = 0; i < data.Length; i += 4)
            {
                data[i] = 255;                        // R 描边遮罩
                data[i + 1] = (byte)rnd.Next(0, 256); // G 描边厚度
                data[i + 2] = (byte)rnd.Next(0, 256); // B 皮肤遮罩
                data[i + 3] = (byte)rnd.Next(0, 256); // A 头发
            }
            string buf = Path.Combine(meshes, "Color.buf");
            File.WriteAllBytes(buf, data);
            string origHash = Core.Sha1(data);

            var list = Core.Discover(root);
            check(list.Count == 1, Lang.T("发现 1 个 Color.buf（实际 ") + list.Count + Lang.T("）"));
            var e = list[0];
            Core.Load(e, true);
            check(e.State == "untracked", Lang.T("初始状态=原版(未记录)（实际 ") + e.State + Lang.T("）"));

            Core.Apply(e, "keep", false, true);
            var patched = File.ReadAllBytes(buf);
            bool rg = true, ba = true;
            for (int i = 0; i < patched.Length; i += 4)
            {
                if (patched[i] != 0 || patched[i + 1] != 0) rg = false;
                if (patched[i + 2] != data[i + 2] || patched[i + 3] != data[i + 3]) ba = false;
            }
            check(rg, Lang.T("R/G 已清零"));
            check(ba, Lang.T("B/A 逐字节保留"));
            check(File.Exists(Path.Combine(meshes, Core.ManifestName)), Lang.T("生成了清单"));
            check(File.Exists(Path.Combine(meshes, "Color.buf.orig." + origHash.Substring(0, 8) + ".bak")), Lang.T("生成了哈希存档"));

            Core.Load(e, true);
            check(e.State == "patched", Lang.T("打完后状态=已去描边（实际 ") + e.State + Lang.T("）"));
            check(Core.Apply(e, "keep", false, true) == Lang.T("已是最新，跳过"), Lang.T("重复运行幂等跳过"));

            Core.Apply(e, "0", false, true);
            var p2 = File.ReadAllBytes(buf);
            bool a0 = true;
            for (int i = 3; i < p2.Length; i += 4) if (p2[i] != 0) a0 = false;
            check(a0, Lang.T("Alpha=0 应用到 A 通道"));
            check(p2[2] == data[2], Lang.T("改 Alpha 后 B 仍保留"));

            Core.Load(e, true);
            Core.Restore(e, "latest", false, false);
            check(Core.Sha1(File.ReadAllBytes(buf)) == origHash, Lang.T("还原后与原始文件逐字节一致"));
            check(!File.Exists(Path.Combine(meshes, Core.ManifestName)), Lang.T("还原后清单已删除"));

            // 作者更新场景
            Core.Load(e, true);
            Core.Apply(e, "keep", false, true);
            var updated = new byte[data.Length];
            Buffer.BlockCopy(data, 0, updated, 0, data.Length);
            for (int i = 0; i < updated.Length; i += 40) { updated[i + 1] = (byte)((updated[i + 1] + 37) % 256); updated[i + 2] = 200; }
            File.WriteAllBytes(buf, updated);
            string updHash = Core.Sha1(updated);
            Core.Load(e, true);
            check(e.State == "modified", Lang.T("作者更新后状态=内容不认识(有原始档)（实际 ") + e.State + Lang.T("）"));
            Core.Apply(e, "keep", false, true);
            var afterUpd = File.ReadAllBytes(buf);
            bool ba2 = true;
            for (int i = 0; i < afterUpd.Length; i += 4)
                if (afterUpd[i + 2] != updated[i + 2] || afterUpd[i + 3] != updated[i + 3]) ba2 = false;
            check(ba2, Lang.T("更新后重打：B/A 按新版本保留（不再用旧档覆盖）"));
            Core.Load(e, true);
            Core.Restore(e, "latest", false, false);
            check(Core.Sha1(File.ReadAllBytes(buf)) == updHash, Lang.T("更新后还原 = 新版内容"));

            // 重命名 / 挪动的稳健性
            string moved = Path.Combine(root, "DISABLED Renamed Mod [group_X]");
            Directory.Move(Path.Combine(root, "TestMod"), moved);
            var list2 = Core.Discover(root);
            check(list2.Count == 1 && list2[0].ModName.Contains("Renamed"), Lang.T("重命名后仍能发现"));
            Core.Load(list2[0], true);
            check(list2[0].Points.Count > 0, Lang.T("重命名后还原点仍在（状态随文件夹走）"));

            // 孤儿（删掉 Color.buf 只留产物）
            Core.Load(list2[0], true);
            Core.Apply(list2[0], "keep", false, true);
            File.Delete(Path.Combine(moved, "Meshes", "Color.buf"));
            var orphans = Core.FindOrphans(root);
            check(orphans.Count >= 2, Lang.T("能识别孤儿存档/清单（") + orphans.Count + Lang.T(" 个）"));
            foreach (var o in orphans) File.Delete(o);
            check(Core.FindOrphans(root).Count == 0, Lang.T("清理后无孤儿"));

            try { Directory.Delete(root, true); } catch { }
            Console.WriteLine(fails == 0 ? "SELFTEST OK" : ("SELFTEST FAILED: " + fails + Lang.T(" 项")));
            return fails == 0 ? 0 : 1;
        }
    }
}

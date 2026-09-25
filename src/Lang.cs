// Lang.cs —— 中英双语支持
//
// 设计：**中文原文就是键**。代码里所有面向用户的文案都写成 Lang.T("中文")，
// 英文表放在 lang.en.tsv（可用记事本改，不用编译），构建时由 gen-lang.ps1
// 生成 LangData.cs 编进 exe，保持单文件、零依赖。
//
// 查不到的条目原样返回中文 —— 漏翻只是显示中文，不会空白、不会崩。
// TSV 里 \r \n \t \\ 是转义写法（因为一个条目必须占一行）。

using System;
using System.Collections.Generic;
using System.IO;

namespace WuwaOutline
{
    internal static class Lang
    {
        /// <summary>true = English，false = 中文。默认中文。</summary>
        public static bool En;

        public static string T(string zh) { return T(zh, null); }

        /// <summary>需要临时覆盖某个词时用两参形式（一般不用，翻译表里改更省事）。</summary>
        public static string T(string zh, string en)
        {
            if (!En) return zh;
            if (en != null) return en;
            if (_map == null) Build();
            string hit;
            return _map.TryGetValue(zh, out hit) ? hit : zh;
        }

        /// <summary>语言名，用于按钮显示与 settings.json。</summary>
        public static string Code { get { return En ? "en" : "zh"; } }

        public static bool IsEn(string v)
        {
            if (v == null) return false;
            v = v.Trim();
            return v.StartsWith("en", StringComparison.OrdinalIgnoreCase) || v == "1";
        }

        /// <summary>命令行 -Lang en|zh 优先；否则读 settings.json 里的 lang=。</summary>
        public static void InitFrom(string langArg, string settingsFile)
        {
            if (langArg != null) { En = IsEn(langArg); return; }
            try
            {
                if (!File.Exists(settingsFile)) return;
                foreach (var line in File.ReadAllLines(settingsFile))
                {
                    if (line.StartsWith("lang=", StringComparison.OrdinalIgnoreCase))
                    {
                        En = IsEn(line.Substring(5));
                        return;
                    }
                }
            }
            catch { }
        }

        private static Dictionary<string, string> _map;

        private static void Build()
        {
            _map = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < LangData.Rows.Length; i++)
            {
                string row = LangData.Rows[i];
                int t = row.IndexOf('\t');
                if (t <= 0) continue;
                string k = Unescape(row.Substring(0, t));
                string v = Unescape(row.Substring(t + 1));
                if (k.Length > 0) _map[k] = v;
            }
        }

        // TSV 里的 \r \n \t \\ -> 真实字符
        private static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    i++;
                    char n = s[i];
                    if (n == 'r') sb.Append('\r');
                    else if (n == 'n') sb.Append('\n');
                    else if (n == 't') sb.Append('\t');
                    else if (n == '\\') sb.Append('\\');
                    else { sb.Append('\\'); sb.Append(n); }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}

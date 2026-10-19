using System;
using System.Text.RegularExpressions;

namespace GameLog
{
    /// <summary>轻量 HTML 文本处理（不依赖第三方解析库）。</summary>
    public static class Html
    {
        static readonly Regex ScriptStyle = new Regex(@"(?is)<(script|style)[^>]*>.*?</\1>", RegexOptions.Compiled);
        static readonly Regex Tag = new Regex(@"(?s)<[^>]+>", RegexOptions.Compiled);
        static readonly Regex Spaces = new Regex(@"[ \t\r\n\u00a0]+", RegexOptions.Compiled);
        static readonly Regex Blank = new Regex(@"[ \t\r\n\u00a0]+", RegexOptions.Compiled);

        /// <summary>去掉标签、解码实体、压缩空白。中文不会被插入多余空格。</summary>
        public static string Text(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string s = ScriptStyle.Replace(html, " ");
            // 块级标签转换行，避免标题与摘要粘连
            s = Regex.Replace(s, @"(?i)</?(p|div|br|li|tr|td|h[1-6]|section|article|ul|ol|dd|dt)[^>]*>", " ");
            s = Tag.Replace(s, "");
            s = System.Net.WebUtility.HtmlDecode(s);
            s = Spaces.Replace(s, " ");
            return s.Trim();
        }

        public static string Attr(string tagHtml, string attrName)
        {
            if (string.IsNullOrEmpty(tagHtml)) return null;
            var m = Regex.Match(tagHtml, "(?i)\\b" + Regex.Escape(attrName) + @"\s*=\s*(""([^""]*)""|'([^']*)'|([^\s>]+))");
            if (!m.Success) return null;
            if (m.Groups[2].Success) return m.Groups[2].Value;
            if (m.Groups[3].Success) return m.Groups[3].Value;
            if (m.Groups[4].Success) return m.Groups[4].Value;
            return null;
        }

        public static string Title(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            var m = Regex.Match(html, "(?is)<title[^>]*>(.*?)</title>");
            return m.Success ? Text(m.Groups[1].Value) : "";
        }

        /// <summary>是否为图片字节（jpg/png/gif/webp/bmp）。</summary>
        public static bool IsImage(byte[] data)
        {
            if (data == null || data.Length < 12) return false;
            if (data[0] == 0xFF && data[1] == 0xD8) return true;
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return true;
            if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46) return true;
            if (data[0] == 0x42 && data[1] == 0x4D) return true;
            if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46) return true;
            return false;
        }

        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        public static bool HasCjk(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if (c >= 0x4E00 && c <= 0x9FFF) return true;
                if (c >= 0x3400 && c <= 0x4DBF) return true;
            }
            return false;
        }

        public static bool HasAsciiLetter(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return true;
            }
            return false;
        }

        /// <summary>0~1 的相似度（基于归一化后的编辑距离）。</summary>
        public static double Similarity(string a, string b)
        {
            a = Normalize(a);
            b = Normalize(b);
            if (a.Length == 0 || b.Length == 0) return 0;
            if (a == b) return 1;
            if (a.Length > 60) a = a.Substring(0, 60);
            if (b.Length > 60) b = b.Substring(0, 60);
            int n = a.Length, m = b.Length;
            var prev = new int[m + 1];
            var cur = new int[m + 1];
            for (int j = 0; j <= m; j++) prev[j] = j;
            for (int i = 1; i <= n; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int v = prev[j - 1] + cost;
                    if (prev[j] + 1 < v) v = prev[j] + 1;
                    if (cur[j - 1] + 1 < v) v = cur[j - 1] + 1;
                    cur[j] = v;
                }
                var t = prev; prev = cur; cur = t;
            }
            int dist = prev[m];
            int max = Math.Max(n, m);
            return max <= 0 ? 0 : 1.0 - (double)dist / max;
        }
    }
}

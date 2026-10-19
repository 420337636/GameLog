using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GameLog
{
    /// <summary>游民星空（gamersky.com）搜索与专区页解析。</summary>
    public static class Gamersky
    {
        public const string Site = "https://www.gamersky.com/";
        const string SearchBase = "https://so.gamersky.com/?s=";

        static readonly Regex HitRegex = new Regex(
            @"(?s)<a[^>]+href=""(?:https?:)?//www\.gamersky\.com/z/([a-zA-Z0-9\-]+)/""[^>]*>(.*?)</a>",
            RegexOptions.Compiled);

        static readonly string[] IgnoredSlugs = { "list_letter", "list_hot", "index", "all" };

        public static string CoverUrlFor(string slug)
        {
            return "https://image.gamersky.com/zqimg/" + slug + "/image/cover.jpg";
        }

        public static string PageUrlFor(string slug)
        {
            return "https://www.gamersky.com/z/" + slug + "/";
        }

        /// <summary>按关键词搜索游戏专区，返回结果（按页面排序）。</summary>
        public static List<GameCandidate> Search(string query)
        {
            var result = new List<GameCandidate>();
            if (string.IsNullOrEmpty(query)) return result;

            string html = null;
            string url = SearchBase + Uri.EscapeDataString(query) + "&type=hot&sort=des&post=0";
            try
            {
                html = Net.GetString(url, Site);
            }
            catch (Exception ex)
            {
                Log.Warn("游民星空搜索失败(" + query + ")：" + ex.Message);
            }

            if (string.IsNullOrEmpty(html)) return result;
            ParseHits(html, query, result);

            if (result.Count == 0)
            {
                try
                {
                    string alt = "https://so.gamersky.com/all/z?s=" + Uri.EscapeDataString(query) + "&type=hot&sort=des&post=0";
                    string html2 = Net.GetString(alt, Site);
                    ParseHits(html2, query, result);
                }
                catch (Exception ex)
                {
                    Log.Warn("游民星空专区搜索失败(" + query + ")：" + ex.Message);
                }
            }
            return result;
        }

        static void ParseHits(string html, string query, List<GameCandidate> result)
        {
            if (string.IsNullOrEmpty(html)) return;
            var seen = new HashSet<string>();
            foreach (var g in result) seen.Add(g.Slug);

            foreach (Match m in HitRegex.Matches(html))
            {
                string slug = m.Groups[1].Value;
                string name = Html.Text(m.Groups[2].Value);
                if (slug.Length < 2 || name.Length < 1) continue;
                bool skip = false;
                foreach (var s in IgnoredSlugs) if (string.Equals(slug, s, StringComparison.OrdinalIgnoreCase)) skip = true;
                if (skip || seen.Contains(slug)) continue;
                if (name.Contains("更多") || name.Contains("专区首页")) continue;

                var c = new GameCandidate();
                c.Name = name;
                c.Slug = slug;
                c.PageUrl = PageUrlFor(slug);
                c.CoverUrl = CoverUrlFor(slug);
                c.Origin = "游民星空搜索";
                result.Add(c);
                seen.Add(slug);
            }
        }

        /// <summary>读取专区页标题中的中文名，如“艾尔登法环游戏专区_...” → 艾尔登法环。</summary>
        public static string PageName(string slug)
        {
            try
            {
                string html = Net.GetString(PageUrlFor(slug), Site);
                return NameFromTitle(Html.Title(html));
            }
            catch (Exception ex)
            {
                Log.Warn("读取专区页失败(" + slug + ")：" + ex.Message);
                return null;
            }
        }

        public static string NameFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return null;
            string t = title.Trim();
            int i = t.IndexOf("游戏专区", StringComparison.Ordinal);
            if (i > 0) t = t.Substring(0, i);
            else
            {
                i = t.IndexOf("_", StringComparison.Ordinal);
                if (i > 0) t = t.Substring(0, i);
            }
            t = t.Trim().Trim('《', '》', '【', '】', ' ', '-', '_', '|', '：', ':');
            if (t.Length == 0) return null;
            return t;
        }

        /// <summary>下载封面：优先使用可预测的 cover.jpg 地址，失败则从专区页里找。</summary>
        public static byte[] FetchCover(string slug, out string usedUrl)
        {
            usedUrl = CoverUrlFor(slug);
            try
            {
                var data = Net.GetBytes(usedUrl, Site);
                if (Html.IsImage(data) && data.Length > 1500) return data;
            }
            catch (Exception ex)
            {
                Log.Warn("封面直链失败(" + slug + ")：" + ex.Message);
            }

            try
            {
                string html = Net.GetString(PageUrlFor(slug), Site);
                string src = FindCoverInPage(html);
                if (!string.IsNullOrEmpty(src))
                {
                    usedUrl = src;
                    var data = Net.GetBytes(src, Site);
                    if (Html.IsImage(data)) return data;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("封面页面解析失败(" + slug + ")：" + ex.Message);
            }
            return null;
        }

        static string FindCoverInPage(string html)
        {
            if (string.IsNullOrEmpty(html)) return null;
            // 专区页的封面通常带 width=100 height=142（属性顺序不固定）
            foreach (Match m in Regex.Matches(html, "(?is)<img\\b[^>]*>"))
            {
                string tag = m.Value;
                if (tag.IndexOf("width=\"100\"", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    tag.IndexOf("height=\"142\"", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string src = Html.Attr(tag, "src");
                    if (!string.IsNullOrEmpty(src)) return Absolute(src);
                }
            }
            var og = Regex.Match(html, "(?i)<meta[^>]+property=\"og:image\"[^>]+content=\"([^\"]+)\"");
            if (og.Success) return Absolute(og.Groups[1].Value);
            return null;
        }

        static string Absolute(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (url.StartsWith("//")) return "https:" + url;
            if (url.StartsWith("/")) return "https://www.gamersky.com" + url;
            return url;
        }
    }
}

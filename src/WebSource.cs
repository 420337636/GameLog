using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GameLog
{
    /// <summary>网页解析结果（标题、摘要、封面图）。</summary>
    public class PageInfo
    {
        public string Title = "";
        public string Description = "";
        public string ImageUrl = "";
        public string FinalUrl = "";
        public string SiteName = "";
        public string Raw = "";
    }

    /// <summary>
    /// 通用网页 / 百度百科来源。用于在游民星空没有收录时，从百度搜索结果页、百度百科等处获取游戏名与封面。
    /// </summary>
    public static class WebSource
    {
        static readonly string[] GameSites =
        {
            "gamersky.com", "3dmgame.com", "ali213.net", "steampowered.com", "steamcommunity.com",
            "epicgames.com", "gog.com", "17173.com", "9game.cn", "taptap.com", "wegame.com",
            "gamersky.net", "gameres.com", "gamelook.com", "bilibili.com", "douban.com",
            "nintendo.com", "playstation.com", "xbox.com", "ubisoft.com", "ea.com", "blizzard.com"
        };

        public static string DomainOf(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            try
            {
                var u = new Uri(url);
                string host = u.Host.ToLowerInvariant();
                if (host.StartsWith("www.")) host = host.Substring(4);
                return host;
            }
            catch
            {
                var m = Regex.Match(url, "https?://([^/]+)");
                if (m.Success)
                {
                    string host = m.Groups[1].Value.ToLowerInvariant();
                    if (host.StartsWith("www.")) host = host.Substring(4);
                    return host;
                }
                return "";
            }
        }

        public static bool IsGameSite(string domain)
        {
            if (string.IsNullOrEmpty(domain)) return false;
            foreach (var s in GameSites)
            {
                if (domain == s || domain.EndsWith("." + s, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static PageInfo Fetch(string url, string referer)
        {
            return Fetch(url, referer, false);
        }

        public static PageInfo Fetch(string url, string referer, bool soft)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                string finalUrl;
                byte[] data = soft ? Net.GetBytesSoft(url, referer, out finalUrl) : Net.GetBytes(url, referer, out finalUrl);
                if (data == null) return null;
                string html = Net.DecodeText(data);
                if (string.IsNullOrEmpty(html)) return null;
                var page = Parse(html, finalUrl);
                if (string.IsNullOrEmpty(page.FinalUrl)) page.FinalUrl = finalUrl;
                return page;
            }
            catch (Exception ex)
            {
                Log.Warn("读取网页失败(" + url + ")：" + ex.Message);
                return null;
            }
        }

        public static PageInfo Parse(string html, string baseUrl)
        {
            var p = new PageInfo();
            if (string.IsNullOrEmpty(html)) return p;
            p.FinalUrl = baseUrl;
            p.Raw = html;
            p.Title = Html.Title(html);

            var d = Regex.Match(html, "(?is)<meta[^>]+name=\"description\"[^>]+content=\"([^\"]*)\"");
            if (!d.Success) d = Regex.Match(html, "(?is)<meta[^>]+content=\"([^\"]*)\"[^>]+name=\"description\"");
            if (d.Success) p.Description = Html.Text(d.Groups[1].Value);

            var og = Regex.Match(html, "(?is)<meta[^>]+(?:property|name)=\"og:image\"[^>]+content=\"([^\"]+)\"");
            if (!og.Success) og = Regex.Match(html, "(?is)<meta[^>]+content=\"([^\"]+)\"[^>]+(?:property|name)=\"og:image\"");
            if (og.Success) p.ImageUrl = Absolute(og.Groups[1].Value, baseUrl);

            if (string.IsNullOrEmpty(p.ImageUrl))
            {
                // 百度百科的主图
                var bk = Regex.Match(html, @"(https?://(?:bkimg|baikebcs)[^\s""'<>]+)");
                if (bk.Success) p.ImageUrl = bk.Groups[1].Value;
            }

            if (string.IsNullOrEmpty(p.ImageUrl))
            {
                foreach (Match m in Regex.Matches(html, "(?is)<img\\b[^>]*>"))
                {
                    string tag = m.Value;
                    string cls = Html.Attr(tag, "class") ?? "";
                    if (cls.IndexOf("cover", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cls.IndexOf("summary-pic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cls.IndexOf("pic", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string src = Html.Attr(tag, "src");
                        if (string.IsNullOrEmpty(src)) src = Html.Attr(tag, "data-src");
                        if (!string.IsNullOrEmpty(src))
                        {
                            p.ImageUrl = Absolute(src, baseUrl);
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(p.ImageUrl))
            {
                var tw = Regex.Match(html, "(?is)<meta[^>]+name=\"twitter:image\"[^>]+content=\"([^\"]+)\"");
                if (tw.Success) p.ImageUrl = Absolute(tw.Groups[1].Value, baseUrl);
            }
            if (string.IsNullOrEmpty(p.ImageUrl))
            {
                var ld = Regex.Match(html, "(?is)\"image\"\\s*:\\s*\"(https?:[^\"]+)\"");
                if (ld.Success) p.ImageUrl = ld.Groups[1].Value.Replace("\\/", "/");
            }

            if (string.IsNullOrEmpty(p.ImageUrl))
            {
                foreach (Match m in Regex.Matches(html, "(?is)<img\\b[^>]*>"))
                {
                    string tag = m.Value;
                    string src = Html.Attr(tag, "src");
                    if (string.IsNullOrEmpty(src)) src = Html.Attr(tag, "data-src");
                    if (string.IsNullOrEmpty(src) || src.StartsWith("data:")) continue;
                    string low = src.ToLowerInvariant();
                    if (low.Contains("logo") || low.Contains("icon") || low.Contains("avatar") ||
                        low.Contains("qrcode") || low.Contains("blank.") || low.Contains("spacer")) continue;
                    if (!(low.Contains(".jpg") || low.Contains(".jpeg") || low.Contains(".png") || low.Contains(".webp"))) continue;
                    p.ImageUrl = Absolute(src, baseUrl);
                    break;
                }
            }

            p.SiteName = DomainOf(baseUrl);
            return p;
        }

        public static string Absolute(string url, string baseUrl)
        {
            if (string.IsNullOrEmpty(url)) return "";
            url = url.Trim();
            if (url.StartsWith("//")) return "https:" + url;
            if (url.StartsWith("http://") || url.StartsWith("https://")) return url;
            if (url.StartsWith("/"))
            {
                string root = "";
                try
                {
                    var u = new Uri(baseUrl);
                    root = u.Scheme + "://" + u.Authority;
                }
                catch { }
                return root + url;
            }
            return url;
        }

        /// <summary>从网页标题里提取游戏名：“艾尔登法环（游戏）_百度百科” → 艾尔登法环。</summary>
        public static string NameFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            string t = Html.Text(title).Trim();

            string[] siteSuffix = { "_百度百科", "-百度百科", "—百度百科", "_游民星空", "-游民星空", "_3DM", "_游侠", "_17173", "_九游", "_TapTap", "_哔哩哔哩", "_豆瓣" };
            foreach (var suffix in siteSuffix)
            {
                int i = t.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
                if (i > 0) { t = t.Substring(0, i); break; }
            }

            string[] seps = { " - Steam", " on Steam", "-Steam", " Steam", " - ", "—", "–", "｜", "|", "_", " _ ", " on " };
            foreach (var sep in seps)
            {
                int i = t.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (i > 2) { t = t.Substring(0, i); break; }
            }

            // 去掉括号里的说明
            t = Regex.Replace(t, @"[\(（][^\)）]{0,40}[\)）]\s*$", "").Trim();
            t = t.Trim('《', '》', '【', '】', '"', '\'', ' ', '：', ':');

            string[] suffixes = { "官网", "官方网站", "中文版", "官方中文版", "免安装中文版", "破解版", "绿色版", "下载", "专区", "攻略", "游戏", "单机游戏", "电脑版", "PC版", "免费版" };
            foreach (var suf in suffixes)
            {
                if (t.EndsWith(suf, StringComparison.Ordinal) && t.Length > suf.Length + 1)
                    t = t.Substring(0, t.Length - suf.Length).Trim();
            }
            return t;
        }

        public static bool LooksLikeGameName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            name = name.Trim();
            if (name.Length < 2 || name.Length > 24) return false;
            if (name.IndexOf("百度", StringComparison.Ordinal) >= 0) return false;
            if (name.IndexOf("下载", StringComparison.Ordinal) >= 0) return false;
            if (name.IndexOf("攻略", StringComparison.Ordinal) >= 0) return false;
            if (name.IndexOf("官网", StringComparison.Ordinal) >= 0) return false;
            if (name.IndexOf('《') >= 0 || name.IndexOf('》') >= 0) return false;
            if (Regex.IsMatch(name, "[！？!?。，,]")) return false;
            if (Html.HasCjk(name) && name.IndexOf(' ') >= 0 && name.Length > 12) return false;
            string[] newsWords = { "通关", "流程", "更新", "消息", "预告", "销量", "在线", "人数", "评测", "采访", "开发者", "备选", "大全", "合集", "排行榜" };
            foreach (var w in newsWords)
            {
                if (name.IndexOf(w, StringComparison.Ordinal) >= 0) return false;
            }
            if (!Html.HasCjk(name) && !Html.HasAsciiLetter(name)) return false;
            return true;
        }

        /// <summary>判断页面是否在讲一款游戏（避免把“微信”这类百科条目当成游戏）。</summary>
        public static bool LooksLikeGamePage(PageInfo page)
        {
            if (page == null) return false;
            string hay = (page.Title ?? "") + " " + (page.Description ?? "");
            if (hay.IndexOf("游戏", StringComparison.Ordinal) < 0) return false;
            string[] hints = { "发行", "开发商", "开发", "平台", "类型", "角色扮演", "动作", "冒险", "射击", "策略", "模拟", "体育", "竞速", "益智", "独立游戏", "单机", "电子游戏", "游戏作品" };
            foreach (var h in hints)
            {
                if (hay.IndexOf(h, StringComparison.Ordinal) >= 0) return true;
            }
            // 标题里直接带“游戏”也算（如“XX游戏专区”）
            return (page.Title ?? "").IndexOf("游戏", StringComparison.Ordinal) >= 0;
        }

        public static string NameFromBaikeTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            string t = Html.Text(title).Trim();
            int i = t.IndexOf("_百度百科", StringComparison.Ordinal);
            if (i > 0) t = t.Substring(0, i);
            i = t.IndexOf('（');
            if (i <= 0) i = t.IndexOf('(');
            if (i > 0) t = t.Substring(0, i);
            return t.Trim().Trim('《', '》', ' ', '：', ':');
        }

        static int baikeFailures;
        static bool baikeBlocked;

        /// <summary>在百度百科查找词条，返回游戏候选（名称 + 封面）。百度百科经常弹验证码，失败两次后本次运行不再尝试。</summary>
        public static GameCandidate LookupBaike(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (baikeBlocked) return null;

            string url = "https://baike.baidu.com/item/" + Uri.EscapeDataString(name);
            var page = Fetch(url, "https://baike.baidu.com/", true);
            if (page == null || string.IsNullOrEmpty(page.Raw))
            {
                baikeFailures++;
                if (baikeFailures >= 2) baikeBlocked = true;
                return null;
            }
            if (page.Raw.IndexOf("百度安全验证", StringComparison.Ordinal) >= 0 ||
                page.Raw.IndexOf("wappass.baidu.com", StringComparison.Ordinal) >= 0)
            {
                baikeFailures++;
                if (baikeFailures >= 2) baikeBlocked = true;
                Log.Warn("百度百科触发安全验证，跳过百科来源");
                return null;
            }
            if (string.IsNullOrEmpty(page.Title) || page.Title.IndexOf("百度百科", StringComparison.Ordinal) < 0)
            {
                baikeFailures++;
                return null;
            }
            if (!BaikeLooksLikeGame(page)) return null;

            string lemma = NameFromBaikeTitle(page.Title);
            if (!LooksLikeGameName(lemma)) lemma = name;

            var c = new GameCandidate();
            c.Name = lemma;
            c.PageUrl = page.FinalUrl;
            c.CoverUrl = CleanBaikeImage(page.ImageUrl);
            c.Description = page.Description;
            c.Domain = "baike.baidu.com";
            c.Origin = "百度百科";
            return c;
        }

        /// <summary>严格判断百科词条是不是一款游戏（排除公司、人物等词条）。</summary>
        static bool BaikeLooksLikeGame(PageInfo page)
        {
            string title = page.Title ?? "";
            if (title.IndexOf("公司", StringComparison.Ordinal) >= 0) return false;
            if (title.IndexOf("有限公司", StringComparison.Ordinal) >= 0) return false;
            if (title.IndexOf("集团", StringComparison.Ordinal) >= 0) return false;

            string raw = page.Raw ?? "";
            string[] gameFields = { "游戏类型", "游戏平台", "发行日期", "开发商", "制作人", "游戏引擎", "发行商", "玩家人数" };
            foreach (var f in gameFields)
            {
                if (raw.IndexOf(f, StringComparison.Ordinal) >= 0) return true;
            }
            return title.IndexOf("游戏", StringComparison.Ordinal) >= 0;
        }

        static string CleanBaikeImage(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            // 去掉 CDN 的缩放参数，尽量拿原图
            int q = url.IndexOf('?');
            if (q > 0) return url.Substring(0, q);
            return url;
        }
    }

    /// <summary>统一封面下载：百科 / 网页 / 游民星空。</summary>
    public static class CoverService
    {
        public static byte[] Fetch(GameCandidate c, out string usedUrl)
        {
            usedUrl = "";
            if (c == null) return null;

            if (!string.IsNullOrEmpty(c.CoverUrl))
            {
                try
                {
                    string referer = string.IsNullOrEmpty(c.PageUrl) ? "https://www.baidu.com/" : BaseRoot(c.PageUrl);
                    var data = Net.GetBytes(c.CoverUrl, referer);
                    if (Html.IsImage(data) && data.Length > 1200)
                    {
                        usedUrl = c.CoverUrl;
                        return data;
                    }
                    Log.Warn("封面不是有效图片：" + c.CoverUrl + " (" + (data == null ? 0 : data.Length) + " 字节)");
                }
                catch (Exception ex) { Log.Warn("下载封面失败(" + c.CoverUrl + ")：" + ex.Message); }
            }

            if (!string.IsNullOrEmpty(c.Slug))
            {
                try
                {
                    string url;
                    var data = Gamersky.FetchCover(c.Slug, out url);
                    if (data != null)
                    {
                        usedUrl = url;
                        return data;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(c.PageUrl))
            {
                try
                {
                    var page = WebSource.Fetch(c.PageUrl, "https://www.baidu.com/");
                    if (page != null && !string.IsNullOrEmpty(page.ImageUrl))
                    {
                        var data = Net.GetBytes(page.ImageUrl, BaseRoot(c.PageUrl));
                        if (Html.IsImage(data) && data.Length > 1200)
                        {
                            usedUrl = page.ImageUrl;
                            return data;
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        static string BaseRoot(string url)
        {
            try
            {
                var u = new Uri(url);
                return u.Scheme + "://" + u.Authority + "/";
            }
            catch { return "https://www.baidu.com/"; }
        }
    }
}

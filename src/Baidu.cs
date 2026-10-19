using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GameLog
{
    public class WebResult
    {
        public string Title = "";
        public string Abstract = "";
        public string Url = "";
    }

    /// <summary>百度搜索结果解析（按结果块切分，兼容新旧模板）。</summary>
    public static class Baidu
    {
        public static List<WebResult> Search(string query)
        {
            var list = new List<WebResult>();
            string html = null;
            string url = "https://www.baidu.com/s?ie=utf-8&wd=" + Uri.EscapeDataString(query);
            try
            {
                html = Net.GetString(url, "https://www.baidu.com/");
            }
            catch (Exception ex)
            {
                Log.Warn("百度请求失败：" + ex.Message);
                return list;
            }

            if (string.IsNullOrEmpty(html)) return list;
            if (html.Contains("百度安全验证") || html.Contains("wappass.baidu.com"))
            {
                Log.Warn("百度返回验证页，跳过搜索结果");
                return list;
            }

            var blocks = Regex.Split(html, "class=\"[^\"]*c-container");            for (int i = 1; i < blocks.Length; i++)
            {
                string block = blocks[i];
                if (block.Length > 6000) block = block.Substring(0, 6000);
                var item = new WebResult();
                var h3 = Regex.Match(block, "(?s)<h3[^>]*>(.*?)</h3>");
                if (h3.Success)
                {
                    item.Title = Html.Text(h3.Groups[1].Value);
                    var link = Regex.Match(h3.Groups[1].Value, "(?is)<a[^>]+href=\"([^\"]+)\"");
                    if (link.Success) item.Url = link.Groups[1].Value;
                }
                if (item.Title.Length == 0)
                {
                    var alt = Regex.Match(block, "(?s)<a[^>]+href=\"(https?://www\\.baidu\\.com/link[^\"]+)\"[^>]*>(.*?)</a>");
                    if (alt.Success)
                    {
                        item.Title = Html.Text(alt.Groups[2].Value);
                        item.Url = alt.Groups[1].Value;
                    }
                }
                var abs = Regex.Match(block, "(?s)<(?:div|span)[^>]*class=\"[^\"]*(?:c-abstract|content-right|content_)[^\"]*\"[^>]*>(.*?)</(?:div|span)>");
                if (abs.Success) item.Abstract = Html.Text(abs.Groups[1].Value);

                if (item.Title.Length > 1) list.Add(item);
                if (list.Count >= 20) break;
            }
            Log.Info("百度搜索「" + query + "」→ " + list.Count + " 条结果（页面 " + html.Length + " 字节）");
            if (list.Count == 0)
            {
                Log.Info("百度页面片段：" + html.Substring(0, Math.Min(300, html.Length)).Replace("\r", " ").Replace("\n", " "));
            }
            return list;
        }
    }
}

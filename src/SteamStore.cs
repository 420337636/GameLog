using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace GameLog
{
    /// <summary>Steam 商店搜索：DeepSeek 给出名字后，用它补封面（比百度结果稳）。</summary>
    public static class SteamStore
    {
        static readonly JavaScriptSerializer Ser = new JavaScriptSerializer();

        public static GameCandidate Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string url = "https://store.steampowered.com/api/storesearch/?term="
                + Uri.EscapeDataString(name) + "&l=schinese&cc=cn";
            string json;
            try { json = Net.GetString(url, "https://store.steampowered.com/"); }
            catch (Exception ex)
            {
                Log.Warn("Steam 搜索失败：" + ex.Message);
                return null;
            }
            if (string.IsNullOrEmpty(json)) return null;

            string want = Html.Normalize(name);
            var items = Regex.Matches(json,
                "\"name\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"(?:(?!\"name\").){0,300}?\"id\"\\s*:\\s*(\\d+)",
                RegexOptions.Singleline);

            GameCandidate best = null;
            int bestScore = 0;
            foreach (Match m in items)
            {
                string title;
                try { title = Ser.Deserialize<string>("\"" + m.Groups[1].Value + "\""); }
                catch { title = Regex.Unescape(m.Groups[1].Value); }
                string id = m.Groups[2].Value;
                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(id)) continue;

                string have = Html.Normalize(title);
                int score = 0;
                if (have == want) score = 100;
                else if (have.IndexOf(want, StringComparison.Ordinal) >= 0 || want.IndexOf(have, StringComparison.Ordinal) >= 0) score = 80;
                if (score <= bestScore) continue;

                var c = new GameCandidate();
                c.Name = title;
                c.Origin = "Steam";
                c.PageUrl = "https://store.steampowered.com/app/" + id + "/";
                c.CoverUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/" + id + "/header.jpg";
                c.Score = score;
                best = c;
                bestScore = score;
            }
            if (best != null) Log.Info("Steam 匹配：" + best.Name + " (" + best.PageUrl + ") score=" + bestScore);
            return best;
        }
    }
}

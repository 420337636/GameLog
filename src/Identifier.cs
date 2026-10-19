using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace GameLog
{
    /// <summary>
    /// 游戏识别：根据 exe 名称 / 文件夹名称 / 路径 → 百度搜索 → 游民星空匹配中文名与封面。
    /// </summary>
    public static class Identifier
    {
        static readonly HashSet<string> Noise = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "game","games","gameclient","launcher","launcher.exe","start","startup","startgame","play","player",
            "client","app","application","bin","binaries","build","builds","engine","content","data","assets",
            "win64","win32","x64","x86","amd64","shipping","shipping.exe","release","debug","dist","main",
            "setup","install","installer","update","updater","unins000","uninstall","config","settings","editor",
            "crash","crashreport","report","helper","service","services","temp","tmp","cache","logs","save","saves",
            "program files","program files (x86)","programdata","windows","users","steam","steamapps","common",
            "games","gog games","epic games","ubisoft","origin","ea games","xboxgames","microsoft games","applications",
            "游戏","启动","启动器","开始游戏","客户端","程序","快捷方式","桌面","下载","steam游戏"
        };

        static readonly string[] DerailWords = { "手游", "移动版", "手机版", "像素", "模拟器", "网页版", "小游戏", "试玩" };

        static readonly string[] IdeaStopWords = { "我的", "游戏", "什么", "这个", "那个", "最新", "大全", "中文", "免费", "电脑", "手机", "下载", "单机" };

        static readonly string[] BadIdeaWords =
        {
            "下载","攻略","官网","破解","补丁","免费","中文版","专题","游民","3DM","3dm","游侠","百度","贴吧",
            "视频","评测","新闻","图片","壁纸","论坛","问答","百科","合集","排行榜","推荐","怎么","如何","什么",
            "教程","修改器","存档","汉化","联机","开服","礼包","盒子","加速器","steam","Steam","大全","最新",
            "通关","流程","更新","预告","销量","在线","评测","采访","开发者","备选方案"
        };

        public static List<string> Keywords(string exePath)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string file = "";
            string dir = "";
            try
            {
                file = Path.GetFileNameWithoutExtension(exePath);
                dir = Path.GetDirectoryName(exePath) ?? "";
            }
            catch { }

            AddKeyword(result, seen, file);

            string[] parts = dir.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                AddKeyword(result, seen, parts[i]);
                if (result.Count >= 6) break;
            }
            return result;
        }

        static void AddKeyword(List<string> list, HashSet<string> seen, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            string s = raw.Trim();
            if (s.Length < 3) return;
            if (IsNoise(s)) return;
            if (Regex.IsMatch(s, @"^[\s\d\W_]+$")) return;

            var variants = new List<string>();
            variants.Add(s);
            string cleaned = Clean(s);
            if (cleaned.Length >= 3 && cleaned != s) variants.Add(cleaned);
            // 仅对“单词式”名称做驼峰/数字切分（Cyberpunk2077 → Cyberpunk 2077），避免把 b1-Win64-Shipping 拆成碎词
            if (s.IndexOf(' ') < 0 && s.IndexOf('-') < 0 && s.IndexOf('_') < 0 && s.IndexOf('.') < 0)
            {
                string spaced = SplitCamelAndDigits(cleaned);
                if (spaced.Length >= 3 && spaced != cleaned) variants.Add(spaced);
            }

            foreach (var v in variants)
            {
                if (v.Length < 3 || IsNoise(v)) continue;
                if (AllNoiseWords(v)) continue;
                if (!Html.HasCjk(v) && IsWeakAscii(v)) continue;
                string key = v.ToLowerInvariant();
                if (seen.Contains(key)) continue;
                seen.Add(key);
                list.Add(v);
            }
        }

        static bool IsWeakAscii(string s)
        {
            string[] toks = s.Split(new char[] { ' ', '-', '_', '.', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (toks.Length == 0) return true;
            bool hasNoise = false;
            int meaningful = 0;
            foreach (var t in toks)
            {
                if (IsNoise(t)) { hasNoise = true; continue; }
                if (Regex.IsMatch(t, @"^\d+$")) continue;
                meaningful++;
                if (t.Length > 3) return false;
            }
            if (meaningful == 0) return true;
            return hasNoise && meaningful <= 1;
        }

        static bool IsNoise(string s)
        {
            return Noise.Contains(s.Trim());
        }

        static bool AllNoiseWords(string s)
        {
            string[] toks = s.Split(new char[] { ' ', '-', '_', '.', '·' }, StringSplitOptions.RemoveEmptyEntries);
            if (toks.Length == 0) return true;
            foreach (var t in toks)
            {
                if (!Noise.Contains(t)) return false;
            }
            return true;
        }

        static string Clean(string s)
        {
            string t = s;
            t = Regex.Replace(t, @"\[[^\]]{0,32}\]", " ");
            t = Regex.Replace(t, @"\([^\)]{0,32}\)", " ");
            t = Regex.Replace(t, @"（[^）]{0,32}）", " ");
            t = Regex.Replace(t, @"【[^】]{0,32}】", " ");
            t = Regex.Replace(t, @"(?i)\b(fitgirl|codex|plaza|skidrow|reloaded|empress|tenoke|goldberg|razor1911|cpy|repack|cracked|portable|setup)\b", " ");
            t = Regex.Replace(t, @"(?i)\bv?\d+(?:\.\d+){1,3}\b", " ");
            t = t.Replace('_', ' ').Replace('.', ' ').Replace('-', ' ');
            t = Regex.Replace(t, @"\s{2,}", " ").Trim();
            return t;
        }

        /// <summary>在字母与数字交界处插入空格：Cyberpunk2077 → Cyberpunk 2077</summary>
        static string SplitCamelAndDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = Regex.Replace(s, "(?<=[a-zA-Z])(?=[0-9])", " ");
            t = Regex.Replace(t, "(?<=[0-9])(?=[a-zA-Z])", " ");
            return Regex.Replace(t, @"\s{2,}", " ").Trim();
        }

        /// <summary>去掉中文两侧多余空格：赛博朋克 2077 → 赛博朋克2077</summary>
        public static string TightenCjk(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = Regex.Replace(s, @"(?<=[\u4e00-\u9fff])\s+(?=[\u4e00-\u9fff0-9A-Za-z])", "");
            t = Regex.Replace(t, @"(?<=[0-9A-Za-z])\s+(?=[\u4e00-\u9fff])", "");
            return t.Trim();
        }

        // ---------------- 识别主流程 ----------------

        public static IdentifyResult Identify(string exePath, Action<string> progress)
        {
            var res = new IdentifyResult();
            var kws = Keywords(exePath);
            if (kws.Count == 0)
            {
                string fallback = "";
                try { fallback = Path.GetFileNameWithoutExtension(exePath) ?? ""; } catch { }
                kws.Add(fallback);
            }
            res.Query = kws[0];
            Log.Info("识别开始：" + exePath + " | 关键词：" + string.Join(" / ", kws.ToArray()));

            var pool = new Dictionary<string, GameCandidate>(StringComparer.OrdinalIgnoreCase);
            var cache = new Dictionary<string, List<GameCandidate>>(StringComparer.OrdinalIgnoreCase);
            bool anyRequest = false;
            DateTime deadline = DateTime.Now.AddSeconds(16);

            for (int i = 0; i < kws.Count && i < 3; i++)
            {
                if (progress != null) progress("在游民星空搜索「" + kws[i] + "」…");
                var hits = SearchCached(cache, kws[i], ref anyRequest);
                Merge(pool, hits, kws[i], "游民星空搜索", 0);
                if (BestScore(pool) >= 95) break;
                if (Net.ConsecutiveFailures >= 2) break;
                if (DateTime.Now > deadline) break;
            }

            if (BestScore(pool) < 85 && Net.ConsecutiveFailures < 2)
            {
                for (int i = 0; i < kws.Count && i < 2; i++)
                {
                    if (progress != null) progress("在百度搜索「" + kws[i] + "」是什么游戏…");
                    var web = new List<WebResult>();
                    try
                    {
                        web = Baidu.Search(kws[i] + " 游戏");
                        anyRequest = true;
                        if (web.Count == 0) web = Baidu.Search(kws[i] + " steam");
                        if (web.Count == 0) web = Baidu.Search(kws[i] + " 攻略");
                    }
                    catch (Exception ex) { Log.Warn("百度搜索异常：" + ex.Message); }

                    var ideas = IdeasFromWeb(web, kws[i]);
                    int used = 0;
                    foreach (var idea in ideas)
                    {
                        if (used >= 4) break;
                        used++;
                        if (progress != null) progress("在游民星空核对「" + idea.Text + "」…");
                        var hits = SearchCached(cache, idea.Text, ref anyRequest);
                        Merge(pool, hits, idea.Text, "百度 + 游民星空", idea.Count);
                        if (BestScore(pool) >= 75) break;
                        if (Net.ConsecutiveFailures >= 2) break;
                    }
                    if (BestScore(pool) >= 75) break;
                    if (Net.ConsecutiveFailures >= 2) break;
                    if (DateTime.Now > deadline) break;

                    Corroborate(pool, web);

                    if (BestScore(pool) < 75 && Net.ConsecutiveFailures < 2 && DateTime.Now < deadline)
                    {
                        EnrichFromWeb(pool, web, ideas, kws[i], progress);
                    }
                }
            }

            // 最后兜底：中文关键词直接查百度百科
            if (BestScore(pool) < 60 && Net.ConsecutiveFailures < 2 && Html.HasCjk(kws[0]))
            {
                if (progress != null) progress("在百度百科查找「" + kws[0] + "」…");
                var c = WebSource.LookupBaike(kws[0]);
                if (c != null) AddExternal(pool, c, Score(kws[0], c, 0) + 8);
            }

            var list = new List<GameCandidate>(pool.Values);
            list.Sort(delegate (GameCandidate a, GameCandidate b) { return b.Score.CompareTo(a.Score); });

            var unique = new List<GameCandidate>();
            var byName = new Dictionary<string, GameCandidate>();
            foreach (var c in list)
            {
                string key = Html.Normalize(c.Name);
                if (string.IsNullOrEmpty(key)) continue;
                GameCandidate existing;
                if (byName.TryGetValue(key, out existing))
                {
                    // 同名候选：优先保留游民星空的数据（封面更规整），分数取高者
                    if (string.IsNullOrEmpty(existing.Slug) && !string.IsNullOrEmpty(c.Slug))
                    {
                        existing.Slug = c.Slug;
                        existing.PageUrl = c.PageUrl;
                        existing.CoverUrl = c.CoverUrl;
                        existing.Origin = c.Origin;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(existing.CoverUrl) && !string.IsNullOrEmpty(c.CoverUrl)) existing.CoverUrl = c.CoverUrl;
                        if (string.IsNullOrEmpty(existing.PageUrl) && !string.IsNullOrEmpty(c.PageUrl)) existing.PageUrl = c.PageUrl;
                    }
                    if (c.Score > existing.Score) existing.Score = c.Score;
                    continue;
                }
                byName[key] = c;
                unique.Add(c);
            }
            res.Candidates = unique;
            list = unique;

            if (list.Count == 0)
            {
                res.Success = false;
                if (Net.ConsecutiveFailures >= 2) res.Message = "网络似乎不可用，请检查网络后重试，或先直接添加稍后再识别";
                else if (anyRequest) res.Message = "没有找到匹配的游戏，请手动输入名称搜索";
                else res.Message = "网络请求失败，请检查网络后重试";
                Log.Info("识别失败：" + res.Message);
                return res;
            }

            var best = list[0];
            // 中文名优先：最佳候选是纯英文、但列表里有可信中文名时，改用中文名
            if (!Html.HasCjk(best.Name))
            {
                foreach (var c in list)
                {
                    if (Html.HasCjk(c.Name) && c.Score >= best.Score * 0.55 && c.Score >= 45)
                    {
                        best = c;
                        break;
                    }
                }
            }
            // 把最终选中的候选放到第一位，对话框默认选中它
            list.Remove(best);
            list.Insert(0, best);
            res.Candidates = list;
            res.Score = best.Score;
            res.Name = TightenCjk(best.Name);
            res.Slug = best.Slug;
            res.PageUrl = best.PageUrl;
            res.CoverUrl = best.CoverUrl;
            res.Success = best.Score >= 60 || (best.Rank == 0 && best.Score >= 50);

            if (progress != null) progress("获取《" + res.Name + "》的封面…");
            string usedUrl = null;
            byte[] cover = null;
            try { cover = CoverService.Fetch(best, out usedUrl); }
            catch (Exception ex) { Log.Warn("下载封面异常：" + ex.Message); }

            if (cover != null)
            {
                res.CoverData = cover;
                res.CoverUrl = usedUrl;
            }

            if (res.Success)
            {
                string source = string.IsNullOrEmpty(best.Origin) ? "" : "（来源：" + best.Origin + "）";
                res.Message = "已匹配到《" + res.Name + "》" + source + "，封面" + (cover != null ? "已获取" : "未获取到");
            }
            else
            {
                res.Message = "没有把握确定是哪款游戏，请手动选择或输入名称";
            }
            Log.Info("识别结束：" + res.Message + " score=" + res.Score.ToString("0.0") + " slug=" + best.Slug);
            return res;
        }

        /// <summary>游民星空没有结果时，从百度百科和百度搜索结果页里找游戏名与封面。</summary>
        static void EnrichFromWeb(Dictionary<string, GameCandidate> pool, List<WebResult> web, List<Idea> ideas, string keyword, Action<string> progress)
        {
            DateTime start = DateTime.Now;
            // 1) 百度百科：先查百度给出的候选名，再查已经匹配到的中文名
            var queries = new List<string>();
            var evidence = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ideas.Count && i < 3; i++)
            {
                AddQuery(queries, evidence, ideas[i].Text, ideas[i].Count);
                string cjk = OnlyCjk(ideas[i].Text);
                if (cjk.Length >= 2) AddQuery(queries, evidence, cjk, ideas[i].Count);
            }
            foreach (var c in pool.Values)
            {
                if (Html.HasCjk(c.Name) && c.Score < 70) AddQuery(queries, evidence, c.Name, 1);
            }
            if (Html.HasCjk(keyword)) AddQuery(queries, evidence, keyword, 1);

            int tried = 0;
            foreach (var q in queries)
            {
                if (tried >= 4) break;
                if (Net.ConsecutiveFailures >= 2) return;
                tried++;
                if (progress != null) progress("在百度百科查找「" + q + "」…");
                var c = WebSource.LookupBaike(q);
                if (c == null) continue;
                int ev;
                if (!evidence.TryGetValue(q, out ev)) ev = 1;
                double s = Score(q, c, 0) + Math.Min(ev, 3) * 4 + 8;
                AddExternal(pool, c, s);
                Log.Info("百度百科命中：" + c.Name + " (查询：" + q + ") score=" + s.ToString("0.0"));
                if (BestScore(pool) >= 85) return;
            }
            if (DateTime.Now > start.AddSeconds(9)) return;

            // 2) 百度搜索出来的网页（优先 Steam / 3DM / 游侠 / 官网）
            var orderedWeb = new List<WebResult>();
            foreach (var r in web) if (PreferredResult(r)) orderedWeb.Add(r);
            foreach (var r in web) if (!PreferredResult(r)) orderedWeb.Add(r);

            int fetched = 0;
            foreach (var r in orderedWeb)
            {
                if (fetched >= 3) break;
                if (DateTime.Now > start.AddSeconds(9)) return;
                if (Net.ConsecutiveFailures >= 2) return;
                if (string.IsNullOrEmpty(r.Url)) continue;
                // 百度结果链接都是 baidu.com/link 跳转，域名要在抓取后才能确定；
                // 这里只跳过明确是百科的结果（百科由上面单独处理）。
                if ((r.Title ?? "").IndexOf("百度百科", StringComparison.Ordinal) >= 0) continue;

                string guess = WebSource.NameFromTitle(r.Title);
                bool plausible = WebSource.LooksLikeGameName(guess);
                bool mentions = Mentions(r.Title, keyword) || Mentions(r.Abstract, keyword);
                if (!plausible && !mentions) continue;

                if (progress != null) progress("读取网页：" + (string.IsNullOrEmpty(guess) ? "搜索结果" : guess) + "…");
                var page = WebSource.Fetch(r.Url, "https://www.baidu.com/");
                fetched++;
                if (page == null || string.IsNullOrEmpty(page.Title)) continue;
                Log.Info("网页详情：" + page.FinalUrl + " | 标题：" + page.Title + " | 图：" + (page.ImageUrl ?? ""));
                if ((page.FinalUrl ?? "").IndexOf("baidu.com", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                bool gamePage = WebSource.LooksLikeGamePage(page);
                bool titleHasKw = Mentions(page.Title, keyword);
                if (!gamePage && !titleHasKw) continue;

                string name = WebSource.NameFromTitle(page.Title);
                if (!WebSource.LooksLikeGameName(name))
                {
                    // 只有页面正文里明确出现《关键词》时，才把关键词当作游戏名，避免把普通文件名硬凑成游戏
                    if (ContainsBracket(page.Raw, keyword)) name = keyword;
                    else if (plausible && !string.Equals(Html.Normalize(guess), Html.Normalize(keyword), StringComparison.OrdinalIgnoreCase)) name = guess;
                    else continue;
                }

                // 网页来源的额外校验：太短的中文名、或只是关键词的碎片，都不算识别成功
                if (Html.HasCjk(name) && name.Length < 3) continue;
                if (keyword.Length > name.Length && keyword.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) continue;

                var c = new GameCandidate();
                c.Name = name;
                c.PageUrl = page.FinalUrl;
                c.CoverUrl = page.ImageUrl;
                c.Description = page.Description;
                c.Domain = WebSource.DomainOf(page.FinalUrl);
                c.Origin = string.IsNullOrEmpty(c.Domain) ? "百度网页" : c.Domain;

                double s = Score(keyword, c, 0) + 6;
                if (string.IsNullOrEmpty(c.CoverUrl)) s -= 25;
                if (string.Equals(Html.Normalize(c.Name), Html.Normalize(keyword), StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(c.CoverUrl)) continue;
                AddExternal(pool, c, s);
                Log.Info("网页命中：" + name + " (" + c.Origin + ") score=" + s.ToString("0.0"));
                if (BestScore(pool) >= 80) return;
            }
        }

        static bool Mentions(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword)) return false;
            string nk = Html.Normalize(keyword);
            if (nk.Length < 3) return false;
            return Html.Normalize(text).IndexOf(nk, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool ContainsBracket(string html, string keyword)
        {
            if (string.IsNullOrEmpty(html) || string.IsNullOrEmpty(keyword)) return false;
            return html.IndexOf("《" + keyword + "》", StringComparison.OrdinalIgnoreCase) >= 0
                || html.IndexOf("《" + keyword + " 》", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>候选名如果原样出现在百度结果里，说明有旁证，加分。</summary>
        static void Corroborate(Dictionary<string, GameCandidate> pool, List<WebResult> web)
        {
            if (pool.Count == 0 || web == null || web.Count == 0) return;
            var texts = new List<string>();
            foreach (var r in web)
            {
                if (!string.IsNullOrEmpty(r.Title)) texts.Add(Html.Normalize(r.Title));
                if (!string.IsNullOrEmpty(r.Abstract)) texts.Add(Html.Normalize(r.Abstract));
            }
            if (texts.Count == 0) return;

            foreach (var c in pool.Values)
            {
                string n = Html.Normalize(c.Name);
                if (n.Length < 2) continue;
                int hits = 0;
                foreach (var t in texts)
                {
                    if (t.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hits++;
                        if (hits >= 3) break;
                    }
                }
                if (hits > 0) c.Score += hits * 8;
            }
        }

        static bool PreferredResult(WebResult r)
        {
            if (r == null) return false;
            string t = r.Title ?? "";
            return t.IndexOf("Steam", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("3DM", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("游侠", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("官网", StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("百度百科", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void AddQuery(List<string> queries, Dictionary<string, int> evidence, string text, int count)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = text.Trim();
            if (text.Length < 2 || text.Length > 24) return;
            if (queries.Count >= 8) return;
            foreach (var q in queries) { if (string.Equals(q, text, StringComparison.OrdinalIgnoreCase)) return; }
            queries.Add(text);
            evidence[text] = count;
        }

        /// <summary>取出字符串里最长的一段中文（“Deltarune三角符文” → 三角符文）。</summary>
        static string OnlyCjk(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string best = "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF)
                {
                    sb.Append(c);
                }
                else
                {
                    if (sb.Length > best.Length) best = sb.ToString();
                    sb.Length = 0;
                }
            }
            if (sb.Length > best.Length) best = sb.ToString();
            return best;
        }

        static void AddExternal(Dictionary<string, GameCandidate> pool, GameCandidate c, double score)
        {
            if (c == null || string.IsNullOrEmpty(c.Name)) return;
            c.Name = TightenCjk(c.Name);
            c.Score = score;
            string key = !string.IsNullOrEmpty(c.Slug)
                ? "slug:" + c.Slug
                : "name:" + Html.Normalize(c.Name);

            GameCandidate existing;
            if (pool.TryGetValue(key, out existing))
            {
                if (score > existing.Score)
                {
                    existing.Score = score;
                    existing.Origin = c.Origin;
                    existing.Domain = c.Domain;
                }
                if (string.IsNullOrEmpty(existing.CoverUrl) && !string.IsNullOrEmpty(c.CoverUrl)) existing.CoverUrl = c.CoverUrl;
                if (string.IsNullOrEmpty(existing.PageUrl) && !string.IsNullOrEmpty(c.PageUrl)) existing.PageUrl = c.PageUrl;
                if (string.IsNullOrEmpty(existing.Description) && !string.IsNullOrEmpty(c.Description)) existing.Description = c.Description;
                return;
            }
            pool[key] = c;
        }

        static List<GameCandidate> SearchCached(Dictionary<string, List<GameCandidate>> cache, string query, ref bool anyRequest)
        {
            List<GameCandidate> hits;
            if (cache.TryGetValue(query, out hits)) return hits;
            try
            {
                hits = Gamersky.Search(query);
                anyRequest = true;
            }
            catch (Exception ex)
            {
                Log.Warn("游民星空搜索异常(" + query + ")：" + ex.Message);
                hits = new List<GameCandidate>();
            }
            cache[query] = hits;
            return hits;
        }

        static void Merge(Dictionary<string, GameCandidate> pool, List<GameCandidate> hits, string basis, string origin, int evidence)
        {
            for (int rank = 0; rank < hits.Count && rank < 8; rank++)
            {
                var hit = hits[rank];
                if (string.IsNullOrEmpty(hit.Slug)) continue;
                double score = Score(basis, hit, rank);
                if (evidence > 0) score += Math.Min(evidence, 3) * 4;

                GameCandidate existing;
                if (pool.TryGetValue(hit.Slug, out existing))
                {
                    if (score > existing.Score)
                    {
                        existing.Score = score;
                        existing.Origin = origin;
                        existing.Rank = rank;
                    }
                }
                else
                {
                    var c = new GameCandidate();
                    c.Name = TightenCjk(hit.Name);
                    c.Slug = hit.Slug;
                    c.PageUrl = hit.PageUrl;
                    c.CoverUrl = hit.CoverUrl;
                    c.Score = score;
                    c.Origin = origin;
                    c.Rank = rank;
                    pool[hit.Slug] = c;
                }
            }
        }

        static double BestScore(Dictionary<string, GameCandidate> pool)
        {
            double best = 0;
            foreach (var c in pool.Values) if (c.Score > best) best = c.Score;
            return best;
        }

        static double Score(string keyword, GameCandidate hit, int rank)
        {
            string nk = Html.Normalize(keyword);
            string nh = Html.Normalize(hit.Name);
            string ns = Html.Normalize(hit.Slug);
            if (nk.Length == 0) return 0;

            double s = 0;
            if (nh.Length > 0)
            {
                if (nh == nk) s += 80;
                else if (nh.Contains(nk) || nk.Contains(nh)) s += 45;
                else if (IsSubsequence(nk, nh)) s += 30;
                else s += 40 * Html.Similarity(nk, nh);
            }
            if (ns.Length > 0)
            {
                if (ns == nk) s += 65;
                else if (ns.Contains(nk) || nk.Contains(ns)) s += 22;
                else if (IsSubsequence(nk, ns)) s += 12;
            }

            if (rank == 0) s += 20;
            else if (rank == 1) s += 12;
            else if (rank == 2) s += 6;

            if (Html.HasAsciiLetter(keyword) && Html.HasCjk(hit.Name)) s += 6;

            foreach (var w in DerailWords)
            {
                if (hit.Name.Contains(w) && !keyword.Contains(w)) { s -= 34; break; }
            }
            return s;
        }

        public class Idea
        {
            public string Text = "";
            public int Count;
        }

        static bool IsSubsequence(string needle, string haystack)
        {
            if (string.IsNullOrEmpty(needle) || string.IsNullOrEmpty(haystack)) return false;
            if (needle.Length < 3) return false;
            int j = 0;
            for (int i = 0; i < haystack.Length && j < needle.Length; i++)
            {
                if (haystack[i] == needle[j]) j++;
            }
            return j == needle.Length;
        }

        static List<Idea> IdeasFromWeb(List<WebResult> web, string keyword)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool asciiKeyword = !Html.HasCjk(keyword);
            foreach (var r in web)
            {
                if (IsTrustedText(r.Title, keyword)) AddIdeas(counts, r.Title, keyword);
                if (IsTrustedText(r.Abstract, keyword)) AddIdeas(counts, r.Abstract, keyword);
            }

            var ordered = new List<KeyValuePair<string, int>>(counts);
            ordered.Sort(delegate (KeyValuePair<string, int> a, KeyValuePair<string, int> b) { return b.Value.CompareTo(a.Value); });

            var list = new List<Idea>();
            foreach (var kv in ordered)
            {
                if (list.Count >= 8) break;
                // 英文关键词无法和中文候选做字面比对，要求同一候选至少在 2 条结果里出现，过滤广告/无关条目
                if (asciiKeyword && web.Count >= 4 && kv.Value < 2) continue;
                var idea = new Idea();
                idea.Text = kv.Key;
                idea.Count = kv.Value;
                list.Add(idea);
            }
            return list;
        }

        /// <summary>中文关键词要求来源文字里确实包含关键词，避免百度广告带出无关游戏。</summary>
        static bool IsTrustedText(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (!Html.HasCjk(keyword)) return true;
            string nk = Html.Normalize(keyword);
            if (nk.Length == 0) return false;
            return Html.Normalize(text).IndexOf(nk, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void AddIdeas(Dictionary<string, int> counts, string text, string keyword)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (Match m in Regex.Matches(text, "《([^》]{2,40})》"))
                Bump(counts, CleanIdea(m.Groups[1].Value), keyword, true);

            string t = text;
            string[] seps = { "游戏专区", "专区", "_", "|", " - ", "—", "｜", "：", ":", "（", "(", "【", "[", "攻略", "下载", "官网", "中文版", "破解版", "补丁" };
            foreach (var sep in seps)
            {
                int i = t.IndexOf(sep, StringComparison.Ordinal);
                if (i > 0)
                {
                    string cut = t.Substring(0, i);
                    Bump(counts, CleanIdea(cut), keyword, false);
                }
            }
            Bump(counts, CleanIdea(t), keyword, false);
        }

        static string CleanIdea(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string t = TightenCjk(raw).Trim();
            t = t.Trim('《', '》', '【', '】', '「', '」', '"', '\'', ' ', '-', '_', '|', '：', ':', ',', '.', '、', '！', '!');
            string[] suffixes = { "官网", "官方网站", "中文版", "官方中文版", "免安装版", "免安装中文版", "破解版", "绿色版", "完整版", "豪华版", "标准版", "年度版", "特别版", "重制版", "复刻版", "游戏", "单机游戏", "电脑版", "PC版", "下载", "专区", "攻略", "秘籍", "视频", "解说" };
            foreach (var suf in suffixes)
            {
                if (t.EndsWith(suf, StringComparison.Ordinal) && t.Length > suf.Length + 1)
                {
                    t = t.Substring(0, t.Length - suf.Length).Trim();
                }
            }
            return t;
        }

        static void Bump(Dictionary<string, int> counts, string idea, string keyword)
        {
            Bump(counts, idea, keyword, false);
        }

        static void Bump(Dictionary<string, int> counts, string idea, string keyword, bool bracketed)
        {
            if (string.IsNullOrEmpty(idea)) return;
            idea = idea.Trim();
            if (idea.Length < 2 || idea.Length > 40) return;

            string nk = Html.Normalize(keyword);
            string ni = Html.Normalize(idea);

            if (!Html.HasCjk(idea))
            {
                // 英文游戏名：只接受与关键词相关、或出现在《》里的名称（如《Schedule I》）
                if (!Html.HasAsciiLetter(idea)) return;
                if (Html.HasCjk(keyword)) return;
                if (ni.Length < 2) return;
                if (string.Equals(ni, nk, StringComparison.OrdinalIgnoreCase)) return;
                bool related = ni.IndexOf(nk, StringComparison.OrdinalIgnoreCase) >= 0
                    || nk.IndexOf(ni, StringComparison.OrdinalIgnoreCase) >= 0
                    || bracketed;
                if (!related) return;
                if (idea.IndexOf(' ') < 0 && idea.Length > 30) return;
            }
            else
            {
                if (string.Equals(ni, nk, StringComparison.OrdinalIgnoreCase)) return;
                // 关键词被截断的碎片（如“我的游戏”→“我的”）不是游戏名
                if (keyword.Length > idea.Length && keyword.IndexOf(idea, StringComparison.OrdinalIgnoreCase) >= 0) return;
                if (Regex.IsMatch(idea, "[《》\"'<>|/\\\\]")) return;
            }

            foreach (var bad in IdeaStopWords)
            {
                if (string.Equals(idea, bad, StringComparison.OrdinalIgnoreCase)) return;
            }
            foreach (var bad in BadIdeaWords)
            {
                if (idea.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0) return;
            }
            foreach (var bad in DerailWords)
            {
                if (idea.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0) return;
            }
            int n;
            if (counts.TryGetValue(idea, out n)) counts[idea] = n + 1;
            else counts[idea] = 1;
        }

        /// <summary>没有网络或识别失败时，给一个可读的默认名称。</summary>
        public static string FallbackName(string exePath)
        {
            var kws = Keywords(exePath);
            if (kws.Count > 0) return kws[0];
            try { return Path.GetFileNameWithoutExtension(exePath); }
            catch { return "未命名游戏"; }
        }
    }
}

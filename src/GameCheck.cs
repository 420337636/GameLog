using System;
using System.Collections.Generic;

namespace GameLog
{
    /// <summary>
    /// “它到底是不是游戏？”——拿名字去百度搜「X 是游戏吗」，看返回的标题/摘要更像游戏还是更像软件工具。
    /// 用来拦住图吧工具箱、AIDA64 这类被误认成游戏的工具软件。
    /// </summary>
    public static class GameCheck
    {
        static readonly string[] SoftwareWords =
        {
            "工具箱", "软件", "教程", "设置方法", "设置", "驱动", "检测", "优化", "清理", "杀毒",
            "输入法", "浏览器", "办公", "破解", "绿色版", "官网", "助手", "管家", "大师", "硬件",
            "系统工具", "电脑工具", "aida64", "cpu-z", "gpu-z", "hwinfo", "图吧", "鲁大师",
            "winrar", "7-zip", "压缩", "录屏", "截图", "翻译", "卸载", "装机", "维修", "跑分",
            "帧数", "中文版", "官方版", "电脑版", "免费版", "便携版", "使用教程", "压力测试", "温度监控"
        };

        static readonly string[] GameWords =
        {
            "游戏", "攻略", "玩法", "剧情", "发售", "steam", "主机", "索尼", "任天堂", "手游",
            "端游", "单机", "评测", "联机", "关卡", "赛季", "预购", "dlc", "角色扮演", "开放世界",
            "射击", "冒险", "竞技", "电竞", "动作", "rpg", "boss", "通关"
        };

        /// <summary>名字本身就能确定是软件/工具（AIDA64、HWiNFO、CPU-Z、图吧工具箱…）。</summary>
        static readonly string[] KnownSoftware =
        {
            "aida64", "cpuz", "gpuz", "hwinfo", "图吧工具箱", "鲁大师", "crystaldiskinfo",
            "crystaldiskmark", "furmark", "processhacker", "dism++", "驱动精灵", "驱动人生",
            "360安全卫士", "火绒", "腾讯电脑管家"
        };

        static bool IsKnownSoftwareName(string name)
        {
            string n = Normalize(name);
            foreach (var s in KnownSoftware)
            {
                if (n.IndexOf(s, StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace(" ", "").Replace("-", "").Replace("_", "").Replace("－", "").ToLowerInvariant();
        }

        /// <summary>true=像游戏，false=像软件工具，null=判断不了。</summary>
        public static bool? IsGame(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (IsKnownSoftwareName(name))
            {
                Log.Info("是否游戏：" + name + " => 已知软件");
                return false;
            }
            List<WebResult> results;
            try { results = Baidu.Search(name.Trim() + " 是游戏吗"); }
            catch (Exception ex)
            {
                Log.Warn("判断是否游戏失败：" + ex.Message);
                return null;
            }
            if (results == null || results.Count == 0) return null;

            int soft = 0, game = 0;
            int n = Math.Min(8, results.Count);
            for (int i = 0; i < n; i++)
            {
                string text = (results[i].Title ?? "") + " " + (results[i].Abstract ?? "") + " " + (results[i].Url ?? "");
                foreach (var w in SoftwareWords)
                {
                    if (text.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) soft++;
                }
                foreach (var w in GameWords)
                {
                    if (text.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) game++;
                }
            }

            Log.Info("是否游戏：" + name + " => 软件特征=" + soft + " 游戏特征=" + game);
            if (soft >= 2 && soft > game) return false;
            if (game >= 3 && game > soft) return true;
            return null;
        }

        /// <summary>名字本身就像文章标题 / 软件名（AIDA64如何设置游戏帧数显示-3DM软件）。</summary>
        public static bool LooksLikeArticle(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            string n = name.Trim();
            string[] bad =
            {
                "如何", "怎么", "教程", "设置", "官网", "软件", "工具", "驱动", "版本",
                "破解", "绿色版", "安装", "更新", "补丁", "推荐", "排行", "大全", "合集"
            };
            foreach (var b in bad)
            {
                if (n.IndexOf(b, StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }
    }
}

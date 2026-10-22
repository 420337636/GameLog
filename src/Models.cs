using System;
using System.Collections.Generic;

namespace GameLog
{
    /// <summary>一款被记录的游戏（对应一个 exe）。</summary>
    public class GameInfo
    {
        public string Id { get; set; }
        public string ExePath { get; set; }
        /// <summary>带扩展名的文件名，如 game.exe</summary>
        public string ExeName { get; set; }
        public string Name { get; set; }
        /// <summary>封面文件（位于 covers 目录内，仅文件名）</summary>
        public string CoverFile { get; set; }
        public string CoverUrl { get; set; }
        public string PageUrl { get; set; }
        public double TotalSeconds { get; set; }
        public long LastPlayedTicks { get; set; }
        public long AddedTicks { get; set; }
        /// <summary>按日期（yyyy-MM-dd）累计的秒数。</summary>
        public Dictionary<string, double> Daily { get; set; }
        public string Note { get; set; }

        public GameInfo()
        {
            Daily = new Dictionary<string, double>();
        }

        public DateTime LastPlayed
        {
            get { return LastPlayedTicks > 0 ? new DateTime(LastPlayedTicks) : DateTime.MinValue; }
        }

        public DateTime Added
        {
            get { return AddedTicks > 0 ? new DateTime(AddedTicks) : DateTime.Now; }
        }

        public double TodaySeconds
        {
            get
            {
                double v;
                if (Daily != null && Daily.TryGetValue(DateTime.Now.ToString("yyyy-MM-dd"), out v)) return v;
                return 0;
            }
        }

        public void AddSeconds(double seconds, DateTime localNow)
        {
            if (seconds <= 0) return;
            TotalSeconds += seconds;
            if (Daily == null) Daily = new Dictionary<string, double>();
            string key = localNow.ToString("yyyy-MM-dd");
            double cur;
            if (Daily.TryGetValue(key, out cur)) Daily[key] = cur + seconds;
            else Daily[key] = seconds;
            // 只保留最近 120 天，避免文件无限增长
            if (Daily.Count > 150)
            {
                var keys = new List<string>(Daily.Keys);
                keys.Sort(StringComparer.Ordinal);
                while (keys.Count > 120)
                {
                    Daily.Remove(keys[0]);
                    keys.RemoveAt(0);
                }
            }
        }
    }

    public class Settings
    {
        public bool AutoStart { get; set; }
        public bool CloseToTray { get; set; }
        public bool MonitorEnabled { get; set; }
        public int TickMs { get; set; }
        public string SortMode { get; set; }
        public bool FirstRunDone { get; set; }
        public bool HideMenuBanner { get; set; }
        /// <summary>自动扫描运行中的进程，识别为游戏就自动加入列表。</summary>
        public bool AutoDetect { get; set; }
        /// <summary>自动扫描间隔（分钟）。</summary>
        public int AutoDetectMinutes { get; set; }
        /// <summary>每轮最多识别几个进程（避免一次请求太多）。</summary>
        public int AutoDetectMaxPerRun { get; set; }
        /// <summary>主界面布局：cards（卡片）或 list（列表）。</summary>
        public string Layout { get; set; }
        /// <summary>记录游戏时长的检测间隔（秒）。</summary>
        public int MonitorSeconds { get; set; }

        public Settings()
        {
            CloseToTray = true;
            MonitorEnabled = true;
            TickMs = 2000;
            SortMode = "recent";
            AutoDetect = true;
            AutoDetectMinutes = 5;
            AutoDetectMaxPerRun = 3;
            Layout = "cards";
            MonitorSeconds = 300;
        }
    }

    /// <summary>自动发现的持久化记录：已判定不是游戏的进程、用户删除过的游戏。</summary>
    public class DiscoveryData
    {
        public Dictionary<string, string> NonGames { get; set; }
        public Dictionary<string, string> Removed { get; set; }

        public DiscoveryData()
        {
            NonGames = new Dictionary<string, string>();
            Removed = new Dictionary<string, string>();
        }
    }

    /// <summary>识别到的一款游民星空游戏。</summary>
    public class GameCandidate
    {
        public string Name { get; set; }
        public string Slug { get; set; }
        public string PageUrl { get; set; }
        public string CoverUrl { get; set; }
        public double Score { get; set; }
        public string Origin { get; set; }
        public int Rank { get; set; }
        public string Description { get; set; }
        public string Domain { get; set; }

        public override string ToString()
        {
            return Name + (string.IsNullOrEmpty(Origin) ? "" : "  ·  " + Origin);
        }
    }

    public class IdentifyResult
    {
        public bool Success { get; set; }
        public string Query { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string PageUrl { get; set; }
        public string CoverUrl { get; set; }
        public byte[] CoverData { get; set; }
        public double Score { get; set; }
        public string Message { get; set; }
        public List<GameCandidate> Candidates { get; set; }

        public IdentifyResult()
        {
            Candidates = new List<GameCandidate>();
        }
    }
}

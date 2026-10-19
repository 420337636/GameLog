using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace GameLog
{
    /// <summary>游戏库与设置的持久化。</summary>
    public class Store
    {
        readonly object gate = new object();
        readonly JavaScriptSerializer ser = new JavaScriptSerializer();

        public string Root { get; private set; }
        public string CoversDir { get; private set; }
        public string DataFile { get; private set; }
        public string SettingsFile { get; private set; }
        public string DiscoveryFile { get; private set; }
        public List<GameInfo> Games { get; private set; }
        public Settings Settings { get; private set; }
        public Dictionary<string, string> NonGames { get; private set; }
        public Dictionary<string, string> Removed { get; private set; }

        public event EventHandler Changed;

        public Store(string root)
        {
            Root = root;
            CoversDir = Path.Combine(root, "covers");
            DataFile = Path.Combine(root, "games.json");
            SettingsFile = Path.Combine(root, "settings.json");
            DiscoveryFile = Path.Combine(root, "discovery.json");
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(CoversDir);
            Games = new List<GameInfo>();
            Settings = new Settings();
            NonGames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Removed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Load();
        }

        public void Load()
        {
            lock (gate)
            {
                try
                {
                    if (File.Exists(DataFile))
                    {
                        string json = File.ReadAllText(DataFile, Encoding.UTF8);
                        var list = ser.Deserialize<List<GameInfo>>(json);
                        if (list != null)
                        {
                            Games = new List<GameInfo>();
                            foreach (var g in list)
                            {
                                if (g == null || string.IsNullOrEmpty(g.ExePath)) continue;
                                if (g.Daily == null) g.Daily = new Dictionary<string, double>();
                                if (string.IsNullOrEmpty(g.Id)) g.Id = Guid.NewGuid().ToString("N");
                                if (string.IsNullOrEmpty(g.Name)) g.Name = Path.GetFileNameWithoutExtension(g.ExePath);
                                Games.Add(g);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("读取 games.json 失败", ex);
                    try
                    {
                        string bad = DataFile + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                        File.Copy(DataFile, bad, true);
                    }
                    catch { }
                }

                try
                {
                    if (File.Exists(SettingsFile))
                    {
                        string json = File.ReadAllText(SettingsFile, Encoding.UTF8);
                        var s = ser.Deserialize<Settings>(json);
                        if (s != null)
                        {
                            if (s.TickMs < 1000 || s.TickMs > 30000) s.TickMs = 2000;
                            if (string.IsNullOrEmpty(s.SortMode)) s.SortMode = "recent";
                            if (string.IsNullOrEmpty(s.Layout)) s.Layout = "cards";
                            Settings = s;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("读取 settings.json 失败", ex);
                }

                try
                {
                    LoadDiscovery();
                    if (Settings.AutoDetectMinutes <= 0) Settings.AutoDetectMinutes = 5;
                    if (Settings.AutoDetectMaxPerRun <= 0) Settings.AutoDetectMaxPerRun = 3;
                }
                catch (Exception ex)
                {
                    Log.Error("读取 discovery.json 失败", ex);
                }
            }
        }

        public void Save()
        {
            lock (gate)
            {
                try
                {
                    string json = ser.Serialize(Games);
                    AtomicWrite(DataFile, json);
                }
                catch (Exception ex)
                {
                    Log.Error("保存 games.json 失败", ex);
                }

                try
                {
                    AtomicWrite(SettingsFile, ser.Serialize(Settings));
                }
                catch (Exception ex)
                {
                    Log.Error("保存 settings.json 失败", ex);
                }
            }
        }

        public void SaveSettings()
        {
            lock (gate)
            {
                try { AtomicWrite(SettingsFile, ser.Serialize(Settings)); }
                catch (Exception ex) { Log.Error("保存 settings.json 失败", ex); }
            }
        }

        static void AtomicWrite(string path, string content)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        void Raise()
        {
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        public List<GameInfo> Snapshot()
        {
            lock (gate) { return new List<GameInfo>(Games); }
        }

        public GameInfo FindById(string id)
        {
            lock (gate)
            {
                foreach (var g in Games) if (g.Id == id) return g;
                return null;
            }
        }

        public GameInfo FindByExe(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return null;
            string full;
            try { full = Path.GetFullPath(exePath); }
            catch { full = exePath; }
            lock (gate)
            {
                foreach (var g in Games)
                {
                    if (string.Equals(g.ExePath, full, StringComparison.OrdinalIgnoreCase)) return g;
                }
            }
            return null;
        }

        public string CoverPath(GameInfo g)
        {
            if (g == null || string.IsNullOrEmpty(g.CoverFile)) return null;
            string p = Path.Combine(CoversDir, g.CoverFile);
            return File.Exists(p) ? p : null;
        }

        public GameInfo Add(string exePath, string name, byte[] coverData, string coverUrl, string pageUrl)
        {
            var g = new GameInfo();
            g.Id = Guid.NewGuid().ToString("N");
            g.ExePath = exePath;
            g.ExeName = Path.GetFileName(exePath);
            g.Name = string.IsNullOrEmpty(name) ? Path.GetFileNameWithoutExtension(exePath) : name.Trim();
            g.AddedTicks = DateTime.Now.Ticks;
            g.CoverUrl = coverUrl;
            g.PageUrl = pageUrl;

            if (coverData != null && coverData.Length > 0)
            {
                try
                {
                    string file = g.Id + GuessExt(coverData);
                    File.WriteAllBytes(Path.Combine(CoversDir, file), coverData);
                    g.CoverFile = file;
                }
                catch (Exception ex) { Log.Error("保存封面失败", ex); }
            }

            lock (gate) { Games.Add(g); }
            ClearDiscoveryMarks(exePath);
            Save();
            Raise();
            Log.Info("添加游戏：" + g.Name + " <- " + exePath);
            return g;
        }

        public void SetCover(GameInfo g, byte[] data)
        {
            if (g == null || data == null || data.Length == 0) return;
            try
            {
                string file = g.Id + GuessExt(data);
                File.WriteAllBytes(Path.Combine(CoversDir, file), data);
                if (!string.IsNullOrEmpty(g.CoverFile) && g.CoverFile != file)
                {
                    try { File.Delete(Path.Combine(CoversDir, g.CoverFile)); } catch { }
                }
                g.CoverFile = file;
                Save();
                Raise();
            }
            catch (Exception ex) { Log.Error("更新封面失败", ex); }
        }

        public static string GuessExt(byte[] data)
        {
            if (data == null || data.Length < 4) return ".jpg";
            if (data[0] == 0x89 && data[1] == 0x50) return ".png";
            if (data[0] == 0x47 && data[1] == 0x49) return ".gif";
            if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46) return ".webp";
            return ".jpg";
        }

        public void Update(GameInfo g)
        {
            Save();
            Raise();
        }

        public void Remove(string id)
        {
            GameInfo target = null;
            lock (gate)
            {
                for (int i = 0; i < Games.Count; i++)
                {
                    if (Games[i].Id == id) { target = Games[i]; Games.RemoveAt(i); break; }
                }
            }
            if (target == null) return;
            if (!string.IsNullOrEmpty(target.CoverFile))
            {
                try { File.Delete(Path.Combine(CoversDir, target.CoverFile)); } catch { }
            }
            Save();
            Raise();
            Log.Info("移除游戏：" + target.Name);
            // 用户主动删除的，记录到 discovery.json，自动发现不再添加
            if (!string.IsNullOrEmpty(target.ExePath)) MarkRemoved(target.ExePath);
        }

        // ---------------- 自动发现记录 ----------------

        public bool IsKnownNonGame(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return false;
            lock (gate) { return NonGames.ContainsKey(Normalize(exePath)); }
        }

        public void MarkNonGame(string exePath, string guessName)
        {
            if (string.IsNullOrEmpty(exePath)) return;
            string key = Normalize(exePath);
            string info = (guessName ?? "") + " | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            lock (gate) { NonGames[key] = info; }
            SaveDiscovery();
            Log.Info("已记录非游戏（不再自动判断）：" + exePath);
        }

        public bool IsRemoved(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return false;
            lock (gate) { return Removed.ContainsKey(Normalize(exePath)); }
        }

        public void MarkRemoved(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return;
            lock (gate) { Removed[Normalize(exePath)] = DateTime.Now.ToString("yyyy-MM-dd HH:mm"); }
            SaveDiscovery();
            Log.Info("已记录删除（不再自动添加）：" + exePath);
        }

        public int NonGameCount { get { lock (gate) { return NonGames.Count; } } }
        public int RemovedCount { get { lock (gate) { return Removed.Count; } } }

        void ClearDiscoveryMarks(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return;
            string key = Normalize(exePath);
            bool changed = false;
            lock (gate)
            {
                if (NonGames.Remove(key)) changed = true;
                if (Removed.Remove(key)) changed = true;
            }
            if (changed) SaveDiscovery();
        }

        static string Normalize(string path)
        {
            return (path ?? "").Trim().ToLowerInvariant();
        }

        void LoadDiscovery()
        {
            try
            {
                if (!File.Exists(DiscoveryFile)) return;
                string json = File.ReadAllText(DiscoveryFile, Encoding.UTF8);
                var data = ser.Deserialize<DiscoveryData>(json);
                if (data == null) return;
                if (data.NonGames != null) NonGames = new Dictionary<string, string>(data.NonGames, StringComparer.OrdinalIgnoreCase);
                if (data.Removed != null) Removed = new Dictionary<string, string>(data.Removed, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Log.Warn("读取 discovery.json 失败：" + ex.Message);
            }
        }

        public void SaveDiscovery()
        {
            try
            {
                var data = new DiscoveryData();
                lock (gate)
                {
                    data.NonGames = new Dictionary<string, string>(NonGames);
                    data.Removed = new Dictionary<string, string>(Removed);
                }
                AtomicWrite(DiscoveryFile, ser.Serialize(data));
            }
            catch (Exception ex)
            {
                Log.Warn("保存 discovery.json 失败：" + ex.Message);
            }
        }

        public void ResetProgress(string id)
        {
            var g = FindById(id);
            if (g == null) return;
            g.TotalSeconds = 0;
            g.Daily = new Dictionary<string, double>();
            g.LastPlayedTicks = 0;
            Save();
            Raise();
        }

        /// <summary>从另一个数据目录导入游戏（例如之前以“绿色模式”存在程序目录里的记录）。</summary>
        public int ImportFrom(string otherRoot)
        {
            try
            {
                if (string.IsNullOrEmpty(otherRoot)) return 0;
                string file = Path.Combine(otherRoot, "games.json");
                if (!File.Exists(file)) return 0;
                string marker = Path.Combine(otherRoot, ".imported");
                if (File.Exists(marker)) return 0;
                if (string.Equals(Path.GetFullPath(otherRoot).TrimEnd('\\'), Path.GetFullPath(Root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return 0;

                var list = ser.Deserialize<List<GameInfo>>(File.ReadAllText(file, Encoding.UTF8));
                if (list == null) return 0;

                int added = 0;
                lock (gate)
                {
                    foreach (var g in list)
                    {
                        if (g == null || string.IsNullOrEmpty(g.ExePath)) continue;
                        bool dup = false;
                        foreach (var e in Games)
                        {
                            if (string.Equals(e.ExePath, g.ExePath, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                        }
                        if (dup) continue;

                        if (g.Daily == null) g.Daily = new Dictionary<string, double>();
                        if (string.IsNullOrEmpty(g.Id)) g.Id = Guid.NewGuid().ToString("N");
                        if (!string.IsNullOrEmpty(g.Name)) g.Name = g.Name.Trim();

                        if (!string.IsNullOrEmpty(g.CoverFile))
                        {
                            try
                            {
                                string src = Path.Combine(otherRoot, "covers", g.CoverFile);
                                if (File.Exists(src))
                                {
                                    string dst = Path.Combine(CoversDir, g.CoverFile);
                                    if (!File.Exists(dst)) File.Copy(src, dst);
                                }
                            }
                            catch { }
                        }
                        Games.Add(g);
                        added++;
                    }
                }
                if (added > 0)
                {
                    Save();
                    Raise();
                    Log.Info("已从 " + otherRoot + " 导入 " + added + " 款游戏记录");
                }
                try { File.WriteAllText(marker, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); }
                catch { }
                return added;
            }
            catch (Exception ex)
            {
                Log.Warn("导入旧数据失败：" + ex.Message);
                return 0;
            }
        }

        public double TotalSeconds()
        {
            lock (gate)
            {
                double t = 0;
                foreach (var g in Games) t += g.TotalSeconds;
                return t;
            }
        }

        public double TodaySeconds()
        {
            lock (gate)
            {
                double t = 0;
                foreach (var g in Games) t += g.TodaySeconds;
                return t;
            }
        }
    }
}

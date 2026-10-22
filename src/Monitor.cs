using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GameLog
{
    /// <summary>进程监控：每 1 秒检测一次已登记的游戏是否在运行，并累计时长。</summary>
    public class Monitor : IDisposable
    {
        readonly Store store;
        readonly object gate = new object();
        Timer timer;
        DateTime lastTick = DateTime.UtcNow;
        readonly HashSet<string> runningIds = new HashSet<string>();
        readonly Dictionary<string, DateTime> sessionStart = new Dictionary<string, DateTime>();
        DateTime lastSave = DateTime.UtcNow;

        public event EventHandler Updated;
        public bool Paused { get; set; }

        public Monitor(Store store)
        {
            this.store = store;
        }

        public void Start()
        {
            if (timer != null) return;
            lastTick = DateTime.UtcNow;
            int seconds = IntervalSeconds;
            // 2 秒后先跑一次，把已经在运行的游戏尽快统计进来；之后按设置的间隔
            timer = new Timer(Tick, null, 2000, seconds * 1000);
            Log.Info("监控已启动：每 " + seconds + " 秒检测一次");
        }

        public void Restart()
        {
            Stop();
            Start();
        }

        public int IntervalSeconds
        {
            get
            {
                int s = store.Settings.MonitorSeconds;
                if (s < 30) s = 300;
                if (s > 3600) s = 3600;
                return s;
            }
        }

        public string IntervalLabel
        {
            get
            {
                int s = IntervalSeconds;
                if (s < 60) return "每 " + s + " 秒";
                int m = s / 60;
                if (m < 60) return m == 1 ? "每 1 分钟" : "每 " + m + " 分钟";
                int h = s / 3600;
                return "每 " + h + " 小时";
            }
        }

        public void Stop()
        {
            if (timer == null) return;
            timer.Dispose();
            timer = null;
            try { FlushPartial(); }
            catch (Exception ex) { Log.Warn("退出前补记时长失败：" + ex.Message); }
            lock (gate) { runningIds.Clear(); sessionStart.Clear(); }
        }

        public bool IsRunning(string gameId)
        {
            if (string.IsNullOrEmpty(gameId)) return false;
            lock (gate) { return runningIds.Contains(gameId); }
        }

        public int RunningCount
        {
            get { lock (gate) { return runningIds.Count; } }
        }

        public DateTime SessionStartOf(string gameId)
        {
            lock (gate)
            {
                DateTime dt;
                return sessionStart.TryGetValue(gameId, out dt) ? dt : DateTime.MinValue;
            }
        }

        void Tick(object state)
        {
            try
            {
                DateTime nowUtc = DateTime.UtcNow;
                double delta;
                List<GameInfo> games;
                bool paused;
                lock (gate)
                {
                    delta = (nowUtc - lastTick).TotalSeconds;
                    lastTick = nowUtc;
                    paused = Paused;
                }

                games = store.Snapshot();
                bool enabled = !paused && store.Settings.MonitorEnabled;

                if (!enabled || games.Count == 0 || delta <= 0)
                {
                    lock (gate)
                    {
                        if (runningIds.Count > 0)
                        {
                            runningIds.Clear();
                            sessionStart.Clear();
                        }
                    }
                    return;
                }

                if (delta > IntervalSeconds * 3) delta = IntervalSeconds; // 休眠/唤醒后避免把睡眠时间算作游戏时间

                HashSet<string> procs = RunningProcessNames();
                var stillRunning = new HashSet<string>();
                bool changed = false;

                lock (gate)
                {
                    foreach (var g in games)
                    {
                        string exe = string.IsNullOrEmpty(g.ExeName) ? Path.GetFileName(g.ExePath) : g.ExeName;
                        string name = "";
                        try { name = Path.GetFileNameWithoutExtension(exe); } catch { }
                        if (string.IsNullOrEmpty(name)) continue;
                        if (!procs.Contains(name)) continue;

                        stillRunning.Add(g.Id);
                        g.AddSeconds(delta, DateTime.Now);
                        if (!runningIds.Contains(g.Id))
                        {
                            sessionStart[g.Id] = DateTime.Now;
                            g.LastPlayedTicks = DateTime.Now.Ticks;
                            changed = true;
                        }
                    }
                    if (stillRunning.Count != runningIds.Count) changed = true;
                    runningIds.Clear();
                    foreach (var id in stillRunning) runningIds.Add(id);
                    foreach (var id in new List<string>(sessionStart.Keys))
                    {
                        if (!stillRunning.Contains(id)) sessionStart.Remove(id);
                    }
                }

                if ((DateTime.UtcNow - lastSave).TotalSeconds >= 30)
                {
                    store.Save();
                    lastSave = DateTime.UtcNow;
                }

                var h = Updated;
                if (h != null && (changed || (DateTime.UtcNow - lastUi).TotalMilliseconds > 950))
                {
                    lastUi = DateTime.UtcNow;
                    h(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Log.Error("监控异常", ex);
            }
        }

        /// <summary>退出/暂停前，把最后一次检测到现在的零头补记上，避免丢掉最多一个间隔的时长。</summary>
        void FlushPartial()
        {
            double delta;
            lock (gate) { delta = (DateTime.UtcNow - lastTick).TotalSeconds; lastTick = DateTime.UtcNow; }
            if (delta <= 0) return;
            if (delta > IntervalSeconds * 2) return;
            bool any = false;
            lock (gate)
            {
                if (runningIds.Count == 0) return;
                foreach (var g in store.Snapshot())
                {
                    if (g == null || !runningIds.Contains(g.Id)) continue;
                    g.AddSeconds(delta, DateTime.Now);
                    any = true;
                }
            }
            if (any)
            {
                store.Save();
                Log.Info("退出前补记 " + delta.ToString("0") + " 秒");
            }
        }

        DateTime lastUi = DateTime.MinValue;

        static HashSet<string> RunningProcessNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Process[] all = null;
            try { all = Process.GetProcesses(); }
            catch (Exception ex) { Log.Warn("枚举进程失败：" + ex.Message); return names; }
            foreach (var p in all)
            {
                try { names.Add(p.ProcessName); }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
            return names;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

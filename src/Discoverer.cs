using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace GameLog
{
    /// <summary>
    /// 自动发现游戏：每隔 N 分钟扫描一次运行中的进程，
    /// 跳过系统盘 / Program Files / 已判断过不是游戏 / 已被用户删除的程序，
    /// 剩下的交给 Identify 识别，是游戏就直接加入列表。
    /// </summary>
    public class Discoverer : IDisposable
    {
        readonly Store store;
        readonly object gate = new object();
        Timer timer;
        int scanning;
        DateTime nextScan = DateTime.MinValue;

        public event EventHandler<GameInfo> Discovered;
        public event EventHandler NonGameFound;

        public Discoverer(Store store)
        {
            this.store = store;
        }

        public void Restart()
        {
            lock (gate)
            {
                if (timer != null)
                {
                    timer.Dispose();
                    timer = null;
                }
                if (!store.Settings.AutoDetect) return;
                int minutes = Math.Max(1, store.Settings.AutoDetectMinutes);
                nextScan = DateTime.Now.AddSeconds(20);
                // 每 30 秒检查一次是否到点，便于设置改动能快速生效
                timer = new Timer(Tick, null, 20000, 30000);
                Log.Info("自动发现已启动：每 " + minutes + " 分钟扫描一次，每轮最多识别 " + store.Settings.AutoDetectMaxPerRun + " 个进程");
            }
        }

        public void Stop()
        {
            lock (gate)
            {
                if (timer != null)
                {
                    timer.Dispose();
                    timer = null;
                }
            }
        }

        /// <summary>立即扫描一次（界面上的「立即扫描」按钮调用），返回新增的游戏数量。</summary>
        public int ScanNow()
        {
            if (Interlocked.CompareExchange(ref scanning, 1, 0) != 0) return -1;
            try
            {
                nextScan = DateTime.Now.AddMinutes(Math.Max(1, store.Settings.AutoDetectMinutes));
                return Scan();
            }
            catch (Exception ex)
            {
                Log.Error("手动扫描异常", ex);
                return 0;
            }
            finally
            {
                Interlocked.Exchange(ref scanning, 0);
            }
        }

        void Tick(object state)
        {
            if (!store.Settings.AutoDetect) return;
            if (DateTime.Now < nextScan) return;
            nextScan = DateTime.Now.AddMinutes(Math.Max(1, store.Settings.AutoDetectMinutes));

            if (Interlocked.CompareExchange(ref scanning, 1, 0) != 0) return;
            try
            {
                Scan();
            }
            catch (Exception ex)
            {
                Log.Error("自动发现异常", ex);
            }
            finally
            {
                Interlocked.Exchange(ref scanning, 0);
            }
        }

        int Scan()
        {
            var games = store.Snapshot();
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in games)
            {
                if (!string.IsNullOrEmpty(g.ExePath)) known.Add(g.ExePath);
            }

            string self = SelfPath();
            var candidates = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in Process.GetProcesses())
            {
                string path = SafePath(p);
                try { p.Dispose(); } catch { }

                if (string.IsNullOrEmpty(path)) continue;
                if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(path, self, StringComparison.OrdinalIgnoreCase)) continue;
                if (!seen.Add(path)) continue;
                if (known.Contains(path)) continue;
                if (store.IsRemoved(path)) continue;
                if (store.IsKnownNonGame(path)) continue;
                if (ShouldSkip(path)) continue;
                if (IsEngineHelper(Path.GetFileNameWithoutExtension(path))) continue;
                // 同一个目录只取一个进程：有些游戏会同时跑两三个 exe
                string dir = Path.GetDirectoryName(path) ?? "";
                if (dir.Length == 0 || !dirs.Add(dir)) continue;
                if (store.HasGameInDirectory(dir)) continue;
                candidates.Add(path);
            }

            if (candidates.Count == 0)
            {
                Log.Info("自动发现：没有可分析的新进程");
                return 0;
            }

            int max = Math.Max(1, store.Settings.AutoDetectMaxPerRun);
            int added = 0;
            Log.Info("自动发现：本轮发现 " + candidates.Count + " 个候选，最多分析 " + max + " 个");
            foreach (var path in candidates)
            {
                if (max-- <= 0) break;
                if (Net.ConsecutiveFailures >= 2)
                {
                    Log.Warn("自动发现：网络异常，本轮停止");
                    break;
                }
                if (Analyze(path)) added++;
            }
            return added;
        }

        bool Analyze(string path)
        {
            Log.Info("自动发现：识别 " + path);
            IdentifyResult r = null;
            try
            {
                r = Identifier.Identify(path, null);
            }
            catch (Exception ex)
            {
                Log.Warn("自动发现：识别失败 " + path + " :: " + ex.Message);
                return false;
            }
            if (r == null) return false;

            if (r.Success)
            {
                Log.Info("自动发现：识别结果《" + r.Name + "》 score=" + r.Score.ToString("0.0"));
                if (r.Score < 70 || IsGenericName(r.Name) || GameCheck.LooksLikeArticle(r.Name))
                {
                    Log.Info("自动发现：置信度不足、名字太泛或像文章标题，不自动加入：" + path + " => " + r.Name);
                    store.MarkNonGame(path, r.Name);
                    return false;
                }

                // 再问一句“这是游戏吗”：图吧工具箱、AIDA64 这类工具会被百度结果里的软件特征拦下
                bool? isGame = GameCheck.IsGame(r.Name);
                if (isGame == false)
                {
                    Log.Info("自动发现：判断为软件/工具，不加入：" + r.Name);
                    store.MarkNonGame(path, r.Name);
                    var nf = NonGameFound;
                    if (nf != null) nf(this, EventArgs.Empty);
                    return false;
                }

                string dir = Path.GetDirectoryName(path) ?? "";
                if (dir.Length > 0 && store.HasGameInDirectory(dir))
                {
                    Log.Info("自动发现：同目录已有游戏，跳过：" + path);
                    return false;
                }

                var g = store.Add(path, r.Name, r.CoverData, r.CoverUrl, r.PageUrl);
                if (g != null)
                {
                    g.Note = "自动发现";
                    store.Save();
                    Log.Info("自动发现：已加入《" + g.Name + "》（" + path + "）");
                    var h = Discovered;
                    if (h != null) h(this, g);
                    return true;
                }
                return false;
            }

            if (Net.ConsecutiveFailures >= 2)
            {
                Log.Warn("自动发现：网络异常，暂不记录 " + path);
                return false;
            }

            // 只有“确实不像游戏”的结果才永久记住；30~70 分这种模棱两可的，下轮再试，
            // 避免因为一次搜索不准就把真正的游戏永久跳过。
            if (r.Score < 30)
            {
                store.MarkNonGame(path, r.Name);
                Log.Info("自动发现：判定不是游戏，已记住不再判断：" + path);
                var n = NonGameFound;
                if (n != null) n(this, EventArgs.Empty);
            }
            else
            {
                Log.Info("自动发现：结果不确定（score=" + r.Score.ToString("0.0") + "），暂不记录，下轮再试：" + path);
            }
            return false;
        }

        /// <summary>测试用：按自动发现的同一套过滤规则处理一个路径，返回处理结果。</summary>
        public string AnalyzeForTest(string path)
        {
            if (string.IsNullOrEmpty(path)) return "skip: empty";
            if (ShouldSkip(path)) return "skip: system/program path";
            if (IsEngineHelper(Path.GetFileNameWithoutExtension(path))) return "skip: engine helper";
            if (store.IsRemoved(path)) return "skip: removed before";
            if (store.IsKnownNonGame(path)) return "skip: known non-game";
            foreach (var g in store.Snapshot())
            {
                if (string.Equals(g.ExePath, path, StringComparison.OrdinalIgnoreCase)) return "skip: already in list";
            }
            string testDir = Path.GetDirectoryName(path) ?? "";
            if (testDir.Length > 0 && store.HasGameInDirectory(testDir)) return "skip: same folder";
            Analyze(path);
            foreach (var g in store.Snapshot())
            {
                if (string.Equals(g.ExePath, path, StringComparison.OrdinalIgnoreCase)) return "added: " + g.Name;
            }
            return "not a game (remembered)";
        }

        /// <summary>
        /// 引擎自带的崩溃上报/辅助进程，不是游戏本体。        /// Unity 游戏都有 UnityCrashHandler32/64.exe，虚幻有 CrashReportClient、UnrealCEFSubProcess 等。
        /// </summary>
        static readonly HashSet<string> EngineHelpers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "unitycrashhandler32", "unitycrashhandler64", "unityplayer",
            "crashreportclient", "crashreportclientwin64", "crashreportclientwin32",
            "unrealcefsubprocess", "epicwebhelper", "crashpadhandler", "crashpad_handler",
            "werfault", "dxdiag", "steamerrorreporter",
            "easyanticheat", "easyanticheat_eos", "beservice", "battleye", "vgc", "vgtray"
        };

        static bool IsEngineHelper(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Trim();
            if (n.IndexOf("crashhandler", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("crashreport", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("crashpad", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return EngineHelpers.Contains(n);
        }

        /// <summary>太泛的名字（“我的游戏”“小游戏”“游戏中心”）不自动加入列表。</summary>
        static bool IsGenericName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            string n = name.Trim();
            if (n.Length <= 6 && n.EndsWith("游戏", StringComparison.Ordinal)) return true;
            string[] bad = { "游戏", "单机游戏", "电脑游戏", "小游戏", "游戏中心", "游戏大厅", "软件", "工具", "助手", "程序" };
            foreach (var b in bad)
            {
                if (string.Equals(n, b, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr OpenProcess(int access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool QueryFullProcessImageName(IntPtr handle, int flags, StringBuilder buffer, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// 取进程路径：先走 MainModule，失败再用 QueryFullProcessImageName（带反作弊/提权的游戏往往只能走这条）。
        /// </summary>
        static string SafePath(Process p)
        {
            try { return p.MainModule.FileName; }
            catch { }
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(0x1000, false, p.Id); // PROCESS_QUERY_LIMITED_INFORMATION
                if (handle == IntPtr.Zero) return null;
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (QueryFullProcessImageName(handle, 0, sb, ref size)) return sb.ToString();
            }
            catch { }
            finally
            {
                if (handle != IntPtr.Zero) CloseHandle(handle);
            }
            return null;
        }

        static string SelfPath()
        {
            try { return Process.GetCurrentProcess().MainModule.FileName; }
            catch { return ""; }
        }

        /// <summary>系统盘、Program Files、Windows、AppData 下的进程大概率是软件，直接跳过；常见游戏目录例外。</summary>
        static bool ShouldSkip(string path)
        {
            string p = path.ToLowerInvariant();
            // 常见游戏安装目录放行（Steam、Epic、GOG、战网、CoD、育碧、Riot、EA、Rockstar）
            string[] gameFolders =
            {
                "\\steamapps\\common\\", "\\epic games\\", "\\gog games\\", "\\battle.net\\",
                "\\call of duty\\", "\\ubisoft\\", "\\riot games\\", "\\ea games\\",
                "\\xboxgames\\", "\\rockstar games\\"
            };
            foreach (var f in gameFolders)
            {
                if (p.IndexOf(f, StringComparison.Ordinal) >= 0) return false;
            }
            if (p.StartsWith("c:")) return true;
            string[] bad =
            {
                "\\program files\\", "\\program files (x86)\\", "\\windows\\", "\\programdata\\",
                "\\appdata\\", "\\$windows", "\\temp\\", "\\users\\public\\", "\\perflogs\\"
            };
            foreach (var b in bad)
            {
                if (p.IndexOf(b, StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameLog
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var o = Options.Parse(args);
            Log.Init(Path.Combine(o.DataDir, "logs"));
            Log.Info("使用数据目录：" + o.DataDir);
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                Log.Error("未处理异常", e.ExceptionObject as Exception);
                try { Console.Error.WriteLine(e.ExceptionObject); } catch { }
            };

            if (o.Mode == "install-menu")
            {
                ShellMenu.Install();
                return;
            }
            if (o.Mode == "uninstall-menu")
            {
                ShellMenu.Uninstall();
                return;
            }
            if (o.Mode == "selftest")
            {
                SelfTest(o);
                return;
            }
            if (o.Mode == "monitortest")
            {
                MonitorTest(o);
                return;
            }
            if (o.Mode == "menutest")
            {
                MenuTest(o);
                return;
            }
            if (o.Mode == "menu-selftest")
            {
                MenuRenderTest(o);
                return;
            }
            if (o.Mode == "render")
            {
                Render(o);
                return;
            }

            Log.Info("命令行：" + string.Join(" ", args));

            bool created;
            using (var mutex = new Mutex(true, "GameLog.SingleInstance.v1", out created))
            {
                if (!created)
                {
                    string msg = !string.IsNullOrEmpty(o.AddPath) ? "ADD\t" + o.AddPath : "SHOW";
                    bool sent = Ipc.TrySend(msg);
                    if (!sent)
                    {
                        bool queued = Ipc.WritePending(o.DataDir, msg);
                        Log.Info("已有实例在运行，管道不可用，写入待处理文件：" + msg + " => " + queued);
                    }
                    else
                    {
                        Log.Info("已有实例在运行，已通过管道转发：" + msg);
                    }
                    return;
                }

                var app = new App();
                app.DataDir = o.DataDir;
                app.StartMinimized = o.Minimized;
                app.PendingAdd = o.AddPath;
                app.Demo = o.Demo;
                App.ResearchDir = o.ResearchDir;
                app.Run();
                GC.KeepAlive(mutex);
            }
        }

        static void Render(Options o)
        {
            try
            {
                Log.Init(Path.Combine(o.DataDir, "logs"));
                Log.Info("render: 开始");
                App.ResearchDir = o.ResearchDir;

                var app = new App();
                app.DataDir = o.DataDir;
                app.Store = new Store(o.DataDir);
                app.Monitor = new Monitor(app.Store);
                if (o.Demo) app.SeedDemo();
                Log.Info("render: 数据就绪");

                var win = new MainWindow(app);
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    var bi = Ui.LoadImage(iconPath, 64);
                    if (bi != null) win.Icon = bi;
                }

                FrameworkElement root;
                double W = 1180;
                double H = 780;

                if (o.Dialog == "settings")
                {
                    var sw = new SettingsWindow(app);
                    root = sw.Content as FrameworkElement;
                    W = 680;
                    H = 800;
                    sw.Content = null;
                }
                else if (o.Dialog == "identify")
                {
                    var games = app.Store.Snapshot();
                    var cands = new List<GameCandidate>();
                    string[] demoNames = { "艾尔登法环", "艾尔登法环：黑夜君临", "上古卷轴4：战栗孤岛" };
                    for (int i = 0; i < demoNames.Length; i++)
                    {
                        var c = new GameCandidate();
                        c.Name = demoNames[i];
                        c.Slug = "eldenring" + i;
                        c.Score = i == 0 ? 91 : (i == 1 ? 40 : 12);
                        c.Origin = "游民星空搜索";
                        cands.Add(c);
                    }
                    var dlg = new IdentifyDialog(app, @"C:\Games\ELDEN RING\Game\eldenring.exe");
                    string coverPath = games.Count > 0 ? app.Store.CoverPath(games[0]) : null;
                    dlg.FillForPreview(cands, coverPath);
                    root = dlg.Content as FrameworkElement;
                    W = 780;
                    H = 588;
                    dlg.Content = null;
                }
                else
                {
                    root = win.Content as FrameworkElement;
                    win.Content = null;
                    win.Rebuild();
                }

                if (root == null) { Log.Error("render: 内容为空"); return; }
                Log.Info("render: 界面已构建");

                var host = new Grid();
                host.Background = Ui.Bg;
                host.Width = W;
                host.Height = H;
                host.Children.Add(root);
                host.Measure(new Size(W, H));
                host.Arrange(new Rect(0, 0, W, H));
                host.UpdateLayout();
                Log.Info("render: 布局完成");

                var rtb = new RenderTargetBitmap((int)W, (int)H, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(host);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                string dir = Path.GetDirectoryName(Path.GetFullPath(o.RenderPath));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                using (var fs = File.Create(o.RenderPath)) enc.Save(fs);
                Log.Info("截图已保存：" + o.RenderPath);
            }
            catch (Exception ex)
            {
                Log.Error("render 失败", ex);
                try
                {
                    File.WriteAllText(Path.Combine(o.DataDir, "render-error.txt"), ex.ToString(), new UTF8Encoding(false));
                }
                catch { }
            }
            Environment.Exit(0);
        }

        static void WriteReport(Options o, string name, StringBuilder sb)
        {
            try
            {
                string outFile = Path.Combine(o.DataDir, "logs", name);
                Directory.CreateDirectory(Path.GetDirectoryName(outFile));
                File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        static void MenuRenderTest(Options o)
        {
            var sb = new StringBuilder();
            try
            {
                var app = new App();
                app.DispatcherUnhandledException += delegate (object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args)
                {
                    Log.Error("menu render dispatcher exception", args.Exception);
                    args.Handled = true;
                };
                Log.Info("menu: app created");
                var w = new Window();
                w.Width = 320;
                w.Height = 240;
                w.WindowStyle = WindowStyle.None;
                w.ShowInTaskbar = false;
                w.Left = -4000;
                w.Top = -4000;
                w.Show();
                Log.Info("menu: window shown");

                var cm = Ui.DarkMenu();
                cm.Items.Add(Ui.MItem("▶   启动游戏", null, true));
                cm.Items.Add(Ui.MItem("在文件夹中显示", null, true));
                cm.Items.Add(new Separator());
                cm.Items.Add(Ui.MItem("重新识别游戏信息", null, true));
                cm.Items.Add(Ui.MItem("更换封面…", null, true));
                cm.Items.Add(Ui.MItem("重置时长", null, true));
                cm.Items.Add(new Separator());
                cm.Items.Add(Ui.MItem("从列表移除", null, true));

                cm.PlacementTarget = w;
                cm.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
                cm.HorizontalOffset = 8;
                cm.VerticalOffset = 8;
                cm.IsOpen = true;
                Log.Info("menu: opened");
                cm.ApplyTemplate();
                cm.UpdateLayout();
                Log.Info("menu: layout done");

                sb.AppendLine("menu open=" + cm.IsOpen + " actual=" + cm.ActualWidth.ToString("0") + "x" + cm.ActualHeight.ToString("0"));
                foreach (var it in cm.Items)
                {
                    var mi = it as MenuItem;
                    if (mi == null) { sb.AppendLine("  [separator]"); continue; }
                    var cp = FindPresenter(mi);
                    string content = cp == null ? "<no presenter>" : (cp.Content == null ? "<null>" : cp.Content.ToString());
                    sb.AppendLine("  item='" + mi.Header + "' size=" + mi.ActualWidth.ToString("0") + "x" + mi.ActualHeight.ToString("0")
                        + " fg=" + (mi.Foreground == null ? "-" : mi.Foreground.ToString())
                        + " presenter=" + content);
                }

                double wdt = Math.Max(120, cm.ActualWidth);
                double hgt = Math.Max(120, cm.ActualHeight);
                var rtb = new RenderTargetBitmap((int)Math.Ceiling(wdt), (int)Math.Ceiling(hgt), 96, 96, PixelFormats.Pbgra32);
                var bd = cm.Template.FindName("bd", cm) as FrameworkElement;
                rtb.Render(bd != null ? (Visual)bd : cm);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                string path = string.IsNullOrEmpty(o.RenderPath) ? Path.Combine(o.DataDir, "menu.png") : o.RenderPath;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                using (var fs = File.Create(path)) enc.Save(fs);
                sb.AppendLine("saved=" + path);

                cm.IsOpen = false;
                w.Close();
            }
            catch (Exception ex)
            {
                sb.AppendLine("menu render FAILED: " + ex);
            }
            WriteReport(o, "menurender.txt", sb);
            Environment.Exit(0);
        }

        static ContentPresenter FindPresenter(DependencyObject root)
        {
            if (root == null) return null;
            int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                var cp = child as ContentPresenter;
                if (cp != null) return cp;
                var inner = FindPresenter(child);
                if (inner != null) return inner;
            }
            return null;
        }

        static void MenuTest(Options o)
        {
            var sb = new StringBuilder();
            sb.AppendLine("menutest " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            try
            {
                ShellMenu.Install();
                sb.AppendLine("installed=" + ShellMenu.IsInstalled());
                sb.AppendLine("command=" + ShellMenu.InstalledCommand());
            }
            catch (Exception ex)
            {
                sb.AppendLine("install FAILED: " + ex);
            }
            try
            {
                ShellMenu.Uninstall();
                sb.AppendLine("after uninstall installed=" + ShellMenu.IsInstalled());
            }
            catch (Exception ex)
            {
                sb.AppendLine("uninstall FAILED: " + ex.Message);
            }
            try
            {
                string reg = ShellMenu.WriteRegFile(o.DataDir, true);
                sb.AppendLine("regfile=" + reg);
                if (!string.IsNullOrEmpty(reg) && File.Exists(reg))
                {
                    sb.AppendLine("--- reg content ---");
                    sb.AppendLine(File.ReadAllText(reg, Encoding.Unicode));
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("regfile FAILED: " + ex.Message);
            }
            WriteReport(o, "menutest.txt", sb);
        }

        static void MonitorTest(Options o)
        {
            var sb = new StringBuilder();
            sb.AppendLine("monitortest " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            string dir = Path.Combine(o.DataDir, "monitortest");
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "TestGame.exe");
            try
            {
                File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), exe, true);
                sb.AppendLine("dummy exe ok: " + exe);
            }
            catch (Exception ex)
            {
                sb.AppendLine("copy FAILED: " + ex.Message);
                WriteReport(o, "monitortest.txt", sb);
                return;
            }

            string dataDir = Path.Combine(o.DataDir, "monitortest-data");
            try { if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true); } catch { }

            var store = new Store(dataDir);
            var g = store.Add(exe, "测试游戏", null, null, null);
            var mon = new Monitor(store);
            mon.Start();

            Process p = null;
            try
            {
                var psi = new ProcessStartInfo(exe, "-n 8 127.0.0.1");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                p = Process.Start(psi);
                sb.AppendLine("process started pid=" + p.Id);
            }
            catch (Exception ex)
            {
                sb.AppendLine("process start FAILED: " + ex.Message);
            }

            Thread.Sleep(3000);
            sb.AppendLine("at 3s: running=" + mon.IsRunning(g.Id) + " seconds=" + g.TotalSeconds.ToString("0.0"));
            Thread.Sleep(7000);
            sb.AppendLine("at 10s: running=" + mon.IsRunning(g.Id) + " seconds=" + g.TotalSeconds.ToString("0.0"));
            mon.Stop();
            if (p != null)
            {
                try { if (!p.HasExited) p.Kill(); } catch { }
                try { p.Dispose(); } catch { }
            }
            sb.AppendLine("exit code path done; expected near 8 seconds");
            store.Save();
            WriteReport(o, "monitortest.txt", sb);
        }

        static void SelfTest(Options o)
        {
            var sb = new StringBuilder();
            sb.AppendLine("GameLog selftest " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("data=" + o.DataDir);

            try
            {
                var store = new Store(o.DataDir);
                sb.AppendLine("store ok, games=" + store.Games.Count + ", covers=" + store.CoversDir);
                var probe = store.Add(Path.Combine(o.DataDir, "selftest-probe.exe"), "Selftest Probe", null, null, null);
                sb.AppendLine("store add ok, id=" + probe.Id);
                store.Remove(probe.Id);
                sb.AppendLine("store remove ok");
            }
            catch (Exception ex)
            {
                sb.AppendLine("store FAILED: " + ex);
            }

            try
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in Process.GetProcesses())
                {
                    try { names.Add(p.ProcessName); } catch { }
                    finally { try { p.Dispose(); } catch { } }
                }
                sb.AppendLine("process scan ok, count=" + names.Count + ", has explorer=" + names.Contains("explorer"));
            }
            catch (Exception ex)
            {
                sb.AppendLine("process scan FAILED: " + ex.Message);
            }

            sb.AppendLine("menu installed=" + ShellMenu.IsInstalled());

            foreach (var path in o.TestPaths)
            {
                sb.AppendLine();
                sb.AppendLine("=== " + path);
                sb.AppendLine("keywords: " + string.Join(" | ", Identifier.Keywords(path).ToArray()));
                var sw = Stopwatch.StartNew();
                IdentifyResult r = null;
                try
                {
                    r = Identifier.Identify(path, delegate (string msg) { sb.AppendLine("  .. " + msg); });
                }
                catch (Exception ex)
                {
                    sb.AppendLine("identify FAILED: " + ex);
                }
                sw.Stop();
                if (r == null) continue;
                sb.AppendLine("success=" + r.Success + "  name=" + r.Name + "  score=" + r.Score.ToString("0.0")
                    + "  cover=" + (r.CoverData == null ? 0 : r.CoverData.Length) + " bytes"
                    + "  elapsed=" + sw.ElapsedMilliseconds + "ms");
                sb.AppendLine("message=" + r.Message);
                int i = 0;
                foreach (var c in r.Candidates)
                {
                    if (i++ >= 6) break;
                    sb.AppendLine("   [" + c.Score.ToString("0.0") + "] " + c.Name + "  (" + c.Slug + ")  " + c.Origin);
                }
            }

            string outFile = Path.Combine(o.DataDir, "logs", "selftest.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outFile));
            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        }
    }

    class Options
    {
        public string DataDir = "";
        public string AddPath = null;
        public bool Minimized;
        public string Mode = "run";
        public string RenderPath = null;
        public bool Demo;
        public string ResearchDir = "";
        public string Dialog = "";
        public List<string> TestPaths = new List<string>();

        public static Options Parse(string[] args)
        {
            var o = new Options();
            o.DataDir = "";
            o.ResearchDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_research");

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (a.ToLowerInvariant())
                {
                    case "--add":
                        if (next != null) { o.AddPath = next.Trim().Trim('"'); i++; }
                        break;
                    case "--minimized":
                    case "--tray":
                        o.Minimized = true;
                        break;
                    case "--data":
                        if (next != null) { o.DataDir = next; i++; }
                        break;
                    case "--install-menu":
                        o.Mode = "install-menu";
                        break;
                    case "--uninstall-menu":
                        o.Mode = "uninstall-menu";
                        break;
                    case "--selftest":
                        o.Mode = "selftest";
                        break;
                    case "--monitortest":
                        o.Mode = "monitortest";
                        break;
                    case "--menutest":
                        o.Mode = "menutest";
                        break;
                    case "--menu-selftest":
                        o.Mode = "menu-selftest";
                        if (next != null && !next.StartsWith("-")) { o.RenderPath = next; i++; }
                        break;
                    case "--render":
                        o.Mode = "render";
                        if (next != null) { o.RenderPath = next; i++; }
                        break;
                    case "--demo":
                        o.Demo = true;
                        break;
                    case "--dialog":
                        if (next != null) { o.Dialog = next.ToLowerInvariant(); i++; }
                        break;
                    case "--research":
                        if (next != null) { o.ResearchDir = next; i++; }
                        break;
                    default:
                        if (!a.StartsWith("-")) o.TestPaths.Add(a.Trim().Trim('"'));
                        break;
                }
            }

            if (o.Mode == "render" && string.IsNullOrEmpty(o.RenderPath))
                o.RenderPath = Path.Combine(Environment.CurrentDirectory, "screenshot.png");

            if (string.IsNullOrEmpty(o.DataDir))
            {
                o.DataDir = ResolveDataDir();
            }
            else if (!IsWritable(o.DataDir))
            {
                string fallback = ResolveDataDir();
                Log.Warn("指定的数据目录不可写：" + o.DataDir + "，改用：" + fallback);
                o.DataDir = fallback;
            }
            return o;
        }

        /// <summary>依次尝试 %APPDATA%、%LOCALAPPDATA%、exe 目录、临时目录，选第一个可写的位置。</summary>
        public static string ResolveDataDir()
        {
            string exeDir = "";
            try { exeDir = AppDomain.CurrentDomain.BaseDirectory; }
            catch { }

            var candidates = new List<string>();
            try
            {
                if (exeDir.Length > 0 && File.Exists(Path.Combine(exeDir, "portable.txt")))
                    candidates.Add(Path.Combine(exeDir, "data"));
            }
            catch { }

            string appData = SafeFolder(Environment.SpecialFolder.ApplicationData);
            if (appData.Length > 0) candidates.Add(Path.Combine(appData, "GameLog"));

            string local = SafeFolder(Environment.SpecialFolder.LocalApplicationData);
            if (local.Length > 0) candidates.Add(Path.Combine(local, "GameLog"));

            if (exeDir.Length > 0) candidates.Add(Path.Combine(exeDir, "GameLogData"));

            try { candidates.Add(Path.Combine(Path.GetTempPath(), "GameLog")); }
            catch { }

            foreach (var c in candidates)
            {
                if (string.IsNullOrEmpty(c)) continue;
                if (IsWritable(c)) return c;
            }
            return exeDir.Length > 0 ? Path.Combine(exeDir, "GameLogData") : ".";
        }

        static string SafeFolder(Environment.SpecialFolder folder)
        {
            try { return Environment.GetFolderPath(folder) ?? ""; }
            catch { return ""; }
        }

        public static bool IsWritable(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (var fs = File.Create(probe)) { fs.WriteByte(0); }
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

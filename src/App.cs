using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GameLog
{
    public class App : Application
    {
        public static readonly string Version = VersionString();
        public static string ResearchDir = "";

        public Discoverer Discovery;
        public Store Store;
        public Monitor Monitor;
        public TrayIcon Tray;
        public MainWindow Window;

        public string DataDir = "";
        public bool StartMinimized;
        public string PendingAdd;
        public string RenderTo;
        public bool Demo;
        public bool Exiting;

        public bool HasTray { get { return Tray != null; } }
        public bool MonitorPaused { get { return Monitor != null && Monitor.Paused; } }

        static string VersionString()
        {
            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
            catch { return "1.0.0"; }
        }

        public string IconPath
        {
            get
            {
                try
                {
                    string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                    if (File.Exists(p)) return p;
                }
                catch { }
                return null;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                OnStartupCore();
            }
            catch (Exception ex)
            {
                Log.Error("启动失败", ex);
                try
                {
                    MessageBox.Show("GameLog 启动失败：\n" + ex.Message + "\n\n数据目录：" + DataDir
                        + "\n请确认该目录可写，或把 GameLog.exe 放到有写入权限的文件夹后重试。",
                        "GameLog", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
                try { Shutdown(); } catch { }
            }
        }

        void OnStartupCore()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DispatcherUnhandledException += delegate (object s, DispatcherUnhandledExceptionEventArgs args)
            {
                Log.Error("界面线程异常", args.Exception);
            };

            Log.Init(Path.Combine(DataDir, "logs"));
            Log.Info("GameLog v" + Version + " 启动，数据目录：" + DataDir + "，参数：add=" + (PendingAdd ?? "") + " minimized=" + StartMinimized + " render=" + (RenderTo ?? ""));

            Store = new Store(DataDir);
            if (Demo) SeedDemo();

            // 如果之前是以“程序目录模式”运行过（例如受限环境），把那里的记录导入当前数据目录
            int imported = 0;
            try
            {
                string alt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameLogData");
                imported = Store.ImportFrom(alt);
            }
            catch { }

            Monitor = new Monitor(Store);
            Monitor.Start();

            Discovery = new Discoverer(Store);
            Discovery.Discovered += OnGameDiscovered;
            Discovery.Restart();

            bool menuFailed = false;
            if (RenderTo == null)
            {
                ShellMenu.EnsureUpToDate();
                if (!ShellMenu.IsInstalled() && !Store.Settings.FirstRunDone)
                {
                    try { ShellMenu.Install(); }
                    catch (Exception ex)
                    {
                        menuFailed = true;
                        Log.Warn("首次运行注册右键菜单失败：" + ex.Message);
                    }
                }
            }

            Window = new MainWindow(this);
            string icon = IconPath;
            if (icon != null)
            {
                var bi = Ui.LoadImage(icon, 64);
                if (bi != null) Window.Icon = bi;
            }
            SessionEnding += delegate { try { Store.Save(); } catch { } };

            if (!StartMinimized) Window.Show();

            Tray = new TrayIcon("GameLog", ShowWindow, ToggleMonitor, delegate { return Monitor.Paused; }, ExitApp, icon);
            Ipc.StartServer(OnIpc);
            StartPendingPoll();

            if (!Store.Settings.FirstRunDone)
            {
                Store.Settings.FirstRunDone = true;
                Store.SaveSettings();
                if (!StartMinimized && Window.IsVisible)
                {
                    if (menuFailed) Window.ShowToast("右键菜单注册失败：请在「设置」里重试，或导出 .reg 文件手动导入");
                    else Window.ShowToast("已自动启用右键菜单：在任意 exe 上右键 →「添加到 GameLog」");
                }
            }

            if (!string.IsNullOrEmpty(PendingAdd))
            {
                string path = PendingAdd;
                ShowWindow();
                Dispatcher.BeginInvoke(new Action(delegate { Window.AddExe(path); }), DispatcherPriority.Background);
            }

            if (imported > 0 && !StartMinimized)
            {
                string msg = "已从程序目录导入 " + imported + " 款游戏记录";
                Dispatcher.BeginInvoke(new Action(delegate { Window.ShowToast(msg); }), DispatcherPriority.Background);
            }
        }

        void OnIpc(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Log.Info("收到 IPC 消息：" + message);
            Dispatcher.BeginInvoke(new Action(delegate { HandleMessage(message); }));
        }

        void HandleMessage(string message)
        {
            if (message.StartsWith("ADD\t", StringComparison.Ordinal))
            {
                ShowWindow();
                Window.AddExe(message.Substring(4));
            }
            else
            {
                ShowWindow();
            }
        }

        void StartPendingPoll()
        {
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(2);
            timer.Tick += delegate
            {
                string file = Ipc.PendingFile(DataDir);
                if (!File.Exists(file)) return;
                string[] lines = null;
                try
                {
                    lines = File.ReadAllLines(file);
                    File.Delete(file);
                }
                catch { return; }
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    Log.Info("收到待处理消息：" + line);
                    HandleMessage(line.Trim());
                }
            };
            timer.Start();
        }

        public void ShowWindow()
        {
            if (Window == null) return;
            try
            {
                if (!Window.IsVisible) Window.Show();
                if (Window.WindowState == WindowState.Minimized) Window.WindowState = WindowState.Normal;
                Window.Activate();
                Window.Topmost = true;
                Window.Topmost = false;
                Window.Focus();
            }
            catch (Exception ex) { Log.Error("显示窗口失败", ex); }
        }

        public void ToggleMonitor()
        {
            if (Monitor == null) return;
            bool paused = !Monitor.Paused;
            Monitor.Paused = paused;
            Store.Settings.MonitorEnabled = !paused;
            Store.SaveSettings();
            if (Tray != null) Tray.SetPaused(paused);
            if (Window != null) Window.UpdateLive();
            Log.Info(paused ? "已暂停记录" : "已恢复记录");
        }

        public void RestartDiscovery()
        {
            if (Discovery != null) Discovery.Restart();
        }

        void OnGameDiscovered(object sender, GameInfo g)
        {
            if (g == null) return;
            Log.Info("自动发现新游戏：" + g.Name + " <- " + g.ExePath);
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (Window != null)
                {
                    Window.Rebuild();
                    Window.ShowToast("自动发现《" + g.Name + "》，已加入列表");
                }
                if (Tray != null)
                {
                    Tray.ShowBalloon("自动发现游戏", "已加入《" + g.Name + "》，开始记录游玩时间");
                }
            }));
        }

        public void NotifyHiddenToTray()
        {
            if (Tray != null) Tray.ShowBalloon("GameLog 还在后台运行", "游戏时间会继续记录；右键托盘图标可以退出。");
        }

        public void ExitApp()
        {
            if (Exiting) return;
            Exiting = true;
            Log.Info("退出 GameLog");
            try { if (Store != null) Store.Save(); } catch { }
            try { if (Monitor != null) Monitor.Stop(); } catch { }
            try { if (Discovery != null) Discovery.Stop(); } catch { }
            try { if (Tray != null) Tray.Dispose(); } catch { }
            try { Shutdown(); } catch { }
        }

        public void SeedDemo()
        {
            if (Store.Games.Count > 0) return;
            string research = ResearchDir;
            if (string.IsNullOrEmpty(research) || !Directory.Exists(research)) return;

            AddDemo("艾尔登法环", @"C:\Games\ELDEN RING\Game\eldenring.exe", "cover_eldenring.jpg", 48 * 3600 + 24 * 60, 42 * 60, research);
            AddDemo("赛博朋克 2077", @"C:\Games\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe", "cover_cp2077.jpg", 21 * 3600 + 8 * 60, 0, research);
            AddDemo("黑神话：悟空", @"C:\Games\Black Myth Wukong\b1\Binaries\Win64\b1-Win64-Shipping.exe", "cover_bmwukong.jpg", 9 * 3600 + 51 * 60, 96 * 60, research);
            AddDemo("星露谷物语", @"C:\Games\Stardew Valley\Stardew Valley.exe", "cover_stardewvalley.jpg", 3 * 3600 + 12 * 60, 0, research);
            Store.Save();
        }

        void AddDemo(string name, string exe, string coverFile, double total, double today, string research)
        {
            try
            {
                string coverPath = Path.Combine(research, coverFile);
                byte[] data = File.Exists(coverPath) ? File.ReadAllBytes(coverPath) : null;
                var g = Store.Add(exe, name, data, null, null);
                g.TotalSeconds = total;
                g.Daily[DateTime.Now.ToString("yyyy-MM-dd")] = today;
                g.LastPlayedTicks = DateTime.Now.AddHours(-2).Ticks;
            }
            catch (Exception ex) { Log.Warn("生成演示数据失败：" + ex.Message); }
        }
    }
}

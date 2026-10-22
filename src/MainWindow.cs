using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace GameLog
{
    public class MainWindow : Window
    {
        readonly App app;
        readonly Store store;
        readonly Monitor monitor;

        WrapPanel cardsHost;
        ScrollViewer scroller;
        Grid emptyHost;
        TextBlock emptyTitle;
        TextBlock emptyHint;
        TextBlock statGames;
        TextBlock statToday;
        TextBlock statTotal;
        TextBlock statusLeft;
        TextBlock statusRight;
        TextBlock monitorChipText;
        Border monitorChip;
        TextBox searchBox;
        Border toast;
        TextBlock toastText;
        DispatcherTimer toastTimer;
        DispatcherTimer liveTimer;
        Border menuBanner;
        Border dropHint;
        readonly List<KeyValuePair<string, Border>> sortButtons = new List<KeyValuePair<string, Border>>();

        string sortMode = "recent";
        string selectedId = null;
        bool manualScanning;
        string layoutMode = "cards";
        double maxSecondsForBars;
        StackPanel listHost;
        readonly Dictionary<string, GameCard> cards = new Dictionary<string, GameCard>();
        readonly Dictionary<string, GameRow> rows = new Dictionary<string, GameRow>();
        readonly List<KeyValuePair<string, Border>> layoutButtons = new List<KeyValuePair<string, Border>>();

        public MainWindow(App app)
        {
            this.app = app;
            this.store = app.Store;
            this.monitor = app.Monitor;
            sortMode = store.Settings.SortMode;
            layoutMode = string.IsNullOrEmpty(store.Settings.Layout) ? "cards" : store.Settings.Layout;

            Title = "GameLog · 游戏时光";
            Width = 1180;
            Height = 780;
            MinWidth = 920;
            MinHeight = 620;
            Background = Ui.Bg;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Ui.DarkTitleBar(this);

            Content = BuildRoot();

            store.Changed += delegate
            {
                Dispatcher.BeginInvoke(new Action(Rebuild));
            };
            monitor.Updated += delegate
            {
                Dispatcher.BeginInvoke(new Action(UpdateLive));
            };

            // 界面自己每秒刷新一次（监控本身可能是 5 分钟一次，界面不能跟着卡住）
            liveTimer = new DispatcherTimer();
            liveTimer.Interval = TimeSpan.FromSeconds(1);
            liveTimer.Tick += delegate { UpdateLive(); };
            liveTimer.Start();

            Loaded += delegate
            {
                Rebuild();
                FocusSearchEnabled();
            };

            Closing += OnClosing;
        }

        void FocusSearchEnabled()
        {
            PreviewKeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    if (searchBox != null) { searchBox.Focus(); searchBox.SelectAll(); }
                    e.Handled = true;
                }
            };
        }

        // ---------------- 界面构建 ----------------

        FrameworkElement BuildRoot()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // menu banner
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // stats
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // cards
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) }); // status

            var header = BuildHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var banner = BuildMenuBanner();
            Grid.SetRow(banner, 1);
            root.Children.Add(banner);

            var stats = BuildStats();
            Grid.SetRow(stats, 2);
            root.Children.Add(stats);

            // 内容区
            var content = new Grid();
            scroller = new ScrollViewer();
            scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroller.Padding = new Thickness(17, 4, 17, 12);
            scroller.Background = Brushes.Transparent;

            cardsHost = new WrapPanel();
            cardsHost.Orientation = Orientation.Horizontal;
            cardsHost.Margin = new Thickness(0);

            listHost = new StackPanel();
            listHost.Orientation = Orientation.Vertical;
            listHost.Margin = new Thickness(9, 0, 9, 0);

            var layoutPanel = new Grid();
            layoutPanel.Children.Add(cardsHost);
            layoutPanel.Children.Add(listHost);
            scroller.Content = layoutPanel;

            content.Children.Add(scroller);
            content.Children.Add(BuildEmptyState());
            Grid.SetRow(content, 3);
            root.Children.Add(content);

            root.Children.Add(BuildStatusBar());

            toast = new Border();
            toast.CornerRadius = new CornerRadius(10);
            toast.Background = Ui.Br("#F21B2330");
            toast.BorderBrush = Ui.Line;
            toast.BorderThickness = new Thickness(1);
            toast.Padding = new Thickness(16, 9, 16, 9);
            toast.HorizontalAlignment = HorizontalAlignment.Center;
            toast.VerticalAlignment = VerticalAlignment.Bottom;
            toast.Margin = new Thickness(0, 0, 0, 46);
            toast.Visibility = Visibility.Collapsed;
            toast.IsHitTestVisible = false;
            toastText = Ui.TxtBlock("", 12.5, Ui.Txt);
            toast.Child = toastText;
            Panel.SetZIndex(toast, 50);
            Grid.SetRow(toast, 3);
            root.Children.Add(toast);

            var drop = BuildDropOverlay();
            Grid.SetRow(drop, 0);
            Grid.SetRowSpan(drop, 5);
            Panel.SetZIndex(drop, 60);
            root.Children.Add(drop);

            SetupDragAndDrop();
            ApplyLayout();
            return root;
        }

        /// <summary>未启用右键菜单时的提示条。</summary>
        FrameworkElement BuildMenuBanner()
        {
            menuBanner = new Border();
            menuBanner.CornerRadius = new CornerRadius(10);
            menuBanner.Background = Ui.Br("#1B2434");
            menuBanner.BorderBrush = Ui.Br("#2C3B55");
            menuBanner.BorderThickness = new Thickness(1);
            menuBanner.Padding = new Thickness(14, 9, 10, 9);
            menuBanner.Margin = new Thickness(26, 14, 26, 0);
            menuBanner.Visibility = Visibility.Collapsed;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var texts = new StackPanel();
            texts.VerticalAlignment = VerticalAlignment.Center;
            var title = Ui.TxtBlock("右键菜单还没启用", 12.5, Ui.Txt, FontWeights.SemiBold);
            var sub = Ui.TxtBlock("启用后，在任意 exe 上右键就能直接添加；也可以把 exe 拖进这个窗口。", 11, Ui.Sub);
            sub.Margin = new Thickness(0, 2, 0, 0);
            texts.Children.Add(title);
            texts.Children.Add(sub);
            grid.Children.Add(texts);

            var why = Ui.PlainBtn("看不到菜单？", delegate { ShowMenuHelp(); });
            why.Height = 28;
            why.FontSize = 11.5;
            Grid.SetColumn(why, 1);
            grid.Children.Add(why);

            var enable = Ui.Btn("立即启用", true, delegate { EnableMenu(); });
            enable.Height = 30;
            enable.MinWidth = 84;
            enable.Margin = new Thickness(8, 0, 0, 0);
            enable.FontSize = 12;
            Grid.SetColumn(enable, 2);
            grid.Children.Add(enable);

            var close = Ui.PlainBtn("✕", delegate
            {
                store.Settings.HideMenuBanner = true;
                store.SaveSettings();
                UpdateMenuBanner();
            });
            close.Height = 26;
            close.Width = 30;
            close.FontSize = 12;
            close.Margin = new Thickness(4, 0, 0, 0);
            close.ToolTip = "不再显示这条提示";
            close.Foreground = Ui.Dim;
            Grid.SetColumn(close, 3);
            grid.Children.Add(close);

            menuBanner.Child = grid;
            return menuBanner;
        }

        /// <summary>拖拽 exe 到窗口时的提示层。</summary>
        FrameworkElement BuildDropOverlay()
        {
            dropHint = new Border();
            dropHint.Margin = new Thickness(12);
            dropHint.CornerRadius = new CornerRadius(16);
            dropHint.Background = Ui.Br("#E60E1117");
            dropHint.BorderBrush = Ui.Accent;
            dropHint.BorderThickness = new Thickness(2);
            dropHint.Visibility = Visibility.Collapsed;
            dropHint.IsHitTestVisible = false;

            var panel = new StackPanel();
            panel.HorizontalAlignment = HorizontalAlignment.Center;
            panel.VerticalAlignment = VerticalAlignment.Center;

            var icon = Ui.TxtBlock("＋", 40, Ui.Accent, FontWeights.Bold);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.Margin = new Thickness(0, 0, 0, 6);

            var t = Ui.TxtBlock("松开鼠标，添加游戏", 19, Ui.Txt, FontWeights.Bold);
            t.HorizontalAlignment = HorizontalAlignment.Center;

            var s = Ui.TxtBlock("支持一次拖入多个 exe，会自动识别中文名和封面", 12, Ui.Sub);
            s.HorizontalAlignment = HorizontalAlignment.Center;
            s.Margin = new Thickness(0, 6, 0, 0);

            panel.Children.Add(icon);
            panel.Children.Add(t);
            panel.Children.Add(s);
            dropHint.Child = panel;
            return dropHint;
        }

        void SetupDragAndDrop()
        {
            AllowDrop = true;
            DragEnter += OnDragOverFiles;
            DragOver += OnDragOverFiles;
            DragLeave += delegate { HideDropHint(); };
            Drop += OnFilesDropped;
        }

        void OnDragOverFiles(object sender, DragEventArgs e)
        {
            bool ok = false;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                {
                    foreach (var f in files)
                    {
                        if (f != null && f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { ok = true; break; }
                    }
                }
            }
            e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
            if (ok) ShowDropHint(); else HideDropHint();
        }

        void OnFilesDropped(object sender, DragEventArgs e)
        {
            HideDropHint();
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null) return;

                int added = 0;
                foreach (var f in files)
                {
                    if (string.IsNullOrEmpty(f)) continue;
                    if (Directory.Exists(f))
                    {
                        ShowToast("请拖入游戏 exe 文件本身，而不是文件夹");
                        continue;
                    }
                    if (!f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    Activate();
                    AddExe(f);
                    added++;
                }
                if (added == 0) ShowToast("只支持 .exe 文件，可以一次拖入多个");
                else Log.Info("拖拽添加 " + added + " 个文件");
            }
            catch (Exception ex)
            {
                Log.Error("拖拽添加失败", ex);
                ShowToast("添加失败：" + ex.Message);
            }
        }

        void ShowDropHint()
        {
            if (dropHint != null) dropHint.Visibility = Visibility.Visible;
        }

        void HideDropHint()
        {
            if (dropHint != null) dropHint.Visibility = Visibility.Collapsed;
        }

        public void EnableMenu()
        {
            try
            {
                ShellMenu.Install();
                if (ShellMenu.IsInstalled())
                {
                    ShowToast("右键菜单已启用：在 exe 上右键 →「添加到 GameLog」（Win11 先点「显示更多选项」）");
                }
                else
                {
                    ShowToast("启用失败：注册表写入被拒绝，请点「看不到菜单？」查看手动启用方法");
                }
            }
            catch (Exception ex)
            {
                Log.Error("启用右键菜单失败", ex);
                ShowToast("启用失败：" + ex.Message);
            }
            UpdateMenuBanner();
            UpdateStats();
        }

        void ShowMenuHelp()
        {
            string msg =
                "1. 在资源管理器里选中任意 .exe → 右键。\n" +
                "2. Windows 11 默认只显示精简菜单，请点「显示更多选项」，或按 Shift + F10。\n" +
                "3. 仍然看不到：说明注册表项没有写入成功（安全软件或受限环境拦截）。\n" +
                "   打开「设置」→「导出 .reg 文件」，双击合并即可；\n" +
                "   也可以直接右键添加，或把 exe 拖进本窗口。";
            Dialogs.Info(this, "为什么看不到右键菜单", msg);
        }

        void UpdateMenuBanner()
        {
            if (menuBanner == null) return;
            bool show = !ShellMenu.IsInstalled() && !store.Settings.HideMenuBanner;
            menuBanner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        FrameworkElement BuildHeader()
        {
            var header = new Grid();
            header.Margin = new Thickness(26, 16, 26, 0);
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // logo
            var iconBox = new Border();
            iconBox.Width = 36;
            iconBox.Height = 36;
            iconBox.CornerRadius = new CornerRadius(11);
            var grad = new LinearGradientBrush();
            grad.StartPoint = new Point(0, 0);
            grad.EndPoint = new Point(1, 1);
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x5B, 0x8C, 0xFF), 0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x8B, 0x5C, 0xF6), 1));
            grad.Freeze();
            iconBox.Background = grad;
            var g = Ui.TxtBlock("G", 18, Brushes.White, FontWeights.Bold);
            g.HorizontalAlignment = HorizontalAlignment.Center;
            g.VerticalAlignment = VerticalAlignment.Center;
            iconBox.Child = g;

            var titleStack = new StackPanel();
            titleStack.VerticalAlignment = VerticalAlignment.Center;
            var title = Ui.TxtBlock("GameLog", 18, Ui.Txt, FontWeights.Bold);
            title.Margin = new Thickness(0, 0, 0, 1);
            var sub = Ui.TxtBlock("游戏时光记录", 10.5, Ui.Dim);
            titleStack.Children.Add(title);
            titleStack.Children.Add(sub);

            var logo = Ui.HStack(12, iconBox, titleStack);
            logo.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(logo, 0);
            header.Children.Add(logo);

            // 搜索
            var search = Ui.SearchBox("搜索游戏名 / 路径…", 236, out searchBox);
            searchBox.TextChanged += delegate { Rebuild(); };
            search.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(search, 2);
            header.Children.Add(search);

            // 排序
            var sortHost = BuildSortBar();
            sortHost.VerticalAlignment = VerticalAlignment.Center;
            sortHost.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(sortHost, 3);
            header.Children.Add(sortHost);

            // 布局切换
            var layoutSwitch = BuildLayoutSwitch();
            layoutSwitch.VerticalAlignment = VerticalAlignment.Center;
            layoutSwitch.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(layoutSwitch, 4);
            header.Children.Add(layoutSwitch);

            // 立即扫描
            var scan = Ui.Btn("⟳  立即扫描", false, delegate { RunManualScan(); });
            scan.VerticalAlignment = VerticalAlignment.Center;
            scan.Margin = new Thickness(10, 0, 0, 0);
            scan.Height = 36;
            scan.MinWidth = 96;
            scan.ToolTip = "马上扫描一次运行中的程序，识别为游戏就自动加入";
            Grid.SetColumn(scan, 5);
            header.Children.Add(scan);

            // 添加
            var add = Ui.Btn("＋  添加游戏", true, delegate { ChooseExe(); });
            add.VerticalAlignment = VerticalAlignment.Center;
            add.Margin = new Thickness(8, 0, 0, 0);
            add.Height = 36;
            Grid.SetColumn(add, 6);
            header.Children.Add(add);

            // 设置
            var settings = Ui.Btn("设置", false, delegate { OpenSettings(); });
            settings.VerticalAlignment = VerticalAlignment.Center;
            settings.Margin = new Thickness(8, 0, 0, 0);
            settings.Height = 36;
            Grid.SetColumn(settings, 7);
            header.Children.Add(settings);

            return header;
        }

        FrameworkElement BuildStats()
        {
            var grid = new Grid();
            grid.Margin = new Thickness(26, 16, 26, 2);

            var statRow = new StackPanel();
            statRow.Orientation = Orientation.Horizontal;

            statRow.Children.Add(StatTile("游戏库", out statGames, 158));
            statRow.Children.Add(StatTile("今日游戏时间", out statToday, 178));
            statRow.Children.Add(StatTile("累计游戏时间", out statTotal, 178));

            monitorChip = new Border();
            monitorChip.CornerRadius = new CornerRadius(10);
            monitorChip.Background = Ui.BgSoft;
            monitorChip.BorderBrush = Ui.Line;
            monitorChip.BorderThickness = new Thickness(1);
            monitorChip.Padding = new Thickness(12, 8, 14, 8);
            monitorChip.Cursor = Cursors.Hand;
            monitorChip.VerticalAlignment = VerticalAlignment.Center;
            monitorChip.Margin = new Thickness(6, 0, 0, 0);
            monitorChip.ToolTip = "点击暂停 / 继续记录游戏时间";
            var chipRow = new StackPanel();
            chipRow.Orientation = Orientation.Horizontal;
            var dot = new Border();
            dot.Width = 8;
            dot.Height = 8;
            dot.CornerRadius = new CornerRadius(4);
            dot.Background = Ui.Good;
            dot.VerticalAlignment = VerticalAlignment.Center;
            dot.Name = "dot";
            monitorChipText = Ui.TxtBlock("正在记录", 12.5, Ui.Txt);
            monitorChipText.Margin = new Thickness(8, 0, 0, 0);
            chipRow.Children.Add(dot);
            chipRow.Children.Add(monitorChipText);
            monitorChip.Child = chipRow;
            monitorChip.MouseLeftButtonUp += delegate { app.ToggleMonitor(); UpdateStats(); };

            var spacer = new Border();
            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.Children.Add(statRow);
            Grid.SetColumn(monitorChip, 2);
            rowGrid.Children.Add(monitorChip);
            grid.Children.Add(rowGrid);
            return grid;
        }

        Border StatTile(string label, out TextBlock value, double width)
        {
            var b = new Border();
            b.Width = width;
            b.Height = 62;
            b.CornerRadius = new CornerRadius(11);
            b.Background = Ui.BgSoft;
            b.BorderBrush = Ui.LineSoft;
            b.BorderThickness = new Thickness(1);
            b.Margin = new Thickness(0, 0, 10, 0);
            b.Padding = new Thickness(14, 10, 14, 10);

            var sp = new StackPanel();
            sp.VerticalAlignment = VerticalAlignment.Center;
            var l = Ui.TxtBlock(label, 10.5, Ui.Dim);
            l.Margin = new Thickness(0, 0, 0, 3);
            value = Ui.TxtBlock("0", 17, Ui.Txt, FontWeights.SemiBold);
            sp.Children.Add(l);
            sp.Children.Add(value);
            b.Child = sp;
            return b;
        }

        FrameworkElement BuildSortBar()
        {
            var host = new Border();
            host.CornerRadius = new CornerRadius(8);
            host.Background = Ui.BgSoft;
            host.BorderBrush = Ui.Line;
            host.BorderThickness = new Thickness(1);
            host.Padding = new Thickness(3);

            var sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.Children.Add(SortButton("最近", "recent"));
            sp.Children.Add(SortButton("时长", "time"));
            sp.Children.Add(SortButton("名称", "name"));
            host.Child = sp;
            return host;
        }

        Border SortButton(string text, string mode)
        {
            var b = new Border();
            b.CornerRadius = new CornerRadius(6);
            b.Padding = new Thickness(11, 5, 11, 6);
            b.Cursor = Cursors.Hand;
            var t = Ui.TxtBlock(text, 12, Ui.Sub);
            b.Child = t;
            b.MouseLeftButtonUp += delegate
            {
                sortMode = mode;
                store.Settings.SortMode = mode;
                store.SaveSettings();
                UpdateSortVisuals();
                Rebuild();
            };
            b.MouseEnter += delegate
            {
                if (sortMode != mode) b.Background = Ui.LineSoft;
            };
            b.MouseLeave += delegate
            {
                if (sortMode != mode) b.Background = Brushes.Transparent;
            };
            sortButtons.Add(new KeyValuePair<string, Border>(mode, b));
            return b;
        }

        void UpdateSortVisuals()
        {
            foreach (var kv in sortButtons)
            {
                bool on = kv.Key == sortMode;
                kv.Value.Background = on ? Ui.AccentSoft : Brushes.Transparent;
                var tb = kv.Value.Child as TextBlock;
                if (tb != null)
                {
                    tb.Foreground = on ? Ui.Accent : Ui.Sub;
                    tb.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
                }
            }
        }

        FrameworkElement BuildEmptyState()
        {
            emptyHost = new Grid();
            emptyHost.Margin = new Thickness(0, 0, 0, 20);
            emptyHost.IsHitTestVisible = true;

            var panel = new StackPanel();
            panel.HorizontalAlignment = HorizontalAlignment.Center;
            panel.VerticalAlignment = VerticalAlignment.Center;
            panel.MaxWidth = 560;

            var iconBox = new Border();
            iconBox.Width = 64;
            iconBox.Height = 64;
            iconBox.CornerRadius = new CornerRadius(18);
            iconBox.Background = Ui.AccentSoft;
            iconBox.HorizontalAlignment = HorizontalAlignment.Center;
            iconBox.Margin = new Thickness(0, 0, 0, 18);
            var icon = Ui.TxtBlock("🎮", 28, Ui.Txt);
            icon.FontFamily = new FontFamily("Segoe UI Emoji");
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            iconBox.Child = icon;

            emptyTitle = Ui.TxtBlock("还没有添加游戏", 20, Ui.Txt, FontWeights.Bold);
            emptyTitle.HorizontalAlignment = HorizontalAlignment.Center;
            emptyTitle.Margin = new Thickness(0, 0, 0, 8);

            emptyHint = Ui.TxtBlock("在任意 .exe 上点右键，选择「添加到 GameLog」；也可以直接把 exe 拖进这个窗口。\n软件会自动搜索游戏中文名与封面，并记录每次运行时长。", 13, Ui.Sub);
            emptyHint.TextWrapping = TextWrapping.Wrap;
            emptyHint.TextAlignment = TextAlignment.Center;
            emptyHint.HorizontalAlignment = HorizontalAlignment.Center;
            emptyHint.LineHeight = 23;
            emptyHint.Margin = new Thickness(0, 0, 0, 8);

            var tip = Ui.TxtBlock("Windows 11：右键后先点「显示更多选项」，或按 Shift + F10。", 11.5, Ui.Dim);
            tip.HorizontalAlignment = HorizontalAlignment.Center;
            tip.Margin = new Thickness(0, 0, 0, 22);

            var add = Ui.Btn("＋  添加游戏", true, delegate { ChooseExe(); });
            add.HorizontalAlignment = HorizontalAlignment.Center;
            add.Height = 38;

            panel.Children.Add(iconBox);
            panel.Children.Add(emptyTitle);
            panel.Children.Add(emptyHint);
            panel.Children.Add(tip);
            panel.Children.Add(add);
            emptyHost.Children.Add(panel);
            return emptyHost;
        }

        FrameworkElement BuildStatusBar()
        {
            var bar = new Border();
            bar.Background = Ui.BgSoft;
            bar.BorderBrush = Ui.LineSoft;
            bar.BorderThickness = new Thickness(0, 1, 0, 0);
            bar.Padding = new Thickness(26, 0, 26, 0);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            statusLeft = Ui.TxtBlock("", 11.5, Ui.Sub);
            statusLeft.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(statusLeft);

            var right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            right.VerticalAlignment = VerticalAlignment.Center;

            statusRight = Ui.TxtBlock("", 11.5, Ui.Dim);
            right.Children.Add(statusRight);

            var openData = Ui.PlainBtn("数据目录", delegate
            {
                try { Process.Start("explorer.exe", "\"" + store.Root + "\""); } catch { }
            });
            openData.Height = 24;
            openData.FontSize = 11.5;
            openData.Margin = new Thickness(6, 0, 0, 0);
            right.Children.Add(openData);

            var ver = Ui.TxtBlock("v" + App.Version, 11.5, Ui.Dim);
            ver.Margin = new Thickness(10, 0, 0, 0);
            right.Children.Add(ver);

            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
            bar.Child = grid;
            Grid.SetRow(bar, 4);
            return bar;
        }

        // ---------------- 数据刷新 ----------------

        public void Rebuild()
        {
            if (cardsHost == null) return;
            cardsHost.Children.Clear();
            listHost.Children.Clear();
            cards.Clear();
            rows.Clear();

            var games = store.Snapshot();
            games.Sort(CompareGames);

            string filter = searchBox == null ? "" : (searchBox.Text ?? "").Trim();
            int shown = 0;

            // 列表布局的进度条以“最长时长”为满格
            maxSecondsForBars = 0;
            foreach (var g in games)
            {
                if (g.TotalSeconds > maxSecondsForBars) maxSecondsForBars = g.TotalSeconds;
            }

            bool listMode = layoutMode == "list";

            foreach (var g in games)
            {
                if (filter.Length > 0)
                {
                    bool match = (g.Name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                        || (g.ExePath ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!match) continue;
                }

                if (listMode)
                {
                    var row = new GameRow(store, g);
                    row.RowClicked += delegate (object s, EventArgs e)
                    {
                        SelectCard((s as GameRow).Game.Id);
                    };
                    row.RowDoubleClicked += delegate (object s, EventArgs e)
                    {
                        LaunchGame((s as GameRow).Game);
                    };
                    row.ContextMenu = BuildCardMenu(g);
                    row.SetSelected(g.Id == selectedId);
                    row.Refresh(monitor, maxSecondsForBars);
                    rows[g.Id] = row;
                    listHost.Children.Add(row);
                }
                else
                {
                    var card = new GameCard(store, g);
                    card.CardClicked += delegate (object s, EventArgs e)
                    {
                        SelectCard((s as GameCard).Game.Id);
                    };
                    card.CardDoubleClicked += delegate (object s, EventArgs e)
                    {
                        LaunchGame((s as GameCard).Game);
                    };
                    card.ContextMenu = BuildCardMenu(g);
                    card.SetSelected(g.Id == selectedId);
                    cards[g.Id] = card;
                    cardsHost.Children.Add(card);
                }
                shown++;
            }

            UpdateSortVisuals();
            UpdateLayoutButtons();
            UpdateStats();
            UpdateMenuBanner();

            bool empty = shown == 0;
            if (emptyHost != null)
            {
                emptyHost.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
                if (empty)
                {
                    if (games.Count == 0)
                    {
                        emptyTitle.Text = "还没有添加游戏";
                        emptyHint.Text = "在任意 .exe 上点右键，选择「添加到 GameLog」；也可以直接把 exe 拖进这个窗口。\n软件会自动搜索游戏中文名与封面，并记录每次运行时长。";
                        emptyHint.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        emptyTitle.Text = "没有匹配的游戏";
                        emptyHint.Text = "换个关键词试试，或清空搜索框。";
                        emptyHint.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        int CompareGames(GameInfo a, GameInfo b)
        {
            if (sortMode == "time") return b.TotalSeconds.CompareTo(a.TotalSeconds);
            if (sortMode == "name") return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            int c = b.LastPlayedTicks.CompareTo(a.LastPlayedTicks);
            if (c != 0) return c;
            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        public void UpdateLive()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(UpdateLive));
                return;
            }
            foreach (var kv in cards) kv.Value.Refresh(monitor);
            foreach (var kv in rows) kv.Value.Refresh(monitor, maxSecondsForBars);
            UpdateStats();
        }

        void UpdateStats()
        {
            var games = store.Snapshot();
            if (statGames != null) statGames.Text = games.Count + " 款";
            if (statToday != null) statToday.Text = Ui.Hours(store.TodaySeconds());
            if (statTotal != null) statTotal.Text = Ui.Hours(store.TotalSeconds());

            bool paused = app.MonitorPaused;
            if (monitorChipText != null)
            {
                monitorChipText.Text = paused ? "已暂停记录" : "正在记录";
                monitorChipText.Foreground = paused ? Ui.Sub : Ui.Txt;
                monitorChip.Background = paused ? Ui.BgSoft : Ui.Br("#152A22");
                monitorChip.BorderBrush = paused ? Ui.Line : Ui.Br("#1F4A3A");
                var sp = monitorChip.Child as StackPanel;
                if (sp != null && sp.Children.Count > 0)
                {
                    var dot = sp.Children[0] as Border;
                    if (dot != null) dot.Background = paused ? Ui.Dim : Ui.Good;
                }
            }

            if (statusLeft != null)
            {
                string label = monitor != null ? monitor.IntervalLabel : "每 5 分钟";
                statusLeft.Text = paused
                    ? "● 已暂停记录 · " + games.Count + " 款游戏已加入监控"
                    : "● 正在记录 · " + label + "检测 " + games.Count + " 款游戏的运行状态";
                statusLeft.Foreground = paused ? Ui.Dim : Ui.Sub;
            }

            if (statusRight != null)
            {
                bool menu = ShellMenu.IsInstalled();
                statusRight.Text = (menu ? "右键菜单：已启用" : "右键菜单：未启用") + "   ·   " + games.Count + " 款游戏";
            }
        }

        void SelectCard(string id)
        {
            selectedId = id;
            foreach (var kv in cards) kv.Value.SetSelected(kv.Key == id);
            foreach (var kv in rows) kv.Value.SetSelected(kv.Key == id);
        }

        public void SelectGame(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!cards.ContainsKey(id) && !rows.ContainsKey(id)) Rebuild();
            SelectCard(id);
            if (cards.ContainsKey(id)) cards[id].BringIntoView();
            else if (rows.ContainsKey(id)) rows[id].BringIntoView();
        }

        FrameworkElement BuildLayoutSwitch()
        {
            var host = new Border();
            host.CornerRadius = new CornerRadius(8);
            host.Background = Ui.BgSoft;
            host.BorderBrush = Ui.Line;
            host.BorderThickness = new Thickness(1);
            host.Padding = new Thickness(3);

            var sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.Children.Add(LayoutButton("▦", "cards", "卡片布局"));
            sp.Children.Add(LayoutButton("☰", "list", "列表布局"));
            host.Child = sp;
            return host;
        }

        Border LayoutButton(string glyph, string mode, string tip)
        {
            var b = new Border();
            b.CornerRadius = new CornerRadius(6);
            b.Padding = new Thickness(9, 4, 9, 5);
            b.Cursor = Cursors.Hand;
            b.ToolTip = tip;
            var t = Ui.TxtBlock(glyph, 13, Ui.Sub);
            b.Child = t;
            b.MouseLeftButtonUp += delegate { SetLayout(mode); };
            layoutButtons.Add(new KeyValuePair<string, Border>(mode, b));
            return b;
        }

        void SetLayout(string mode)
        {
            if (layoutMode == mode) return;
            layoutMode = mode;
            store.Settings.Layout = mode;
            store.SaveSettings();
            ApplyLayout();
            Rebuild();
            Log.Info("界面布局切换为：" + mode);
        }

        void ApplyLayout()
        {
            bool cardMode = layoutMode != "list";
            if (cardsHost != null) cardsHost.Visibility = cardMode ? Visibility.Visible : Visibility.Collapsed;
            if (listHost != null) listHost.Visibility = cardMode ? Visibility.Collapsed : Visibility.Visible;
        }

        void UpdateLayoutButtons()
        {
            foreach (var kv in layoutButtons)
            {
                bool on = kv.Key == layoutMode;
                kv.Value.Background = on ? Ui.AccentSoft : Brushes.Transparent;
                var tb = kv.Value.Child as TextBlock;
                if (tb != null)
                {
                    tb.Foreground = on ? Ui.Accent : Ui.Sub;
                    tb.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
                }
            }
        }

        ContextMenu BuildCardMenu(GameInfo g)
        {
            var cm = Ui.DarkMenu();
            cm.Items.Add(Ui.MItem("▶   启动游戏", delegate { LaunchGame(g); }, true));
            cm.Items.Add(Ui.MItem("在文件夹中显示", delegate { ShowInFolder(g); }, true));
            cm.Items.Add(new Separator());
            cm.Items.Add(Ui.MItem("重新识别游戏信息", delegate { ReIdentify(g); }, true));
            cm.Items.Add(Ui.MItem("更换封面…", delegate { ChangeCover(g); }, true));
            cm.Items.Add(Ui.MItem("重置时长", delegate { ResetGame(g); }, true));
            cm.Items.Add(new Separator());
            var del = Ui.MItem("从列表移除", delegate { RemoveGame(g); }, true);
            del.Foreground = Ui.Danger;
            cm.Items.Add(del);
            return cm;
        }

        // ---------------- 交互操作 ----------------

        public void ChooseExe()
        {
            var dlg = new OpenFileDialog();
            dlg.Title = "选择游戏 exe";
            dlg.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
            dlg.CheckFileExists = true;
            dlg.Multiselect = false;
            try
            {
                if (dlg.ShowDialog(this) == true) AddExe(dlg.FileName);
            }
            catch (Exception ex)
            {
                Log.Error("选择文件失败", ex);
            }
        }

        public void AddExe(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                path = path.Trim().Trim('"');
                if (!File.Exists(path))
                {
                    ShowToast("文件不存在：" + path);
                    return;
                }
                path = Path.GetFullPath(path);
                if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    ShowToast("只能添加 .exe 文件");
                    return;
                }

                var dup = store.FindByExe(path);
                if (dup != null)
                {
                    Rebuild();
                    SelectGame(dup.Id);
                    ShowToast("已经在游戏列表里了：《" + dup.Name + "》");
                    return;
                }

                Activate();
                var dlg = new IdentifyDialog(app, path);
                dlg.Owner = this;
                bool? ok = dlg.ShowDialog();
                if (ok == true && dlg.Result != null)
                {
                    var r = dlg.Result;
                    var g = store.Add(path, r.Name, r.CoverData, r.CoverUrl, r.PageUrl);
                    Rebuild();
                    SelectGame(g.Id);
                    ShowToast("已添加《" + g.Name + "》，开始记录游戏时间");
                }
            }
            catch (Exception ex)
            {
                Log.Error("添加游戏失败", ex);
                ShowToast("添加失败：" + ex.Message);
            }
        }

        void LaunchGame(GameInfo g)
        {
            try
            {
                var psi = new ProcessStartInfo(g.ExePath);
                psi.WorkingDirectory = Path.GetDirectoryName(g.ExePath);
                psi.UseShellExecute = true;
                Process.Start(psi);
                ShowToast("正在启动《" + g.Name + "》");
                Log.Info("启动游戏：" + g.ExePath);
            }
            catch (Exception ex)
            {
                Log.Error("启动游戏失败", ex);
                ShowToast("启动失败：" + ex.Message);
            }
        }

        void ShowInFolder(GameInfo g)
        {
            try { Process.Start("explorer.exe", "/select,\"" + g.ExePath + "\""); }
            catch (Exception ex) { Log.Error("打开文件夹失败", ex); }
        }

        void ReIdentify(GameInfo g)
        {
            var dlg = new IdentifyDialog(app, g.ExePath);
            dlg.Owner = this;
            dlg.Title = "重新识别 · " + g.Name;
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                var r = dlg.Result;
                if (!string.IsNullOrEmpty(r.Name)) g.Name = r.Name;
                if (!string.IsNullOrEmpty(r.PageUrl)) g.PageUrl = r.PageUrl;
                if (!string.IsNullOrEmpty(r.CoverUrl)) g.CoverUrl = r.CoverUrl;
                if (r.CoverData != null) store.SetCover(g, r.CoverData);
                store.Update(g);
                Rebuild();
                ShowToast("已更新为《" + g.Name + "》");
            }
        }

        void ChangeCover(GameInfo g)
        {
            var dlg = new OpenFileDialog();
            dlg.Title = "选择封面图片（建议 2:3 竖版）";
            dlg.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif|所有文件|*.*";
            if (dlg.ShowDialog(this) == true)
            {
                try
                {
                    byte[] data = File.ReadAllBytes(dlg.FileName);
                    if (data.Length > 8 * 1024 * 1024)
                    {
                        ShowToast("图片太大了（超过 8MB）");
                        return;
                    }
                    store.SetCover(g, data);
                    Rebuild();
                    ShowToast("封面已更新");
                }
                catch (Exception ex)
                {
                    Log.Error("更换封面失败", ex);
                    ShowToast("读取图片失败：" + ex.Message);
                }
            }
        }

        void ResetGame(GameInfo g)
        {
            if (Dialogs.Confirm(this, "重置时长", "确定要把《" + g.Name + "》的累计时长清零吗？\n此操作不可撤销。"))
            {
                store.ResetProgress(g.Id);
                Rebuild();
                ShowToast("已清零《" + g.Name + "》的时长");
            }
        }

        void RemoveGame(GameInfo g)
        {
            if (Dialogs.Confirm(this, "从列表移除", "确定要移除《" + g.Name + "》吗？\n本地记录的 " + Ui.DurCompact(g.TotalSeconds) + " 时长会一起删除。"))
            {
                store.Remove(g.Id);
                Rebuild();
                ShowToast("已移除《" + g.Name + "》");
            }
        }

        void OpenSettings()
        {
            var w = new SettingsWindow(app);
            w.Owner = this;
            w.ShowDialog();
            Rebuild();
        }

        public void RunManualScan()
        {
            if (app.Discovery == null)
            {
                ShowToast("自动发现模块不可用");
                return;
            }
            if (manualScanning)
            {
                ShowToast("已经在扫描了，请稍候…");
                return;
            }
            manualScanning = true;
            ShowToast("正在扫描运行中的程序…");
            Log.Info("手动触发扫描");

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                int added = -1;
                try { added = app.Discovery.ScanNow(); }
                catch (Exception ex) { Log.Error("手动扫描失败", ex); }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    manualScanning = false;
                    if (added < 0) ShowToast("扫描正在进行中");
                    else if (added == 0) ShowToast("扫描完成，没有发现新游戏");
                    else ShowToast("扫描完成，自动添加了 " + added + " 款游戏");
                    Rebuild();
                }));
            });
        }

        public void ShowToast(string message)
        {
            if (toast == null) return;
            toastText.Text = message;
            toast.Visibility = Visibility.Visible;
            if (toastTimer == null)
            {
                toastTimer = new DispatcherTimer();
                toastTimer.Interval = TimeSpan.FromSeconds(3.2);
                toastTimer.Tick += delegate
                {
                    toast.Visibility = Visibility.Collapsed;
                    toastTimer.Stop();
                };
            }
            toastTimer.Stop();
            toastTimer.Start();
        }

        void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (app.Exiting) return;
            if (store.Settings.CloseToTray && app.HasTray)
            {
                e.Cancel = true;
                Hide();
                app.NotifyHiddenToTray();
                return;
            }
            e.Cancel = true;
            app.ExitApp();
        }
    }
}

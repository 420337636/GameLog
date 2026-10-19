using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace GameLog
{
    /// <summary>设置窗口：常规、右键菜单、数据、关于。</summary>
    public class SettingsWindow : Window
    {
        readonly App app;
        readonly Store store;
        TextBlock menuStatus;
        Button installBtn;
        Button removeBtn;

        public SettingsWindow(App app)
        {
            this.app = app;
            this.store = app.Store;
            Title = "设置";
            Width = 680;
            Height = 800;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Ui.Bg;
            Ui.DarkTitleBar(this);
            Content = Build();
        }

        FrameworkElement Build()
        {
            var scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Padding = new Thickness(26, 18, 26, 18);

            var root = new StackPanel();

            var title = Ui.TxtBlock("设置", 19, Ui.Txt, FontWeights.Bold);
            title.Margin = new Thickness(0, 0, 0, 16);
            root.Children.Add(title);

            // 常规
            root.Children.Add(SectionTitle("常规"));
            var auto = Ui.Check("开机自动启动 GameLog", AutoStart.IsEnabled(), null);
            auto.Checked += delegate { AutoStart.SetEnabled(true); store.Settings.AutoStart = true; store.SaveSettings(); };
            auto.Unchecked += delegate { AutoStart.SetEnabled(false); store.Settings.AutoStart = false; store.SaveSettings(); };
            root.Children.Add(Row(auto, "登录 Windows 后自动在托盘启动，游戏时间不会漏记。"));

            var tray = Ui.Check("关闭窗口时最小化到托盘", store.Settings.CloseToTray, null);
            tray.Checked += delegate { store.Settings.CloseToTray = true; store.SaveSettings(); };
            tray.Unchecked += delegate { store.Settings.CloseToTray = false; store.SaveSettings(); };
            root.Children.Add(Row(tray, "关闭主窗口时继续在后台记录；从托盘菜单可以完全退出。"));

            var record = Ui.Check("记录游戏运行时长", store.Settings.MonitorEnabled, null);
            record.Checked += delegate { SetMonitorEnabled(true); };
            record.Unchecked += delegate { SetMonitorEnabled(false); };
            root.Children.Add(Row(record, "每隔 1 秒检查已添加的游戏是否在运行。"));

            root.Children.Add(SectionTitle("启动方式"));
            var launchRow = new StackPanel();
            launchRow.Orientation = Orientation.Horizontal;
            var shortcutBtn = Ui.Btn("创建桌面快捷方式", false, delegate
            {
                string p = ShellMenu.CreateDesktopShortcut();
                ShowToastSafe(string.IsNullOrEmpty(p) ? "创建快捷方式失败，请查看日志" : "已创建桌面快捷方式：" + p);
            });
            shortcutBtn.Height = 34;
            var unblockBtn = Ui.Btn("解除 SmartScreen 锁定", false, delegate
            {
                bool ok = ShellMenu.UnblockSelf();
                ShowToastSafe(ok ? "已清除「来自 Internet」标记，重新启动后不再被拦截" : "没有找到锁定标记（或没有权限），可按提示手动解除");
            });
            unblockBtn.Height = 34;
            unblockBtn.Margin = new Thickness(8, 0, 0, 0);
            launchRow.Children.Add(shortcutBtn);
            launchRow.Children.Add(unblockBtn);
            root.Children.Add(launchRow);

            var smartscreenTip = Ui.TxtBlock("双击 exe 时如果出现蓝色的「Windows 已保护你的电脑」：点「更多信息」→「仍要运行」即可；也可以用上面的桌面快捷方式启动。", 11.5, Ui.Dim);
            smartscreenTip.TextWrapping = TextWrapping.Wrap;
            smartscreenTip.Margin = new Thickness(0, 8, 0, 0);
            smartscreenTip.LineHeight = 19;
            root.Children.Add(smartscreenTip);

            root.Children.Add(SectionTitle("资源管理器右键菜单"));
            menuStatus = Ui.TxtBlock(ShellMenu.IsInstalled() ? "状态：已启用" : "状态：未启用", 12.5, ShellMenu.IsInstalled() ? Ui.Good : Ui.Dim);
            menuStatus.Margin = new Thickness(0, 0, 0, 8);
            root.Children.Add(menuStatus);

            string regPath = @"HKCU\Software\Classes\exefile\shell\" + ShellMenu.Verb;
            var regInfo = Ui.TxtBlock("注册表位置：" + regPath, 10.5, Ui.Dim);
            regInfo.FontFamily = Ui.Mono;
            regInfo.TextWrapping = TextWrapping.Wrap;
            regInfo.Margin = new Thickness(0, 0, 0, 8);
            root.Children.Add(regInfo);

            string installedCmd = ShellMenu.InstalledCommand();
            if (!string.IsNullOrEmpty(installedCmd))
            {
                var cmdInfo = Ui.TxtBlock("命令：" + installedCmd, 10.5, Ui.Dim);
                cmdInfo.FontFamily = Ui.Mono;
                cmdInfo.TextWrapping = TextWrapping.Wrap;
                cmdInfo.Margin = new Thickness(0, 0, 0, 8);
                root.Children.Add(cmdInfo);
            }

            var menuButtons = new StackPanel();
            menuButtons.Orientation = Orientation.Horizontal;
            installBtn = Ui.Btn("安装右键菜单", true, delegate { InstallMenu(); });
            installBtn.Height = 34;
            removeBtn = Ui.Btn("移除右键菜单", false, delegate { RemoveMenu(); });
            removeBtn.Height = 34;
            removeBtn.Margin = new Thickness(8, 0, 0, 0);
            menuButtons.Children.Add(installBtn);
            menuButtons.Children.Add(removeBtn);

            var regBtn = Ui.Btn("导出 .reg 文件", false, delegate
            {
                string p = ShellMenu.WriteRegFile(store.Root, true);
                if (!string.IsNullOrEmpty(p))
                {
                    ShowToastSafe("已导出注册表文件：" + p);
                    try { Process.Start("explorer.exe", "/select,\"" + p + "\""); } catch { }
                }
                else
                {
                    ShowToastSafe("导出失败，请查看日志");
                }
            });
            regBtn.Height = 34;
            regBtn.Margin = new Thickness(8, 0, 0, 0);
            menuButtons.Children.Add(regBtn);

            root.Children.Add(menuButtons);

            var hint = Ui.TxtBlock("注册在当前用户下，不需要管理员权限。Windows 11 的右键菜单需要先点「显示更多选项」（或按 Shift + F10）才能看到「添加到 GameLog」。", 11.5, Ui.Dim);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 8, 0, 0);
            hint.LineHeight = 19;
            root.Children.Add(hint);

            // 数据
            root.Children.Add(SectionTitle("数据"));
            var pathText = Ui.TxtBlock(store.Root, 11.5, Ui.Sub);
            pathText.TextWrapping = TextWrapping.Wrap;
            pathText.Margin = new Thickness(0, 0, 0, 8);
            root.Children.Add(pathText);

            var dataButtons = new StackPanel();
            dataButtons.Orientation = Orientation.Horizontal;
            var openData = Ui.Btn("打开数据目录", false, delegate
            {
                try { Process.Start("explorer.exe", "\"" + store.Root + "\""); } catch { }
            });
            openData.Height = 34;
            var openLog = Ui.Btn("查看日志", false, delegate
            {
                try { Process.Start("notepad.exe", "\"" + Log.FilePath + "\""); } catch { }
            });
            openLog.Height = 34;
            openLog.Margin = new Thickness(8, 0, 0, 0);
            var export = Ui.Btn("导出 CSV", false, delegate { ExportCsv(); });
            export.Height = 34;
            export.Margin = new Thickness(8, 0, 0, 0);
            dataButtons.Children.Add(openData);
            dataButtons.Children.Add(openLog);
            dataButtons.Children.Add(export);
            root.Children.Add(dataButtons);

            var clear = Ui.Btn("清空全部时长", false, delegate { ClearAll(); });
            clear.Height = 34;
            clear.Margin = new Thickness(0, 10, 0, 0);
            clear.HorizontalAlignment = HorizontalAlignment.Left;
            clear.Foreground = Ui.Danger;
            root.Children.Add(clear);

            // 关于
            root.Children.Add(SectionTitle("关于"));
            var about = Ui.TxtBlock(
                "GameLog v" + App.Version + " · 单文件绿色版，无需安装运行库\n" +
                "游戏中文名与封面来自百度搜索和游民星空（gamersky.com），仅供个人记录使用。\n" +
                "时长按进程运行时间统计，每 30 秒自动保存；联网识别失败时可在卡片右键菜单里重新识别。",
                11.5, Ui.Dim);
            about.TextWrapping = TextWrapping.Wrap;
            about.LineHeight = 20;
            root.Children.Add(about);

            scroll.Content = root;

            var host = new Grid();
            host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            host.Children.Add(scroll);

            var footer = new Border();
            footer.Background = Ui.BgSoft;
            footer.BorderBrush = Ui.LineSoft;
            footer.BorderThickness = new Thickness(0, 1, 0, 0);
            footer.Padding = new Thickness(26, 12, 26, 12);
            var close = Ui.Btn("关闭", false, delegate { Close(); });
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.MinWidth = 96;
            footer.Child = close;
            Grid.SetRow(footer, 1);
            host.Children.Add(footer);

            return host;
        }

        void SetMonitorEnabled(bool enabled)
        {
            store.Settings.MonitorEnabled = enabled;
            store.SaveSettings();
            app.Monitor.Paused = !enabled;
            if (app.Tray != null) app.Tray.SetPaused(app.Monitor.Paused);
        }

        void InstallMenu()
        {
            try
            {
                ShellMenu.Install();
                menuStatus.Text = "状态：已启用";
                menuStatus.Foreground = Ui.Good;
                app.Window.Rebuild();
                app.Window.ShowToast("右键菜单已安装：在任意 exe 上右键即可添加");
            }
            catch (Exception ex)
            {
                menuStatus.Text = "安装失败：" + ex.Message;
                menuStatus.Foreground = Ui.Danger;
            }
        }

        void RemoveMenu()
        {
            ShellMenu.Uninstall();
            menuStatus.Text = "状态：未启用";
            menuStatus.Foreground = Ui.Dim;
            app.Window.Rebuild();
        }

        void ExportCsv()
        {
            try
            {
                string file = Path.Combine(store.Root, "gametime-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".csv");
                var sb = new StringBuilder();
                sb.AppendLine("名称,总秒数,总时长,今日秒数,最后运行,exe 路径");
                foreach (var g in store.Snapshot())
                {
                    sb.AppendLine(string.Join(",",
                        Csv(g.Name),
                        ((long)g.TotalSeconds).ToString(),
                        Csv(Ui.DurCompact(g.TotalSeconds)),
                        ((long)g.TodaySeconds).ToString(),
                        Csv(g.LastPlayed > DateTime.MinValue ? g.LastPlayed.ToString("yyyy-MM-dd HH:mm") : ""),
                        Csv(g.ExePath)));
                }
                File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
                ShowToastSafe("已导出：" + file);
                try { Process.Start("explorer.exe", "/select,\"" + file + "\""); } catch { }
            }
            catch (Exception ex)
            {
                ShowToastSafe("导出失败：" + ex.Message);
            }
        }

        static string Csv(string s)
        {
            if (s == null) return "";
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        void ClearAll()
        {
            if (!Dialogs.Confirm(this, "清空全部时长", "确定要清空所有游戏的累计时长吗？\n游戏列表、封面和名称会保留，此操作不可撤销。"))
                return;
            foreach (var g in store.Snapshot()) store.ResetProgress(g.Id);
            app.Window.Rebuild();
            ShowToastSafe("已清空全部游戏时长");
        }

        void ShowToastSafe(string msg)
        {
            try { app.Window.ShowToast(msg); } catch { }
        }

        static TextBlock SectionTitle(string text)
        {
            var t = Ui.TxtBlock(text, 12.5, Ui.Accent, FontWeights.SemiBold);
            t.Margin = new Thickness(0, 18, 0, 8);
            return t;
        }

        static FrameworkElement Row(UIElement control, string description)
        {
            var sp = new StackPanel();
            sp.Margin = new Thickness(0, 0, 0, 8);
            sp.Children.Add(control);
            var d = Ui.TxtBlock(description, 11, Ui.Sub);
            d.TextWrapping = TextWrapping.Wrap;
            d.Margin = new Thickness(27, 3, 0, 0);
            sp.Children.Add(d);
            return sp;
        }
    }
}

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GameLog
{
    /// <summary>托盘图标（WinForms NotifyIcon）。</summary>
    public class TrayIcon : IDisposable
    {
        readonly NotifyIcon icon;
        readonly ToolStripMenuItem pauseItem;
        readonly ToolStripMenuItem autoStartItem;

        public TrayIcon(string title, Action show, Action toggleMonitor, Func<bool> monitorEnabled, Action exit, string iconPath)
        {
            Icon ico = null;
            try
            {
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath)) ico = new Icon(iconPath);
            }
            catch { }
            if (ico == null) ico = SystemIcons.Application;

            var menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;

            var open = new ToolStripMenuItem("打开 GameLog");
            open.Click += delegate { show(); };
            menu.Items.Add(open);
            menu.Items.Add(new ToolStripSeparator());

            pauseItem = new ToolStripMenuItem("暂停记录");
            pauseItem.Click += delegate
            {
                toggleMonitor();
                pauseItem.Checked = !monitorEnabled();
            };
            menu.Items.Add(pauseItem);

            autoStartItem = new ToolStripMenuItem("开机自启");
            autoStartItem.Checked = AutoStart.IsEnabled();
            autoStartItem.Click += delegate
            {
                bool next = !AutoStart.IsEnabled();
                AutoStart.SetEnabled(next);
                autoStartItem.Checked = next;
            };
            menu.Items.Add(autoStartItem);
            menu.Items.Add(new ToolStripSeparator());

            var quit = new ToolStripMenuItem("退出");
            quit.Click += delegate { exit(); };
            menu.Items.Add(quit);

            icon = new NotifyIcon();
            icon.Icon = ico;
            icon.Text = title;
            icon.Visible = true;
            icon.ContextMenuStrip = menu;
            icon.DoubleClick += delegate { show(); };
        }

        public void SetPaused(bool paused)
        {
            pauseItem.Checked = paused;
            icon.Text = paused ? "GameLog（已暂停记录）" : "GameLog";
        }

        public void ShowBalloon(string title, string text)
        {
            try
            {
                icon.BalloonTipTitle = title;
                icon.BalloonTipText = text;
                icon.ShowBalloonTip(3000);
            }
            catch { }
        }

        public void Dispose()
        {
            try
            {
                icon.Visible = false;
                icon.Dispose();
            }
            catch { }
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameLog
{
    /// <summary>深色风格的确认对话框。</summary>
    public static class Dialogs
    {
        public static bool Confirm(Window owner, string title, string message)
        {
            return Confirm(owner, title, message, "确定", "取消");
        }

        public static bool Confirm(Window owner, string title, string message, string okText, string cancelText)
        {
            bool confirmed = false;

            var w = new Window();
            w.Title = title;
            w.Width = 440;
            w.SizeToContent = SizeToContent.Height;
            w.ResizeMode = ResizeMode.NoResize;
            w.WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
            w.Background = Ui.Bg;
            w.Foreground = Ui.Txt;
            w.FontFamily = Ui.Font;
            w.UseLayoutRounding = true;
            w.SnapsToDevicePixels = true;
            if (owner != null) w.Owner = owner;
            Ui.DarkTitleBar(w);

            var root = new StackPanel();
            root.Margin = new Thickness(26, 22, 26, 18);

            var msg = Ui.TxtBlock(message, 13, Ui.Sub);
            msg.TextWrapping = TextWrapping.Wrap;
            msg.LineHeight = 22;
            msg.HorizontalAlignment = HorizontalAlignment.Left;
            root.Children.Add(msg);

            var row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.HorizontalAlignment = HorizontalAlignment.Right;
            row.Margin = new Thickness(0, 20, 0, 0);

            var cancel = Ui.Btn(cancelText, false, delegate { w.Close(); });
            cancel.MinWidth = 88;
            cancel.Height = 34;

            var ok = Ui.Btn(okText, true, delegate
            {
                confirmed = true;
                w.Close();
            });
            ok.MinWidth = 88;
            ok.Height = 34;
            ok.Margin = new Thickness(8, 0, 0, 0);

            row.Children.Add(cancel);
            row.Children.Add(ok);
            root.Children.Add(row);

            w.Content = root;
            w.ShowDialog();
            return confirmed;
        }
        public static void Info(Window owner, string title, string message)
        {
            var w = new Window();
            w.Title = title;
            w.Width = 520;
            w.SizeToContent = SizeToContent.Height;
            w.ResizeMode = ResizeMode.NoResize;
            w.WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
            w.Background = Ui.Bg;
            w.Foreground = Ui.Txt;
            w.FontFamily = Ui.Font;
            w.UseLayoutRounding = true;
            w.SnapsToDevicePixels = true;
            if (owner != null) w.Owner = owner;
            Ui.DarkTitleBar(w);

            var root = new StackPanel();
            root.Margin = new Thickness(26, 22, 26, 18);

            var msg = Ui.TxtBlock(message, 12.5, Ui.Sub);
            msg.TextWrapping = TextWrapping.Wrap;
            msg.LineHeight = 22;
            root.Children.Add(msg);

            var ok = Ui.Btn("知道了", true, delegate { w.Close(); });
            ok.MinWidth = 96;
            ok.HorizontalAlignment = HorizontalAlignment.Right;
            ok.Margin = new Thickness(0, 20, 0, 0);
            root.Children.Add(ok);

            w.Content = root;
            w.ShowDialog();
        }
    }
}

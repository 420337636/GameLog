using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace GameLog
{
    /// <summary>界面主题与控件工厂（深色风格）。</summary>
    public static class Ui
    {
        public static readonly Brush Bg = Br("#0E1117");
        public static readonly Brush BgSoft = Br("#131821");
        public static readonly Brush Card = Br("#191F29");
        public static readonly Brush CardHover = Br("#202836");
        public static readonly Brush Line = Br("#252D3B");
        public static readonly Brush LineSoft = Br("#1E242F");
        public static readonly Brush Txt = Br("#E9EEF7");
        public static readonly Brush Sub = Br("#97A3B6");
        public static readonly Brush Dim = Br("#6C7889");
        public static readonly Brush Accent = Br("#5B8CFF");
        public static readonly Brush AccentDeep = Br("#3D6BE0");
        public static readonly Brush AccentSoft = Br("#22304A");
        public static readonly Brush Good = Br("#3DDC97");
        public static readonly Brush Warn = Br("#FFB454");
        public static readonly Brush Danger = Br("#FF6172");

        public static readonly FontFamily Font = new FontFamily("Microsoft YaHei UI, Microsoft YaHei, Segoe UI, Arial");
        public static readonly FontFamily Mono = new FontFamily("Consolas, Cascadia Mono, Courier New");

        public static SolidColorBrush Br(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static void Apply(Window w)
        {
            w.Background = Bg;
            w.Foreground = Txt;
            w.FontFamily = Font;
            w.UseLayoutRounding = true;
            w.SnapsToDevicePixels = true;
            w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        public static TextBlock TxtBlock(string text, double size, Brush fg)
        {
            return TxtBlock(text, size, fg, FontWeights.Normal);
        }

        public static TextBlock TxtBlock(string text, double size, Brush fg, FontWeight weight)
        {
            var t = new TextBlock();
            t.Text = text;
            t.FontSize = size;
            t.Foreground = fg;
            t.FontWeight = weight;
            t.FontFamily = Font;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            t.VerticalAlignment = VerticalAlignment.Center;
            return t;
        }

        public static Border Panel(double radius, Brush bg)
        {
            var b = new Border();
            b.CornerRadius = new CornerRadius(radius);
            b.Background = bg;
            b.SnapsToDevicePixels = true;
            return b;
        }

        public static Border Panel(double radius, Brush bg, Brush border, double thickness)
        {
            var b = Panel(radius, bg);
            b.BorderBrush = border;
            b.BorderThickness = new Thickness(thickness);
            return b;
        }

        public static Border HStack(double gap, params UIElement[] items)
        {
            var sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.VerticalAlignment = VerticalAlignment.Center;
            bool first = true;
            foreach (var it in items)
            {
                if (it == null) continue;
                var fe = it as FrameworkElement;
                if (fe != null && !first) fe.Margin = new Thickness(gap, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom);
                sp.Children.Add(it);
                first = false;
            }
            var host = new Border();
            host.Child = sp;
            return host;
        }

        public static FrameworkElement Spacer()
        {
            return new Border();
        }

        static ControlTemplate ButtonTemplate(Brush bg, Brush border, bool hasBorder, double sidePadding)
        {
            var bd = new FrameworkElementFactory(typeof(Border), "bd");
            bd.SetValue(Border.BackgroundProperty, bg);
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            bd.SetValue(Border.SnapsToDevicePixelsProperty, true);
            if (hasBorder)
            {
                bd.SetValue(Border.BorderBrushProperty, border);
                bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            }
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetValue(FrameworkElement.MarginProperty, new Thickness(sidePadding, 0, sidePadding, 0));
            bd.AppendChild(cp);
            var tpl = new ControlTemplate(typeof(Button));
            tpl.VisualTree = bd;
            return tpl;
        }

        public static Button Btn(string text, bool primary, RoutedEventHandler click)
        {
            var b = new Button();
            b.Content = text;
            b.Foreground = primary ? Brushes.White : Txt;
            b.Background = Brushes.Transparent;
            b.BorderThickness = new Thickness(0);
            b.Cursor = Cursors.Hand;
            b.FontFamily = Font;
            b.FontSize = 13;
            b.FocusVisualStyle = null;
            b.Template = primary ? ButtonTemplate(Accent, null, false, 16) : ButtonTemplate(BgSoft, Line, true, 16);
            b.Height = 34;
            b.MinWidth = 78;
            b.Padding = new Thickness(14, 0, 14, 0);
            b.MouseEnter += delegate { b.Opacity = 0.84; };
            b.MouseLeave += delegate { b.Opacity = 1.0; };
            if (click != null) b.Click += click;
            return b;
        }

        public static Button PlainBtn(string text, RoutedEventHandler click)
        {
            var b = new Button();
            b.Content = text;
            b.Foreground = Sub;
            b.Background = Brushes.Transparent;
            b.BorderThickness = new Thickness(0);
            b.Cursor = Cursors.Hand;
            b.FontFamily = Font;
            b.FontSize = 13;
            b.FocusVisualStyle = null;
            b.Template = ButtonTemplate(Brushes.Transparent, null, false, 8);
            b.Height = 30;
            b.Padding = new Thickness(8, 0, 8, 0);
            b.MouseEnter += delegate { b.Foreground = Txt; };
            b.MouseLeave += delegate { b.Foreground = Sub; };
            if (click != null) b.Click += click;
            return b;
        }

        static ControlTemplate InputTemplate()
        {
            var bd = new FrameworkElementFactory(typeof(Border), "bd");
            bd.SetValue(Border.BackgroundProperty, Br("#161C26"));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            bd.SetValue(Border.BorderBrushProperty, Line);
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            bd.SetValue(Border.PaddingProperty, new Thickness(10, 0, 10, 0));
            var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
            host.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(host);
            var tpl = new ControlTemplate(typeof(TextBox));
            tpl.VisualTree = bd;
            return tpl;
        }

        public static TextBox Input()
        {
            var t = new TextBox();
            t.Background = Brushes.Transparent;
            t.Foreground = Txt;
            t.BorderThickness = new Thickness(0);
            t.FontSize = 13;
            t.FontFamily = Font;
            t.Height = 34;
            t.CaretBrush = Txt;
            t.SelectionBrush = Accent;
            t.Template = InputTemplate();
            t.VerticalContentAlignment = VerticalAlignment.Center;
            return t;
        }

        public static Grid SearchBox(string hint, double width, out TextBox box)
        {
            var g = new Grid();
            g.Width = width;
            box = Input();
            var input = box;
            input.ToolTip = hint;
            var ph = TxtBlock(hint, 13, Dim);
            ph.Margin = new Thickness(12, 0, 0, 0);
            ph.IsHitTestVisible = false;
            ph.HorizontalAlignment = HorizontalAlignment.Left;
            input.TextChanged += delegate
            {
                ph.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            };
            g.Children.Add(box);
            g.Children.Add(ph);
            return g;
        }

        static ControlTemplate CheckTemplate()
        {
            var sp = new FrameworkElementFactory(typeof(StackPanel));
            sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            var box = new FrameworkElementFactory(typeof(Border), "box");
            box.SetValue(FrameworkElement.WidthProperty, 18.0);
            box.SetValue(FrameworkElement.HeightProperty, 18.0);
            box.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            box.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            box.SetValue(Border.BorderBrushProperty, Dim);
            box.SetValue(Border.BackgroundProperty, Br("#161C26"));
            box.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            var mark = new FrameworkElementFactory(typeof(TextBlock), "mark");
            mark.SetValue(TextBlock.TextProperty, "✓");
            mark.SetValue(TextBlock.FontSizeProperty, 12.0);
            mark.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            mark.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            mark.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            mark.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            box.AppendChild(mark);

            sp.AppendChild(box);
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(FrameworkElement.MarginProperty, new Thickness(9, 0, 0, 0));
            cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            sp.AppendChild(cp);

            var tpl = new ControlTemplate(typeof(CheckBox));
            tpl.VisualTree = sp;
            var trig = new Trigger();
            trig.Property = ToggleButton.IsCheckedProperty;
            trig.Value = true;
            trig.Setters.Add(new Setter(Border.BackgroundProperty, Accent, "box"));
            trig.Setters.Add(new Setter(Border.BorderBrushProperty, Accent, "box"));
            trig.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "mark"));
            tpl.Triggers.Add(trig);
            return tpl;
        }

        public static CheckBox Check(string label, bool isChecked, RoutedEventHandler changed)
        {
            var c = new CheckBox();
            c.Content = label;
            c.IsChecked = isChecked;
            c.Foreground = Txt;
            c.FontFamily = Font;
            c.FontSize = 13;
            c.Cursor = Cursors.Hand;
            c.Template = CheckTemplate();
            c.VerticalAlignment = VerticalAlignment.Center;
            if (changed != null) c.Click += changed;
            return c;
        }

        public static Style MenuItemStyle()
        {
            var bd = new FrameworkElementFactory(typeof(Border), "bd");
            bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            bd.SetValue(Border.PaddingProperty, new Thickness(12, 7, 14, 7));
            bd.SetValue(FrameworkElement.MinWidthProperty, 170.0);

            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            var headerBinding = new System.Windows.Data.Binding("Header");
            headerBinding.RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent);
            cp.SetBinding(ContentPresenter.ContentProperty, headerBinding);
            bd.AppendChild(cp);

            var tpl = new ControlTemplate(typeof(MenuItem));
            tpl.VisualTree = bd;

            var hover = new Trigger();
            hover.Property = MenuItem.IsHighlightedProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Border.BackgroundProperty, AccentSoft, "bd"));
            tpl.Triggers.Add(hover);

            var disabled = new Trigger();
            disabled.Property = UIElement.IsEnabledProperty;
            disabled.Value = false;
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4, "bd"));
            tpl.Triggers.Add(disabled);

            var st = new Style(typeof(MenuItem));
            st.Setters.Add(new Setter(MenuItem.TemplateProperty, tpl));
            st.Setters.Add(new Setter(MenuItem.ForegroundProperty, Txt));
            st.Setters.Add(new Setter(MenuItem.FontFamilyProperty, Font));
            st.Setters.Add(new Setter(MenuItem.FontSizeProperty, 13.0));
            st.Setters.Add(new Setter(MenuItem.CursorProperty, Cursors.Hand));
            return st;
        }

        static Style SeparatorStyle()
        {
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(FrameworkElement.HeightProperty, 1.0);
            bd.SetValue(Border.BackgroundProperty, Line);
            bd.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 5, 8, 5));
            var tpl = new ControlTemplate(typeof(Separator));
            tpl.VisualTree = bd;
            var st = new Style(typeof(Separator));
            st.Setters.Add(new Setter(Separator.TemplateProperty, tpl));
            return st;
        }

        static ControlTemplate MenuTemplate()
        {
            var bd = new FrameworkElementFactory(typeof(Border), "bd");
            bd.SetValue(Border.BackgroundProperty, Br("#1B2230"));
            bd.SetValue(Border.BorderBrushProperty, Line);
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            bd.SetValue(Border.PaddingProperty, new Thickness(6));

            var sv = new FrameworkElementFactory(typeof(ScrollViewer));
            sv.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            sv.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);

            var items = new FrameworkElementFactory(typeof(ItemsPresenter));
            sv.AppendChild(items);
            bd.AppendChild(sv);

            var tpl = new ControlTemplate(typeof(ContextMenu));
            tpl.VisualTree = bd;
            return tpl;
        }

        public static MenuItem MItem(string header, Action action, bool enabled)
        {
            var mi = new MenuItem();
            mi.Header = header;
            mi.IsEnabled = enabled;
            mi.Style = MenuItemStyle();
            if (action != null) mi.Click += delegate { action(); };
            return mi;
        }

        public static ContextMenu DarkMenu()
        {
            var cm = new ContextMenu();
            cm.Foreground = Txt;
            cm.FontFamily = Font;
            cm.SnapsToDevicePixels = true;
            cm.Template = MenuTemplate();
            cm.Resources[typeof(Separator)] = SeparatorStyle();
            return cm;
        }

        public static string Dur(double seconds)
        {
            if (seconds < 1) return "尚未开始";
            long t = (long)Math.Round(seconds);
            if (t < 60) return "不到 1 分钟";
            long m = t / 60;
            if (m < 60) return m + " 分钟";
            long h = m / 60;
            long rem = m % 60;
            if (rem == 0) return h + " 小时";
            return h + " 小时 " + rem + " 分";
        }

        public static string DurCompact(double seconds)
        {
            if (seconds < 1) return "0 分钟";
            long m = (long)Math.Round(seconds / 60.0);
            if (m < 1) return "不到 1 分钟";
            if (m < 60) return m + " 分钟";
            long h = m / 60;
            long rem = m % 60;
            return rem == 0 ? h + " 小时" : h + " 小时 " + rem + " 分";
        }

        public static string Ago(DateTime dt)
        {
            if (dt <= DateTime.MinValue || dt.Year < 2000) return "还没玩过";
            var span = DateTime.Now - dt;
            if (span.TotalMinutes < 2) return "刚刚还在玩";
            if (span.TotalMinutes < 60) return (int)span.TotalMinutes + " 分钟前玩过";
            if (span.TotalHours < 24) return (int)span.TotalHours + " 小时前玩过";
            if (span.TotalDays < 30) return (int)span.TotalDays + " 天前玩过";
            return dt.ToString("yyyy-MM-dd") + " 玩过";
        }

        static readonly System.Collections.Generic.Dictionary<string, BitmapImage> ImageCache =
            new System.Collections.Generic.Dictionary<string, BitmapImage>();
        static readonly object ImageGate = new object();

        public static BitmapImage LoadImage(string path, int decodeWidth)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            string key;
            try { key = path + "|" + decodeWidth + "|" + File.GetLastWriteTimeUtc(path).Ticks; }
            catch { key = path + "|" + decodeWidth; }
            lock (ImageGate)
            {
                BitmapImage cached;
                if (ImageCache.TryGetValue(key, out cached)) return cached;
            }
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(path, UriKind.Absolute);
                if (decodeWidth > 0) bi.DecodePixelWidth = decodeWidth;
                bi.EndInit();
                bi.Freeze();
                lock (ImageGate)
                {
                    if (!ImageCache.ContainsKey(key)) ImageCache[key] = bi;
                }
                return bi;
            }
            catch { return null; }
        }

        public static Brush CoverBrush(Store store, GameInfo g)
        {
            string path = store.CoverPath(g);
            var img = LoadImage(path, 520);
            if (img != null)
            {
                var ib = new ImageBrush(img);
                ib.Stretch = Stretch.UniformToFill;
                ib.Freeze();
                return ib;
            }
            // 没有封面时的渐变占位
            var grad = new LinearGradientBrush();
            grad.StartPoint = new Point(0, 0);
            grad.EndPoint = new Point(1, 1);
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x2A, 0x38, 0x55), 0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x1A, 0x22, 0x33), 1));
            grad.Freeze();
            return grad;
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        /// <summary>Win10/11 深色标题栏。</summary>
        public static void DarkTitleBar(Window w)
        {
            try
            {
                var helper = new WindowInteropHelper(w);
                if (helper.Handle == IntPtr.Zero)
                {
                    w.SourceInitialized += delegate { ApplyDarkTitle(w); };
                    return;
                }
                ApplyDarkTitle(w);
            }
            catch { }
        }

        static void ApplyDarkTitle(Window w)
        {
            try
            {
                IntPtr h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) return;
                int one = 1;
                if (DwmSetWindowAttribute(h, 20, ref one, 4) != 0) DwmSetWindowAttribute(h, 19, ref one, 4);
            }
            catch { }
        }
    }
}

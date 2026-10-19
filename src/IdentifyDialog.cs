using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameLog
{
    /// <summary>添加/重新识别游戏时的确认窗口：显示识别进度、候选结果与封面预览。</summary>
    public class IdentifyDialog : Window
    {
        readonly App app;
        readonly string exePath;

        public IdentifyResult Result;

        TextBlock statusText;
        TextBlock nameText;
        TextBlock pageText;
        TextBlock scoreText;
        TextBlock pathText;
        Border coverBox;
        ProgressBar bar;
        ListBox list;
        TextBox manualBox;
        Button useBtn;
        Button skipBtn;
        TextBlock manualHint;

        IdentifyResult current;
        string selectedSlug;
        readonly Dictionary<string, byte[]> coverCache = new Dictionary<string, byte[]>();
        bool busy;
        bool previewMode;
        string previewCoverPath;

        /// <summary>仅用于离线渲染预览：填入示例候选，不发起网络请求。</summary>
        public void FillForPreview(List<GameCandidate> candidates, string coverPath)
        {
            previewMode = true;
            previewCoverPath = coverPath;
            var r = new IdentifyResult();
            r.Success = true;
            r.Message = "已匹配到游民星空《" + (candidates.Count > 0 ? candidates[0].Name : "") + "》，封面已获取";
            current = r;
            statusText.Text = r.Message;
            manualHint.Text = "";
            bar.Visibility = Visibility.Collapsed;
            FillCandidates(candidates);
            if (candidates.Count > 0) list.SelectedIndex = 0;
            useBtn.IsEnabled = candidates.Count > 0;
        }

        void ApplyCoverFile(string path)
        {
            var bi = Ui.LoadImage(path, 360);
            if (bi == null) return;
            var ib = new ImageBrush(bi);
            ib.Stretch = Stretch.UniformToFill;
            ib.Freeze();
            coverBox.Background = ib;
        }

        public IdentifyDialog(App app, string exePath)
        {
            this.app = app;
            this.exePath = exePath;

            Title = "识别游戏";
            Width = 780;
            Height = 588;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Ui.Bg;
            Ui.DarkTitleBar(this);
            Content = Build();

            Loaded += delegate { StartIdentify(); };
        }

        FrameworkElement Build()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 头部
            var head = new StackPanel();
            head.Margin = new Thickness(24, 18, 24, 0);
            var t = Ui.TxtBlock("识别游戏信息", 17, Ui.Txt, FontWeights.Bold);
            t.Margin = new Thickness(0, 0, 0, 4);
            head.Children.Add(t);
            pathText = Ui.TxtBlock(exePath, 11.5, Ui.Dim);
            pathText.ToolTip = exePath;
            head.Children.Add(pathText);
            root.Children.Add(head);

            bar = new ProgressBar();
            bar.IsIndeterminate = true;
            bar.Height = 3;
            bar.Margin = new Thickness(24, 12, 24, 0);
            bar.Foreground = Ui.Accent;
            bar.Background = Ui.Br("#1B2230");
            bar.BorderThickness = new Thickness(0);
            bar.Visibility = Visibility.Collapsed;
            Grid.SetRow(bar, 0);
            bar.VerticalAlignment = VerticalAlignment.Bottom;
            root.Children.Add(bar);

            // 主体
            var body = new Grid();
            body.Margin = new Thickness(24, 14, 24, 0);
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            var left = new Grid();
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            body.Children.Add(left);

            var manualRow = new Grid();
            manualRow.Margin = new Thickness(0, 0, 0, 8);
            manualRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            manualRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var manualSearch = Ui.SearchBox("手动输入游戏名搜索…", 240, out manualBox);
            manualBox.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) ManualSearch();
            };
            manualRow.Children.Add(manualSearch);
            var searchBtn = Ui.Btn("搜索", false, delegate { ManualSearch(); });
            searchBtn.Height = 34;
            searchBtn.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(searchBtn, 1);
            manualRow.Children.Add(searchBtn);
            left.Children.Add(manualRow);

            list = new ListBox();
            list.Background = Brushes.Transparent;
            list.BorderThickness = new Thickness(0);
            list.Foreground = Ui.Txt;
            list.SelectionChanged += delegate { OnCandidateSelected(); };
            list.Margin = new Thickness(0);
            list.Padding = new Thickness(0);
            Grid.SetRow(list, 1);
            left.Children.Add(list);

            // 右侧预览
            var right = new StackPanel();
            coverBox = new Border();
            coverBox.Width = 168;
            coverBox.Height = 236;
            coverBox.CornerRadius = new CornerRadius(12);
            coverBox.Background = PlaceholderBrush();
            coverBox.HorizontalAlignment = HorizontalAlignment.Left;
            coverBox.Margin = new Thickness(0, 0, 0, 12);
            right.Children.Add(coverBox);

            nameText = Ui.TxtBlock("", 18, Ui.Txt, FontWeights.Bold);
            nameText.TextWrapping = TextWrapping.Wrap;
            nameText.MaxWidth = 300;
            nameText.HorizontalAlignment = HorizontalAlignment.Left;
            nameText.Margin = new Thickness(0, 0, 0, 6);
            right.Children.Add(nameText);

            pageText = Ui.TxtBlock("", 11, Ui.Dim);
            pageText.MaxWidth = 300;
            pageText.HorizontalAlignment = HorizontalAlignment.Left;
            pageText.Margin = new Thickness(0, 0, 0, 4);
            right.Children.Add(pageText);

            scoreText = Ui.TxtBlock("", 11, Ui.Dim);
            scoreText.HorizontalAlignment = HorizontalAlignment.Left;
            right.Children.Add(scoreText);

            var tip = Ui.TxtBlock("识别结果来自游民星空 / 百度 / 百度百科；封面与中文名会保存到本地。", 10.5, Ui.Dim);
            tip.TextWrapping = TextWrapping.Wrap;
            tip.MaxWidth = 300;
            tip.Margin = new Thickness(0, 10, 0, 0);
            tip.HorizontalAlignment = HorizontalAlignment.Left;
            right.Children.Add(tip);

            Grid.SetColumn(right, 2);
            body.Children.Add(right);

            // 底部
            var footer = new Grid();
            footer.Margin = new Thickness(24, 12, 24, 16);
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            var statusStack = new StackPanel();
            statusStack.VerticalAlignment = VerticalAlignment.Center;
            statusText = Ui.TxtBlock("正在准备…", 11.5, Ui.Sub);
            statusText.TextWrapping = TextWrapping.Wrap;
            statusText.MaxWidth = 420;
            statusText.HorizontalAlignment = HorizontalAlignment.Left;
            manualHint = Ui.TxtBlock("", 10.5, Ui.Dim);
            manualHint.Margin = new Thickness(0, 2, 0, 0);
            statusStack.Children.Add(statusText);
            statusStack.Children.Add(manualHint);
            footer.Children.Add(statusStack);

            skipBtn = Ui.Btn("跳过识别，直接添加", false, delegate { OnSkip(); });
            skipBtn.Height = 36;
            skipBtn.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(skipBtn, 1);
            footer.Children.Add(skipBtn);

            useBtn = Ui.Btn("使用这个游戏", true, delegate { OnUse(); });
            useBtn.Height = 36;
            useBtn.MinWidth = 118;
            useBtn.Margin = new Thickness(10, 0, 0, 0);
            useBtn.IsEnabled = false;
            Grid.SetColumn(useBtn, 2);
            footer.Children.Add(useBtn);

            return root;
        }

        static Brush PlaceholderBrush()
        {
            var grad = new LinearGradientBrush();
            grad.StartPoint = new Point(0, 0);
            grad.EndPoint = new Point(1, 1);
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x22, 0x2C, 0x3C), 0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x16, 0x1C, 0x27), 1));
            grad.Freeze();
            return grad;
        }

        Style ItemStyle()
        {
            var bd = new FrameworkElementFactory(typeof(Border), "bd");
            bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            bd.SetValue(Border.PaddingProperty, new Thickness(10, 8, 10, 8));
            bd.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 2));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            bd.AppendChild(cp);
            var tpl = new ControlTemplate(typeof(ListBoxItem));
            tpl.VisualTree = bd;

            var sel = new Trigger();
            sel.Property = ListBoxItem.IsSelectedProperty;
            sel.Value = true;
            sel.Setters.Add(new Setter(Border.BackgroundProperty, Ui.AccentSoft, "bd"));
            tpl.Triggers.Add(sel);

            var hover = new Trigger();
            hover.Property = UIElement.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Border.BackgroundProperty, Ui.LineSoft, "bd"));
            tpl.Triggers.Add(hover);

            var st = new Style(typeof(ListBoxItem));
            st.Setters.Add(new Setter(ListBoxItem.TemplateProperty, tpl));
            st.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Ui.Txt));
            st.Setters.Add(new Setter(ListBoxItem.CursorProperty, Cursors.Hand));
            st.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            return st;
        }

        void StartIdentify()
        {
            busy = true;
            bar.Visibility = Visibility.Visible;
            useBtn.IsEnabled = false;
            statusText.Text = "正在分析 exe 名称与文件夹…";

            ThreadPool.QueueUserWorkItem(delegate
            {
                IdentifyResult r = null;
                try
                {
                    r = Identifier.Identify(exePath, delegate (string msg)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (busy) statusText.Text = msg;
                        }));
                    });
                }
                catch (Exception ex)
                {
                    Log.Error("识别过程异常", ex);
                }
                Dispatcher.BeginInvoke(new Action(delegate { OnIdentifyDone(r); }));
            });
        }

        void OnIdentifyDone(IdentifyResult r)
        {
            busy = false;
            bar.Visibility = Visibility.Collapsed;
            current = r;

            if (r == null)
            {
                statusText.Text = "识别失败（网络异常），可以手动输入游戏名搜索，或直接添加。";
                useBtn.IsEnabled = false;
                return;
            }

            FillCandidates(r.Candidates);
            statusText.Text = r.Message;
            if (r.Candidates.Count > 0)
            {
                list.SelectedIndex = 0;
            }
            useBtn.IsEnabled = r.Candidates.Count > 0;
        }

        void FillCandidates(List<GameCandidate> candidates)
        {
            list.Items.Clear();
            foreach (var c in candidates)
            {
                var item = new ListBoxItem();
                item.Tag = c;
                var sp = new StackPanel();
                var n = Ui.TxtBlock(c.Name, 13.5, Ui.Txt, FontWeights.SemiBold);
                var s = Ui.TxtBlock((string.IsNullOrEmpty(c.Origin) ? "" : c.Origin + "  ·  ") + ScoreLabel(c.Score), 10.5, Ui.Dim);
                s.Margin = new Thickness(0, 2, 0, 0);
                sp.Children.Add(n);
                sp.Children.Add(s);
                item.Content = sp;
                item.Style = ItemStyle();
                list.Items.Add(item);
            }
        }

        static string ScoreLabel(double score)
        {
            if (score >= 90) return "匹配度 很高";
            if (score >= 75) return "匹配度 较高";
            if (score >= 60) return "匹配度 一般";
            return "匹配度 较低";
        }

        void OnCandidateSelected()
        {
            var item = list.SelectedItem as ListBoxItem;
            GameCandidate c = item == null ? null : item.Tag as GameCandidate;
            if (c == null)
            {
                selectedSlug = null;
                useBtn.IsEnabled = false;
                return;
            }

            selectedSlug = string.IsNullOrEmpty(c.Slug) ? "n:" + c.Name : "s:" + c.Slug;
            nameText.Text = c.Name;
            if (!string.IsNullOrEmpty(c.Slug)) pageText.Text = "www.gamersky.com/z/" + c.Slug + "/";
            else if (!string.IsNullOrEmpty(c.PageUrl)) pageText.Text = WebSource.DomainOf(c.PageUrl) + "  ·  " + (c.Origin ?? "");
            else pageText.Text = c.Origin ?? "";
            scoreText.Text = ScoreLabel(c.Score);
            useBtn.IsEnabled = true;
            LoadCoverPreview(c);
        }

        void LoadCoverPreview(GameCandidate c)
        {
            coverBox.Background = PlaceholderBrush();
            if (previewMode)
            {
                if (!string.IsNullOrEmpty(previewCoverPath)) ApplyCoverFile(previewCoverPath);
                return;
            }
            string slug = string.IsNullOrEmpty(c.Slug) ? "n:" + c.Name : "s:" + c.Slug;
            var cand = c;
            ThreadPool.QueueUserWorkItem(delegate
            {
                byte[] data = null;
                lock (coverCache)
                {
                    if (coverCache.ContainsKey(slug)) data = coverCache[slug];
                }
                if (data == null)
                {
                    try
                    {
                        string url;
                        data = CoverService.Fetch(cand, out url);
                    }
                    catch { }
                    if (data != null)
                    {
                        lock (coverCache) { if (!coverCache.ContainsKey(slug)) coverCache[slug] = data; }
                    }
                }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (selectedSlug != slug) return;
                    if (data == null)
                    {
                        coverBox.Background = PlaceholderBrush();
                        return;
                    }
                    try
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = new MemoryStream(data);
                        bi.DecodePixelWidth = 360;
                        bi.EndInit();
                        bi.Freeze();
                        var ib = new ImageBrush(bi);
                        ib.Stretch = Stretch.UniformToFill;
                        ib.Freeze();
                        coverBox.Background = ib;
                    }
                    catch { }
                }));
            });
        }

        void ManualSearch()
        {
            string q = manualBox.Text == null ? "" : manualBox.Text.Trim();
            if (q.Length == 0 || busy) return;
            busy = true;
            bar.Visibility = Visibility.Visible;
            statusText.Text = "正在搜索「" + q + "」（游民星空 + 百度百科）…";
            manualHint.Text = "";

            ThreadPool.QueueUserWorkItem(delegate
            {
                List<GameCandidate> hits = new List<GameCandidate>();
                try { hits = Gamersky.Search(q); }
                catch (Exception ex) { Log.Warn("手动搜索失败：" + ex.Message); }

                GameCandidate baike = null;
                try { baike = WebSource.LookupBaike(q); }
                catch (Exception ex) { Log.Warn("百科查询失败：" + ex.Message); }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    busy = false;
                    bar.Visibility = Visibility.Collapsed;
                    if (baike != null)
                    {
                        baike.Score = 88;
                        baike.Origin = "百度百科";
                        hits.Insert(0, baike);
                    }
                    if (hits.Count == 0)
                    {
                        statusText.Text = "没有搜到「" + q + "」，换个名字试试（建议用中文名或英文原名）。";
                        return;
                    }
                    foreach (var h in hits) { if (h.Score <= 0) h.Score = 80; if (string.IsNullOrEmpty(h.Origin)) h.Origin = "手动搜索"; }
                    FillCandidates(hits);
                    list.SelectedIndex = 0;
                    statusText.Text = "找到 " + hits.Count + " 个结果，选择正确的游戏后点击「使用这个游戏」。";
                }));
            });
        }

        void OnUse()
        {
            var item = list.SelectedItem as ListBoxItem;
            GameCandidate c = item == null ? null : item.Tag as GameCandidate;
            if (c == null) return;

            useBtn.IsEnabled = false;
            bar.Visibility = Visibility.Visible;
            statusText.Text = "正在保存识别结果…";

            string slug = string.IsNullOrEmpty(c.Slug) ? "n:" + c.Name : "s:" + c.Slug;
            var cand = c;
            ThreadPool.QueueUserWorkItem(delegate
            {
                byte[] data = null;
                string usedUrl = c.CoverUrl;
                lock (coverCache)
                {
                    if (coverCache.ContainsKey(slug)) data = coverCache[slug];
                }
                if (data == null)
                {
                    try { data = CoverService.Fetch(cand, out usedUrl); }
                    catch (Exception ex) { Log.Warn("获取封面失败：" + ex.Message); }
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    var r = new IdentifyResult();
                    r.Success = true;
                    r.Name = c.Name;
                    r.Slug = c.Slug;
                    r.PageUrl = c.PageUrl;
                    r.CoverData = data;
                    r.CoverUrl = usedUrl;
                    r.Score = c.Score;
                    r.Candidates = current != null ? current.Candidates : new List<GameCandidate>();
                    r.Message = "已选择《" + c.Name + "》";
                    Result = r;
                    DialogResult = true;
                }));
            });
        }

        void OnSkip()
        {
            var r = new IdentifyResult();
            r.Success = false;
            r.Name = Identifier.FallbackName(exePath);
            r.Message = "跳过识别";
            Result = r;
            Log.Info("跳过识别，使用默认名称：" + r.Name);
            DialogResult = true;
        }
    }
}

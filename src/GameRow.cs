using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GameLog
{
    /// <summary>列表布局中的一行：封面缩略图 + 名称 + 今日/累计时长 + 时长进度条。</summary>
    public class GameRow : Border
    {
        public GameInfo Game { get; private set; }
        public const double TrackWidth = 220;
        public const double TrackHeight = 8;

        readonly Store store;
        readonly TextBlock nameText;
        readonly TextBlock subText;
        readonly TextBlock todayText;
        readonly TextBlock totalText;
        readonly Border progressFill;
        readonly Border runningBadge;

        public event EventHandler RowClicked;
        public event EventHandler RowDoubleClicked;

        public GameRow(Store store, GameInfo game)
        {
            this.store = store;
            Game = game;

            Height = 64;
            Margin = new Thickness(0, 0, 0, 6);
            CornerRadius = new CornerRadius(10);
            Background = Ui.Card;
            BorderBrush = Ui.Line;
            BorderThickness = new Thickness(1);
            Cursor = Cursors.Hand;
            ToolTip = game.ExePath;
            Clip = new RectangleGeometry(new Rect(0, 0, 1, 1), 10, 10);

            var grid = new Grid();
            grid.Margin = new Thickness(10, 0, 14, 0);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });   // cover
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });  // today
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });  // total
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TrackWidth + 24) }); // progress
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });   // running

            // 封面
            var cover = new Border();
            cover.Width = 40;
            cover.Height = 54;
            cover.CornerRadius = new CornerRadius(7);
            cover.VerticalAlignment = VerticalAlignment.Center;
            cover.Background = Ui.CoverBrush(store, game);
            if (store.CoverPath(game) == null)
            {
                var initial = Ui.TxtBlock(InitialOf(game.Name), 18, new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), FontWeights.Bold);
                initial.HorizontalAlignment = HorizontalAlignment.Center;
                initial.VerticalAlignment = VerticalAlignment.Center;
                cover.Child = initial;
            }
            Grid.SetColumn(cover, 0);
            grid.Children.Add(cover);

            // 名称 + 上次游玩
            var titleStack = new StackPanel();
            titleStack.VerticalAlignment = VerticalAlignment.Center;
            titleStack.Margin = new Thickness(10, 0, 8, 0);
            nameText = Ui.TxtBlock(game.Name, 14, Ui.Txt, FontWeights.SemiBold);
            subText = Ui.TxtBlock(Ui.Ago(game.LastPlayed), 10.5, Ui.Dim);
            subText.Margin = new Thickness(0, 2, 0, 0);
            titleStack.Children.Add(nameText);
            titleStack.Children.Add(subText);
            Grid.SetColumn(titleStack, 1);
            grid.Children.Add(titleStack);

            // 今日
            todayText = Ui.TxtBlock("", 12, Ui.Sub);
            todayText.TextAlignment = TextAlignment.Left;
            todayText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(todayText, 2);
            grid.Children.Add(todayText);

            // 累计
            totalText = Ui.TxtBlock("", 13, Ui.Txt, FontWeights.SemiBold);
            totalText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(totalText, 3);
            grid.Children.Add(totalText);

            // 进度条
            var track = new Border();
            track.Width = TrackWidth;
            track.Height = TrackHeight;
            track.CornerRadius = new CornerRadius(4);
            track.Background = Ui.LineSoft;
            track.VerticalAlignment = VerticalAlignment.Center;
            progressFill = new Border();
            progressFill.Height = TrackHeight;
            progressFill.CornerRadius = new CornerRadius(4);
            progressFill.HorizontalAlignment = HorizontalAlignment.Left;
            progressFill.Background = Ui.Accent;
            progressFill.Width = 0;
            track.Child = progressFill;
            var trackHost = new Border();
            trackHost.Child = track;
            trackHost.Margin = new Thickness(6, 0, 6, 0);
            trackHost.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(trackHost, 4);
            grid.Children.Add(trackHost);

            // 运行中
            runningBadge = new Border();
            runningBadge.CornerRadius = new CornerRadius(20);
            runningBadge.Background = Ui.Br("#152A22");
            runningBadge.Padding = new Thickness(8, 3, 9, 3);
            runningBadge.HorizontalAlignment = HorizontalAlignment.Left;
            runningBadge.VerticalAlignment = VerticalAlignment.Center;
            runningBadge.Visibility = Visibility.Collapsed;
            var badgeRow = new StackPanel();
            badgeRow.Orientation = Orientation.Horizontal;
            var dot = new Border();
            dot.Width = 6;
            dot.Height = 6;
            dot.CornerRadius = new CornerRadius(3);
            dot.Background = Ui.Good;
            dot.VerticalAlignment = VerticalAlignment.Center;
            var badgeText = Ui.TxtBlock("运行中", 10, Ui.Good);
            badgeText.Margin = new Thickness(5, 0, 0, 0);
            badgeRow.Children.Add(dot);
            badgeRow.Children.Add(badgeText);
            runningBadge.Child = badgeRow;
            Grid.SetColumn(runningBadge, 5);
            grid.Children.Add(runningBadge);

            Child = grid;

            SizeChanged += delegate
            {
                Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), 10, 10);
            };

            MouseEnter += delegate
            {
                BorderBrush = Ui.Accent;
                Background = Ui.CardHover;
            };
            MouseLeave += delegate
            {
                BorderBrush = IsSelected ? Ui.Accent : Ui.Line;
                Background = IsSelected ? Ui.CardHover : Ui.Card;
            };
            MouseLeftButtonUp += delegate
            {
                var h = RowClicked;
                if (h != null) h(this, EventArgs.Empty);
            };
            MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2)
                {
                    var h = RowDoubleClicked;
                    if (h != null) h(this, EventArgs.Empty);
                }
            };
            MouseRightButtonDown += delegate
            {
                var h = RowClicked;
                if (h != null) h(this, EventArgs.Empty);
            };

            Refresh(null, 0);
        }

        public bool IsSelected { get; private set; }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            BorderBrush = selected ? Ui.Accent : Ui.Line;
            Background = selected ? Ui.CardHover : Ui.Card;
        }

        public void Refresh(Monitor monitor, double maxSeconds)
        {
            var g = Game;
            nameText.Text = g.Name;
            subText.Text = Ui.Ago(g.LastPlayed);
            todayText.Text = g.TodaySeconds > 0 ? "今日 " + Ui.Hours(g.TodaySeconds) : "今日 0 小时";
            todayText.Foreground = g.TodaySeconds >= 3600 ? Ui.Accent : Ui.Sub;
            totalText.Text = Ui.Hours(g.TotalSeconds);

            double ratio = maxSeconds > 0 ? g.TotalSeconds / maxSeconds : 0;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            progressFill.Width = Math.Max(ratio * TrackWidth, g.TotalSeconds > 0 ? 4 : 0);

            bool running = monitor != null && monitor.IsRunning(g.Id);
            runningBadge.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            if (running)
            {
                DateTime start = monitor.SessionStartOf(g.Id);
                if (start != DateTime.MinValue)
                {
                    double session = (DateTime.Now - start).TotalSeconds;
                    subText.Text = "本次 " + Ui.Hours(session) + " · " + Ui.Ago(g.LastPlayed);
                }
            }
        }

        static string InitialOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            return name.Trim().Substring(0, 1).ToUpperInvariant();
        }
    }
}

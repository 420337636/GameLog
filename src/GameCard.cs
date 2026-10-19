using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GameLog
{
    /// <summary>主界面中的一张游戏卡片：封面 + 名称 + 累计时长 + 运行状态。</summary>
    public class GameCard : Border
    {
        public GameInfo Game { get; private set; }
        readonly Store store;
        readonly TextBlock nameText;
        readonly TextBlock timeText;
        readonly TextBlock todayText;
        readonly Border runningBadge;
        readonly Border cover;
        readonly TextBlock initial;
        readonly Border hoverGlow;

        public const double CardWidth = 206;
        public const double CardHeight = 358;

        public event EventHandler CardClicked;
        public event EventHandler CardDoubleClicked;

        public GameCard(Store store, GameInfo game)
        {
            this.store = store;
            Game = game;

            Width = CardWidth;
            Height = CardHeight;
            CornerRadius = new CornerRadius(12);
            Background = Ui.Card;
            BorderBrush = Ui.Line;
            BorderThickness = new Thickness(1);
            Margin = new Thickness(9);
            Cursor = Cursors.Hand;
            ToolTip = game.ExePath;
            Clip = new RectangleGeometry(new Rect(0, 0, CardWidth, CardHeight), 12, 12);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(280) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            cover = new Border();
            cover.CornerRadius = new CornerRadius(11, 11, 0, 0);
            cover.Background = Ui.CoverBrush(store, game);
            cover.SnapsToDevicePixels = true;

            hoverGlow = new Border();
            hoverGlow.CornerRadius = new CornerRadius(11, 11, 0, 0);
            hoverGlow.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            hoverGlow.Background.Freeze();
            hoverGlow.Child = null;

            initial = Ui.TxtBlock(InitialOf(game.Name), 52, new SolidColorBrush(Color.FromArgb(88, 255, 255, 255)), FontWeights.Bold);
            initial.HorizontalAlignment = HorizontalAlignment.Center;
            initial.VerticalAlignment = VerticalAlignment.Center;
            if (store.CoverPath(game) == null)
            {
                var host = new Grid();
                host.Children.Add(initial);
                cover.Child = host;
            }

            runningBadge = new Border();
            runningBadge.CornerRadius = new CornerRadius(20);
            runningBadge.Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x10, 0x18, 0x22));
            runningBadge.Padding = new Thickness(9, 4, 10, 4);
            runningBadge.HorizontalAlignment = HorizontalAlignment.Left;
            runningBadge.VerticalAlignment = VerticalAlignment.Top;
            runningBadge.Margin = new Thickness(9);
            runningBadge.Visibility = Visibility.Collapsed;
            var badgeRow = new StackPanel();
            badgeRow.Orientation = Orientation.Horizontal;
            var dot = new Border();
            dot.Width = 7;
            dot.Height = 7;
            dot.CornerRadius = new CornerRadius(4);
            dot.Background = Ui.Good;
            dot.VerticalAlignment = VerticalAlignment.Center;
            var badgeText = Ui.TxtBlock("运行中", 11, Ui.Good);
            badgeText.Margin = new Thickness(6, 0, 0, 0);
            badgeRow.Children.Add(dot);
            badgeRow.Children.Add(badgeText);
            runningBadge.Child = badgeRow;

            var coverHost = new Grid();
            coverHost.Children.Add(cover);
            coverHost.Children.Add(hoverGlow);
            coverHost.Children.Add(runningBadge);
            Grid.SetRow(coverHost, 0);
            grid.Children.Add(coverHost);

            var info = new StackPanel();
            info.Margin = new Thickness(12, 9, 12, 10);
            nameText = Ui.TxtBlock(game.Name, 14.5, Ui.Txt, FontWeights.SemiBold);
            nameText.Margin = new Thickness(0, 0, 0, 4);
            timeText = Ui.TxtBlock("", 11.5, Ui.Sub);
            timeText.Margin = new Thickness(0, 0, 0, 2);
            todayText = Ui.TxtBlock("", 11.5, Ui.Accent);
            todayText.Visibility = Visibility.Collapsed;
            info.Children.Add(nameText);
            info.Children.Add(timeText);
            info.Children.Add(todayText);
            Grid.SetRow(info, 1);
            grid.Children.Add(info);

            Child = grid;

            MouseEnter += delegate
            {
                BorderBrush = Ui.Accent;
                Background = Ui.CardHover;
                hoverGlow.Background = new SolidColorBrush(Color.FromArgb(18, 0x5B, 0x8C, 0xFF));
                hoverGlow.Background.Freeze();
            };
            MouseLeave += delegate
            {
                if (IsSelected) BorderBrush = Ui.Accent;
                else BorderBrush = Ui.Line;
                Background = Ui.Card;
                hoverGlow.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                hoverGlow.Background.Freeze();
            };
            MouseLeftButtonUp += delegate
            {
                var h = CardClicked;
                if (h != null) h(this, EventArgs.Empty);
            };
            MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2)
                {
                    var h = CardDoubleClicked;
                    if (h != null) h(this, EventArgs.Empty);
                }
            };
            MouseRightButtonDown += delegate
            {
                var h = CardClicked;
                if (h != null) h(this, EventArgs.Empty);
            };

            Refresh(null);
        }

        public bool IsSelected { get; set; }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            BorderBrush = selected ? Ui.Accent : Ui.Line;
            Background = selected ? Ui.CardHover : Ui.Card;
        }

        public void Refresh(Monitor monitor)
        {
            GameInfo g = Game;
            nameText.Text = g.Name;
            timeText.Text = "累计 " + Ui.Hours(g.TotalSeconds);
            if (g.TodaySeconds >= 60)
            {
                todayText.Text = "今日 " + Ui.Hours(g.TodaySeconds);
                todayText.Visibility = Visibility.Visible;
            }
            else
            {
                todayText.Visibility = Visibility.Collapsed;
            }

            bool running = monitor != null && monitor.IsRunning(g.Id);
            runningBadge.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            if (running)
            {
                DateTime start = monitor.SessionStartOf(g.Id);
                if (start != DateTime.MinValue)
                {
                    double session = (DateTime.Now - start).TotalSeconds;
                    timeText.Text = "本次 " + Ui.Hours(session) + " · 累计 " + Ui.Hours(g.TotalSeconds);
                }
            }
        }

        static string InitialOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            string t = name.Trim();
            if (t.Length == 0) return "?";
            return t.Substring(0, 1).ToUpperInvariant();
        }
    }
}

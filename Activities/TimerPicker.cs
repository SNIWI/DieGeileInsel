using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DynamicIsland
{
    /// <summary>
    /// Timer-Dauer wählen wie in der Uhr-App des iPhones: drei Drehräder (Std · Min · Sek) mit
    /// hellem Auswahlband, darunter runde Knöpfe „Abbrechen“ und „Start“, rechts Schnellwahl.
    /// Bedienung: Mausrad oder ziehen auf einem Rad, Antippen der Zahl darüber/darunter.
    /// </summary>
    public sealed class TimerPickerActivity : Activity
    {
        readonly IslandWindow host;
        WheelColumn h, m, s;

        public TimerPickerActivity(IslandWindow host)
        {
            this.host = host;
            Id = "timer-picker";
            Priority = 1;
        }

        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(420, 224);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph("", 14, Ui.Br(Ui.Orange)), Ui.Text("Timer", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph("", 14, Ui.Br(Ui.Orange));

        TimeSpan Chosen => new TimeSpan(h.Value, m.Value, s.Value);

        public override FrameworkElement BuildExpanded()
        {
            var last = TimeSpan.FromSeconds(Math.Max(1, host.Settings.LastTimerSeconds));
            h = new WheelColumn(23, last.Hours, "Std.");
            m = new WheelColumn(59, last.Minutes, "Min.");
            s = new WheelColumn(59, last.Seconds, "Sek.");

            // Räder mit gemeinsamem Auswahlband (wie iOS)
            var wheels = new Grid { Height = WheelColumn.RowH * 5, VerticalAlignment = VerticalAlignment.Center };
            wheels.Children.Add(new Border
            {
                Height = WheelColumn.RowH + 4,
                CornerRadius = new CornerRadius(10),
                Background = Ui.Br(Colors.White, 26),
                VerticalAlignment = VerticalAlignment.Center
            });
            var cols = new StackPanel { Orientation = Orientation.Horizontal };
            cols.Children.Add(h); cols.Children.Add(m); cols.Children.Add(s);
            wheels.Children.Add(cols);

            // Schnellwahl
            var presets = new WrapPanel { Width = 216, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var (label, secs) in new[] { ("1 Min", 60), ("3 Min", 180), ("5 Min", 300), ("10 Min", 600), ("15 Min", 900), ("30 Min", 1800) })
            {
                int sec = secs;
                var chip = new Border
                {
                    Margin = new Thickness(0, 0, 5, 5),
                    Padding = new Thickness(10, 4, 10, 5),
                    CornerRadius = new CornerRadius(14),
                    Background = Ui.Br(Colors.White, 22),
                    Cursor = Cursors.Hand,
                    Child = Ui.Text(label, 12.5, Ui.White, false)
                };
                Ui.Pressable(chip, 0.92);
                chip.MouseLeftButtonDown += (o, e) => e.Handled = true;
                chip.MouseLeftButtonUp += (o, e) => { e.Handled = true; SetTo(TimeSpan.FromSeconds(sec)); };
                presets.Children.Add(chip);
            }
            var pomo = new Border
            {
                Margin = new Thickness(0, 0, 5, 5), Padding = new Thickness(10, 4, 10, 5), CornerRadius = new CornerRadius(14),
                Background = Ui.Br(Ui.Red, 50), Cursor = Cursors.Hand, Child = Ui.Text("🍅 Pomodoro", 12.5, Ui.White, false)
            };
            Ui.Pressable(pomo, 0.92);
            pomo.MouseLeftButtonDown += (o, e) => e.Handled = true;
            pomo.MouseLeftButtonUp += (o, e) => { e.Handled = true; Finished = true; host.StartPomodoro(true, 1); };
            presets.Children.Add(pomo);

            // Knöpfe wie in der Uhr-App: grau links, grün rechts
            var cancel = CircleButton("Abbrechen", Ui.Br(Colors.White, 34), Ui.White, () => { Finished = true; host.Collapse(); });
            var start = CircleButton("Start", Ui.Br(Ui.Green, 70), Ui.Br(Ui.Green), Start);
            var btns = Ui.HStack(14, cancel, start);
            btns.Margin = new Thickness(0, 8, 0, 0);
            btns.HorizontalAlignment = HorizontalAlignment.Left;

            var right = Ui.VStack(0, presets, btns);

            right.VerticalAlignment = VerticalAlignment.Center;
            right.Margin = new Thickness(22, 0, 0, 0);

            var g = new Grid { Margin = new Thickness(22, 10, 18, 10) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(right, 1);
            g.Children.Add(wheels);
            g.Children.Add(right);
            return g;
        }

        static Border CircleButton(string text, Brush bg, Brush fg, Action click)
        {
            var b = new Border
            {
                Width = 60, Height = 60, CornerRadius = new CornerRadius(30),
                Background = bg, Cursor = Cursors.Hand,
                // doppelter Ring wie beim iPhone
                Child = new Border
                {
                    Margin = new Thickness(3), CornerRadius = new CornerRadius(27),
                    BorderBrush = Brushes.Black, BorderThickness = new Thickness(2),
                    Child = new TextBlock { Text = text, FontFamily = Ui.TextFont, FontSize = text.Length > 6 ? 9.5 : 11.5, FontWeight = FontWeights.SemiBold, Foreground = fg, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                }
            };
            Ui.Pressable(b, 0.9);
            b.MouseLeftButtonDown += (o, e) => e.Handled = true;
            b.MouseLeftButtonUp += (o, e) => { e.Handled = true; click(); };
            return b;
        }

        void SetTo(TimeSpan t)
        {
            h.Set(t.Hours); m.Set(t.Minutes); s.Set(t.Seconds);
        }

        void Start()
        {
            var t = Chosen;
            if (t.TotalSeconds < 1) return;
            host.Settings.LastTimerSeconds = (int)t.TotalSeconds;
            host.Settings.Save();
            Finished = true;
            host.StartTimer(t);
        }
    }

    /// <summary>Ein Drehrad mit Zahlen (wie der iOS-Picker): Mitte groß und hell, außen kleiner und blasser.</summary>
    public sealed class WheelColumn : Grid
    {
        public const double RowH = 30;
        static readonly double[] Op = { 0.18, 0.42, 1, 0.42, 0.18 };
        static readonly double[] Size = { 14, 17, 23, 17, 14 };
        readonly int max;
        readonly TextBlock[] rows = new TextBlock[5];
        readonly TranslateTransform slide = new TranslateTransform();
        public int Value { get; private set; }
        double dragY;
        bool dragging;

        public WheelColumn(int max, int value, string unit)
        {
            this.max = max;
            Value = Math.Max(0, Math.Min(max, value));
            Width = 74;
            Height = RowH * 5;
            Background = Brushes.Transparent;
            ClipToBounds = true;
            Cursor = Cursors.SizeNS;
            var stack = new StackPanel { RenderTransform = slide, Width = 40, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(6, 0, 0, 0) };
            for (int i = 0; i < 5; i++)
            {
                var t = new TextBlock
                {
                    Height = RowH, FontFamily = Ui.TextFont, Foreground = Ui.White, TextAlignment = TextAlignment.Right,
                    FontSize = Size[i], Opacity = Op[i], FontWeight = i == 2 ? FontWeights.SemiBold : FontWeights.Normal,
                    Padding = new Thickness(0, Math.Max(0, (RowH - Size[i] * 1.33) / 2), 0, 0)
                };
                System.Windows.Documents.Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular);
                rows[i] = t;
                stack.Children.Add(t);
            }
            Children.Add(stack);
            // Einheit fest neben der mittleren Zeile
            Children.Add(new TextBlock
            {
                Text = unit, FontFamily = Ui.TextFont, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Ui.White,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(49, 2, 0, 0), IsHitTestVisible = false
            });
            Refresh();

            MouseWheel += (o, e) => { e.Handled = true; Step(e.Delta > 0 ? -1 : 1); };
            MouseLeftButtonDown += (o, e) =>
            {
                e.Handled = true;
                dragging = true; dragY = e.GetPosition(this).Y;
                CaptureMouse();
            };
            MouseMove += (o, e) =>
            {
                if (!dragging) return;
                double y = e.GetPosition(this).Y;
                while (y - dragY > RowH * 0.7) { Step(-1); dragY += RowH * 0.7; }
                while (dragY - y > RowH * 0.7) { Step(1); dragY -= RowH * 0.7; }
            };
            MouseLeftButtonUp += (o, e) =>
            {
                if (!dragging) return;
                e.Handled = true;
                dragging = false;
                ReleaseMouseCapture();
                // nur getippt (nicht gezogen)? → zur angetippten Zeile springen
                double y = e.GetPosition(this).Y;
                if (Math.Abs(y - dragY) < 3)
                {
                    int row = (int)(y / RowH);
                    if (row != 2) Step(row - 2);
                }
            };
        }

        int Wrap(int v) => ((v % (max + 1)) + max + 1) % (max + 1);

        void Refresh()
        {
            for (int i = 0; i < 5; i++) rows[i].Text = Wrap(Value + i - 2).ToString("00");
        }

        public void Step(int dir)
        {
            if (dir == 0) return;
            Value = Wrap(Value + dir);
            Refresh();
            if (Ui.ReduceMotion) return;
            // Zahlen gleiten nach (wie beim Drehen des iOS-Rads)
            slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(RowH * 0.55 * Math.Sign(dir), 0, TimeSpan.FromMilliseconds(170))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        public void Set(int v)
        {
            int d = Math.Max(0, Math.Min(max, v)) - Value;
            if (d == 0) return;
            Value = Math.Max(0, Math.Min(max, v));
            Refresh();
            if (!Ui.ReduceMotion)
                slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(RowH * 0.55 * Math.Sign(d), 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }
}

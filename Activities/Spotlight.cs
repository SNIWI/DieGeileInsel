using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace DynamicIsland
{
    /// <summary>
    /// „App im Vordergrund“: Bei einem Ereignis (Claude fertig, Timer, Kopieren …) wird die Island
    /// länger und zeigt groß das Symbol der App, daneben den Inhalt – wie bei der iPhone-Island.
    /// Symbol springt hinein, Text gleitet nach.
    /// </summary>
    public sealed class SpotlightActivity : Activity
    {
        readonly Func<FrameworkElement> icon;
        readonly Color color;
        readonly string title, sub;
        readonly Func<bool> tap;

        public SpotlightActivity(Func<FrameworkElement> icon, Color color, string title, string sub, double seconds, Func<bool> tap = null)
        {
            this.icon = icon;
            this.color = color;
            this.title = title ?? "";
            this.sub = sub ?? "";
            this.tap = tap;
            Id = "spotlight";
            IsAlert = true;
            Priority = 95;
            ExpiresAt = DateTime.Now.AddSeconds(seconds);
        }

        public override bool CanExpand => false;
        public override double CompactHeight => 52;
        public override double CompactWidth
        {
            get
            {
                int len = Math.Max(title.Length, (int)(sub.Length * 0.85));
                return Math.Max(300, Math.Min(470, 120 + len * 7.6));
            }
        }

        public override bool OnTap() => tap != null && tap();

        public override FrameworkElement BuildCompact()
        {
            // Symbol im farbigen Kreis, dahinter ein weiches Leuchten
            var glow = new Border
            {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(20),
                Background = Ui.Br(color, 120),
                Effect = new BlurEffect { Radius = 14 },
                IsHitTestVisible = false
            };
            var circle = new Border
            {
                Width = 38, Height = 38, CornerRadius = new CornerRadius(19),
                Background = Ui.Br(color, 60),
                Child = icon?.Invoke() ?? Ui.Glyph("", 18, Ui.Br(color))
            };
            if (circle.Child is FrameworkElement ic) { ic.HorizontalAlignment = HorizontalAlignment.Center; ic.VerticalAlignment = VerticalAlignment.Center; }
            var iconScale = new ScaleTransform(0.2, 0.2);
            var iconRot = new RotateTransform(-25);
            var itg = new TransformGroup();
            itg.Children.Add(iconScale);
            itg.Children.Add(iconRot);
            var iconHost = new Grid { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = itg };
            iconHost.Children.Add(glow);
            iconHost.Children.Add(circle);

            var t1 = Ui.Text(title, 14.5, Ui.White, true, true);
            var t2 = Ui.Text(sub, 12, Ui.Dim, false, true);
            t1.TextTrimming = t2.TextTrimming = TextTrimming.CharacterEllipsis;
            var texts = Ui.VStack(0, t1, t2);
            texts.VerticalAlignment = VerticalAlignment.Center;
            texts.Margin = new Thickness(12, 0, 6, 0);
            var slide = new TranslateTransform(22, 0);
            texts.RenderTransform = slide;
            texts.Opacity = 0;

            var g = new Grid { Margin = new Thickness(7, 0, 18, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(texts, 1);
            g.Children.Add(iconHost);
            g.Children.Add(texts);

            g.Loaded += (s, e) =>
            {
                if (Ui.ReduceMotion)
                {
                    iconScale.ScaleX = iconScale.ScaleY = 1; iconRot.Angle = 0; slide.X = 0; texts.Opacity = 1;
                    return;
                }
                var pop = new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(620)) { EasingFunction = new SpringEase { Damping = 0.55 } };
                iconScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                iconScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
                iconRot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-25, 0, TimeSpan.FromMilliseconds(520)) { EasingFunction = new SpringEase { Damping = 0.7 } });
                var begin = TimeSpan.FromMilliseconds(140);
                slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(22, 0, TimeSpan.FromMilliseconds(420)) { BeginTime = begin, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                texts.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { BeginTime = begin });
                // Leuchten hinter dem Symbol pulsiert zweimal
                var pulse = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1600) };
                pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
                pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))));
                pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000))));
                pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.35, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1600))));
                glow.BeginAnimation(UIElement.OpacityProperty, pulse);
            };
            return g;
        }

        public override FrameworkElement BuildMinimal() => icon?.Invoke() ?? Ui.Glyph("", 14, Ui.Br(color));
    }
}

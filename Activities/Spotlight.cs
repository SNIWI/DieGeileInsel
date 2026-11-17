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
    /// Symbol springt hinein, Text gleitet nach. Aussehen je nach gewähltem Design (siehe AlertLook).
    /// </summary>
    public sealed class SpotlightActivity : Activity
    {
        readonly Func<FrameworkElement> icon;
        readonly Color color;
        readonly string title, sub;
        readonly Func<bool> tap;
        /// <summary>Farbe der Überschrift (z. B. die eingestellte Farbe für „Claude ist fertig“), sonst je nach Design.</summary>
        public Color? TitleColor { get; set; }
        /// <summary>Freier Platz am rechten Rand (z. B. für das ✓ aus dem Claude-Kreis, das dort hineingleitet).</summary>
        public double ReserveRight { get; set; }

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
        static string D => AlertLook.D;
        public override double CompactHeight => AlertLook.SpotHeight;
        public override double CompactWidth => BaseWidth + ReserveRight;
        double BaseWidth
        {
            get
            {
                if (AlertLook.OneLine)
                {
                    double k = D == "minimal" ? 0.94 : 1;
                    return Math.Max(D == "minimal" ? 150 : 170, Math.Min(360, 58 + (title.Length * 7.0 + (sub.Length > 0 ? sub.Length * 6.3 + 12 : 0)) * k));
                }
                int len = Math.Max(title.Length + (D == "ios" ? 6 : 0), (int)(sub.Length * 0.85));
                return Math.Max(300, Math.Min(470, 120 + len * 7.6));
            }
        }

        public override bool OnTap() => tap != null && tap();

        public override FrameworkElement BuildCompact()
        {
            bool oneLine = AlertLook.OneLine;
            double cs = AlertLook.SpotIcon;

            // Symbol: Fläche je nach Design (Kreis, abgerundetes Quadrat, keine), dahinter ggf. ein Leuchten
            var glow = AlertLook.GlowBehind(color, cs);
            var iconEl = icon?.Invoke() ?? Ui.Glyph("", 18, Ui.Br(color));
            if (oneLine) iconEl.LayoutTransform = new ScaleTransform(0.68, 0.68);   // kleineres Symbol
            else if (D == "ios") iconEl.LayoutTransform = new ScaleTransform(0.92, 0.92);
            iconEl.HorizontalAlignment = HorizontalAlignment.Center;
            iconEl.VerticalAlignment = VerticalAlignment.Center;
            var box = new Border { Child = iconEl };
            AlertLook.StyleBox(box, color, cs);
            var iconScale = new ScaleTransform(0.2, 0.2);
            var iconRot = new RotateTransform(-25);
            var itg = new TransformGroup();
            itg.Children.Add(iconScale);
            itg.Children.Add(iconRot);
            var iconHost = new Grid { Width = cs + 2, Height = cs + 2, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = itg };
            if (glow != null) iconHost.Children.Add(glow);
            iconHost.Children.Add(box);

            var titleBrush = AlertLook.TitleBrush(color, TitleColor);
            FrameworkElement texts;
            if (oneLine)
            {
                // eine schlichte Zeile: Titel fett · Inhalt gedimmt
                var line = new TextBlock { FontFamily = Ui.TextFont, FontSize = D == "minimal" ? 12.5 : 13, Foreground = Ui.White, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
                line.Inlines.Add(new System.Windows.Documents.Run(title) { FontWeight = FontWeights.SemiBold, Foreground = titleBrush });
                if (sub.Length > 0) line.Inlines.Add(new System.Windows.Documents.Run((D == "minimal" ? " · " : "  ") + sub) { Foreground = Ui.Dim });
                texts = line;
                texts.Margin = new Thickness(D == "minimal" ? 6 : 9, 0, 4, 0);
            }
            else if (D == "ios")
            {
                // wie eine iPhone-Mitteilung: Titel + „jetzt“, darunter der Text
                var t1 = Ui.Text(title, 13.5, titleBrush, true, true);
                var now = Ui.Text("jetzt", 11, Ui.Dim, false, true);
                now.VerticalAlignment = VerticalAlignment.Center;
                now.Margin = new Thickness(8, 0, 0, 0);
                var top = new Grid();
                top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(now, 1);
                top.Children.Add(t1);
                top.Children.Add(now);
                var t2 = Ui.Text(sub, 12.5, Ui.Br(Colors.White, 200), false, true);
                t1.TextTrimming = t2.TextTrimming = TextTrimming.CharacterEllipsis;
                t2.Margin = new Thickness(0, 1, 0, 0);
                var st = Ui.VStack(0, top, t2);
                st.VerticalAlignment = VerticalAlignment.Center;
                st.Margin = new Thickness(11, 0, 4, 0);
                texts = st;
            }
            else
            {
                var t1 = Ui.Text(title, 14.5, titleBrush, true, true);
                var t2 = Ui.Text(sub, 12, Ui.Dim, false, true);
                t1.TextTrimming = t2.TextTrimming = TextTrimming.CharacterEllipsis;
                var st = Ui.VStack(0, t1, t2);
                st.VerticalAlignment = VerticalAlignment.Center;
                st.Margin = new Thickness(12, 0, 6, 0);
                texts = st;
            }
            var slide = new TranslateTransform(22, 0);
            texts.RenderTransform = slide;
            texts.Opacity = 0;
            var blur = new BlurEffect { Radius = 10 };   // Apple-Stil: Inhalt wird scharf gezogen statt nur eingeblendet
            texts.Effect = blur;

            Thickness pad = D switch
            {
                "small" => new Thickness(AlertLook.SpotPadLeft, 0, 15, 0),
                "minimal" => new Thickness(AlertLook.SpotPadLeft, 0, 14, 0),
                "ios" => new Thickness(AlertLook.SpotPadLeft, 0, 16, 0),
                _ => new Thickness(AlertLook.SpotPadLeft, 0, 18, 0)
            };
            pad.Right += ReserveRight;
            var g = new Grid { Margin = pad };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(texts, 1);
            g.Children.Add(iconHost);
            g.Children.Add(texts);

            g.Loaded += (s, e) =>
            {
                if (Ui.ReduceMotion || Ui.AlertMotion == "none")
                {
                    iconScale.ScaleX = iconScale.ScaleY = 1; iconRot.Angle = 0; slide.X = 0; texts.Opacity = 1; texts.Effect = null;
                    return;
                }
                bool soft = Ui.AlertMotion != "morph";   // „Schieben“: Symbol blendet ruhig ein, ohne Federn und Drehung
                var pop = soft
                    ? new DoubleAnimation(0.7, 1, TimeSpan.FromMilliseconds(380)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }
                    : new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(620)) { EasingFunction = new SpringEase { Damping = 0.55 } };
                iconScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                iconScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
                if (soft) iconRot.Angle = 0; else iconRot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-25, 0, TimeSpan.FromMilliseconds(520)) { EasingFunction = new SpringEase { Damping = 0.7 } });
                var begin = TimeSpan.FromMilliseconds(140);
                slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(soft ? -14 : 22, 0, TimeSpan.FromMilliseconds(420)) { BeginTime = begin, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                texts.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { BeginTime = begin });
                var sharpen = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(460)) { BeginTime = begin, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                sharpen.Completed += (o, a) => texts.Effect = null;   // danach ohne Effekt (spart Leistung)
                blur.BeginAnimation(BlurEffect.RadiusProperty, sharpen);
                if (glow == null) return;
                // Leuchten hinter dem Symbol pulsiert zweimal
                var pulse = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1600) };
                pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
                pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))));
                pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000))));
                pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.35, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1600))));
                glow.BeginAnimation(UIElement.OpacityProperty, pulse);
            };
            return AlertLook.WithWash(g, color);
        }

        public override FrameworkElement BuildMinimal() => icon?.Invoke() ?? Ui.Glyph("", 14, Ui.Br(color));
    }
}

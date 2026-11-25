using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace DynamicIsland
{
    /// <summary>
    /// Gemeinsamer, aufgeräumter Aufbau für die Player-Seiten (Spotify und andere Medien):
    /// großes Cover links mit weichem Farb-Leuchten, rechts Titel, Spulen und Steuerung.
    /// Animationen wie bei Apple Music: Cover schrumpft beim Pausieren, Titel gleitet beim Songwechsel herein.
    /// </summary>
    public sealed class PlayerUi
    {
        public const double ArtSize = 124;

        public Image Art;
        public FrameworkElement ArtBox;
        readonly ScaleTransform artScale = new ScaleTransform(1, 1);
        readonly Border glow;
        readonly TranslateTransform textSlide = new TranslateTransform();
        FrameworkElement texts;
        string shownKey;
        bool? shownPlaying;
        Color shownColor;

        // Songwechsel: altes Cover gleitet hinaus, neues herein (Richtung nach Weiter/Zurück)
        readonly Image ghost;
        readonly TranslateTransform artSlide = new TranslateTransform(), ghostSlide = new TranslateTransform();
        readonly ScaleTransform ghostScale = new ScaleTransform(1, 1);
        readonly SolidColorBrush glowBrush = new SolidColorBrush(Color.FromArgb(200, 0x90, 0x90, 0x98));
        readonly ScaleTransform glowScale = new ScaleTransform(1, 1);
        readonly BlurEffect textBlur = new BlurEffect { Radius = 0 };
        ImageSource lastSource;
        int hintDir = 1;
        DateTime hintAt;

        public PlayerUi(Action onArtClick, string artTip)
        {
            var box = Ui.ArtBox(ArtSize, 18, out Art);
            box.Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Direction = 270, Opacity = 0.55, Color = Colors.Black };
            // Cover in eine abgerundete „Bühne“ legen, damit es beim Hinein-/Hinausgleiten sauber abgeschnitten wird
            box.Children.Remove(Art);
            Art.Clip = null;
            Art.RenderTransform = artSlide;
            ghost = new Image
            {
                Width = ArtSize, Height = ArtSize, Stretch = Stretch.UniformToFill, Opacity = 0, IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup { Children = { ghostScale, ghostSlide } }
            };
            RenderOptions.SetBitmapScalingMode(ghost, BitmapScalingMode.HighQuality);
            var stage = new Grid { Width = ArtSize, Height = ArtSize, Clip = new RectangleGeometry(new Rect(0, 0, ArtSize, ArtSize), 18, 18) };
            stage.Children.Add(ghost);
            stage.Children.Add(Art);
            box.Children.Add(stage);
            glow = new Border
            {
                Width = ArtSize * 0.9, Height = ArtSize * 0.9,
                CornerRadius = new CornerRadius(ArtSize * 0.45),
                Background = glowBrush,
                Effect = new BlurEffect { Radius = 34 },
                IsHitTestVisible = false,
                Opacity = 0.75,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = glowScale
            };
            if (!Ui.ReduceMotion)
            {
                // Leuchten atmet langsam mit der Musik
                var breathe = new DoubleAnimation(0.94, 1.08, TimeSpan.FromSeconds(2.6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                glowScale.BeginAnimation(ScaleTransform.ScaleXProperty, breathe);
                glowScale.BeginAnimation(ScaleTransform.ScaleYProperty, breathe);
            }
            var host = new Grid
            {
                Width = ArtSize, Height = ArtSize,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = artScale,
                Cursor = Cursors.Hand,
                ToolTip = artTip
            };
            host.Children.Add(glow);
            host.Children.Add(box);
            host.MouseLeftButtonDown += (s, e) => e.Handled = true;
            host.MouseLeftButtonUp += (s, e) => { e.Handled = true; onArtClick?.Invoke(); };
            ArtBox = host;
        }

        /// <summary>Cover links, rechts: Kopfzeile (Titel …), Spulen, Steuerung.</summary>
        public FrameworkElement Layout(FrameworkElement top, FrameworkElement titleBlock, FrameworkElement seek, FrameworkElement controls)
        {
            texts = titleBlock;
            if (texts != null) { texts.RenderTransform = textSlide; texts.Effect = textBlur; }
            var right = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            seek.Margin = new Thickness(0, 10, 0, 0);
            controls.Margin = new Thickness(0, 4, 0, 0);
            controls.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetRow(seek, 1);
            Grid.SetRow(controls, 2);
            right.Children.Add(top);
            right.Children.Add(seek);
            right.Children.Add(controls);

            var g = new Grid { Margin = new Thickness(24, 14, 24, 12), VerticalAlignment = VerticalAlignment.Center };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(right, 1);
            g.Children.Add(ArtBox);
            g.Children.Add(right);
            shownKey = null;
            shownPlaying = null;
            return g;
        }

        bool waitArt;
        DateTime waitSince;
        /// <summary>Zum Vorführen: ±1 löst beim nächsten Takt einen Songwechsel in dieser Richtung aus.</summary>
        public static int TestChange;

        /// <summary>Weiter (+1) oder Zurück (−1) gedrückt: bestimmt, in welche Richtung der nächste Songwechsel gleitet.</summary>
        public void Hint(int dir) { hintDir = dir < 0 ? -1 : 1; hintAt = DateTime.Now; }

        int Dir => (DateTime.Now - hintAt).TotalSeconds < 5 ? hintDir : 1;

        static readonly IEasingFunction Out = new CubicEase { EasingMode = EasingMode.EaseOut };
        static readonly IEasingFunction In = new CubicEase { EasingMode = EasingMode.EaseIn };

        void SongChange(bool playing)
        {
            int dir = Dir;
            // Titel: gleitet aus der Richtung herein und wird dabei scharf
            textSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dir * 34, 0, TimeSpan.FromMilliseconds(520)) { EasingFunction = new SpringEase { Damping = 0.75 } });
            texts?.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360)) { EasingFunction = Out });
            textBlur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(420)) { EasingFunction = Out });

            if (Art.Source == lastSource && lastSource != null) { waitArt = true; waitSince = DateTime.Now; return; }
            SlideCover(playing);
        }

        void SlideCover(bool playing)
        {
            int dir = Dir;
            double d = ArtSize * 0.75;
            var dur = TimeSpan.FromMilliseconds(520);
            // altes Cover: gleitet hinaus, wird kleiner und blendet aus
            if (lastSource != null)
            {
                ghost.Source = lastSource;
                ghost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(380)) { EasingFunction = In });
                ghostSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, -dir * d, TimeSpan.FromMilliseconds(420)) { EasingFunction = In });
                var shrink = new DoubleAnimation(1, 0.82, TimeSpan.FromMilliseconds(420)) { EasingFunction = In };
                ghostScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
                ghostScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
            }
            // neues Cover: kommt aus der anderen Seite und federt ein
            artSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dir * d, 0, dur) { EasingFunction = new SpringEase { Damping = 0.72 } });
            Art.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = Out });
            var pop = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(620) };
            double b = playing ? 1 : 0.88;
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(b * 0.93, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140)), Out));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(b, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(620)), new SpringEase { Damping = 0.55 }));
            artScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            artScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            // Leuchten blitzt kurz auf
            glow.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(playing ? 1 : 0.5, playing ? 0.75 : 0.3, TimeSpan.FromMilliseconds(900)) { EasingFunction = Out });
            lastSource = Art.Source;
        }

        /// <summary>Weiter/Zurück-Knopf: Symbol stupst in seine Richtung (wie bei Apple Music).</summary>
        public static void Nudge(FrameworkElement button, int dir)
        {
            if (Ui.ReduceMotion || button == null) return;
            var target = (button as Border)?.Child as FrameworkElement ?? button;
            var tt = target.RenderTransform as TranslateTransform;
            if (tt == null || tt.IsFrozen) { tt = new TranslateTransform(); target.RenderTransform = tt; }
            var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(460) };
            a.KeyFrames.Add(new EasingDoubleKeyFrame(dir * 7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90)), Out));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(460)), new SpringEase { Damping = 0.5 }));
            tt.BeginAnimation(TranslateTransform.XProperty, a);
        }

        /// <summary>Kleine Island beim Songwechsel: Cover federt auf, Titel rollt von unten herein.</summary>
        public static void CompactChange(FrameworkElement art, FrameworkElement title)
        {
            if (Ui.ReduceMotion) return;
            if (art != null)
            {
                var st = art.RenderTransform as ScaleTransform;
                if (st == null || st.IsFrozen) { st = new ScaleTransform(1, 1); art.RenderTransform = st; art.RenderTransformOrigin = new Point(0.5, 0.5); }
                var p = new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(560)) { EasingFunction = new SpringEase { Damping = 0.5 } };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, p);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, p);
                art.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(320)));
            }
            if (title != null)
            {
                var tt = title.RenderTransform as TranslateTransform;
                if (tt == null || tt.IsFrozen) { tt = new TranslateTransform(); title.RenderTransform = tt; }
                tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(460)) { EasingFunction = new SpringEase { Damping = 0.7 } });
                title.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360)) { EasingFunction = Out });
            }
        }

        /// <summary>Jeden Takt: Songwechsel, Play/Pause und Farbe animieren.</summary>
        public void Animate(string trackKey, bool playing, Color accent, TextBlock playIcon)
        {
            bool motion = !Ui.ReduceMotion;
            if (accent != shownColor)
            {
                bool firstColor = shownColor == default;
                shownColor = accent;
                var c = accent == Colors.White ? Color.FromRgb(0x90, 0x90, 0x98) : accent;
                c.A = 200;
                // Farbe des Leuchtens gleitet zum neuen Cover über statt zu springen
                if (motion && !firstColor) glowBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(c, TimeSpan.FromMilliseconds(700)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
                else { glowBrush.BeginAnimation(SolidColorBrush.ColorProperty, null); glowBrush.Color = c; }
            }
            if (TestChange != 0)
            {
                // Vorführung (/test?event=song): Songwechsel ohne echten Titelwechsel
                Hint(TestChange); TestChange = 0;
                lastSource = Art.Source;
                if (motion) { SongChange(playing); waitArt = false; SlideCover(playing); }
                return;
            }
            if (trackKey != shownKey)
            {
                bool first = shownKey == null;
                shownKey = trackKey;
                if (!first && motion) SongChange(playing);
            }
            else if (waitArt)
            {
                // Titel kam vor dem Cover (Windows-Medien): Cover-Wechsel nachholen, sobald es da ist
                if (Art.Source != lastSource) { waitArt = false; SlideCover(playing); }
                else if ((DateTime.Now - waitSince).TotalSeconds > 3) waitArt = false;
            }
            else lastSource = Art.Source;   // für den nächsten Wechsel: dieses Cover gleitet dann hinaus
            if (shownPlaying != playing)
            {
                bool first = shownPlaying == null;
                shownPlaying = playing;
                double to = playing ? 1 : 0.88;      // pausiert: Cover etwas kleiner, Leuchten schwächer
                if (first || !motion)
                {
                    artScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    artScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    artScale.ScaleX = artScale.ScaleY = to;
                    glow.Opacity = playing ? 0.75 : 0.3;
                }
                else
                {
                    var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(560)) { EasingFunction = new SpringEase { Damping = playing ? 0.62 : 0.9 } };
                    artScale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                    artScale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
                    glow.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(playing ? 0.75 : 0.3, TimeSpan.FromMilliseconds(500)));
                    if (playIcon != null)
                    {
                        var st = playIcon.RenderTransform as ScaleTransform;
                        if (st == null || st.IsFrozen) { st = new ScaleTransform(1, 1); playIcon.RenderTransform = st; playIcon.RenderTransformOrigin = new Point(0.5, 0.5); }
                        var p = new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = new SpringEase { Damping = 0.55 } };
                        st.BeginAnimation(ScaleTransform.ScaleXProperty, p);
                        st.BeginAnimation(ScaleTransform.ScaleYProperty, p);
                    }
                }
            }
        }
    }
}

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
    /// Animationen wie bei Apple Music: Cover schrumpft beim Pausieren (Schatten wird flacher),
    /// Songwechsel gleitet in Richtung Weiter/Zurück, Play/Pause verwandelt sich statt zu springen.
    /// </summary>
    public sealed class PlayerUi
    {
        public const double ArtSize = 124;
        const double PausedScale = 0.88;
        const double GlowPlaying = 0.7, GlowPaused = 0.22;
        public const string PlayGlyph = "", PauseGlyph = "";

        public Image Art;
        public FrameworkElement ArtBox;
        readonly ScaleTransform artScale = new ScaleTransform(1, 1);
        readonly Border glow;
        readonly DropShadowEffect shadow;
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
        ImageSource lastSource;
        int hintDir = 1;
        DateTime hintAt;

        public PlayerUi(Action onArtClick, string artTip)
        {
            var box = Ui.ArtBox(ArtSize, 18, out Art);
            shadow = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 5, Direction = 270, Opacity = 0.55, Color = Colors.Black };
            box.Effect = shadow;
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
                Effect = new BlurEffect { Radius = 34, RenderingBias = RenderingBias.Performance },
                IsHitTestVisible = false,
                Opacity = GlowPlaying,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = glowScale
            };
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
            if (texts != null) texts.RenderTransform = textSlide;
            var right = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 0, 0) };
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            seek.Margin = new Thickness(0, 10, 0, 0);
            controls.Margin = new Thickness(0, 2, 0, 0);
            controls.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetRow(seek, 1);
            Grid.SetRow(controls, 2);
            right.Children.Add(top);
            right.Children.Add(seek);
            right.Children.Add(controls);

            var g = new Grid { Margin = new Thickness(24, 16, 24, 12), VerticalAlignment = VerticalAlignment.Center };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(right, 1);
            g.Children.Add(ArtBox);
            g.Children.Add(right);
            shownKey = null;
            shownPlaying = null;
            return g;
        }

        /// <summary>Einheitlicher Fortschrittsbalken für beide Player: schlank und hell, Knopf erst beim Drüberfahren.</summary>
        public static DragBar SeekBar(Brush fill = null) =>
            new DragBar(fill ?? Ui.Br(Colors.White, 225), 4) { AutoKnob = true };

        bool waitArt;
        DateTime waitSince;
        /// <summary>Zum Vorführen: ±1 löst beim nächsten Takt einen Songwechsel in dieser Richtung aus.</summary>
        public static int TestChange;
        /// <summary>Zum Vorführen: true schaltet einmal Play/Pause um (nur die Animation).</summary>
        public static bool TestToggle;

        /// <summary>Weiter (+1) oder Zurück (−1) gedrückt: bestimmt, in welche Richtung der nächste Songwechsel gleitet.</summary>
        public void Hint(int dir) { hintDir = dir < 0 ? -1 : 1; hintAt = DateTime.Now; }

        int Dir => (DateTime.Now - hintAt).TotalSeconds < 5 ? hintDir : 1;

        static readonly IEasingFunction Out = new CubicEase { EasingMode = EasingMode.EaseOut };
        static readonly IEasingFunction In = new CubicEase { EasingMode = EasingMode.EaseIn };
        static readonly IEasingFunction Smooth = new QuinticEase { EasingMode = EasingMode.EaseOut };

        void SongChange(bool playing)
        {
            int dir = Dir;
            if (texts != null)
            {
                // Titel: gleitet aus der Richtung herein und wird dabei scharf.
                // Die Unschärfe hängt nur während der Animation am Text – sonst verliert er die scharfe Schriftglättung.
                var blur = new BlurEffect { Radius = 8, RenderingBias = RenderingBias.Performance };
                texts.Effect = blur;
                var unblur = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(380)) { EasingFunction = Out };
                unblur.Completed += (s, e) => { if (texts.Effect == blur) texts.Effect = null; };
                blur.BeginAnimation(BlurEffect.RadiusProperty, unblur);
                textSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dir * 26, 0, TimeSpan.FromMilliseconds(560)) { EasingFunction = Smooth });
                texts.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(340)) { EasingFunction = Out });
            }

            if (Art.Source == lastSource && lastSource != null) { waitArt = true; waitSince = DateTime.Now; return; }
            SlideCover(playing);
        }

        void SlideCover(bool playing)
        {
            int dir = Dir;
            double d = ArtSize * 0.6;
            // altes Cover: gleitet hinaus, wird kleiner und blendet aus
            if (lastSource != null)
            {
                ghost.Source = lastSource;
                ghost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(340)) { EasingFunction = In });
                ghostSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, -dir * d, TimeSpan.FromMilliseconds(400)) { EasingFunction = In });
                var shrink = new DoubleAnimation(1, 0.86, TimeSpan.FromMilliseconds(400)) { EasingFunction = In };
                ghostScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
                ghostScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
            }
            // neues Cover: kommt aus der anderen Seite und läuft weich aus (ohne Überschwingen – wirkt ruhiger)
            artSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dir * d, 0, TimeSpan.FromMilliseconds(560)) { EasingFunction = Smooth });
            Art.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)) { EasingFunction = Out });
            // ganz leichtes Einatmen der Größe
            double b = playing ? 1 : PausedScale;
            var pop = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(640) };
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(b * 0.95, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160)), Out));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(b, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(640)), Smooth));
            artScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            artScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            // Leuchten blitzt kurz auf
            double gl = playing ? GlowPlaying : GlowPaused;
            glow.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(Math.Min(1, gl + 0.3), gl, TimeSpan.FromMilliseconds(900)) { EasingFunction = Out });
            lastSource = Art.Source;
        }

        /// <summary>Weiter/Zurück-Knopf: Symbol stupst in seine Richtung (wie bei Apple Music).</summary>
        public static void Nudge(FrameworkElement button, int dir)
        {
            if (Ui.ReduceMotion || button == null) return;
            var target = (button as Border)?.Child as FrameworkElement ?? button;
            var tt = target.RenderTransform as TranslateTransform;
            if (tt == null || tt.IsFrozen) { tt = new TranslateTransform(); target.RenderTransform = tt; }
            var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(420) };
            a.KeyFrames.Add(new EasingDoubleKeyFrame(dir * 6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90)), Out));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)), Smooth));
            tt.BeginAnimation(TranslateTransform.XProperty, a);
        }

        /// <summary>Kleine Island beim Songwechsel: Cover dreht sich weich ein, Titel rollt von unten herein.</summary>
        public static void CompactChange(FrameworkElement art, FrameworkElement title)
        {
            if (Ui.ReduceMotion) return;
            if (art != null)
            {
                var st = art.RenderTransform as ScaleTransform;
                if (st == null || st.IsFrozen) { st = new ScaleTransform(1, 1); art.RenderTransform = st; art.RenderTransformOrigin = new Point(0.5, 0.5); }
                var p = new DoubleAnimation(0.7, 1, TimeSpan.FromMilliseconds(480)) { EasingFunction = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut } };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, p);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, p);
                art.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = Out });
            }
            if (title != null)
            {
                var tt = title.RenderTransform as TranslateTransform;
                if (tt == null || tt.IsFrozen) { tt = new TranslateTransform(); title.RenderTransform = tt; }
                tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(420)) { EasingFunction = Smooth });
                title.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = Out });
            }
        }

        // ---------- Play/Pause-Symbol: schrumpft, wechselt, federt zurück ----------
        bool iconMorph;
        string iconWant;

        void SetIcon(TextBlock icon, bool playing, bool animate)
        {
            if (icon == null) return;
            iconWant = playing ? PauseGlyph : PlayGlyph;
            if (iconMorph) return;                 // läuft gerade – der Abschluss setzt das gewünschte Symbol
            if (icon.Text == iconWant) return;
            var st = icon.RenderTransform as ScaleTransform;
            if (st == null || st.IsFrozen) { st = new ScaleTransform(1, 1); icon.RenderTransform = st; icon.RenderTransformOrigin = new Point(0.5, 0.5); }
            if (!animate || icon.Text.Length == 0)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                icon.BeginAnimation(UIElement.OpacityProperty, null);
                st.ScaleX = st.ScaleY = 1; icon.Opacity = 1;
                icon.Text = iconWant;
                return;
            }
            iconMorph = true;
            var down = new DoubleAnimation(0.4, TimeSpan.FromMilliseconds(110)) { EasingFunction = In };
            down.Completed += (s, e) =>
            {
                icon.Text = iconWant;
                var up = new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(380)) { EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut } };
                up.Completed += (s2, e2) => iconMorph = false;
                st.BeginAnimation(ScaleTransform.ScaleXProperty, up);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, up);
                icon.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
            };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, down);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, down);
            icon.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.25, TimeSpan.FromMilliseconds(110)));
        }

        // ---------- Leuchten atmet nur, während Musik läuft ----------
        bool breathing;
        void Breathe(bool on)
        {
            if (Ui.ReduceMotion) on = false;
            if (on == breathing) return;
            breathing = on;
            if (on)
            {
                var a = new DoubleAnimation(0.95, 1.07, TimeSpan.FromSeconds(2.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                glowScale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                glowScale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            }
            else
            {
                // pausiert: ruhig auf 1 zurück und dann stehen bleiben (spart Rechenzeit – das Leuchten ist weichgezeichnet)
                var a = new DoubleAnimation(1, TimeSpan.FromMilliseconds(600)) { EasingFunction = Out, FillBehavior = FillBehavior.Stop };
                a.Completed += (s, e) => { if (!breathing) { glowScale.ScaleX = glowScale.ScaleY = 1; } };
                glowScale.ScaleX = glowScale.ScaleY = 1;
                glowScale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                glowScale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            }
        }

        /// <summary>Jeden Takt: Songwechsel, Play/Pause, Symbol und Farbe animieren.</summary>
        public void Animate(string trackKey, bool playing, Color accent, TextBlock playIcon)
        {
            bool motion = !Ui.ReduceMotion;
            if (TestToggle) { TestToggle = false; testUntil = DateTime.Now.AddSeconds(2.5); }
            if (DateTime.Now < testUntil) playing = !playing;   // Vorführung: kurz den anderen Zustand zeigen
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
            }
            else if (trackKey != shownKey)
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

            bool firstState = shownPlaying == null;
            SetIcon(playIcon, playing, motion && !firstState);
            if (shownPlaying != playing)
            {
                shownPlaying = playing;
                Breathe(playing);
                double to = playing ? 1 : PausedScale;      // pausiert: Cover etwas kleiner, Leuchten und Schatten schwächer
                double gl = playing ? GlowPlaying : GlowPaused;
                if (firstState || !motion)
                {
                    artScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    artScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    artScale.ScaleX = artScale.ScaleY = to;
                    glow.BeginAnimation(UIElement.OpacityProperty, null);
                    glow.Opacity = gl;
                    shadow.Opacity = playing ? 0.55 : 0.3;
                    shadow.BlurRadius = playing ? 20 : 12;
                    shadow.ShadowDepth = playing ? 5 : 2;
                }
                else
                {
                    // Play: federt groß (leichtes Überschwingen). Pause: sinkt ruhig zusammen.
                    IEasingFunction ease = playing ? new BackEase { Amplitude = 0.45, EasingMode = EasingMode.EaseOut } : Smooth;
                    var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(playing ? 520 : 460)) { EasingFunction = ease };
                    artScale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                    artScale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
                    glow.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(gl, TimeSpan.FromMilliseconds(500)) { EasingFunction = Out });
                    var sd = TimeSpan.FromMilliseconds(460);
                    shadow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(playing ? 0.55 : 0.3, sd) { EasingFunction = Out });
                    shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(playing ? 20 : 12, sd) { EasingFunction = Out });
                    shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, new DoubleAnimation(playing ? 5 : 2, sd) { EasingFunction = Out });
                }
            }
        }

        static DateTime testUntil;
    }
}

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

        public PlayerUi(Action onArtClick, string artTip)
        {
            var box = Ui.ArtBox(ArtSize, 18, out Art);
            box.Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Direction = 270, Opacity = 0.55, Color = Colors.Black };
            glow = new Border
            {
                Width = ArtSize * 0.9, Height = ArtSize * 0.9,
                CornerRadius = new CornerRadius(ArtSize * 0.45),
                Background = Ui.Br(Colors.White, 0),
                Effect = new BlurEffect { Radius = 34 },
                IsHitTestVisible = false,
                Opacity = 0.75
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

        /// <summary>Jeden Takt: Songwechsel, Play/Pause und Farbe animieren.</summary>
        public void Animate(string trackKey, bool playing, Color accent, TextBlock playIcon)
        {
            bool motion = !Ui.ReduceMotion;
            if (accent != shownColor)
            {
                shownColor = accent;
                glow.Background = Ui.Br(accent == Colors.White ? Color.FromRgb(0x90, 0x90, 0x98) : accent, 200);
            }
            if (trackKey != shownKey)
            {
                bool first = shownKey == null;
                shownKey = trackKey;
                if (!first && motion)
                {
                    // Cover blendet über und federt, Titel gleitet von rechts herein
                    Art.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.25, 1, TimeSpan.FromMilliseconds(420)));
                    textSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(26, 0, TimeSpan.FromMilliseconds(460)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                    texts?.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(380)));
                    var pop = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(520) };
                    double b = playing ? 1 : 0.88;
                    pop.KeyFrames.Add(new EasingDoubleKeyFrame(b * 0.92, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
                    pop.KeyFrames.Add(new EasingDoubleKeyFrame(b, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520)), new SpringEase { Damping = 0.6 }));
                    artScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                    artScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
                }
            }
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

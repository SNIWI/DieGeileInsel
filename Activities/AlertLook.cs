using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DynamicIsland
{
    /// <summary>
    /// Designs für Hinweise (Einstellungen → Animationen → Hinweise → Design):
    ///   small   – Klein: flache Pille, kleines Symbol im getönten Kreis, eine Zeile
    ///   normal  – Groß: großes Symbol im Kreis mit Leuchten, zwei Zeilen
    ///   ios     – Mitteilung: App-Symbol im abgerundeten Quadrat, Titel + „jetzt“, darunter der Text
    ///   minimal – Minimal: nur das farbige Symbol, eine schmale Zeile, kein Kreis
    ///   glow    – Leuchtend: Symbol im kräftig leuchtenden Kreis, farbiger Schimmer von links, Titel in der Farbe
    /// </summary>
    public static class AlertLook
    {
        public static string D => Ui.AlertDesign ?? "small";
        public static bool OneLine => D == "small" || D == "minimal";

        public static readonly (string Key, string Name)[] Designs =
        {
            ("small", "Klein"), ("normal", "Groß"), ("ios", "Mitteilung"), ("minimal", "Minimal"), ("glow", "Leuchtend"),
        };

        static Color A(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

        /// <summary>Färbt die Symbol-Fläche passend zum Design (Kreis, abgerundetes Quadrat oder nichts).</summary>
        public static void StyleBox(Border b, Color c, double size)
        {
            b.Width = size;
            b.Height = size;
            b.BorderThickness = new Thickness(0);
            b.BorderBrush = null;
            switch (D)
            {
                case "minimal":
                    b.Background = Brushes.Transparent;
                    b.CornerRadius = new CornerRadius(size / 2);
                    break;
                case "ios":
                    b.CornerRadius = new CornerRadius(size * 0.27);
                    b.Background = new LinearGradientBrush(A(c, 120), A(c, 45), 90);
                    b.BorderBrush = Ui.Br(c, 80);
                    b.BorderThickness = new Thickness(0.8);
                    break;
                case "glow":
                    b.CornerRadius = new CornerRadius(size / 2);
                    b.Background = Ui.Br(c, 95);
                    b.BorderBrush = Ui.Br(c, 210);
                    b.BorderThickness = new Thickness(1.2);
                    break;
                default:
                    b.CornerRadius = new CornerRadius(size / 2);
                    b.Background = Ui.Br(c, (byte)(D == "normal" ? 60 : 55));
                    break;
            }
        }

        /// <summary>Weiches Leuchten hinter dem Symbol (null = keins in diesem Design).</summary>
        public static Border GlowBehind(Color c, double size)
        {
            byte a; double blur;
            switch (D)
            {
                case "minimal": return null;
                case "ios": a = 55; blur = 10; break;
                case "glow": a = 190; blur = 18; break;
                case "normal": a = 120; blur = 14; break;
                default: a = 70; blur = 8; break;
            }
            return new Border
            {
                Width = size + 2, Height = size + 2,
                CornerRadius = D == "ios" ? new CornerRadius(size * 0.3) : new CornerRadius(size / 2 + 1),
                Background = Ui.Br(c, a),
                Effect = new BlurEffect { Radius = blur },
                IsHitTestVisible = false
            };
        }

        /// <summary>Hintergrund der ganzen Pille: „Leuchtend“ bekommt einen farbigen Schimmer von links.</summary>
        public static Brush Wash(Color c)
        {
            if (D != "glow") return null;
            return new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(A(c, 95), 0),
                new GradientStop(A(c, 30), 0.45),
                new GradientStop(Colors.Transparent, 0.85)
            }, new Point(0, 0.5), new Point(1, 0.5));
        }

        /// <summary>Titelfarbe: „Leuchtend“ färbt den Titel in der Ereignisfarbe.</summary>
        public static Brush TitleBrush(Color c, Color? custom) =>
            custom.HasValue ? Ui.Br(custom.Value) : D == "glow" ? Ui.Br(CatStyle.Mix(c, Colors.White, 0.25)) : Ui.White;

        /// <summary>Höhe einer Kurz-Meldung (Spotlight).</summary>
        public static double SpotHeight => D switch { "small" => 36, "minimal" => 34, "ios" => 58, _ => 52 };
        /// <summary>Abstand des Symbols vom linken Rand einer Kurz-Meldung (das ✓ rechts hält denselben Abstand).</summary>
        public static double SpotPadLeft => D switch { "small" => 6, "minimal" => 9, "ios" => 10, _ => 7 };
        /// <summary>Größe der Symbol-Fläche einer Kurz-Meldung.</summary>
        public static double SpotIcon => D switch { "small" => 24, "minimal" => 22, "ios" => 36, "glow" => 36, _ => 38 };

        /// <summary>Höhe eines Banners (Nachricht, Gerät …).</summary>
        public static double BannerHeight => D switch { "small" => 50, "minimal" => 40, "ios" => 64, "glow" => 64, _ => 70 };
        public static double BannerIcon => D switch { "small" => 32, "minimal" => 24, "ios" => 40, "glow" => 40, _ => 42 };

        /// <summary>Hülle, die den Inhalt mit dem Design-Hintergrund über die ganze Pille legt.</summary>
        public static FrameworkElement WithWash(FrameworkElement content, Color c)
        {
            var w = Wash(c);
            if (w == null) return content;
            var g = new Grid { Background = w };
            g.Children.Add(content);
            return g;
        }
    }
}

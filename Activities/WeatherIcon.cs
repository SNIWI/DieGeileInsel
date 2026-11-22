using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DynamicIsland
{
    /// <summary>
    /// Farbige Wetter-Symbole als Vektorgrafik (WPF kann farbige Emojis nicht darstellen – sie wären nur einfarbig).
    /// Sonne gelb, Mond hell, Wolken weiß/grau, Regen blau, Schnee hellblau, Blitz gelb. Zeichenfläche 24 × 24.
    /// </summary>
    public static class WeatherIcon
    {
        static readonly Brush SunFill = Grad(Color.FromRgb(0xFF, 0xE0, 0x4D), Color.FromRgb(0xFF, 0xA8, 0x1A));
        static readonly Brush SunRay = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0xC2, 0x2E)));
        static readonly Brush MoonFill = Grad(Color.FromRgb(0xFF, 0xF3, 0xC4), Color.FromRgb(0xF2, 0xD3, 0x6B));
        static readonly Brush CloudFill = Grad(Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0xCF, 0xD8, 0xE6));
        static readonly Brush CloudBack = Grad(Color.FromRgb(0xB4, 0xBE, 0xCD), Color.FromRgb(0x8C, 0x97, 0xA8));
        static readonly Brush StormCloud = Grad(Color.FromRgb(0x9A, 0xA3, 0xB3), Color.FromRgb(0x5E, 0x67, 0x78));
        static readonly Brush Rain = Frozen(new SolidColorBrush(Color.FromRgb(0x3F, 0xA9, 0xFF)));
        static readonly Brush Ice = Frozen(new SolidColorBrush(Color.FromRgb(0x8F, 0xE3, 0xFF)));
        static readonly Brush Snow = Frozen(new SolidColorBrush(Color.FromRgb(0xE4, 0xF4, 0xFF)));
        static readonly Brush Bolt = Grad(Color.FromRgb(0xFF, 0xE6, 0x5C), Color.FromRgb(0xFF, 0xB0, 0x1F));
        static readonly Brush Fog = Frozen(new SolidColorBrush(Color.FromRgb(0xC2, 0xCA, 0xD6)));

        /// <summary>Symbol für einen WMO-Wettercode (Open-Meteo) in der gewünschten Größe.</summary>
        public static FrameworkElement Create(int code, bool day, double size)
        {
            var c = new Canvas { Width = 24, Height = 24 };
            switch (code)
            {
                case 0:
                    if (day) Sun(c, 12, 12, 1); else Moon(c, 12, 12, 1);
                    break;
                case 1:
                    if (day) Sun(c, 10, 10, 0.85); else Moon(c, 10, 10, 0.85);
                    Cloud(c, 9, 10.5, 0.62, CloudFill);
                    break;
                case 2:
                    if (day) Sun(c, 8, 8, 0.7); else Moon(c, 8.5, 8, 0.7);
                    Cloud(c, 2, 4.5, 0.9, CloudFill);
                    break;
                case 3:
                    Cloud(c, 5, 0, 0.75, CloudBack);
                    Cloud(c, 0.5, 4.5, 0.9, CloudFill);
                    break;
                case 45: case 48:
                    Cloud(c, 2, -1.5, 0.85, CloudFill);
                    Line(c, 4, 16, 20, 16, Fog, 1.8);
                    Line(c, 6, 19.5, 18, 19.5, Fog, 1.8);
                    Line(c, 8, 23, 16, 23, Fog, 1.8);
                    break;
                case 51: case 53: case 55: case 56: case 57:
                case 80: case 81: case 82:
                    if (day) Sun(c, 7.5, 6.5, 0.62); else Moon(c, 8, 6.5, 0.62);
                    Cloud(c, 2.5, 1.5, 0.85, CloudFill);
                    Drops(c, code == 56 || code == 57 ? Ice : Rain, 2);
                    break;
                case 61: case 63: case 65: case 66: case 67:
                    Cloud(c, 1.5, -1.5, 0.9, CloudFill);
                    Drops(c, code >= 66 ? Ice : Rain, 3);
                    break;
                case 71: case 73: case 75: case 77: case 85: case 86:
                    Cloud(c, 1.5, -1.5, 0.9, CloudFill);
                    foreach (var (x, y) in new[] { (7.5, 18.5), (12, 21.5), (16.5, 18.5) }) Flake(c, x, y);
                    break;
                case 95: case 96: case 99:
                    Cloud(c, 1.5, -2, 0.9, StormCloud);
                    var bolt = new Polygon
                    {
                        Points = new PointCollection { new Point(12.6, 12.5), new Point(9, 18.6), new Point(11.7, 18.6), new Point(10.2, 23.6), new Point(15.6, 16.6), new Point(12.8, 16.6), new Point(14.6, 12.5) },
                        Fill = Bolt
                    };
                    c.Children.Add(bolt);
                    Line(c, 6.5, 16, 5.5, 18.5, Rain, 1.7);
                    Line(c, 18, 16, 17, 18.5, Rain, 1.7);
                    break;
                default:
                    Cloud(c, 1.5, 3, 0.9, CloudBack);
                    break;
            }
            size *= Ui.GlyphBoost;   // wächst auf Seiten mit größerer Schrift mit
            return new Viewbox { Width = size, Height = size, Child = c, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, SnapsToDevicePixels = true };
        }

        // ---------- Bausteine ----------
        static void Sun(Canvas c, double cx, double cy, double s)
        {
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4, r1 = 6.6 * s, r2 = 9.4 * s;
                Line(c, cx + Math.Cos(a) * r1, cy + Math.Sin(a) * r1, cx + Math.Cos(a) * r2, cy + Math.Sin(a) * r2, SunRay, 1.9 * s);
            }
            Dot(c, cx, cy, 4.8 * s, SunFill);
        }

        static void Moon(Canvas c, double cx, double cy, double s)
        {
            var g = new CombinedGeometry(GeometryCombineMode.Exclude,
                new EllipseGeometry(new Point(cx, cy), 7 * s, 7 * s),
                new EllipseGeometry(new Point(cx + 4.2 * s, cy - 3 * s), 5.8 * s, 5.8 * s));
            c.Children.Add(new Path { Data = g, Fill = MoonFill });
        }

        /// <summary>Wolke, Grundform etwa x 3–21 / y 6–19, verschoben (ox, oy) und skaliert (um die linke obere Ecke).</summary>
        static void Cloud(Canvas c, double ox, double oy, double s, Brush fill)
        {
            var g = new GeometryGroup { FillRule = FillRule.Nonzero };
            g.Children.Add(new EllipseGeometry(new Point(8, 13.5), 4.6, 4.6));
            g.Children.Add(new EllipseGeometry(new Point(13.2, 10.6), 5.6, 5.6));
            g.Children.Add(new EllipseGeometry(new Point(17.6, 14), 3.9, 3.9));
            g.Children.Add(new RectangleGeometry(new Rect(5, 13, 13, 5.9), 2.5, 2.5));
            var t = new TransformGroup();
            t.Children.Add(new ScaleTransform(s, s));
            t.Children.Add(new TranslateTransform(ox, oy));
            g.Transform = t;
            c.Children.Add(new Path { Data = g, Fill = fill });
        }

        static void Drops(Canvas c, Brush b, int n)
        {
            var xs = n == 2 ? new[] { 9.5, 15.5 } : new[] { 7.5, 12.5, 17.5 };
            for (int i = 0; i < xs.Length; i++)
            {
                double y = 17 + (i % 2) * 1.6;
                Line(c, xs[i], y, xs[i] - 1.4, y + 3.6, b, 1.9);
            }
        }

        static void Flake(Canvas c, double x, double y)
        {
            for (int i = 0; i < 3; i++)
            {
                double a = i * Math.PI / 3, r = 2.1;
                Line(c, x - Math.Cos(a) * r, y - Math.Sin(a) * r, x + Math.Cos(a) * r, y + Math.Sin(a) * r, Snow, 1.2);
            }
        }

        static void Dot(Canvas c, double cx, double cy, double r, Brush fill)
        {
            var e = new Ellipse { Width = r * 2, Height = r * 2, Fill = fill };
            Canvas.SetLeft(e, cx - r); Canvas.SetTop(e, cy - r);
            c.Children.Add(e);
        }

        static void Line(Canvas c, double x1, double y1, double x2, double y2, Brush b, double th) =>
            c.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = b, StrokeThickness = th, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });

        static Brush Grad(Color a, Color b) => Frozen(new LinearGradientBrush(a, b, 90));
        static Brush Frozen(Brush b) { b.Freeze(); return b; }
    }
}

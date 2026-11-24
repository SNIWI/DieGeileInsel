using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DynamicIsland
{
    /// <summary>Kleine, selbst gezeichnete App-Symbole (für die Seitensymbole oben in der großen Island).</summary>
    public static class Logos
    {
        public static readonly Color ClaudeOrange = Color.FromRgb(0xD9, 0x77, 0x57);
        public static readonly Color DiscordBlurple = Color.FromRgb(0x58, 0x65, 0xF2);

        /// <summary>Claude-Logo: Strahlenkranz aus spitz zulaufenden, gefüllten Strahlen
        /// (unregelmäßig wie im Original, innen breit und in der Mitte verschmolzen).</summary>
        public static FrameworkElement Claude(double size, double opacity = 1, Color? tint = null)
        {
            var c = new Canvas { Width = 24, Height = 24 };
            var brush = Ui.Br(tint ?? ClaudeOrange);
            double[] len = { 11.2, 9.4, 10.6, 8.8, 11.0, 9.6, 10.2, 8.9, 10.8, 9.7, 10.4 };
            double[] jit = { 0, 0.06, -0.04, 0.07, -0.03, 0.05, -0.06, 0.03, -0.02, 0.06, -0.05 };
            var geo = new StreamGeometry { FillRule = FillRule.Nonzero };
            using (var ctx = geo.Open())
            {
                var ctr = new Point(12, 12);
                for (int i = 0; i < len.Length; i++)
                {
                    double a = i * Math.PI * 2 / len.Length - Math.PI / 2 + jit[i];
                    var d = new Vector(Math.Cos(a), Math.Sin(a));
                    var p = new Vector(-d.Y, d.X);
                    double L = len[i], wMid = 1.45, wTip = 0.75;
                    ctx.BeginFigure(ctr + p * 1.0, true, true);
                    ctx.LineTo(ctr + d * (L * 0.42) + p * wMid, true, true);
                    ctx.LineTo(ctr + d * (L - 0.8) + p * wTip, true, true);
                    ctx.ArcTo(ctr + d * (L - 0.8) - p * wTip, new Size(wTip, wTip), 0, false, SweepDirection.Clockwise, true, true);
                    ctx.LineTo(ctr + d * (L * 0.42) - p * wMid, true, true);
                    ctx.LineTo(ctr - p * 1.0, true, true);
                }
            }
            geo.Freeze();
            c.Children.Add(new Ellipse { Width = 5, Height = 5, Fill = brush, Margin = new Thickness(9.5, 9.5, 0, 0) });
            c.Children.Add(new Path { Data = geo, Fill = brush, Stroke = brush, StrokeThickness = 0.5, StrokeLineJoin = PenLineJoin.Round });
            return Wrap(c, size, opacity);
        }

        /// <summary>Discord: blauer Controller-Kopf mit zwei Augen.</summary>
        public static FrameworkElement Discord(double size, double opacity = 1, Color? tint = null)
        {
            var c = new Canvas { Width = 24, Height = 24 };
            c.Children.Add(new Path
            {
                Data = Geometry.Parse("M 5,6 C 7.5,4.6 9.5,4.4 10,4.6 L 10.6,5.8 C 11.5,5.6 12.5,5.6 13.4,5.8 L 14,4.6 C 14.5,4.4 16.5,4.6 19,6 " +
                                      "C 21,9 21.6,12.5 21.3,16.4 C 19.6,17.7 17.9,18.4 16.3,18.8 L 15.2,17 C 16,16.7 16.6,16.4 17.2,16 " +
                                      "C 13.8,17.6 10.2,17.6 6.8,16 C 7.4,16.4 8,16.7 8.8,17 L 7.7,18.8 C 6.1,18.4 4.4,17.7 2.7,16.4 C 2.4,12.5 3,9 5,6 Z"),
                Fill = Ui.Br(tint ?? DiscordBlurple)
            });
            foreach (var x in new[] { 8.6, 15.4 })
            {
                var eye = new Ellipse { Width = 3.2, Height = 3.6, Fill = Brushes.Black };
                Canvas.SetLeft(eye, x - 1.6);
                Canvas.SetTop(eye, 10.4);
                c.Children.Add(eye);
            }
            return Wrap(c, size, opacity);
        }

        public static readonly Color TeamsPurple = Color.FromRgb(0x5B, 0x5F, 0xC7);

        /// <summary>Teams: lila Kachel mit weißem „T“, dahinter eine Person.</summary>
        public static FrameworkElement Teams(double size, double opacity = 1, Color? tint = null)
        {
            var c = new Canvas { Width = 24, Height = 24 };
            var light = tint ?? Color.FromRgb(0x7B, 0x83, 0xEB);
            var head = new Ellipse { Width = 5.6, Height = 5.6, Fill = Ui.Br(light) };
            Canvas.SetLeft(head, 15.6); Canvas.SetTop(head, 2.4);
            c.Children.Add(head);
            c.Children.Add(new Path { Data = Geometry.Parse("M 13,9.2 L 21.6,9.2 C 22.4,9.2 23,9.8 23,10.6 L 23,15.6 C 23,18.2 21,20 18.6,20 C 16.2,20 14.4,18.4 14,16.4 Z"), Fill = Ui.Br(light) });
            c.Children.Add(new Rectangle { Width = 14, Height = 14, RadiusX = 2.4, RadiusY = 2.4, Fill = Ui.Br(tint ?? TeamsPurple), Margin = new Thickness(1, 5.5, 0, 0) });
            c.Children.Add(new Path { Data = Geometry.Parse("M 4.4,9.2 L 12.6,9.2 L 12.6,11 L 9.6,11 L 9.6,17 L 7.4,17 L 7.4,11 L 4.4,11 Z"), Fill = tint.HasValue ? Brushes.Black : Brushes.White });
            return Wrap(c, size, opacity);
        }

        static FrameworkElement Wrap(Canvas c, double size, double opacity) =>
            new Viewbox { Width = size, Height = size, Child = c, Opacity = opacity, IsHitTestVisible = false };
    }
}

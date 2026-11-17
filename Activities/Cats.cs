using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DynamicIsland
{
    /// <summary>Eine Katze, wie sie in den Einstellungen gespeichert wird.</summary>
    public sealed class CatConfig
    {
        public string Name { get; set; } = "Mimi";
        public string Breed { get; set; } = "black";
        public string PhotoPath { get; set; } = "";
        public bool UsePhoto { get; set; }

        // Eigene Anpassungen ("" = so wie die gewählte Vorlage)
        public string FurColor { get; set; } = "";
        public string EyeColor { get; set; } = "";
        public string Pattern { get; set; } = "";
        /// <summary>none, collar, bow, scarf, hat, crown, glasses, flower, headphones</summary>
        public string Accessory { get; set; } = "none";
        public string AccessoryColor { get; set; } = "#FF453A";
        public bool Fluffy { get; set; }
        /// <summary>0,85 klein · 1 normal · 1,15 groß</summary>
        public double Size { get; set; } = 1;
        /// <summary>playful = verspielt, calm = ruhig, sleepy = verschlafen</summary>
        public string Mood { get; set; } = "calm";
    }

    public enum CatPattern { Plain, Tabby, Points, Tuxedo, Calico }

    /// <summary>Ein Aussehen zum Aussuchen (Fell, Augen, Muster).</summary>
    public sealed class CatBreed
    {
        public string Key, Name;
        public Color Fur, Eyes, Mark, Mark2;
        public CatPattern Pattern;
    }

    public static class CatStyle
    {
        static Color C(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

        public static readonly CatBreed[] Breeds =
        {
            new CatBreed { Key = "black",  Name = "Schwarz",   Fur = C(0x1C1C1E), Eyes = C(0xFFD60A), Pattern = CatPattern.Plain },
            new CatBreed { Key = "tuxedo", Name = "Smoking",   Fur = C(0x1C1C1E), Eyes = C(0x30D158), Mark = C(0xF5F5F7), Pattern = CatPattern.Tuxedo },
            new CatBreed { Key = "orange", Name = "Getigert",  Fur = C(0xE8893A), Eyes = C(0x30D158), Mark = C(0xB0561C), Pattern = CatPattern.Tabby },
            new CatBreed { Key = "grey",   Name = "Grau",      Fur = C(0x9A9AA0), Eyes = C(0xFFD60A), Mark = C(0x55555B), Pattern = CatPattern.Tabby },
            new CatBreed { Key = "siam",   Name = "Siam",      Fur = C(0xEFE3CF), Eyes = C(0x64D2FF), Mark = C(0x5B3A29), Pattern = CatPattern.Points },
            new CatBreed { Key = "calico", Name = "Glück",     Fur = C(0xF5F5F7), Eyes = C(0xFF9F0A), Mark = C(0xE8893A), Mark2 = C(0x2C2C2E), Pattern = CatPattern.Calico },
            new CatBreed { Key = "white",  Name = "Weiß",      Fur = C(0xF2F2F7), Eyes = C(0x64D2FF), Pattern = CatPattern.Plain },
            new CatBreed { Key = "blue",   Name = "Kartäuser", Fur = C(0x7C8794), Eyes = C(0xFF9F0A), Pattern = CatPattern.Plain },
            new CatBreed { Key = "cream",  Name = "Creme",     Fur = C(0xE9D3A8), Eyes = C(0x30D158), Mark = C(0xCFAF78), Pattern = CatPattern.Tabby },
        };

        public static readonly string[] Names = { "Mimi", "Luna", "Simba", "Nala", "Felix", "Kitty", "Oskar", "Minka" };

        public static readonly (string Key, string Name)[] Accessories =
        {
            ("none", "Nichts"), ("collar", "Halsband"), ("bow", "Schleife"), ("scarf", "Schal"), ("hat", "Partyhut"),
            ("crown", "Krone"), ("glasses", "Brille"), ("flower", "Blume"), ("headphones", "Kopfhörer"),
        };

        public static readonly string[] FurColors = { "#1C1C1E", "#48484A", "#7C8794", "#A9A9AF", "#F2F2F7", "#EFE3CF", "#E9D3A8", "#E8893A", "#A0522D", "#5C3B28" };
        public static readonly string[] EyeColors = { "#FFD60A", "#30D158", "#64D2FF", "#0A84FF", "#FF9F0A", "#BF5AF2", "#A2845E", "#FF453A" };
        public static readonly string[] AccentColors = { "#FF453A", "#FF375F", "#FF9F0A", "#FFD60A", "#30D158", "#64D2FF", "#0A84FF", "#BF5AF2", "#F5F5F7", "#1C1C1E" };

        public static bool TryColor(string hex, out Color c)
        {
            c = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            try { c = (Color)ColorConverter.ConvertFromString(hex.Trim()); return true; }
            catch { return false; }
        }

        /// <summary>Vorlage + deine Anpassungen = so sieht die Katze wirklich aus.</summary>
        public static CatBreed Resolve(CatConfig cfg)
        {
            var b = Get(cfg?.Breed);
            if (cfg == null) return b;
            var r = new CatBreed { Key = b.Key, Name = b.Name, Fur = b.Fur, Eyes = b.Eyes, Mark = b.Mark, Mark2 = b.Mark2, Pattern = b.Pattern };
            bool furChanged = TryColor(cfg.FurColor, out var fur);
            if (furChanged) r.Fur = fur;
            if (TryColor(cfg.EyeColor, out var eyes)) r.Eyes = eyes;
            bool patChanged = false;
            if (!string.IsNullOrEmpty(cfg.Pattern) && Enum.TryParse(cfg.Pattern, true, out CatPattern pat))
            {
                patChanged = pat != r.Pattern;
                r.Pattern = pat;
            }
            if (furChanged || patChanged)
            {
                // passende Fleck-Farben zur (neuen) Fellfarbe
                switch (r.Pattern)
                {
                    case CatPattern.Tabby: r.Mark = Mix(r.Fur, Lum(r.Fur) < 0.3 ? Colors.White : Colors.Black, 0.35); break;
                    case CatPattern.Points: r.Mark = Mix(r.Fur, Colors.Black, 0.68); break;
                    case CatPattern.Tuxedo: r.Mark = Lum(r.Fur) > 0.7 ? Color.FromRgb(0x1C, 0x1C, 0x1E) : Color.FromRgb(0xF5, 0xF5, 0xF7); break;
                    case CatPattern.Calico:
                        r.Mark = Color.FromRgb(0xE8, 0x89, 0x3A);
                        r.Mark2 = Color.FromRgb(0x2C, 0x2C, 0x2E);
                        break;
                }
            }
            return r;
        }

        public static CatBreed Get(string key)
        {
            foreach (var b in Breeds)
                if (string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase)) return b;
            return Breeds[0];
        }

        public static double Lum(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

        public static Color Mix(Color a, Color b, double t) => Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

        static readonly Dictionary<string, ImageSource> photoCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Lädt ein Katzenfoto (oder null, wenn es fehlt / kaputt ist).</summary>
        public static ImageSource LoadPhoto(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (photoCache.TryGetValue(path, out var cached)) return cached;
            ImageSource img = null;
            try
            {
                if (System.IO.File.Exists(path))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.DecodePixelWidth = 160;
                    bi.UriSource = new Uri(path, UriKind.Absolute);
                    bi.EndInit();
                    bi.Freeze();
                    img = bi;
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            if (img != null) photoCache[path] = img;
            return img;
        }

        /// <summary>Rundes Bild der Katze für Hinweise (Foto, sonst gezeichnet).</summary>
        public static FrameworkElement Avatar(CatConfig cfg, double size)
        {
            var photo = cfg != null ? LoadPhoto(cfg.PhotoPath) : null;
            if (photo != null)
                return new Ellipse
                {
                    Width = size,
                    Height = size,
                    VerticalAlignment = VerticalAlignment.Center,
                    Fill = new ImageBrush(photo) { Stretch = Stretch.UniformToFill }
                };
            return new CatView(cfg, size) { IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
        }
    }

    /// <summary>
    /// Eine kleine, gezeichnete Katze (oder dein Foto mit Ohren). Sie blinzelt, wedelt mit dem
    /// Schwanz, schaut sich um, schläft ein und schnurrt beim Anklicken.
    /// Gezeichnet auf einer 30×30-Fläche, die per Viewbox auf jede Größe skaliert wird.
    /// </summary>
    public sealed class CatView : Grid
    {
        static readonly Random Rnd = new Random();
        const string TailPath = "M 19,26 C 27,27 29.5,20 27,13";

        public readonly CatConfig Config;
        readonly Canvas art = new Canvas { Width = 30, Height = 30 };
        readonly Canvas pose = new Canvas { Width = 30, Height = 30 };          // sitzend (normal)
        Canvas curled;                                                           // eingerollt (Schlaf), nur gezeichnete Katzen
        readonly ScaleTransform poseScale = new ScaleTransform(1, 1, 15, 30);
        readonly ScaleTransform curlScale = new ScaleTransform(1, 1, 15, 30);
        readonly ScaleTransform face = new ScaleTransform(1, 1);                 // Blickrichtung beim Laufen
        readonly TranslateTransform walk = new TranslateTransform();
        readonly TranslateTransform pawL = new TranslateTransform(), pawR = new TranslateTransform();
        bool walking;

        /// <summary>Einstellungen (setzt die Island): im Schlaf zusammenrollen · neben der Island herumlaufen.</summary>
        public static bool CurlUp = true, Walk = true;
        /// <summary>Nur die Katzen neben der Island dürfen laufen (nicht auf dem Kratzbaum). Bereich in Pixeln um den Sitzplatz.</summary>
        public bool CanWalk { get; set; }
        public double WalkMin { get; set; } = -4;
        public double WalkMax { get; set; } = 56;
        readonly ScaleTransform breathe = new ScaleTransform(1, 1);
        readonly RotateTransform wiggle = new RotateTransform(0);
        readonly TranslateTransform hop = new TranslateTransform();
        readonly RotateTransform tailRot = new RotateTransform(0, 19, 26);
        readonly ScaleTransform eyeScale = new ScaleTransform(1, 1, 15, 11.3);
        readonly TranslateTransform eyeShift = new TranslateTransform();
        readonly TextBlock zzz;
        UIElement eyesOpen, eyesClosed;
        DateTime nextIdle;
        bool sleeping;
        DispatcherTimer happyTimer;

        public CatView(CatConfig cfg, double size = 30)
        {
            Config = cfg ?? new CatConfig();
            size *= Math.Max(0.7, Math.Min(1.3, Config.Size <= 0 ? 1 : Config.Size));
            Width = size;
            Height = size;
            Background = Brushes.Transparent; // ganze Fläche anklickbar
            Cursor = Cursors.Hand;
            ToolTip = Config.Name;
            nextIdle = DateTime.Now.AddSeconds(1.5 + Rnd.NextDouble() * 4);

            art.Children.Add(pose);
            pose.RenderTransform = poseScale;
            var vb = new Viewbox { Width = size, Height = size, Child = art, RenderTransformOrigin = new Point(0.5, 1) };
            var tg = new TransformGroup();
            tg.Children.Add(face);
            tg.Children.Add(breathe);
            tg.Children.Add(wiggle);
            tg.Children.Add(hop);
            vb.RenderTransform = tg;
            Children.Add(vb);
            RenderTransform = walk;

            var breed = CatStyle.Resolve(Config);
            var photo = Config.UsePhoto ? CatStyle.LoadPhoto(Config.PhotoPath) : null;
            if (photo != null) BuildPhoto(breed, photo);
            else { BuildDrawn(breed); BuildCurled(breed); }
            BuildAccessory();

            zzz = new TextBlock
            {
                Text = "z",
                FontFamily = Ui.TextFont,
                FontWeight = FontWeights.Bold,
                FontSize = Math.Max(8, size * 0.3),
                Foreground = Ui.White,
                Opacity = 0,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -2, -5, 0)
            };
            Children.Add(zzz);
            Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Direction = 270, Opacity = 0.5, Color = Colors.Black };
        }

        // ---------------------------------------------------------------
        //  Zeichnen
        // ---------------------------------------------------------------
        T Put<T>(T e, double x = 0, double y = 0) where T : UIElement
        {
            Canvas.SetLeft(e, x);
            Canvas.SetTop(e, y);
            pose.Children.Add(e);
            return e;
        }

        static void AddTo(Canvas c, UIElement e, double x, double y)
        {
            Canvas.SetLeft(e, x);
            Canvas.SetTop(e, y);
            c.Children.Add(e);
        }

        static Polygon Tri(Brush fill, Brush stroke, double sw, params double[] p)
        {
            var pc = new PointCollection();
            for (int i = 0; i + 1 < p.Length; i += 2) pc.Add(new Point(p[i], p[i + 1]));
            return new Polygon { Points = pc, Fill = fill, Stroke = stroke, StrokeThickness = sw, StrokeLineJoin = PenLineJoin.Round };
        }

        static Path Stroke(string data, Brush brush, double width) => new Path
        {
            Data = Geometry.Parse(data),
            Stroke = brush,
            StrokeThickness = width,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };

        /// <summary>Ebene, die auf eine Ellipse (Kopf/Körper) zugeschnitten ist – für Muster.</summary>
        static Canvas Clipped(Rect r) => new Canvas { Clip = new EllipseGeometry(r), IsHitTestVisible = false };

        void BuildDrawn(CatBreed b)
        {
            var fur = b.Fur;
            bool dark = CatStyle.Lum(fur) < 0.3;
            bool points = b.Pattern == CatPattern.Points;
            bool tux = b.Pattern == CatPattern.Tuxedo;
            var furB = Ui.Br(fur);
            var lineB = Ui.Br(dark ? CatStyle.Mix(fur, Colors.White, 0.38) : CatStyle.Mix(fur, Colors.Black, 0.45));
            var markB = Ui.Br(b.Mark);
            var pink = Ui.Br(Color.FromRgb(0xFF, 0x8F, 0xA3));
            var tailB = points ? markB : furB;
            var earB = points ? markB : furB;
            var pawB = points || tux ? markB : Ui.Br(CatStyle.Mix(fur, Colors.White, 0.3));

            // Schwanz (Umriss + Fell), dreht sich am Ansatz
            var tail = new Canvas { RenderTransform = tailRot };
            tail.Children.Add(Stroke(TailPath, lineB, 4.4));
            tail.Children.Add(Stroke(TailPath, tailB, 3));
            Put(tail);

            // Körper mit Muster
            Put(new Ellipse { Width = 16, Height = 15, Fill = furB, Stroke = lineB, StrokeThickness = 0.8 }, 7, 14);
            var bodyMarks = Clipped(new Rect(7, 14, 16, 15));
            if (b.Pattern == CatPattern.Tabby)
                bodyMarks.Children.Add(Stroke("M 7.5,19 Q 10,19.4 10.6,21.2 M 7.2,22.4 Q 9.8,22.8 10.4,24.6 M 22.5,19 Q 20,19.4 19.4,21.2 M 22.8,22.4 Q 20.2,22.8 19.6,24.6", markB, 1.1));
            if (tux) AddTo(bodyMarks, new Ellipse { Width = 8, Height = 11, Fill = markB }, 11, 16.5);
            if (b.Pattern == CatPattern.Calico)
            {
                AddTo(bodyMarks, new Ellipse { Width = 8, Height = 7, Fill = markB }, 6, 15);
                AddTo(bodyMarks, new Ellipse { Width = 7, Height = 6, Fill = Ui.Br(b.Mark2) }, 17, 21);
            }
            Put(bodyMarks);
            Put(new Ellipse { Width = 4.6, Height = 3, Fill = pawB, Stroke = lineB, StrokeThickness = 0.6, RenderTransform = pawL }, 9.6, 26.3);
            Put(new Ellipse { Width = 4.6, Height = 3, Fill = pawB, Stroke = lineB, StrokeThickness = 0.6, RenderTransform = pawR }, 15.8, 26.3);

            // Flauschig: Fellbüschel an den Wangen und an der Brust
            if (Config.Fluffy)
            {
                Put(Tri(furB, lineB, 0.7, 6.8, 9.5, 3.6, 12.6, 5.6, 13.1, 4.4, 15.4, 8, 15.6));
                Put(Tri(furB, lineB, 0.7, 23.2, 9.5, 26.4, 12.6, 24.4, 13.1, 25.6, 15.4, 22, 15.6));
                Put(Tri(furB, lineB, 0.7, 11.2, 18.2, 12.6, 21.4, 13.8, 19.6, 15, 22.2, 16.2, 19.6, 17.4, 21.4, 18.8, 18.2));
            }

            // Ohren + Kopf mit Muster
            Put(Tri(earB, lineB, 0.8, 6.6, 9.5, 8, 0.8, 13.5, 4.8));
            Put(Tri(earB, lineB, 0.8, 16.5, 4.8, 22, 0.8, 23.4, 9.5));
            Put(new Ellipse { Width = 18, Height = 15, Fill = furB, Stroke = lineB, StrokeThickness = 0.8 }, 6, 4);
            var headMarks = Clipped(new Rect(6, 4, 18, 15));
            switch (b.Pattern)
            {
                case CatPattern.Tabby:
                    headMarks.Children.Add(Stroke("M 13,4.5 L 13.6,8 M 15,4.2 L 15,7.8 M 17,4.5 L 16.4,8 M 6.5,12 L 9,12.4 M 23.5,12 L 21,12.4", markB, 1));
                    break;
                case CatPattern.Points:
                    AddTo(headMarks, new Ellipse { Width = 10, Height = 8, Fill = markB, Opacity = 0.9 }, 10, 9.5);
                    break;
                case CatPattern.Tuxedo:
                    AddTo(headMarks, new Ellipse { Width = 8, Height = 5.5, Fill = markB }, 11, 12.2);
                    break;
                case CatPattern.Calico:
                    AddTo(headMarks, new Ellipse { Width = 9, Height = 8, Fill = markB }, 4.5, 3);
                    AddTo(headMarks, new Ellipse { Width = 7, Height = 6.5, Fill = Ui.Br(b.Mark2) }, 18.5, 3.5);
                    break;
            }
            Put(headMarks);
            Put(Tri(pink, null, 0, 9, 6, 9.3, 3, 11.4, 4.9)).Opacity = 0.75;
            Put(Tri(pink, null, 0, 18.6, 4.9, 20.7, 3, 21, 6)).Opacity = 0.75;

            // Augen (offen + geschlossen)
            var eyesC = new Canvas();
            var eg = new TransformGroup();
            eg.Children.Add(eyeScale);
            eg.Children.Add(eyeShift);
            eyesC.RenderTransform = eg;
            var eyeB = Ui.Br(b.Eyes);
            AddTo(eyesC, new Ellipse { Width = 3.4, Height = 3.8, Fill = eyeB }, 9.6, 9.4);
            AddTo(eyesC, new Ellipse { Width = 3.4, Height = 3.8, Fill = eyeB }, 17, 9.4);
            AddTo(eyesC, new Ellipse { Width = 1.3, Height = 2.8, Fill = Brushes.Black }, 10.65, 9.9);
            AddTo(eyesC, new Ellipse { Width = 1.3, Height = 2.8, Fill = Brushes.Black }, 18.05, 9.9);
            Put(eyesC);
            eyesOpen = eyesC;

            var closedB = points ? Ui.Br(b.Fur) : dark ? lineB : Ui.Br(Colors.Black, 170);
            var closed = Stroke("M 9.6,11.4 Q 11.3,12.9 13,11.4 M 17,11.4 Q 18.7,12.9 20.4,11.4", closedB, 0.9);
            closed.Visibility = Visibility.Collapsed;
            Put(closed);
            eyesClosed = closed;

            // Nase, Mund, Schnurrhaare
            Put(Tri(pink, null, 0, 14.1, 13.6, 15.9, 13.6, 15, 14.7));
            Put(Stroke("M 15,14.7 Q 14.2,15.8 13.3,15.3 M 15,14.7 Q 15.8,15.8 16.7,15.3", points ? Ui.Br(b.Fur, 200) : lineB, 0.6));
            bool lightWhiskers = dark || points;
            Put(Stroke("M 12,14.2 L 7.4,13.5 M 12,15 L 7.5,15.7 M 18,14.2 L 22.6,13.5 M 18,15 L 22.5,15.7",
                lightWhiskers ? Ui.Br(Colors.White, 110) : Ui.Br(Colors.Black, 90), 0.4));
        }

        /// <summary>Eingerollt schlafen: liegender Körper, Kopf vorne auf den Pfoten, Schwanz um den Körper gelegt.</summary>
        void BuildCurled(CatBreed b)
        {
            var fur = b.Fur;
            bool dark = CatStyle.Lum(fur) < 0.3;
            bool points = b.Pattern == CatPattern.Points;
            bool tux = b.Pattern == CatPattern.Tuxedo;
            var furB = Ui.Br(fur);
            var lineB = Ui.Br(dark ? CatStyle.Mix(fur, Colors.White, 0.38) : CatStyle.Mix(fur, Colors.Black, 0.45));
            var markB = Ui.Br(b.Mark);
            var pink = Ui.Br(Color.FromRgb(0xFF, 0x8F, 0xA3));
            var tailB = points ? markB : furB;
            var earB = points ? markB : furB;
            var pawB = points || tux ? markB : Ui.Br(CatStyle.Mix(fur, Colors.White, 0.3));

            var c = new Canvas { Width = 30, Height = 30, Opacity = 0, IsHitTestVisible = false, RenderTransform = curlScale };
            void P(UIElement e, double x = 0, double y = 0) => AddTo(c, e, x, y);

            // Körper (liegend, rund) mit Muster
            P(new Ellipse { Width = 22, Height = 13, Fill = furB, Stroke = lineB, StrokeThickness = 0.8 }, 5.5, 16);
            var marks = Clipped(new Rect(5.5, 16, 22, 13));
            switch (b.Pattern)
            {
                case CatPattern.Tabby:
                    marks.Children.Add(Stroke("M 15.5,16.2 Q 16.6,18.4 15.9,20.6 M 19.4,16 Q 20.6,18.4 19.9,20.8 M 23.2,16.8 Q 24.3,18.8 23.6,20.8", markB, 1.1));
                    break;
                case CatPattern.Calico:
                    AddTo(marks, new Ellipse { Width = 8, Height = 6, Fill = markB }, 13, 15.5);
                    AddTo(marks, new Ellipse { Width = 7, Height = 6, Fill = Ui.Br(b.Mark2) }, 21, 20);
                    break;
                case CatPattern.Points:
                    AddTo(marks, new Ellipse { Width = 9, Height = 9, Fill = markB, Opacity = 0.55 }, 21, 19);
                    break;
            }
            P(marks);

            // Schwanz: um die Vorderseite gelegt
            const string tail = "M 26.6,22.6 C 29.4,28.4 22.4,30.2 14.6,29.8 C 10.6,29.6 7.6,28.9 6.2,27.4";
            P(Stroke(tail, lineB, 4.2));
            P(Stroke(tail, tailB, 2.8));

            // Pfoten unter dem Kinn
            P(new Ellipse { Width = 4.2, Height = 2.6, Fill = pawB, Stroke = lineB, StrokeThickness = 0.6 }, 5.4, 25.4);
            P(new Ellipse { Width = 4.2, Height = 2.6, Fill = pawB, Stroke = lineB, StrokeThickness = 0.6 }, 9.4, 25.8);

            // Kopf auf den Pfoten, Ohren angelegt
            P(Tri(earB, lineB, 0.8, 4.2, 18.4, 4.4, 12.8, 8.6, 15.6));
            P(Tri(earB, lineB, 0.8, 10.4, 15, 14, 12.4, 15, 17.6));
            P(new Ellipse { Width = 12, Height = 10.4, Fill = furB, Stroke = lineB, StrokeThickness = 0.8 }, 3.6, 15.2);
            var head = Clipped(new Rect(3.6, 15.2, 12, 10.4));
            if (b.Pattern == CatPattern.Points) AddTo(head, new Ellipse { Width = 7, Height = 5.5, Fill = markB, Opacity = 0.9 }, 6, 19.5);
            if (tux) AddTo(head, new Ellipse { Width = 5.5, Height = 4, Fill = markB }, 6.8, 21.8);
            if (b.Pattern == CatPattern.Tabby) head.Children.Add(Stroke("M 8.6,15.6 L 8.9,17.6 M 10.4,15.5 L 10.4,17.5", markB, 0.9));
            if (b.Pattern == CatPattern.Calico) AddTo(head, new Ellipse { Width = 6, Height = 6, Fill = markB }, 2.6, 14.4);
            P(head);
            P(Tri(pink, null, 0, 5.2, 17.4, 5.4, 14.4, 7.4, 16));
            P(Tri(pink, null, 0, 11.2, 15.6, 13.3, 13.8, 13.8, 16.6));
            var closedB = points ? Ui.Br(b.Fur) : dark ? lineB : Ui.Br(Colors.Black, 170);
            P(Stroke("M 6,20.6 Q 7.3,21.7 8.6,20.6 M 10.6,20.6 Q 11.9,21.7 13.2,20.6", closedB, 0.85));
            P(Tri(pink, null, 0, 9, 22.6, 10.6, 22.6, 9.8, 23.5));

            art.Children.Add(c);
            curled = c;
        }

        /// <summary>Zubehör obendrauf: Halsband, Schleife, Hut, Krone, Brille …</summary>
        void BuildAccessory()
        {
            var key = Config.Accessory ?? "none";
            if (key == "none") return;
            if (!CatStyle.TryColor(Config.AccessoryColor, out var c)) c = Color.FromRgb(0xFF, 0x45, 0x3A);
            var cb = Ui.Br(c);
            var edge = Ui.Br(CatStyle.Mix(c, Colors.Black, 0.35));
            var gold = Ui.Br(Color.FromRgb(0xFF, 0xD6, 0x0A));
            var white = Ui.Br(Color.FromRgb(0xF5, 0xF5, 0xF7));
            switch (key)
            {
                case "collar":
                    Put(Stroke("M 8.6,17.4 Q 15,21.4 21.4,17.4", edge, 2.4));
                    Put(Stroke("M 8.6,17.4 Q 15,21.4 21.4,17.4", cb, 1.6));
                    Put(new Ellipse { Width = 2.8, Height = 2.8, Fill = gold, Stroke = Ui.Br(Color.FromRgb(0xC9, 0x9A, 0x00)), StrokeThickness = 0.4 }, 13.6, 18.9);
                    break;
                case "scarf":
                    Put(Stroke("M 8.2,17.2 Q 15,21.8 21.8,17.2", edge, 3.4));
                    Put(Stroke("M 8.2,17.2 Q 15,21.8 21.8,17.2", cb, 2.6));
                    Put(Tri(cb, edge, 0.5, 17.6, 18.8, 19.8, 18.2, 20.8, 23.6, 18.6, 24));
                    Put(Stroke("M 18.3,21 L 20.2,20.6 M 18.7,22.6 L 20.6,22.2", white, 0.5)).Opacity = 0.7;
                    break;
                case "bow":
                    Put(Tri(cb, edge, 0.5, 5.6, 2.6, 5.8, 7, 8.6, 4.8));
                    Put(Tri(cb, edge, 0.5, 8.6, 4.8, 11.4, 2.4, 11.4, 6.8));
                    Put(new Ellipse { Width = 2.2, Height = 2.2, Fill = cb, Stroke = edge, StrokeThickness = 0.5 }, 7.5, 3.7);
                    break;
                case "hat":
                    Put(Tri(cb, edge, 0.6, 11.6, 5.6, 15.2, 0.4, 18.6, 5.6));
                    Put(Stroke("M 12.9,3.8 L 16.9,4.6 M 14,2.1 L 16.1,2.6", white, 0.6)).Opacity = 0.85;
                    Put(new Ellipse { Width = 2.6, Height = 2.6, Fill = white }, 13.9, -0.9);
                    break;
                case "crown":
                    Put(Tri(cb, edge, 0.5, 10.4, 5.6, 10.4, 1.8, 12.6, 3.6, 15, 0.6, 17.4, 3.6, 19.6, 1.8, 19.6, 5.6));
                    Put(new Ellipse { Width = 1.4, Height = 1.4, Fill = white }, 14.3, 3.4);
                    break;
                case "glasses":
                    var frame = Ui.Br(c);
                    Put(new Ellipse { Width = 5, Height = 5, Stroke = frame, StrokeThickness = 0.75, Fill = Ui.Br(Colors.White, 25) }, 8.8, 8.8);
                    Put(new Ellipse { Width = 5, Height = 5, Stroke = frame, StrokeThickness = 0.75, Fill = Ui.Br(Colors.White, 25) }, 16.2, 8.8);
                    Put(Stroke("M 13.8,11 Q 15,10.2 16.2,11 M 8.8,10.6 L 6.6,9.8 M 21.2,10.6 L 23.4,9.8", frame, 0.7));
                    break;
                case "flower":
                    for (int i = 0; i < 5; i++)
                    {
                        double a = i * Math.PI * 2 / 5 - Math.PI / 2;
                        Put(new Ellipse { Width = 2.6, Height = 2.6, Fill = cb }, 20.5 + Math.Cos(a) * 1.7 - 1.3, 4.4 + Math.Sin(a) * 1.7 - 1.3);
                    }
                    Put(new Ellipse { Width = 1.8, Height = 1.8, Fill = gold }, 19.6, 3.5);
                    break;
                case "headphones":
                    Put(Stroke("M 6.4,11 Q 6,1.6 15,1.8 Q 24,1.6 23.6,11", edge, 1.8));
                    Put(Stroke("M 6.4,11 Q 6,1.6 15,1.8 Q 24,1.6 23.6,11", cb, 1.1));
                    Put(new Border { Width = 3.2, Height = 5, CornerRadius = new CornerRadius(1.4), Background = cb, BorderBrush = edge, BorderThickness = new Thickness(0.5) }, 4.4, 9.2);
                    Put(new Border { Width = 3.2, Height = 5, CornerRadius = new CornerRadius(1.4), Background = cb, BorderBrush = edge, BorderThickness = new Thickness(0.5) }, 22.4, 9.2);
                    break;
            }
        }

        /// <summary>Nach so vielen ruhigen Sekunden schläft die Katze ein (je nach Charakter).</summary>
        public double SleepAfterSeconds => Config.Mood == "sleepy" ? 25 : Config.Mood == "playful" ? 150 : 60;

        void BuildPhoto(CatBreed b, ImageSource photo)
        {
            var furB = Ui.Br(b.Fur);
            var lineB = Ui.Br(CatStyle.Lum(b.Fur) < 0.3 ? CatStyle.Mix(b.Fur, Colors.White, 0.38) : CatStyle.Mix(b.Fur, Colors.Black, 0.45));
            Put(Tri(furB, lineB, 0.8, 4.5, 12, 6.5, 1.5, 13, 6));
            Put(Tri(furB, lineB, 0.8, 17, 6, 23.5, 1.5, 25.5, 12));
            Put(new Ellipse
            {
                Width = 24,
                Height = 24,
                Fill = new ImageBrush(photo) { Stretch = Stretch.UniformToFill },
                Stroke = furB,
                StrokeThickness = 1.6
            }, 3, 5);
        }

        // ---------------------------------------------------------------
        //  Verhalten
        // ---------------------------------------------------------------
        public bool Sleeping
        {
            get => sleeping;
            set
            {
                if (sleeping == value) return;
                sleeping = value;
                ApplySleep();
            }
        }

        void ApplySleep()
        {
            if (sleeping) StopWalk();
            ApplyCurl(sleeping && CurlUp && curled != null);
            ShowEyes(!sleeping);
            // im Schlaf liegt der Schwanz eingerollt am Körper
            tailRot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(sleeping ? 38 : 0,
                TimeSpan.FromSeconds(Ui.ReduceMotion ? 0.01 : 0.7)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
            if (sleeping && !Ui.ReduceMotion)
            {
                breathe.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.05, TimeSpan.FromSeconds(1.7))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
                var z = new DoubleAnimationUsingKeyFrames { Duration = new Duration(TimeSpan.FromSeconds(3.2)), RepeatBehavior = RepeatBehavior.Forever };
                z.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                z.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.9))));
                z.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.4))));
                zzz.BeginAnimation(OpacityProperty, z);
            }
            else
            {
                breathe.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                breathe.ScaleY = 1;
                zzz.BeginAnimation(OpacityProperty, null);
                zzz.Opacity = sleeping ? 0.8 : 0;
            }
        }

        bool isCurled;

        /// <summary>Einrollen: sitzende Katze sinkt zusammen, die eingerollte erscheint. Aufwachen: streckt sich wieder hoch.</summary>
        void ApplyCurl(bool curl)
        {
            if (curled == null || curl == isCurled) return;
            isCurled = curl;
            if (Ui.ReduceMotion || instantCurl)
            {
                poseScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                poseScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                pose.BeginAnimation(OpacityProperty, null);
                curled.BeginAnimation(OpacityProperty, null);
                pose.Opacity = curl ? 0 : 1;
                curled.Opacity = curl ? 1 : 0;
                return;
            }
            TimeSpan T(double s) => TimeSpan.FromSeconds(s);
            var inOut = new SineEase { EasingMode = EasingMode.EaseInOut };
            if (curl)
            {
                // langsam hinlegen: zusammensinken, dabei wird sie breiter und rund
                poseScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.5, T(0.55)) { EasingFunction = inOut });
                poseScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1.12, T(0.55)) { EasingFunction = inOut });
                pose.BeginAnimation(OpacityProperty, new DoubleAnimation(0, T(0.4)) { BeginTime = T(0.2) });
                curled.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, T(0.4)) { BeginTime = T(0.25) });
                curlScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.35, 1, T(0.6)) { BeginTime = T(0.25), EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut } });
                curlScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.85, 1, T(0.6)) { BeginTime = T(0.25), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
            else
            {
                // aufwachen und strecken: kurz höher als normal, dann federt sie zurück
                curled.BeginAnimation(OpacityProperty, new DoubleAnimation(0, T(0.25)));
                curlScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.3, T(0.3)));
                pose.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, T(0.25)) { BeginTime = T(0.05) });
                var up = new DoubleAnimationUsingKeyFrames();
                up.KeyFrames.Add(new EasingDoubleKeyFrame(0.55, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                up.KeyFrames.Add(new EasingDoubleKeyFrame(1.1, KeyTime.FromTimeSpan(T(0.35)), new CubicEase { EasingMode = EasingMode.EaseOut }));
                up.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(T(0.65)), inOut));
                poseScale.BeginAnimation(ScaleTransform.ScaleYProperty, up);
                poseScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.1, 1, T(0.5)) { EasingFunction = inOut });
            }
        }

        // ---------------------------------------------------------------
        //  Herumlaufen (nur neben der Island): trippelt ein Stück zur Seite, schaut in Laufrichtung
        // ---------------------------------------------------------------
        void StartWalk(double to)
        {
            if (walking || sleeping || Ui.ReduceMotion) return;
            double from = walk.X;
            double dist = Math.Abs(to - from);
            if (dist < 6) return;
            walking = true;
            double secs = Math.Max(0.7, dist / (Config.Mood == "playful" ? 34 : Config.Mood == "sleepy" ? 18 : 25));
            face.ScaleX = to > from ? -1 : 1;   // Schwanz zeigt nach hinten

            // Schritte: leicht auf und ab, Pfoten abwechselnd, kleines Watscheln
            const double step = 0.26;
            var bob = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(step), RepeatBehavior = RepeatBehavior.Forever };
            bob.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            bob.KeyFrames.Add(new EasingDoubleKeyFrame(-1.3, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(step / 2)), new SineEase()));
            bob.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(step)), new SineEase()));
            hop.BeginAnimation(TranslateTransform.YProperty, bob);
            var paw = new DoubleAnimation(0, -1.4, TimeSpan.FromSeconds(step / 2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            pawL.BeginAnimation(TranslateTransform.YProperty, paw);
            pawR.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -1.4, TimeSpan.FromSeconds(step / 2))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(step / 2) });
            wiggle.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-3, 3, TimeSpan.FromSeconds(step)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            tailRot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-8, 6, TimeSpan.FromSeconds(step * 2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });

            var move = new DoubleAnimation(from, to, TimeSpan.FromSeconds(secs)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            move.Completed += (s, e) => { if (walking) StopWalk(); };
            walk.BeginAnimation(TranslateTransform.XProperty, move);
        }

        void StopWalk()
        {
            if (!walking) return;
            walking = false;
            double x = walk.X;
            walk.BeginAnimation(TranslateTransform.XProperty, null);
            walk.X = x;                                  // bleibt dort stehen
            hop.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(0.12)));
            pawL.BeginAnimation(TranslateTransform.YProperty, null);
            pawR.BeginAnimation(TranslateTransform.YProperty, null);
            wiggle.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(0.15)));
            tailRot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(sleeping ? 38 : 0, TimeSpan.FromSeconds(0.3)));
            nextIdle = DateTime.Now.AddSeconds(1 + Rnd.NextDouble() * 2);
        }

        /// <summary>Blickrichtung: gespiegelt = schaut nach rechts (Schwanz links), eingerollt liegt der Kopf dann rechts.</summary>
        public void Face(bool flipped) => face.ScaleX = flipped ? -1 : 1;

        /// <summary>Hört auf zu laufen und gibt zurück, wie weit sie vom Platz weg ist (für einen Platzwechsel).</summary>
        public double TakeWalkOffset()
        {
            StopWalk();
            double x = walk.X;
            walk.BeginAnimation(TranslateTransform.XProperty, null);
            walk.X = 0;
            return x;
        }

        bool instantCurl;
        /// <summary>Schlafend/wach ohne Übergang (z. B. schon eingerollt, wenn die Island aufgeht).</summary>
        public void SetSleepingInstant(bool s)
        {
            instantCurl = true;
            Sleeping = s;
            instantCurl = false;
        }

        /// <summary>Laufen wurde ausgeschaltet: zurück an den Platz.</summary>
        public void GoHome()
        {
            StopWalk();
            if (Math.Abs(walk.X) < 0.5) { face.ScaleX = 1; return; }
            walk.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(0.4)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
            face.ScaleX = 1;
        }

        void ShowEyes(bool open)
        {
            if (eyesOpen == null) return;
            eyesOpen.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            eyesClosed.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        }

        public void Wake()
        {
            if (!sleeping) return;
            Sleeping = false;
            nextIdle = DateTime.Now.AddSeconds(0.6);
        }

        /// <summary>Wird ca. 20× pro Sekunde aufgerufen und löst ab und zu eine kleine Bewegung aus.</summary>
        public void Tick(DateTime now)
        {
            if (walking && (!Walk || !CanWalk)) GoHome();
            if (sleeping || walking || Ui.ReduceMotion || now < nextIdle) return;
            bool playful = Config.Mood == "playful", sleepy = Config.Mood == "sleepy";
            nextIdle = now.AddSeconds(playful ? 1.4 + Rnd.NextDouble() * 2.6 : sleepy ? 4 + Rnd.NextDouble() * 6 : 2.5 + Rnd.NextDouble() * 5);
            if (!(Walk && CanWalk) && Math.Abs(walk.X) > 0.5) { GoHome(); return; }
            // ab und zu ein Stück laufen – verspielte öfter, verschlafene selten; meist wieder zurück zum Platz
            if (Walk && CanWalk && Rnd.NextDouble() < (playful ? 0.3 : sleepy ? 0.08 : 0.18))
            {
                double to = Math.Abs(walk.X) > 8 && Rnd.NextDouble() < 0.55 ? 0 : WalkMin + Rnd.NextDouble() * (WalkMax - WalkMin);
                StartWalk(to);
                if (walking) return;
            }
            double r = Rnd.NextDouble();
            if (playful && r > 0.8) { if (r > 0.9) Hop(); else Swish(); return; }
            if (sleepy && r > 0.75) { Blink(true); return; }
            if (r < 0.40) Blink();
            else if (r < 0.65) Swish();
            else if (r < 0.85) Look();
            else if (r < 0.95) Blink(true);
            else Hop();
        }

        static DoubleAnimationUsingKeyFrames Keys(params (double t, double v)[] k)
        {
            var a = new DoubleAnimationUsingKeyFrames();
            foreach (var (t, v) in k)
                a.KeyFrames.Add(new EasingDoubleKeyFrame(v, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t)),
                    new SineEase { EasingMode = EasingMode.EaseInOut }));
            return a;
        }

        public void Blink(bool twice = false)
        {
            if (eyesOpen == null || sleeping) return;
            var a = twice
                ? Keys((0, 1), (0.07, 0.1), (0.16, 1), (0.3, 1), (0.37, 0.1), (0.46, 1))
                : Keys((0, 1), (0.07, 0.1), (0.16, 1));
            eyeScale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }

        public void Swish() =>
            tailRot.BeginAnimation(RotateTransform.AngleProperty, Keys((0, 0), (0.3, -20), (0.65, 12), (0.95, -6), (1.2, 0)));

        public void Look()
        {
            if (eyesOpen == null) return;
            double d = Rnd.Next(2) == 0 ? -1.2 : 1.2;
            eyeShift.BeginAnimation(TranslateTransform.XProperty, Keys((0, 0), (0.18, d), (1.4, d), (1.6, 0)));
        }

        public void Hop()
        {
            StopWalk();
            Wake();
            if (Ui.ReduceMotion) return;
            hop.BeginAnimation(TranslateTransform.YProperty, Keys((0, 0), (0.14, -4), (0.32, 0), (0.42, -1), (0.5, 0)));
        }

        /// <summary>Streicheln: glückliche Augen, kleines Wackeln, Herz.</summary>
        public void Purr()
        {
            StopWalk();
            Wake();
            ShowEyes(false);
            happyTimer?.Stop();
            happyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
            happyTimer.Tick += (s, e) =>
            {
                happyTimer.Stop();
                if (!sleeping) ShowEyes(true);
            };
            happyTimer.Start();

            if (!Ui.ReduceMotion)
            {
                wiggle.BeginAnimation(RotateTransform.AngleProperty, Keys((0, 0), (0.08, -7), (0.18, 6), (0.28, -4), (0.38, 3), (0.5, 0)));
                Swish();
            }

            var heart = new TextBlock
            {
                Text = "♥",
                FontFamily = Ui.SymbolFont,
                FontSize = Math.Max(9, Width * 0.38),
                Foreground = Ui.Br(Ui.Pink),
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(-4, -2, 0, 0),
                Opacity = 0
            };
            var ht = new TranslateTransform();
            heart.RenderTransform = ht;
            Children.Add(heart);
            var fade = Keys((0, 0), (0.15, 1), (0.9, 1), (1.3, 0));
            fade.Completed += (s, e) => Children.Remove(heart);
            heart.BeginAnimation(OpacityProperty, fade);
            if (!Ui.ReduceMotion) ht.BeginAnimation(TranslateTransform.YProperty, Keys((0, 2), (1.3, -3)));
        }
    }

    /// <summary>
    /// Kleiner Kratzbaum (52 × 52): Holzplatte, Sisal-Stamm, Liegefläche und ein Spielball,
    /// der sanft hin und her schwingt. Steht unten in der großen Island.
    /// </summary>
    public sealed class CatTree : Canvas
    {
        public CatTree()
        {
            Width = 52;
            Height = 52;
            IsHitTestVisible = false;
            var wood = Ui.Br(Color.FromRgb(0xB8, 0x8B, 0x5E));
            var woodDark = Ui.Br(Color.FromRgb(0x8A, 0x62, 0x3E));
            var sisal = Ui.Br(Color.FromRgb(0xD9, 0xC2, 0x9A));
            var sisalLine = Ui.Br(Color.FromRgb(0xB0, 0x95, 0x6A));
            var plush = Ui.Br(Color.FromRgb(0xE9, 0xDC, 0xC6));
            var plushDark = Ui.Br(Color.FromRgb(0xC4, 0xB4, 0x9A));

            // Bodenplatte
            Add(new Rectangle { Width = 50, Height = 5, RadiusX = 2.5, RadiusY = 2.5, Fill = wood, Stroke = woodDark, StrokeThickness = 0.8 }, 1, 47);
            // Sisal-Stamm mit Wicklung
            Add(new Rectangle { Width = 9, Height = 21, Fill = sisal, Stroke = sisalLine, StrokeThickness = 0.6 }, 20.5, 26.5);
            for (double y = 28.5; y < 47; y += 2.6)
                Add(new Line { X1 = 20.8, Y1 = y, X2 = 29.2, Y2 = y + 0.8, Stroke = sisalLine, StrokeThickness = 0.6 }, 0, 0);
            // Liegefläche
            Add(new Rectangle { Width = 40, Height = 6, RadiusX = 3, RadiusY = 3, Fill = plush, Stroke = plushDark, StrokeThickness = 0.8 }, 5, 21);

            // Spielball an einer Schnur, schwingt
            var toy = new Canvas();
            var swing = new RotateTransform(0, 41, 27);
            toy.RenderTransform = swing;
            toy.Children.Add(new Line { X1 = 41, Y1 = 27, X2 = 41, Y2 = 36, Stroke = Ui.Br(Colors.White, 140), StrokeThickness = 0.7 });
            var ball = new Ellipse { Width = 6.5, Height = 6.5, Fill = Ui.Br(Ui.Pink) };
            SetLeft(ball, 37.75);
            SetTop(ball, 35.5);
            toy.Children.Add(ball);
            Children.Add(toy);
            if (!Ui.ReduceMotion)
                swing.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-14, 14, TimeSpan.FromSeconds(1.6))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
        }

        void Add(UIElement e, double x, double y)
        {
            SetLeft(e, x);
            SetTop(e, y);
            Children.Add(e);
        }
    }

    /// <summary>
    /// Hoher Kratzbaum (64 × 112) für die Seite der großen Island: Bodenplatte, Sisal-Stamm,
    /// zwei Liegeflächen und ein schwingender Spielball.
    /// </summary>
    public sealed class CatTower : Canvas
    {
        public CatTower()
        {
            Width = 64;
            Height = 112;
            IsHitTestVisible = false;
            var wood = Ui.Br(Color.FromRgb(0xB8, 0x8B, 0x5E));
            var woodDark = Ui.Br(Color.FromRgb(0x8A, 0x62, 0x3E));
            var sisal = Ui.Br(Color.FromRgb(0xD9, 0xC2, 0x9A));
            var sisalLine = Ui.Br(Color.FromRgb(0xB0, 0x95, 0x6A));
            var plush = Ui.Br(Color.FromRgb(0xE9, 0xDC, 0xC6));
            var plushDark = Ui.Br(Color.FromRgb(0xC4, 0xB4, 0x9A));

            Add(new Rectangle { Width = 60, Height = 6, RadiusX = 3, RadiusY = 3, Fill = wood, Stroke = woodDark, StrokeThickness = 0.8 }, 2, 106);
            Add(new Rectangle { Width = 9, Height = 84, Fill = sisal, Stroke = sisalLine, StrokeThickness = 0.6 }, 27.5, 22);
            for (double y = 30; y < 106; y += 2.8)
                Add(new Line { X1 = 27.8, Y1 = y, X2 = 36.2, Y2 = y + 0.9, Stroke = sisalLine, StrokeThickness = 0.6 }, 0, 0);
            Add(new Rectangle { Width = 30, Height = 6, RadiusX = 3, RadiusY = 3, Fill = plush, Stroke = plushDark, StrokeThickness = 0.8 }, 0, 64);
            Add(new Rectangle { Width = 52, Height = 6, RadiusX = 3, RadiusY = 3, Fill = plush, Stroke = plushDark, StrokeThickness = 0.8 }, 6, 22);

            var toy = new Canvas();
            var swing = new RotateTransform(0, 54, 28);
            toy.RenderTransform = swing;
            toy.Children.Add(new Line { X1 = 54, Y1 = 28, X2 = 54, Y2 = 40, Stroke = Ui.Br(Colors.White, 140), StrokeThickness = 0.7 });
            var ball = new Ellipse { Width = 7, Height = 7, Fill = Ui.Br(Ui.Pink) };
            SetLeft(ball, 50.5);
            SetTop(ball, 39.5);
            toy.Children.Add(ball);
            Children.Add(toy);
            if (!Ui.ReduceMotion)
                swing.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-14, 14, TimeSpan.FromSeconds(1.7))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
        }

        void Add(UIElement e, double x, double y)
        {
            SetLeft(e, x);
            SetTop(e, y);
            Children.Add(e);
        }

        /// <summary>Plätze für bis zu 4 Katzen (Größe 26): oben, Boden rechts, Mitte links, Boden links.</summary>
        public static readonly Point[] Spots = { new Point(19, -4), new Point(37, 80), new Point(2, 38), new Point(1, 80) };
    }
}

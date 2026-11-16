using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Windows.Media.Ocr;

namespace DynamicIsland
{
    /// <summary>
    /// Text vom Bildschirm lesen: Bildschirm einfrieren, Bereich aufziehen, Windows-Texterkennung (offline).
    /// </summary>
    public static class ScreenOcr
    {
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc, int index);

        /// <summary>Hauptbildschirm in echten Pixeln aufnehmen.</summary>
        public static BitmapSource CapturePrimary()
        {
            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr mem = IntPtr.Zero, bmp = IntPtr.Zero;
            try
            {
                int w = GetDeviceCaps(screen, 118), h = GetDeviceCaps(screen, 117); // DESKTOPHORZRES / DESKTOPVERTRES
                if (w <= 0 || h <= 0) { w = (int)SystemParameters.PrimaryScreenWidth; h = (int)SystemParameters.PrimaryScreenHeight; }
                mem = CreateCompatibleDC(screen);
                bmp = CreateCompatibleBitmap(screen, w, h);
                var old = SelectObject(mem, bmp);
                BitBlt(mem, 0, 0, w, h, screen, 0, 0, 0x00CC0020 | 0x40000000); // SRCCOPY | CAPTUREBLT
                SelectObject(mem, old);
                var src = Imaging.CreateBitmapSourceFromHBitmap(bmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally
            {
                if (bmp != IntPtr.Zero) DeleteObject(bmp);
                if (mem != IntPtr.Zero) DeleteDC(mem);
                ReleaseDC(IntPtr.Zero, screen);
            }
        }

        /// <summary>Bereich auf dem eingefrorenen Bild aufziehen lassen. null = abgebrochen.</summary>
        public static Task<BitmapSource> PickRegionAsync(BitmapSource shot)
        {
            var tcs = new TaskCompletionSource<BitmapSource>();
            var w = new RegionPicker(shot, tcs);
            w.Show();
            w.Activate();
            return tcs.Task;
        }

        /// <summary>Text im Bild erkennen (Sprachen deines Windows-Profils).</summary>
        public static async Task<string> RecognizeAsync(BitmapSource bmp)
        {
            var engine = OcrEngine.TryCreateFromUserProfileLanguages()
                         ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
            if (engine == null) throw new Exception("Keine Texterkennungs-Sprache installiert (Windows-Einstellungen → Sprache).");

            // Kleine Ausschnitte vergrößern (Texterkennung mag Buchstaben ab ~20 px), zu große verkleinern
            double max = OcrEngine.MaxImageDimension;
            double scale = 1;
            int minSide = Math.Min(bmp.PixelWidth, bmp.PixelHeight);
            if (minSide < 160) scale = Math.Min(3, 160.0 / Math.Max(1, minSide));
            if (Math.Max(bmp.PixelWidth, bmp.PixelHeight) * scale > max) scale = max / Math.Max(bmp.PixelWidth, bmp.PixelHeight);
            BitmapSource src = bmp;
            if (Math.Abs(scale - 1) > 0.01) src = new TransformedBitmap(bmp, new ScaleTransform(scale, scale));

            var conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            conv.CopyPixels(px, stride, 0);
            using var sb = Windows.Graphics.Imaging.SoftwareBitmap.CreateCopyFromBuffer(
                px.AsBuffer(), Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, w, h, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
            var result = await engine.RecognizeAsync(sb);
            var text = new StringBuilder();
            foreach (var line in result.Lines) text.AppendLine(line.Text);
            return text.ToString().Trim();
        }

        /// <summary>Vollbild über dem eingefrorenen Bildschirm: Rechteck aufziehen.</summary>
        sealed class RegionPicker : Window
        {
            readonly BitmapSource shot;
            readonly TaskCompletionSource<BitmapSource> tcs;
            readonly Canvas canvas = new Canvas();
            readonly Path dim;
            readonly Rectangle frame;
            readonly RectangleGeometry hole = new RectangleGeometry(new Rect(0, 0, 0, 0), 6, 6);
            Point start;
            bool down, done;

            public RegionPicker(BitmapSource shot, TaskCompletionSource<BitmapSource> tcs)
            {
                this.shot = shot;
                this.tcs = tcs;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                Topmost = true;
                Left = 0;
                Top = 0;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
                Cursor = Cursors.Cross;
                Background = new ImageBrush(shot) { Stretch = Stretch.Fill };
                UseLayoutRounding = true;

                var full = new RectangleGeometry(new Rect(0, 0, Width, Height));
                dim = new Path
                {
                    Fill = new SolidColorBrush(Color.FromArgb(0x8C, 0, 0, 0)),
                    Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, hole)
                };
                frame = new Rectangle
                {
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5,
                    RadiusX = 6,
                    RadiusY = 6,
                    Visibility = Visibility.Collapsed
                };
                canvas.Children.Add(dim);
                canvas.Children.Add(frame);

                var hint = new Border
                {
                    CornerRadius = new CornerRadius(18),
                    Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x1C, 0x1C, 0x1E)),
                    Padding = new Thickness(18, 9, 18, 9),
                    Child = Ui.Text("Bereich mit Text aufziehen  ·  Doppelklick = ganzer Bildschirm  ·  Esc = abbrechen", 13, Ui.White, false),
                    IsHitTestVisible = false
                };
                hint.Loaded += (s, e) => { Canvas.SetLeft(hint, (Width - hint.ActualWidth) / 2); Canvas.SetTop(hint, 28); };
                canvas.Children.Add(hint);
                Content = canvas;

                MouseLeftButtonDown += OnDown;
                MouseMove += OnMove;
                MouseLeftButtonUp += OnUp;
                KeyDown += (s, e) => { if (e.Key == Key.Escape) Finish(null); };
                Deactivated += (s, e) => { if (!down) Finish(null); };
            }

            void OnDown(object s, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2) { Finish(shot); return; }
                down = true;
                start = e.GetPosition(canvas);
                CaptureMouse();
            }

            void OnMove(object s, MouseEventArgs e)
            {
                if (!down) return;
                var r = new Rect(start, e.GetPosition(canvas));
                hole.Rect = r;
                frame.Visibility = Visibility.Visible;
                frame.Width = r.Width;
                frame.Height = r.Height;
                Canvas.SetLeft(frame, r.X);
                Canvas.SetTop(frame, r.Y);
            }

            void OnUp(object s, MouseButtonEventArgs e)
            {
                if (!down) return;
                down = false;
                ReleaseMouseCapture();
                var r = new Rect(start, e.GetPosition(canvas));
                if (r.Width < 8 || r.Height < 8) { hole.Rect = new Rect(); frame.Visibility = Visibility.Collapsed; return; }
                double sx = shot.PixelWidth / ActualWidth, sy = shot.PixelHeight / ActualHeight;
                var px = new Int32Rect(
                    (int)Math.Max(0, r.X * sx), (int)Math.Max(0, r.Y * sy),
                    (int)Math.Min(shot.PixelWidth - r.X * sx, r.Width * sx), (int)Math.Min(shot.PixelHeight - r.Y * sy, r.Height * sy));
                if (px.Width < 4 || px.Height < 4) { Finish(null); return; }
                var crop = new CroppedBitmap(shot, px);
                crop.Freeze();
                Finish(crop);
            }

            void Finish(BitmapSource result)
            {
                if (done) return;
                done = true;
                tcs.TrySetResult(result);
                Close();
            }
        }
    }
}

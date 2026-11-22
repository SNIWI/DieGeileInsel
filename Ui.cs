using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DynamicIsland
{
    /// <summary>Kleine Bausteine für die Oberfläche (Farben, Texte, Buttons, Balken …).</summary>
    public static class Ui
    {
        public static readonly CultureInfo De = new CultureInfo("de-DE");

        public static readonly FontFamily IconFont = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        public static readonly FontFamily TextFont = new FontFamily("Segoe UI Variable Display, Segoe UI");
        // Optische Größen wie bei SF Pro: kleine Schrift bekommt offenere Formen und mehr Laufweite
        static readonly FontFamily SmallFont = new FontFamily("Segoe UI Variable Small, Segoe UI");
        static readonly FontFamily BodyFont = new FontFamily("Segoe UI Variable Text, Segoe UI");

        /// <summary>Passende optische Größe der Systemschrift für eine Schriftgröße.</summary>
        public static FontFamily FontFor(double size) => size < 12 ? SmallFont : size < 19 ? BodyFont : TextFont;
        public static readonly FontFamily EmojiFont = new FontFamily("Segoe UI Emoji, Segoe UI Symbol");
        public static readonly FontFamily SymbolFont = new FontFamily("Segoe UI Symbol, Segoe UI");

        // Apple-Systemfarben (Dark Mode)
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9F, 0x0A);
        public static readonly Color Green = Color.FromRgb(0x30, 0xD1, 0x58);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x45, 0x3A);
        public static readonly Color Blue = Color.FromRgb(0x0A, 0x84, 0xFF);
        public static readonly Color Yellow = Color.FromRgb(0xFF, 0xD6, 0x0A);
        public static readonly Color Purple = Color.FromRgb(0xBF, 0x5A, 0xF2);
        public static readonly Color Teal = Color.FromRgb(0x64, 0xD2, 0xFF);
        public static readonly Color Gray = Color.FromRgb(0x8E, 0x8E, 0x93);
        public static readonly Color Pink = Color.FromRgb(0xFF, 0x37, 0x5F);
        public static readonly Color WhiteC = Colors.White;

        /// <summary>Akzentfarbe der ganzen Island (einstellbar) – Regler, Häkchen, Symbole, Menü-Auswahl.</summary>
        public static Color Accent = Blue;
        /// <summary>Eigene Farbe für den Spotify-Player (einstellbar).</summary>
        public static Color SpotifyAccent = Color.FromRgb(0x1E, 0xD7, 0x60);
        public static SolidColorBrush AccentBrush => Br(Accent);
        public static SolidColorBrush SpotifyBrush => Br(SpotifyAccent);

        public static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try { return (Color)ColorConverter.ConvertFromString(hex.Trim()); } catch { return fallback; }
        }

        public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        /// <summary>Windows-Einstellung „Animationen anzeigen“ aus = Bewegung reduzieren (Apple: Reduce Motion).</summary>
        public static bool ReduceMotion => !SystemParameters.ClientAreaAnimation;
        /// <summary>Hinweise einzeilig (Design „Klein“ oder „Minimal“, Einstellungen → Hinweise → Aussehen).</summary>
        public static bool CompactAlerts = true;

        /// <summary>Zeilenumbrüche/Tabs zu einem Leerzeichen – für einzeilige Hinweise (Mitteilungen bringen oft mehrere Zeilen mit).</summary>
        public static string OneLine(string s) =>
            string.IsNullOrEmpty(s) ? s : System.Text.RegularExpressions.Regex.Replace(s, @"\s*[\r\n\t\u2028\u2029]+\s*", " ").Trim();

        /// <summary>Textfarbe nur setzen, wenn sie sich wirklich ändert (Refresh läuft ~20× pro Sekunde).</summary>
        public static void SetFg(TextBlock t, Color c)
        {
            if (t == null) return;
            if (!(t.Foreground is SolidColorBrush b && b.Color == c)) t.Foreground = Br(c);
        }
        /// <summary>Design der Hinweise: small, normal, ios, minimal, glow.</summary>
        public static string AlertDesign = "small";
        /// <summary>Erscheinen von Hinweisen: slide · morph · none.</summary>
        public static string AlertMotion = "slide";

        public static SolidColorBrush Br(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static SolidColorBrush Br(Color c, byte alpha) => Br(Color.FromArgb(alpha, c.R, c.G, c.B));

        public static readonly SolidColorBrush White = Br(Colors.White);
        // iOS-Töne (Dark Mode): sekundärer Text, Füllflächen, Karten
        public static readonly SolidColorBrush Dim = Br(Color.FromArgb(0x99, 0xEB, 0xEB, 0xF5));
        public static readonly SolidColorBrush Faint = Br(Colors.White, 38);
        public static readonly SolidColorBrush Card = Br(Color.FromRgb(0x1C, 0x1C, 0x1E));
        public static readonly SolidColorBrush CardHover = Br(Color.FromRgb(0x2C, 0x2C, 0x2E));

        /// <summary>Kante von Knöpfen/Chips – für den cleanen Look unsichtbar (flach wie iOS-Kontrollzentrum).</summary>
        public static readonly Brush GlassRim = Frozen(new SolidColorBrush(Colors.Transparent));

        /// <summary>Füllung runder Knöpfe: ruhige, flache Fläche ohne Glanz.</summary>
        public static readonly Brush GlassFill = Frozen(new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)));

        static Brush Frozen(Brush b) { b.Freeze(); return b; }

        /// <summary>Lesbare Schrift auf der Akzentfarbe (dunkel bei hellen Farben wie Gelb oder Weiß).</summary>
        public static SolidColorBrush OnAccent
        {
            get
            {
                double l = 0.2126 * Accent.R / 255 + 0.7152 * Accent.G / 255 + 0.0722 * Accent.B / 255;
                return l > 0.62 ? Br(Colors.Black) : White;
            }
        }

        /// <summary>Schrift-Faktor beim Aufbau einer Seite der großen Island (sonst 1). Symbole wachsen etwas weniger mit.</summary>
        public static double TextBoost = 1;
        public static double GlyphBoost => 1 + (TextBoost - 1) * 0.6;

        public static TextBlock Text(string s, double size, Brush fg = null, bool bold = true, bool mono = false)
        {
            size *= TextBoost;
            var t = new TextBlock
            {
                Text = s ?? "",
                FontSize = size,
                Foreground = fg ?? White,
                FontFamily = FontFor(size),
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            if (mono) Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular);
            return t;
        }

        public static TextBlock Glyph(string g, double size, Brush fg = null, FontFamily font = null) => new TextBlock
        {
            Text = g ?? "",
            FontFamily = font ?? IconFont,
            FontSize = size * GlyphBoost,
            Foreground = fg ?? White,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        /// <summary>Drück-Animation (Skalierung) – ohne Bewegung, wenn Animationen in Windows aus sind.</summary>
        public static void Press(ScaleTransform s, double to, int ms, IEasingFunction ease)
        {
            var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ReduceMotion ? 1 : ms)) { EasingFunction = ease };
            s.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            s.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }

        static void Fade(UIElement el, double to, int ms) =>
            el.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)));

        // ---------- Menüs im macOS-Stil (dunkles Glas, runde Ecken, blaue Auswahl) ----------
        const string MenuXaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style x:Key='DiMenu' TargetType='ContextMenu'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='HasDropShadow' Value='False'/>
    <Setter Property='Foreground' Value='White'/>
    <Setter Property='FontFamily' Value='Segoe UI Variable Text, Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ContextMenu'>
          <Border Margin='12' Background='#F2232325' BorderBrush='#30FFFFFF' BorderThickness='0.5' CornerRadius='12' Padding='5'>
            <Border.Effect><DropShadowEffect BlurRadius='26' ShadowDepth='8' Direction='270' Opacity='0.5' Color='Black'/></Border.Effect>
            <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Cycle'/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style x:Key='{x:Static MenuItem.SeparatorStyleKey}' TargetType='Separator'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Separator'>
          <Border Height='1' Margin='10,4,10,4' Background='#24FFFFFF'/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='MenuItem'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Foreground' Value='White'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='MenuItem'>
          <Grid>
            <Border x:Name='Bd' Background='Transparent' CornerRadius='6' Padding='8,5,10,5' MinWidth='200'>
              <Grid>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='20'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name='Check' Text='✓' FontWeight='SemiBold' Visibility='Hidden' VerticalAlignment='Center'/>
                <ContentPresenter Grid.Column='1' ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center'/>
                <TextBlock x:Name='Arrow' Grid.Column='2' Text='›' FontSize='16' Opacity='0.55' Margin='14,-3,0,0'
                           Visibility='Collapsed' VerticalAlignment='Center'/>
              </Grid>
            </Border>
            <Popup x:Name='PART_Popup' Placement='Right' HorizontalOffset='-6' VerticalOffset='-17' AllowsTransparency='True'
                   Focusable='False' PopupAnimation='Fade'
                   IsOpen='{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}'>
              <Border Margin='12' Background='#F2232325' BorderBrush='#30FFFFFF' BorderThickness='0.5' CornerRadius='12' Padding='5'>
                <Border.Effect><DropShadowEffect BlurRadius='26' ShadowDepth='8' Direction='270' Opacity='0.5' Color='Black'/></Border.Effect>
                <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Cycle'/>
              </Border>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'><Setter TargetName='Check' Property='Visibility' Value='Visible'/></Trigger>
            <Trigger Property='Role' Value='SubmenuHeader'><Setter TargetName='Arrow' Property='Visibility' Value='Visible'/></Trigger>
            <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Bd' Property='Background' Value='#0A84FF'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";

        /// <summary>Gibt einem Kontextmenü das Apple-Aussehen. Schlägt es fehl, bleibt das normale Menü.</summary>
        public static void StyleMenu(ContextMenu cm)
        {
            try
            {
                var dict = (ResourceDictionary)XamlReader.Parse(MenuXaml.Replace("#0A84FF", Hex(Accent)));
                cm.Resources.MergedDictionaries.Add(dict);
                cm.Style = (Style)dict["DiMenu"];
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        /// <summary>Runder Button. Klicks werden NICHT an die Island weitergereicht.</summary>
        public static Border RoundButton(string glyph, double iconSize, Action onClick, double box = 36,
                                         Brush bg = null, Brush fg = null, FontFamily font = null)
        {
            var scale = new ScaleTransform(1, 1);
            bool glass = bg == null || ReferenceEquals(bg, Faint);   // Standard-Knöpfe als Glas-Kapsel
            var b = new Border
            {
                Width = box,
                Height = box,
                CornerRadius = new CornerRadius(box / 2),
                Background = glass ? GlassFill : bg,
                BorderBrush = glass ? GlassRim : null,
                BorderThickness = new Thickness(glass ? 0.8 : 0),
                Cursor = Cursors.Hand,
                Child = Glyph(glyph, iconSize, fg, font),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = scale
            };
            b.MouseEnter += (s, e) => Fade(b, 0.8, 120);
            b.MouseLeave += (s, e) => { Fade(b, 1, 160); Press(scale, 1, 300, new SpringEase()); };
            b.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                Press(scale, 0.86, 90, new CubicEase { EasingMode = EasingMode.EaseOut });
            };
            b.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                Press(scale, 1, 340, new SpringEase()); // federt zurück wie bei iOS
                try { onClick?.Invoke(); } catch (Exception ex) { Program.Log(ex); }
            };
            return b;
        }

        public static FrameworkElement LabeledButton(string glyph, string label, Action onClick, Brush bg = null, FontFamily font = null, double width = 70)
        {
            var sp = new StackPanel { Orientation = Orientation.Vertical, Width = width };
            var btn = RoundButton(glyph, 17, onClick, 44, bg, null, font); // 44 pt Mindestgröße (Apple HIG)
            btn.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(btn);
            var t = Text(label, 11, Dim, false);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 5, 0, 0);
            sp.Children.Add(t);
            return sp;
        }

        /// <summary>
        /// Apple-Druckgefühl für Karten, Zeilen und Chips: gibt sofort beim Drücken nach (nicht erst beim Loslassen)
        /// und federt zurück. Zieht man weg (z. B. zum Wischen), springt es ohne Auslösen zurück.
        /// </summary>
        public static T Pressable<T>(T el, double scale = 0.96) where T : FrameworkElement
        {
            if (el.RenderTransform != null && !(el.RenderTransform is MatrixTransform m && m.Matrix.IsIdentity)) return el;
            var sc = new ScaleTransform(1, 1);
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            el.RenderTransform = sc;
            bool down = false;
            Point at = default;
            void Release(int ms) { if (!down) return; down = false; Press(sc, 1, ms, new SpringEase { Damping = 0.9 }); }
            el.PreviewMouseLeftButtonDown += (s, e) =>
            {
                down = true;
                at = e.GetPosition(el);
                Press(sc, scale, 90, new CubicEase { EasingMode = EasingMode.EaseOut });
            };
            el.PreviewMouseMove += (s, e) =>
            {
                if (!down) return;
                var p = e.GetPosition(el);
                if (Math.Abs(p.X - at.X) > 8 || Math.Abs(p.Y - at.Y) > 8 || e.LeftButton != MouseButtonState.Pressed) Release(260);
            };
            el.PreviewMouseLeftButtonUp += (s, e) => Release(340);
            el.MouseLeave += (s, e) => Release(260);
            return el;
        }

        /// <summary>Gruppen senkrecht mit gleichmäßigem Abstand über die ganze Höhe verteilen (mind. 8 px).</summary>
        public static Grid Spread(Thickness margin, params FrameworkElement[] groups)
        {
            var g = new Grid { Margin = margin };
            int row = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                if (i > 0)
                {
                    g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 8 });
                    row++;
                }
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var el = groups[i];
                el.Margin = new Thickness(el.Margin.Left, 0, el.Margin.Right, 0);
                el.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetRow(el, row);
                g.Children.Add(el);
                row++;
            }
            return g;
        }

        /// <summary>Blauer Text-Button wie in iOS.</summary>
        public static TextBlock Link(string text, Action onClick, double size = 13)
        {
            var t = Text(text, size, Br(Accent), false);
            t.Cursor = Cursors.Hand;
            t.MouseEnter += (s, e) => t.Opacity = 0.7;
            t.MouseLeave += (s, e) => t.Opacity = 1;
            t.MouseLeftButtonDown += (s, e) => e.Handled = true;
            t.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                try { onClick?.Invoke(); } catch (Exception ex) { Program.Log(ex); }
            };
            return t;
        }

        public static StackPanel HStack(double gap, params UIElement[] items)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            bool first = true;
            foreach (var it in items)
            {
                if (it == null) continue;
                if (!first && it is FrameworkElement fe)
                    fe.Margin = new Thickness(fe.Margin.Left + gap, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom);
                sp.Children.Add(it);
                first = false;
            }
            return sp;
        }

        public static StackPanel VStack(double gap, params UIElement[] items)
        {
            var sp = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            bool first = true;
            foreach (var it in items)
            {
                if (it == null) continue;
                if (!first && it is FrameworkElement fe)
                    fe.Margin = new Thickness(fe.Margin.Left, fe.Margin.Top + gap, fe.Margin.Right, fe.Margin.Bottom);
                sp.Children.Add(it);
                first = false;
            }
            return sp;
        }

        /// <summary>Zeile mit Inhalt links, (optional) in der Mitte und rechts.</summary>
        public static Grid Row3(UIElement left, UIElement middle, UIElement right, Thickness pad)
        {
            var g = new Grid { Margin = pad };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (left != null) { Grid.SetColumn(left, 0); g.Children.Add(left); }
            if (middle != null) { Grid.SetColumn(middle, 1); g.Children.Add(middle); }
            if (right != null) { Grid.SetColumn(right, 2); g.Children.Add(right); }
            return g;
        }

        public static Ellipse Dot(Color c, double size) => new Ellipse
        {
            Width = size,
            Height = size,
            Fill = Br(c),
            VerticalAlignment = VerticalAlignment.Center
        };

        public static void Pulse(UIElement e, double min = 0.25, double seconds = 0.7)
        {
            var a = new DoubleAnimation(1, min, TimeSpan.FromSeconds(seconds))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            e.BeginAnimation(UIElement.OpacityProperty, a);
        }

        /// <summary>Abgerundetes Albumcover mit Noten-Symbol als Platzhalter.</summary>
        public static Grid ArtBox(double size, double radius, out Image img)
        {
            var g = new Grid { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(radius),
                Background = Faint,
                Child = Glyph("♫", size * 0.5, Dim, SymbolFont)
            });
            img = new Image
            {
                Width = size,
                Height = size,
                Stretch = Stretch.UniformToFill,
                Clip = new RectangleGeometry(new Rect(0, 0, size, size), radius, radius)
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            g.Children.Add(img);
            return g;
        }

        public static string Fmt(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{t.Minutes}:{t.Seconds:00}";
        }
    }

    /// <summary>Dünner Fortschrittsbalken.</summary>
    public sealed class Bar : Grid
    {
        readonly ColumnDefinition c0, c1;
        readonly Border fill;

        public Bar(double height, Brush fillBrush = null)
        {
            Height = height;
            VerticalAlignment = VerticalAlignment.Center;
            Children.Add(new Border { Background = Ui.Faint, CornerRadius = new CornerRadius(height / 2) });
            var inner = new Grid();
            c0 = new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) };
            c1 = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
            inner.ColumnDefinitions.Add(c0);
            inner.ColumnDefinitions.Add(c1);
            fill = new Border { Background = fillBrush ?? Ui.White, CornerRadius = new CornerRadius(height / 2) };
            Grid.SetColumn(fill, 0);
            inner.Children.Add(fill);
            Children.Add(inner);
        }

        double current = -1;
        public event Action<double> ValueChanged;

        public double Value
        {
            get => Math.Max(0, current);
            set
            {
                double v = double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
                if (Math.Abs(v - current) < 0.0005) return;
                current = v;
                c0.Width = new GridLength(v, GridUnitType.Star);
                c1.Width = new GridLength(1 - v, GridUnitType.Star);
                ValueChanged?.Invoke(v);
            }
        }

        public Brush Fill { set => fill.Background = value; }
    }

    /// <summary>
    /// Schieberegler im Apple-Stil: dünner Balken, wird beim Darüberfahren etwas dicker,
    /// lässt sich anklicken, ziehen und mit dem Mausrad bewegen.
    /// </summary>
    public sealed class DragBar : Grid
    {
        public readonly Bar Bar;
        readonly double baseHeight;
        public bool Dragging { get; private set; }
        public Action<double> Changed;    // beim Ziehen (live)
        public Action<double> Committed;  // beim Loslassen
        public Func<double> Current;      // aktueller Wert (fürs Mausrad)
        public double WheelStep;          // 0 = Mausrad aus

        // Regler-Knopf aus Liquid Glass (Kapsel), hebt sich beim Ziehen an
        readonly double KnobW = 24, KnobH = 15;
        readonly GlassBorder knob;
        readonly ScaleTransform knobScale = new ScaleTransform(1, 1);

        public DragBar(Brush fill, double height = 5, bool slim = false)
        {
            baseHeight = height;
            if (slim) { KnobW = 16; KnobH = 10; }   // schmaler Regler (z. B. Mikrofon)
            Bar = new Bar(height, fill);
            Height = slim ? 14 : 22;
            Background = Brushes.Transparent;
            Cursor = Cursors.Hand;
            VerticalAlignment = VerticalAlignment.Center;
            Children.Add(Bar);

            knob = new GlassBorder
            {
                Width = KnobW,
                Height = KnobH,
                CornerRadius = new CornerRadius(KnobH / 2),
                Background = Ui.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = knobScale,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Direction = 270, Opacity = 0.35, Color = Colors.Black }
            };
            Children.Add(knob);
            Bar.ValueChanged += v => PlaceKnob();
            SizeChanged += (s, e) => PlaceKnob();

            MouseEnter += (s, e) => { Bar.Height = baseHeight + 2; if (!Dragging) Ui.Press(knobScale, 1.08, 160, new SpringEase()); };
            MouseLeave += (s, e) => { if (!Dragging) { Bar.Height = baseHeight; Ui.Press(knobScale, 1, 260, new SpringEase()); } };
            MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                Dragging = true;
                CaptureMouse();
                knob.Lift(true);
                Ui.Press(knobScale, 1.32, 220, new SpringEase());
                Apply(e.GetPosition(Bar).X, false);
            };
            MouseMove += (s, e) => { if (Dragging) Apply(e.GetPosition(Bar).X, false); };
            MouseLeftButtonUp += (s, e) =>
            {
                if (!Dragging) return;
                e.Handled = true;
                Apply(e.GetPosition(Bar).X, true);
                Dragging = false;
                ReleaseMouseCapture();
                knob.Lift(false);
                Ui.Press(knobScale, IsMouseOver ? 1.08 : 1, 380, new SpringEase());
                if (!IsMouseOver) Bar.Height = baseHeight;
            };
            LostMouseCapture += (s, e) =>
            {
                Dragging = false;
                knob.Lift(false);
                Ui.Press(knobScale, 1, 300, new SpringEase());
            };
            MouseWheel += (s, e) =>
            {
                if (WheelStep <= 0 || Current == null) return;
                e.Handled = true;
                var v = Clamp(Current() + (e.Delta > 0 ? WheelStep : -WheelStep));
                Bar.Value = v;
                Changed?.Invoke(v);
                Committed?.Invoke(v);
            };
        }

        void Apply(double x, bool commit)
        {
            double w = Bar.ActualWidth;
            if (w <= 0) return;
            double f = Clamp(x / w);
            Bar.Value = f;
            Changed?.Invoke(f);
            if (commit) Committed?.Invoke(f);
        }

        static double Clamp(double v) => Math.Max(0, Math.Min(1, v));

        // Knopf sitzt mittig über dem Ende der Füllung, bleibt aber innerhalb des Reglers
        void PlaceKnob()
        {
            double w = ActualWidth;
            if (w <= KnobW) return;
            double left = Math.Max(0, Math.Min(w - KnobW, Bar.Value * w - KnobW / 2));
            knob.Margin = new Thickness(left, 0, 0, 0);
        }
    }

    /// <summary>
    /// Ring mit Prozentzahl in der Mitte (Akku, Speicherplatz). Füllt sich beim Erscheinen
    /// und zählt dabei bis zum exakten Wert hoch.
    /// </summary>
    public sealed class RingGauge : Grid
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(RingGauge), new PropertyMetadata(0.0, (d, e) => ((RingGauge)d).Redraw()));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        readonly Path arc;
        readonly TextBlock label;
        readonly double size, thick, target;
        readonly string fixedText;

        public RingGauge(double target, Color color, double size = 46, double thick = 4, string text = null)
        {
            this.size = size;
            this.thick = thick;
            this.target = Math.Max(0, Math.Min(1, target));
            fixedText = text;
            Width = size;
            Height = size;
            VerticalAlignment = VerticalAlignment.Center;
            Children.Add(new Ellipse { Stroke = Ui.Faint, StrokeThickness = thick, Margin = new Thickness(thick / 2) });
            arc = new Path { Stroke = Ui.Br(color), StrokeThickness = thick, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            Children.Add(arc);
            label = Ui.Text("", size * 0.27, Ui.White, true, true);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            Children.Add(label);
            Loaded += (s, e) =>
            {
                if (Ui.ReduceMotion) Value = this.target;
                else BeginAnimation(ValueProperty, new DoubleAnimation(0, this.target, TimeSpan.FromMilliseconds(1100))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            };
            Redraw();
        }

        void Redraw()
        {
            double v = Math.Max(0, Math.Min(1, Value));
            label.Text = fixedText ?? Math.Round(v * 100).ToString(CultureInfo.InvariantCulture) + "%";
            double c = size / 2, r = (size - thick) / 2;
            if (v <= 0.001) { arc.Data = null; return; }
            if (v >= 0.999) { arc.Data = new EllipseGeometry(new Point(c, c), r, r); return; }
            double a = v * 2 * Math.PI;
            var start = new Point(c, c - r);
            var end = new Point(c + r * Math.Sin(a), c - r * Math.Cos(a));
            var fig = new PathFigure { StartPoint = start, IsClosed = false };
            fig.Segments.Add(new ArcSegment(end, new Size(r, r), 0, v > 0.5, SweepDirection.Clockwise, true));
            arc.Data = new PathGeometry(new[] { fig });
        }
    }

    /// <summary>Hüpfende Balken wie beim iPhone. Reagiert auf den echten Pegel.</summary>
    public sealed class Visualizer : StackPanel
    {
        static readonly Random Rnd = new Random();
        readonly Border[] bars;
        readonly double[] cur;
        readonly double maxH, barW;
        public bool FakeWhenSilent = true;

        public Visualizer(int count = 4, double maxHeight = 16, double barWidth = 3, double gap = 2.5)
        {
            Orientation = Orientation.Horizontal;
            Height = maxHeight;
            VerticalAlignment = VerticalAlignment.Center;
            maxH = maxHeight;
            barW = barWidth;
            bars = new Border[count];
            cur = new double[count];
            for (int i = 0; i < count; i++)
            {
                bars[i] = new Border
                {
                    Width = barWidth,
                    Height = barWidth,
                    CornerRadius = new CornerRadius(barWidth / 2),
                    Background = Ui.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(i == 0 ? 0 : gap, 0, 0, 0)
                };
                Children.Add(bars[i]);
            }
        }

        public void SetColor(Color c)
        {
            var b = Ui.Br(c);
            foreach (var bar in bars) bar.Background = b;
        }

        public void Step(double level, bool active)
        {
            double lvl = Math.Sqrt(Math.Max(0, Math.Min(1, level)));
            for (int i = 0; i < bars.Length; i++)
            {
                double target;
                if (!active) target = 0.12;
                else if (lvl < 0.02) target = FakeWhenSilent ? 0.25 + Rnd.NextDouble() * 0.45 : 0.12;
                else target = Math.Min(1, lvl * (0.5 + Rnd.NextDouble() * 0.8));
                cur[i] += (target - cur[i]) * 0.5;
                bars[i].Height = Math.Max(barW, cur[i] * maxH);
            }
        }
    }

    /// <summary>Akku-Symbol im iOS-Stil.</summary>
    public sealed class BatteryIcon : StackPanel
    {
        readonly Border fill;
        readonly TextBlock bolt;
        readonly double innerW;

        public BatteryIcon(double w = 26, double h = 13)
        {
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Center;
            var g = new Grid { Width = w, Height = h };
            g.Children.Add(new Border { BorderBrush = Ui.Dim, BorderThickness = new Thickness(1.2), CornerRadius = new CornerRadius(3.5) });
            fill = new Border
            {
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2.2),
                Background = Ui.White
            };
            g.Children.Add(fill);
            bolt = Ui.Glyph("", h * 0.72, Brushes.Black);
            bolt.Visibility = Visibility.Collapsed;
            g.Children.Add(bolt);
            Children.Add(g);
            Children.Add(new Border
            {
                Width = 2,
                Height = h * 0.4,
                Background = Ui.Dim,
                CornerRadius = new CornerRadius(0, 1, 1, 0),
                Margin = new Thickness(1, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            innerW = w - 4.4;
        }

        public void Set(int pct, bool charging)
        {
            pct = Math.Max(0, Math.Min(100, pct));
            fill.Width = Math.Max(1.5, innerW * pct / 100.0);
            fill.Background = charging ? Ui.Br(Ui.Green) : pct <= 20 ? Ui.Br(Ui.Red) : Ui.White;
            bolt.Visibility = charging ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Apple-Feder: analytisch gelöste, gedämpfte Schwingung mit Dämpfungsgrad ζ (0,8 = ganz leichtes
    /// Nachschwingen wie bei iOS, 1 = kritisch gedämpft). Über die Animationsdauer normiert.
    /// </summary>
    public sealed class SpringEase : IEasingFunction
    {
        public double Damping { get; set; } = 0.8;

        public double Ease(double t)
        {
            if (t >= 1) return 1;
            if (t <= 0) return 0;
            if (Ui.ReduceMotion) return 1 - Math.Pow(1 - t, 3); // ohne Überschwingen
            double z = Math.Max(0.3, Math.Min(1.0, Damping));
            double w = 7.5 / z;                 // so gewählt, dass die Feder am Ende der Dauer ruht
            if (z >= 0.999) return 1 - Math.Exp(-w * t) * (1 + w * t);
            double wd = w * Math.Sqrt(1 - z * z);
            return 1 - Math.Exp(-z * w * t) * (Math.Cos(wd * t) + z * w / wd * Math.Sin(wd * t));
        }
    }
}

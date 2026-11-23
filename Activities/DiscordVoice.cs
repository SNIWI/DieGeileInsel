using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DynamicIsland
{
    /// <summary>Runde Profilbilder aus dem Internet (mit Zwischenspeicher).</summary>
    public static class WebAvatar
    {
        static readonly Dictionary<string, ImageSource> cache = new Dictionary<string, ImageSource>();

        public static ImageSource Load(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (cache.TryGetValue(url, out var img)) return img;
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(url);
                bi.DecodePixelWidth = 96;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                img = bi;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                img = null;
            }
            cache[url] = img;
            return img;
        }

        /// <summary>Profilbild als Kreis mit Ring (grün = spricht gerade).</summary>
        public static Border Circle(string url, double size, out Border ring, double ringWidth = 2)
        {
            var g = new Grid { Width = size, Height = size };
            g.Children.Add(new Ellipse { Fill = Ui.Faint });
            var img = Load(url);
            if (img != null) g.Children.Add(new Ellipse { Fill = new ImageBrush(img) { Stretch = Stretch.UniformToFill } });
            double outer = size + ringWidth * 2 + 2;
            ring = new Border
            {
                Width = outer,
                Height = outer,
                CornerRadius = new CornerRadius(outer / 2),
                BorderThickness = new Thickness(ringWidth),
                BorderBrush = Brushes.Transparent,
                Background = Brushes.Black,
                Padding = new Thickness(1),
                Child = g,
                VerticalAlignment = VerticalAlignment.Center
            };
            return ring;
        }

        public static FrameworkElement Circle(string url, double size) => Circle(url, size, out _, 0);
    }

    /// <summary>
    /// Discord-Sprachkanal oder -Anruf wie im Discord-Overlay: Profilbilder mit grünem Ring,
    /// sobald jemand spricht. Erweitert: Liste aller Teilnehmer + Mikro / Ton / Auflegen.
    /// </summary>
    public sealed class DiscordVoiceActivity : Activity
    {
        public static readonly Color Blurple = Color.FromRgb(0x58, 0x65, 0xF2);
        static readonly SolidColorBrush SpeakBrush = Ui.Br(Ui.Green);
        static readonly SolidColorBrush BlurpleBrush = Ui.Br(Blurple);
        static readonly SolidColorBrush RedBrush = Ui.Br(Ui.Red);
        const double InnerW = 376;    // Seitenbreite 420 minus Rand

        readonly DiscordService d;
        readonly List<KeyValuePair<string, Border>> rings = new List<KeyValuePair<string, Border>>();
        readonly List<KeyValuePair<string, TextBlock>> names = new List<KeyValuePair<string, TextBlock>>();
        readonly List<KeyValuePair<string, Border>> badges = new List<KeyValuePair<string, Border>>();
        string builtFor = "";

        string Signature => Ch == null ? "" : string.Join(",", Ch.Members.Select(x => x.Id)) + "|" + (Ch.Members.Count > 0 ? AvatarSize : 0);

        /// <summary>Hat sich die Teilnehmerliste geändert (und nicht nur Stumm/Ton)?</summary>
        public bool NeedsRebuild => Signature != builtFor;
        TextBlock cLeft, eSub, mIcon;
        Border muteBtn, deafBtn;
        /// <summary>Go-Live-Stream beenden (setzt die Island; Discord-RPC kann das nicht selbst).</summary>
        public Action StopStream;

        public DiscordVoiceActivity(DiscordService service)
        {
            d = service;
            Id = "discord-voice";
            Priority = 60;
        }

        public VoiceChannel Ch => d.Channel;
        int Count => Ch?.Members.Count ?? 0;

        public override double CompactWidth => 260;
        // Alle Teilnehmer als Kacheln (wie FaceTime / Discord-Overlay). Je mehr Leute, desto kleiner die Bilder,
        // damit möglichst alle auf eine Seite passen – erst ab sehr vielen wird gescrollt.
        double AvatarSize => Count <= 4 ? 52 : Count <= 10 ? 38 : 32;
        double CellW => Count <= 4 ? 88 : Count <= 10 ? 70 : 60;
        double RowH => AvatarSize + 31;
        int PerRow => Math.Max(1, (int)(InnerW / CellW));
        int Rows => Count == 0 ? 1 : (Count + PerRow - 1) / PerRow;
        public override Size ExpandedSize => new Size(420, Math.Max(240, 80 + TileRows * (TileH + 6) * 1.25));

        public override FrameworkElement BuildCompact()
        {
            rings.Clear();
            names.Clear();
            badges.Clear();
            builtFor = Signature;
            var icon = Ui.Glyph(Ch != null && Ch.IsCall ? "" : "", 14, BlurpleBrush);
            cLeft = Ui.Text(Ch?.Name ?? "Discord", 13, Ui.White, true);
            cLeft.MaxWidth = 130;
            var left = Ui.HStack(7, icon, cLeft);

            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (Ch != null)
            {
                int i = 0;
                foreach (var m in Ch.Members.Take(4))
                {
                    var c = WebAvatar.Circle(m.AvatarUrl, 18, out var ring, 1.5);
                    if (i++ > 0) c.Margin = new Thickness(-6, 0, 0, 0);
                    rings.Add(new KeyValuePair<string, Border>(m.Id, ring));
                    stack.Children.Add(c);
                }
                if (Ch.Members.Count > 4)
                {
                    var more = Ui.Text("+" + (Ch.Members.Count - 4), 11.5, Ui.Dim, true, true);
                    more.Margin = new Thickness(5, 0, 0, 0);
                    stack.Children.Add(more);
                }
            }
            var g = Ui.Row3(left, null, stack, new Thickness(13, 0, 10, 0));
            Update();
            return g;
        }

        // ---------- Discord-Look ----------
        static readonly SolidColorBrush DcTile = Ui.Br(Color.FromRgb(0x23, 0x24, 0x28));
        static readonly SolidColorBrush DcButton = Ui.Br(Color.FromRgb(0x2B, 0x2D, 0x31));
        static readonly SolidColorBrush DcGreen = Ui.Br(Color.FromRgb(0x23, 0xA5, 0x5A));
        static readonly SolidColorBrush DcRed = Ui.Br(Color.FromRgb(0xDA, 0x37, 0x3C));
        readonly List<KeyValuePair<string, Border>> tiles = new List<KeyValuePair<string, Border>>();

        // Kacheln wie im Discord-Anruf: je mehr Leute, desto kleiner
        int Cols => Count <= 4 ? Math.Max(1, Count) : Count <= 8 ? 4 : 6;
        int TileRows => Count == 0 ? 1 : (Count + Cols - 1) / Cols;
        double TileH => Count <= 4 ? 92 : Count <= 8 ? 60 : 50;

        public override FrameworkElement BuildExpanded()
        {
            rings.Clear();
            names.Clear();
            badges.Clear();
            tiles.Clear();
            builtFor = Signature;
            var root = new Grid { Margin = Ui.PageMargin };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ---------- Kopf: „Sprachverbindung“ in Grün wie im Discord-Panel ----------
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var signal = Ui.Glyph("", 13, DcGreen);   // Funk-/Verbindungssymbol
            var status = Ui.Text(Ch != null && Ch.IsCall ? "Anruf verbunden" : "Sprachverbindung", 13, DcGreen);
            var dlogo = Logos.Discord(15);
            dlogo.Margin = new Thickness(0, 0, 2, 0);
            eSub = Ui.Text("", 12, Ui.Soft, false);
            var line1 = Ui.HStack(6, dlogo, signal, status, eSub);
            var title = Ui.Text(Ch?.Name ?? "Discord", 17, Ui.White);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            var left = Ui.VStack(1, line1, title);
            left.Margin = new Thickness(0, 0, 10, 0);
            head.Children.Add(left);
            root.Children.Add(head);

            // ---------- Teilnehmer als Kacheln ----------
            FrameworkElement body;
            if (Ch != null && Count > 0)
            {
                var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = Cols, Margin = new Thickness(-3, 10, -3, 0) };
                double av = Count <= 4 ? 46 : Count <= 8 ? 32 : 26;
                foreach (var m in Ch.Members)
                {
                    // groß: nur die Kachel bekommt den grünen Rand, das Profilbild keinen Ring
                    var face = WebAvatar.Circle(m.AvatarUrl, av, out _, 0);
                    face.HorizontalAlignment = HorizontalAlignment.Center;
                    face.VerticalAlignment = VerticalAlignment.Center;
                    face.Margin = new Thickness(0, 0, 0, Count <= 8 ? 14 : 12);
                    var badge = Badge();
                    badge.Margin = new Thickness(0, 6, 6, 0);
                    badge.VerticalAlignment = VerticalAlignment.Top;
                    badges.Add(new KeyValuePair<string, Border>(m.Id, badge));

                    // Name unten links in einer dunklen Kapsel (wie in Discord)
                    var name = Ui.Text(m.Id == d.SelfId ? "Du" : m.Name, Count <= 8 ? 11 : 10, Ui.White, true);
                    name.TextTrimming = TextTrimming.CharacterEllipsis;
                    names.Add(new KeyValuePair<string, TextBlock>(m.Id, name));
                    var pill = new Border
                    {
                        Background = Ui.Br(Colors.Black, 150),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(6, 1, 6, 2),
                        Margin = new Thickness(6, 0, 6, 6),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Child = name
                    };
                    var inner = new Grid();
                    inner.Children.Add(face);
                    inner.Children.Add(badge);
                    inner.Children.Add(pill);
                    var tile = new Border
                    {
                        Height = TileH,
                        Margin = new Thickness(3),
                        CornerRadius = new CornerRadius(12),
                        Background = DcTile,
                        BorderThickness = new Thickness(2),
                        BorderBrush = Brushes.Transparent,
                        ClipToBounds = true,
                        Child = inner,
                        ToolTip = m.Name
                    };
                    tiles.Add(new KeyValuePair<string, Border>(m.Id, tile));
                    grid.Children.Add(tile);
                }
                body = grid;
            }
            else
            {
                var empty = Ui.Text("Noch niemand im Kanal", 13, Ui.Soft, false);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.VerticalAlignment = VerticalAlignment.Center;
                body = empty;
            }
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            // ---------- Steuerleiste unten, mittig ----------
            muteBtn = DcRound("", d.ToggleMute, "Mikrofon stumm (Strg + Alt + M)");
            deafBtn = DcRound("", d.ToggleDeaf, "Ton aus");
            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            bar.Children.Add(muteBtn);
            bar.Children.Add(deafBtn);
            if (StopStream != null) bar.Children.Add(DcRound("", StopStream, "Stream beenden (Go Live)"));
            // Auflegen: breite rote Kapsel
            var leave = Ui.RoundButton("", 15, d.Leave, 38, DcRed, Ui.White);
            leave.Width = 52;
            leave.CornerRadius = new CornerRadius(17);
            leave.Margin = new Thickness(0);
            leave.ToolTip = "Verlassen";
            bar.Children.Add(leave);
            // Steuerung rechts im Kopf – so bleibt für die Kacheln genug Höhe
            bar.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(bar, 1);
            head.Children.Add(bar);
            Update();
            return root;
        }

        static Border DcRound(string glyph, Action act, string tip)
        {
            var b = Ui.RoundButton(glyph, 14, act, 34, DcButton, Ui.White);
            b.Margin = new Thickness(0, 0, 8, 0);
            b.ToolTip = tip;
            return b;
        }

        /// <summary>Kleines rundes Abzeichen unten rechts am Profilbild: stumm oder Ton aus.</summary>
        static Border Badge() => new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new CornerRadius(8),
                Background = RedBrush,
                Child = Ui.Glyph("\uE720", 9, Ui.White)
            },
            Visibility = Visibility.Collapsed
        };

        public override FrameworkElement BuildMinimal()
        {
            mIcon = Ui.Glyph("", 14, BlurpleBrush);
            return mIcon;
        }

        static FrameworkElement MicOff()
        {
            var g = new Grid { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(Ui.Glyph("", 12, RedBrush));
            g.Children.Add(new Line
            {
                X1 = 2, Y1 = 2, X2 = 12, Y2 = 12,
                Stroke = RedBrush,
                StrokeThickness = 1.6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
            return g;
        }

        public override void Update()
        {
            var ch = Ch;
            if (ch == null) return;
            foreach (var kv in rings)
            {
                var m = ch.Members.Find(x => x.Id == kv.Key);
                kv.Value.BorderBrush = m != null && m.Speaking ? SpeakBrush : Brushes.Transparent;
            }
            var speaker = ch.Members.FirstOrDefault(x => x.Speaking);
            if (cLeft != null) cLeft.Text = speaker != null ? speaker.Name : ch.Name;
            if (mIcon != null) mIcon.Foreground = speaker != null ? SpeakBrush : BlurpleBrush;
            foreach (var kv in badges)
            {
                var m = ch.Members.Find(x => x.Id == kv.Key);
                bool show = m != null && (m.Muted || m.Deafened);
                kv.Value.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                if (show && kv.Value.Child is Border inner && inner.Child is TextBlock icon)
                    icon.Text = m.Deafened ? "\uE74F" : "\uE720";
            }
            foreach (var kv in names)
            {
                var m = ch.Members.Find(x => x.Id == kv.Key);
                kv.Value.Foreground = m != null && m.Speaking ? Ui.White : Ui.Br(Colors.White, 200);
            }
            if (eSub != null)
                eSub.Text = "· " + Ui.Fmt(DateTime.Now - ch.JoinedAt);
            if (muteBtn != null) muteBtn.Background = d.MicOff ? DcRed : DcButton;   // „Ton aus“ schaltet das Mikro mit aus
            if (deafBtn != null) deafBtn.Background = d.SelfDeaf ? DcRed : DcButton;
            // sprechende Person: grüner Rahmen um die Kachel (wie in Discord)
            foreach (var kv in tiles)
            {
                var m = ch.Members.Find(x => x.Id == kv.Key);
                kv.Value.BorderBrush = m != null && m.Speaking ? DcGreen : Brushes.Transparent;
            }
        }
    }
}

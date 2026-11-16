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
        public override Size ExpandedSize => new Size(420, 84 + Rows * RowH);

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

        public override FrameworkElement BuildExpanded()
        {
            rings.Clear();
            names.Clear();
            badges.Clear();
            builtFor = Signature;
            var root = new StackPanel { Margin = new Thickness(22, 16, 22, 12) };

            // Kopfzeile: Kanal links, Steuerung rechts – alles auf einen Blick
            var circle = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = BlurpleBrush,
                Child = Ui.Glyph(Ch != null && Ch.IsCall ? "\uE717" : "\uE720", 17, Ui.White)
            };
            var title = Ui.Text(Ch?.Name ?? "Discord", 15, Ui.White);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            eSub = Ui.Text("", 12, Ui.Dim, false);
            var texts = Ui.VStack(1, title, eSub);
            texts.Margin = new Thickness(11, 0, 10, 0);
            texts.VerticalAlignment = VerticalAlignment.Center;

            muteBtn = Ui.RoundButton("\uE720", 15, d.ToggleMute, 36);
            muteBtn.ToolTip = "Mikrofon stumm (Strg + Alt + M)";
            deafBtn = Ui.RoundButton("\uE7F6", 15, d.ToggleDeaf, 36);
            deafBtn.ToolTip = "Ton aus";
            var leave = Ui.RoundButton("\uE778", 15, d.Leave, 36, RedBrush);
            leave.ToolTip = "Verlassen";
            var buttons = Ui.HStack(8, muteBtn, deafBtn, leave);
            if (StopStream != null)
            {
                var stream = Ui.RoundButton("", 15, StopStream, 36);
                stream.ToolTip = "Stream beenden (Go Live)";
                buttons.Children.Insert(2, stream);
            }
            root.Children.Add(Ui.Row3(circle, texts, buttons, new Thickness(0)));

            // Trennlinie wie in iOS-Listen
            root.Children.Add(new Border { Height = 1, Background = Ui.Br(Colors.White, 26), Margin = new Thickness(0, 12, 0, 6) });

            var grid = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            if (Ch != null && Count > 0)
            {
                double av = AvatarSize, cw = CellW;
                foreach (var m in Ch.Members)
                {
                    var face = WebAvatar.Circle(m.AvatarUrl, av, out var ring, av >= 44 ? 2.5 : 2);
                    rings.Add(new KeyValuePair<string, Border>(m.Id, ring));
                    var holder = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
                    holder.Children.Add(face);
                    var badge = Badge();
                    badges.Add(new KeyValuePair<string, Border>(m.Id, badge));
                    holder.Children.Add(badge);

                    var name = Ui.Text(m.Id == d.SelfId ? "Du" : m.Name, av >= 44 ? 11.5 : 10.5, Ui.Dim, false);
                    name.TextTrimming = TextTrimming.CharacterEllipsis;
                    name.TextAlignment = TextAlignment.Center;
                    name.HorizontalAlignment = HorizontalAlignment.Center;
                    name.MaxWidth = cw - 6;
                    name.Margin = new Thickness(0, 4, 0, 0);
                    names.Add(new KeyValuePair<string, TextBlock>(m.Id, name));

                    var cell = new StackPanel { Width = cw, Height = RowH, ToolTip = m.Name };
                    cell.Children.Add(holder);
                    cell.Children.Add(name);
                    grid.Children.Add(cell);
                }
            }
            else
            {
                var empty = Ui.Text("Noch niemand im Kanal", 13, Ui.Dim, false);
                empty.Margin = new Thickness(0, 24, 0, 0);
                grid.Children.Add(empty);
            }
            root.Children.Add(grid);
            Update();
            return root;
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
                kv.Value.Foreground = m != null && m.Speaking ? Ui.White : Ui.Dim;
            }
            if (eSub != null)
                eSub.Text = (Count == 1 ? "1 Person" : Count + " Personen") + " · " + Ui.Fmt(DateTime.Now - ch.JoinedAt);
            if (muteBtn != null) muteBtn.Background = d.MicOff ? RedBrush : Ui.GlassFill;   // „Ton aus“ schaltet das Mikro mit aus
            if (deafBtn != null) deafBtn.Background = d.SelfDeaf ? RedBrush : Ui.GlassFill;
        }
    }
}

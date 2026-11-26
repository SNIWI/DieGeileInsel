using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DynamicIsland
{
    /// <summary>
    /// Seite „Weltuhr“: Uhrzeiten in anderen Städten (Antippen der Uhr in der Übersicht).
    /// Städte stellst du unter Einstellungen → Weltuhr ein (Suche über Open-Meteo, wie beim Wetter).
    /// </summary>
    public sealed class WorldClockActivity : Activity
    {
        public const string Glyph = "";   // Weltkugel
        public const int Max = 6;
        readonly IslandWindow host;
        readonly List<(WorldCity City, TimeZoneInfo Zone, TextBlock Time, TextBlock Info, RotateTransform H, RotateTransform M, Ellipse Face, Line[] Hands, bool Day)> rows = new();
        string shownKey;
        bool compact;

        public WorldClockActivity(IslandWindow h)
        {
            host = h;
            Id = "worldclock";
            Priority = 1;
        }

        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(420, 240);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph(Glyph, 14, Ui.Br(Ui.Orange)), Ui.Text("Weltuhr", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph(Glyph, 14, Ui.Br(Ui.Orange));

        List<WorldCity> Cities => (host.Settings.WorldClocks ?? new List<WorldCity>()).Take(Max).ToList();

        static string Key(List<WorldCity> l) => string.Join("|", l.Select(c => c.Name + "@" + c.Zone));

        public override FrameworkElement BuildExpanded()
        {
            rows.Clear();
            var cities = Cities;
            shownKey = Key(cities);
            var root = new Grid { Margin = new Thickness(24, 10, 24, 12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Kopf: Titel + Einstellungen
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = Ui.Text("Weltuhr", 19, Ui.White);
            title.VerticalAlignment = VerticalAlignment.Center;
            var gear = Ui.CircleButton("", () => host.OpenSetup("worldclock"), 30);
            gear.ToolTip = "Städte hinzufügen oder entfernen";
            Grid.SetColumn(gear, 1);
            head.Children.Add(title);
            head.Children.Add(gear);
            root.Children.Add(head);

            if (cities.Count == 0)
            {
                var msg = Ui.VStack(6, Center(Ui.Text("Noch keine Städte", 14, Ui.Dim, false)), Center(Ui.Link("Städte hinzufügen", () => host.OpenSetup("worldclock"), 13)));
                msg.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetRow(msg, 1);
                root.Children.Add(msg);
                return root;
            }

            // bis 3 Städte: eine Spalte mit großen Zeilen, sonst zwei Spalten
            bool two = cities.Count > 3;
            compact = two;
            var grid = new UniformGrid { Columns = two ? 2 : 1, Rows = two ? (cities.Count + 1) / 2 : cities.Count, Margin = new Thickness(-6, 6, -6, 0) };
            Grid.SetRow(grid, 1);
            foreach (var c in cities) grid.Children.Add(Row(c, two));
            root.Children.Add(grid);
            Update();
            return root;
        }

        FrameworkElement Row(WorldCity c, bool small)
        {
            var zone = Zone(c.Zone);
            double dial = small ? 30 : 36;
            var face = new Ellipse { Width = dial, Height = dial };
            var hr = new RotateTransform(0, dial / 2, dial / 2);
            var mr = new RotateTransform(0, dial / 2, dial / 2);
            var hHand = new Line { X1 = dial / 2, Y1 = dial / 2, X2 = dial / 2, Y2 = dial * 0.24, StrokeThickness = 2.2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, RenderTransform = hr };
            var mHand = new Line { X1 = dial / 2, Y1 = dial / 2, X2 = dial / 2, Y2 = dial * 0.1, StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, RenderTransform = mr };
            var dot = new Ellipse { Width = 3.5, Height = 3.5, Fill = Ui.Br(Ui.Orange) };
            Canvas.SetLeft(dot, dial / 2 - 1.75); Canvas.SetTop(dot, dial / 2 - 1.75);
            var clock = new Canvas { Width = dial, Height = dial, VerticalAlignment = VerticalAlignment.Center };
            clock.Children.Add(face);
            clock.Children.Add(hHand);
            clock.Children.Add(mHand);
            clock.Children.Add(dot);

            var name = Ui.Text(c.Name, small ? 13 : 14.5, Ui.White);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            var info = Ui.Text("", small ? 11 : 12, Ui.Dim, false);
            info.TextTrimming = TextTrimming.CharacterEllipsis;
            var texts = Ui.VStack(0, name, info);
            texts.Margin = new Thickness(small ? 8 : 11, 0, 6, 0);
            texts.VerticalAlignment = VerticalAlignment.Center;
            var time = Ui.Text("", small ? 18 : 24, Ui.White, false, true);
            time.FontWeight = FontWeights.Light;
            time.VerticalAlignment = VerticalAlignment.Center;

            var g = new Grid { Margin = new Thickness(small ? 3 : 0, 2, small ? 3 : 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(texts, 1); Grid.SetColumn(time, 2);
            g.Children.Add(clock); g.Children.Add(texts); g.Children.Add(time);
            var card = new Border { CornerRadius = new CornerRadius(14), Background = Ui.Br(Colors.White, 14), Padding = new Thickness(small ? 8 : 12, 0, small ? 10 : 14, 0), Margin = new Thickness(4, 3, 4, 3), Child = g, ToolTip = c.Country };
            rows.Add((c, zone, time, info, hr, mr, face, new[] { hHand, mHand }, false));
            int idx = rows.Count - 1;
            Paint(idx, true);
            return card;
        }

        /// <summary>Tag: helles Ziffernblatt mit dunklen Zeigern, Nacht: dunkel mit hellen (wie auf dem iPhone).</summary>
        void Paint(int i, bool force)
        {
            var r = rows[i];
            var now = Now(r.Zone);
            bool day = now.Hour >= 6 && now.Hour < 18;
            if (!force && day == r.Day) return;
            r.Face.Fill = day ? Ui.Br(Colors.White, 235) : Ui.Br(Colors.White, 30);
            var hand = day ? new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)) : Ui.White;
            foreach (var l in r.Hands) l.Stroke = hand;
            r.Day = day;
            rows[i] = r;
        }

        public override void Update()
        {
            if (Key(Cities) != shownKey) { host.Rebuild(this); return; }
            var local = DateTime.Now;
            var st = host.Settings;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var t = Now(r.Zone);
                r.Time.Text = st.Clock24 ? t.ToString("HH:mm", Ui.De) : t.ToString("h:mm", Ui.De) + (t.Hour < 12 ? " AM" : " PM");
                r.Info.Text = Offset(t, local, compact);
                r.M.Angle = t.Minute * 6 + t.Second * 0.1;
                r.H.Angle = (t.Hour % 12) * 30 + t.Minute * 0.5;
                Paint(i, false);
            }
        }

        /// <summary>„Heute, +6 Std.“ / „Morgen, +8 Std.“ / „Gleiche Zeit“.</summary>
        static string Offset(DateTime there, DateTime here, bool shortForm)
        {
            double h = Math.Round((there - here).TotalMinutes / 15) / 4.0;
            string day = there.Date == here.Date ? "Heute" : there.Date > here.Date ? "Morgen" : "Gestern";
            if (h == 0) return day + (shortForm ? ", gleich" : ", gleiche Zeit");
            string sign = h > 0 ? "+" : "−";
            double a = Math.Abs(h);
            string num = a % 1 == 0 ? a.ToString("0") : $"{(int)a}:{(int)Math.Round(a % 1 * 60):00}";
            return $"{day}, {sign}{num}" + (shortForm ? "" : " Std.");
        }

        static DateTime Now(TimeZoneInfo z) => z == null ? DateTime.Now : TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Utc, z);

        static readonly Dictionary<string, TimeZoneInfo> zones = new();
        /// <summary>IANA-Zone („America/New_York“) → TimeZoneInfo, auch ohne ICU über die Windows-Zuordnung.</summary>
        public static TimeZoneInfo Zone(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (zones.TryGetValue(id, out var z)) return z;
            try { z = TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch
            {
                try { if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var win)) z = TimeZoneInfo.FindSystemTimeZoneById(win); }
                catch { }
            }
            zones[id] = z;
            return z;
        }

        static FrameworkElement Center(FrameworkElement e) { e.HorizontalAlignment = HorizontalAlignment.Center; return e; }

        // ---------- Städte suchen (Open-Meteo, kostenlos, liefert die Zeitzone gleich mit) ----------
        static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public static async Task<WorldCity> FindAsync(string query)
        {
            query = (query ?? "").Trim();
            if (query.Length < 2) return null;
            var j = JsonNode.Parse(await http.GetStringAsync(
                "https://geocoding-api.open-meteo.com/v1/search?count=1&language=de&name=" + Uri.EscapeDataString(query)));
            var r = j["results"]?.AsArray();
            if (r == null || r.Count == 0) return null;
            string zone = r[0]["timezone"]?.GetValue<string>();
            if (string.IsNullOrEmpty(zone) || Zone(zone) == null) return null;
            return new WorldCity
            {
                Name = r[0]["name"]?.GetValue<string>() ?? query,
                Country = r[0]["country"]?.GetValue<string>() ?? "",
                Zone = zone,
            };
        }

        /// <summary>Standard beim ersten Start.</summary>
        public static List<WorldCity> Defaults() => new List<WorldCity>
        {
            new WorldCity { Name = "New York", Country = "Vereinigte Staaten", Zone = "America/New_York" },
            new WorldCity { Name = "London", Country = "Vereinigtes Königreich", Zone = "Europe/London" },
            new WorldCity { Name = "Tokio", Country = "Japan", Zone = "Asia/Tokyo" },
        };
    }

    /// <summary>Eine Stadt der Weltuhr.</summary>
    public sealed class WorldCity
    {
        public string Name { get; set; } = "";
        public string Country { get; set; } = "";
        /// <summary>IANA-Zeitzone, z. B. „America/New_York“.</summary>
        public string Zone { get; set; } = "";
    }
}

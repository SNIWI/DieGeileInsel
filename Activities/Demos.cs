using System;

namespace DynamicIsland
{
    /// <summary>
    /// Demo-Aktivitäten für alles, wofür Windows keine Schnittstelle hat
    /// (Navigation, Flüge, Uber, Lieferdienste, Sport, Anrufe …).
    /// Echte Daten kannst du über die lokale API einspeisen (siehe README).
    /// </summary>
    public static class Demos
    {
        static string Dist(double m) => m >= 1000
            ? (m / 1000).ToString("0.0", Ui.De) + " km"
            : $"{Math.Max(0, Math.Round(m / 10) * 10):0} m";

        public static void Navigation(IslandWindow h)
        {
            var steps = new (string arrow, string text, string street, double meters)[]
            {
                ("↱", "Rechts abbiegen", "Hauptstraße", 350),
                ("↑", "Geradeaus weiter", "Bahnhofstraße", 800),
                ("↰", "Links abbiegen", "Am Marktplatz", 250),
                ("⚑", "Ziel erreicht", "Schulstraße 12", 80),
            };
            const double routeLength = 1480;
            int i = 0;
            double dist = steps[0].meters, left = routeLength;
            var last = DateTime.Now;
            var g = new GenericActivity
            {
                Id = "demo-nav", Priority = 60, GlyphFont = Ui.SymbolFont,
                Accent = Ui.Green, CompactW = 240, RightColor = Ui.WhiteC
            };
            g.Ticker = x =>
            {
                var now = DateTime.Now;
                double dt = (now - last).TotalSeconds;
                last = now;
                dist -= dt * 15;   // Demo: 15 m pro Sekunde
                left -= dt * 15;
                if (dist <= 0)
                {
                    if (i < steps.Length - 1) { i++; dist = steps[i].meters; }
                    else { x.Finished = true; return; }
                }
                var s = steps[i];
                var eta = TimeSpan.FromSeconds(Math.Max(0, left) / 9);
                x.Glyph = s.arrow;
                x.LeftText = Dist(dist);
                x.RightText = $"{Math.Max(1, (int)Math.Ceiling(eta.TotalMinutes))} Min";
                x.Title = s.text;
                x.Subtitle = $"in {Dist(dist)} · {s.street}";
                x.Detail = $"Ankunft {DateTime.Now.Add(eta):HH:mm} · noch {Dist(Math.Max(0, left))}";
                x.Progress = 1 - Math.Max(0, left) / routeLength;
            };
            g.Buttons.Add(new ActionButton("", Ui.Br(Ui.Red), () => { g.Finished = true; h.Relayout(); }));
            g.Ticker(g);
            h.Add(g);
        }

        public static void Flight(IslandWindow h)
        {
            var g = new GenericActivity
            {
                Id = "demo-flight", Priority = 58, Glyph = "",
                Accent = Ui.Blue, CompactW = 240, LeftText = "LH 2034"
            };
            g.Ticker = x =>
            {
                double p = Math.Min(1, x.Seconds / 90.0);   // Demo: 90 s = ganzer Flug
                var rest = TimeSpan.FromMinutes(70 * (1 - p));
                x.Progress = p;
                x.RightText = p >= 1 ? "Gelandet"
                    : rest.TotalMinutes >= 60 ? $"{(int)rest.TotalHours} h {rest.Minutes} min"
                    : $"{(int)Math.Ceiling(rest.TotalMinutes)} min";
                x.Title = "LH 2034 · München → Berlin";
                x.Subtitle = p < 0.05 ? "Boarding · Gate G24" : p >= 1 ? "Gelandet · Gepäckband 3" : "In der Luft · 10.400 m";
                x.Detail = "MUC 17:10   ✈   BER 18:20";
                if (x.Seconds > 98) x.Finished = true;
            };
            g.Ticker(g);
            h.Add(g);
        }

        public static void Ride(IslandWindow h)
        {
            var g = new GenericActivity { Id = "demo-ride", Priority = 57, Glyph = "", Accent = Ui.WhiteC, CompactW = 240 };
            g.Ticker = x =>
            {
                const double total = 60;
                double rest = Math.Max(0, total - x.Seconds);
                int min = Math.Max(1, (int)Math.Ceiling(rest / 15));
                x.Progress = 1 - rest / total;
                x.LeftText = rest > 0 ? "Fahrer kommt" : "Fahrer ist da";
                x.RightText = rest > 0 ? $"{min} Min" : "Jetzt";
                x.RightColor = rest > 0 ? Ui.WhiteC : Ui.Green;
                x.Title = rest > 0 ? $"Dein Fahrer kommt in {min} Min" : "Dein Fahrer ist da";
                x.Subtitle = "Ali · Toyota Prius, weiß · 4,9 ★";
                x.Detail = "Kennzeichen B-UB 1234";
                if (x.Seconds > total + 12) x.Finished = true;
            };
            g.Ticker(g);
            h.Add(g);
        }

        public static void Food(IslandWindow h)
        {
            var stages = new[] { "Bestellung bestätigt", "Wird zubereitet", "Unterwegs zu dir", "Gleich da!", "Geliefert – guten Appetit!" };
            var g = new GenericActivity
            {
                Id = "demo-food", Priority = 56, Glyph = "\U0001F355", GlyphFont = Ui.EmojiFont,
                Accent = Ui.Orange, CompactW = 220
            };
            g.Ticker = x =>
            {
                const double total = 75;
                double p = Math.Min(1, x.Seconds / total);
                int st = Math.Min(stages.Length - 1, (int)(p * (stages.Length - 1)));
                int min = (int)Math.Ceiling((1 - p) * 25);
                x.Progress = p;
                x.Title = stages[st];
                x.Subtitle = "Pizzeria Da Mario · 2 Artikel";
                x.LeftText = p >= 1 ? "Geliefert" : null;
                x.RightText = p >= 1 ? "✓" : $"{min} Min";
                x.Detail = p >= 1 ? "Zugestellt" : $"Ankunft ca. {DateTime.Now.AddMinutes(min):HH:mm}";
                if (x.Seconds > total + 10) x.Finished = true;
            };
            g.Ticker(g);
            h.Add(g);
        }

        public static void Sports(IslandWindow h)
        {
            int home = 0, away = 0, lastMinute = 0;
            string lastEvent = "Anstoß";
            var rnd = new Random();
            var g = new GenericActivity
            {
                Id = "demo-sport", Priority = 55, Glyph = "⚽", GlyphFont = Ui.EmojiFont,
                Accent = Ui.WhiteC, CompactW = 220, RightColor = Ui.WhiteC
            };
            g.Ticker = x =>
            {
                int minute = Math.Min(90, (int)(x.Seconds * 1.5));   // Demo: 1 s = 1,5 Spielminuten
                for (int m = lastMinute + 1; m <= minute; m++)
                {
                    if (rnd.NextDouble() < 0.035)
                    {
                        if (rnd.NextDouble() < 0.55) { home++; lastEvent = $"⚽ {m}'  Tor für FCB"; }
                        else { away++; lastEvent = $"⚽ {m}'  Tor für BVB"; }
                    }
                }
                lastMinute = minute;
                x.LeftText = $"FCB {home}";
                x.RightText = $"{away} BVB";
                x.Title = $"FCB   {home} : {away}   BVB";
                x.Subtitle = minute >= 90 ? "Abpfiff · Bundesliga" : $"{minute}' · Bundesliga, 11. Spieltag";
                x.Detail = lastEvent;
                if (x.Seconds > 70) x.Finished = true;
            };
            g.Ticker(g);
            h.Add(g);
        }

        public static void Call(IslandWindow h)
        {
            DateTime? acceptedAt = null;
            bool muted = false;
            var g = new GenericActivity
            {
                Id = "demo-call", Priority = 70, Banner = true, Glyph = "", Accent = Ui.Green,
                Title = "Mama", Subtitle = "Mobil · eingehender Anruf", CompactW = 370
            };
            g.Buttons.Add(new ActionButton("", Ui.Br(Ui.Red), () => { g.Finished = true; h.Relayout(); }));
            g.Buttons.Add(new ActionButton("", Ui.Br(Ui.Green), () =>
            {
                acceptedAt = DateTime.Now;
                g.Banner = false;
                g.CompactW = 190;
                g.Buttons.Clear();
                g.Buttons.Add(new ActionButton("", Ui.Faint, () => muted = !muted));
                g.Buttons.Add(new ActionButton("", Ui.Br(Ui.Red), () => { g.Finished = true; h.Relayout(); }));
                h.Rebuild(g);
            }));
            g.Ticker = x =>
            {
                if (acceptedAt == null)
                {
                    if (x.Seconds > 25) x.Finished = true;   // verpasster Anruf
                    return;
                }
                var t = DateTime.Now - acceptedAt.Value;
                x.LeftText = null;
                x.RightText = Ui.Fmt(t);
                x.RightColor = Ui.Green;
                x.Subtitle = (muted ? "Stumm · " : "Anruf · ") + Ui.Fmt(t);
            };
            h.Add(g);
        }

        public static void ScreenRecording(IslandWindow h)
        {
            var g = new GenericActivity
            {
                Id = "demo-rec", Priority = 65, Glyph = "●", GlyphFont = Ui.SymbolFont, Accent = Ui.Red,
                CompactW = 190, Title = "Bildschirmaufnahme", Subtitle = "Gesamter Bildschirm · mit Ton"
            };
            g.Buttons.Add(new ActionButton("", Ui.Br(Ui.Red), () =>
            {
                g.Finished = true;
                h.Alert("rec-saved", 3, a =>
                {
                    a.Banner = true; a.Glyph = ""; a.Accent = Ui.Green;
                    a.Title = "Aufnahme gespeichert"; a.Subtitle = "Demo – es wurde nichts aufgenommen";
                });
            }));
            g.Ticker = x => x.RightText = Ui.Fmt(TimeSpan.FromSeconds(x.Seconds));
            g.Ticker(g);
            h.Add(g);
        }

        public static void Hotspot(IslandWindow h)
        {
            var g = new GenericActivity
            {
                Id = "demo-hotspot", Priority = 30, Glyph = "", Accent = Ui.Green, CompactW = 220,
                LeftText = "Hotspot", RightText = "2 Geräte", Title = "Persönlicher Hotspot",
                Subtitle = "2 Geräte verbunden", Detail = "iPad · Laptop"
            };
            g.Buttons.Add(new ActionButton("", Ui.Faint, () => { g.Finished = true; h.Relayout(); }));
            h.Add(g);
        }

        public static void AirDrop(IslandWindow h)
        {
            var g = new GenericActivity
            {
                Id = "demo-airdrop", Priority = 62, Glyph = "", Accent = Ui.Blue, CompactW = 260,
                BarInCompact = true, Title = "AirDrop · Urlaub.jpg", Subtitle = "an iPhone", Progress = 0
            };
            g.Ticker = x =>
            {
                double p = Math.Min(1, x.Seconds / 7);
                x.Progress = p;
                x.RightText = p >= 1 ? "Fertig" : $"{(int)(p * 100)} %";
                if (p >= 1) { x.Glyph = ""; x.Accent = Ui.Green; x.Subtitle = "Gesendet"; }
                if (x.Seconds > 9) x.Finished = true;
            };
            g.Ticker(g);
            h.Add(g);
        }

        public static void FaceId(IslandWindow h) => h.Alert("demo-faceid", 2.8, g =>
        {
            g.Glyph = ""; g.Accent = Ui.WhiteC; g.CompactW = 190;
            g.LeftText = null; g.RightText = null; g.StartedAt = DateTime.Now;
            g.Ticker = x =>
            {
                if (x.Seconds > 1.1) { x.Glyph = ""; x.Accent = Ui.Green; x.RightText = "Entsperrt"; }
            };
        });

        public static void Pay(IslandWindow h) => h.Alert("demo-pay", 4.5, g =>
        {
            g.Banner = true; g.Glyph = ""; g.Accent = Ui.WhiteC;
            g.Title = "Apple Pay"; g.Subtitle = "Zum Bezahlen Gerät nähern";
            g.RightText = "4,90 €"; g.RightColor = Ui.WhiteC; g.StartedAt = DateTime.Now;
            g.Ticker = x =>
            {
                if (x.Seconds > 1.6) { x.Glyph = ""; x.Accent = Ui.Green; x.Title = "Fertig"; x.Subtitle = "Bäckerei Schmidt"; }
            };
        });

        public static void AirPods(IslandWindow h) => h.Alert("demo-airpods", 4, g =>
        {
            g.Banner = true; g.Glyph = ""; g.Accent = Ui.WhiteC;
            g.Title = "AirPods Pro"; g.Subtitle = "Verbunden · Etui 62 %";
            g.RightFactory = () =>
            {
                var b = new BatteryIcon();
                b.Set(85, false);
                return Ui.HStack(7, Ui.Text("85 %", 14, Ui.Br(Ui.Green), true, true), b);
            };
        });

        public static void Charging(IslandWindow h) => h.ShowChargingAlert(true, 78);

        public static void Silent(IslandWindow h) => h.Alert("demo-silent", 2.2, g =>
        {
            g.Glyph = ""; g.Accent = Ui.Red; g.CompactW = 200;
            g.LeftText = "Stummmodus"; g.RightText = "An"; g.RightColor = Ui.Red;
        });
    }
}

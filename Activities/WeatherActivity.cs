using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DynamicIsland
{
    /// <summary>Seite „Wetter“: jetzt, die nächsten Stunden und die nächsten Tage.</summary>
    public sealed class WeatherActivity : Activity
    {
        public const string Glyph = "";   // Wolke/Sonne (Segoe Fluent „Weather“)
        readonly IslandWindow host;
        DateTime shown;

        public WeatherActivity(IslandWindow h)
        {
            host = h;
            Id = "weather";
            Priority = 1;
        }

        WeatherService W => host.Weather;

        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(420, 240);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Icon(16),
                              Ui.Text(W.HasData ? WeatherService.Deg(W.Temp) : "Wetter", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Icon(17);

        FrameworkElement Icon(double size) => W.HasData ? WeatherIcon.Create(W.Code, W.IsDay, size) : Ui.Glyph(Glyph, size - 3, Ui.Br(Ui.Teal));

        static TextBlock Emoji(string e, double size)
        {
            var t = Ui.Glyph(e, size, Ui.White, Ui.EmojiFont);
            t.VerticalAlignment = VerticalAlignment.Center;
            return t;
        }

        public override FrameworkElement BuildExpanded()
        {
            shown = W.LastUpdate;
            var root = new StackPanel { Margin = new Thickness(22, 10, 22, 6) };
            if (!W.HasData)
            {
                var msg = Ui.Text(string.IsNullOrEmpty(W.Error) ? "Wetter wird geladen …" : W.Error, 13.5, Ui.Dim, false);
                msg.TextWrapping = TextWrapping.Wrap;
                msg.HorizontalAlignment = HorizontalAlignment.Center;
                msg.TextAlignment = TextAlignment.Center;
                msg.Margin = new Thickness(0, 46, 0, 8);
                root.Children.Add(msg);
                var l = Ui.Link("Ort einstellen", () => host.OpenSetup("weather"), 13);
                l.HorizontalAlignment = HorizontalAlignment.Center;
                root.Children.Add(l);
                return root;
            }

            bool clean = host.CleanPage(this);
            if (selectedDay.HasValue)
            {
                var day = W.Days.FirstOrDefault(x => x.Date.Date == selectedDay.Value);
                if (day != null) return BuildDay(day, clean);
                selectedDay = null;
            }

            // Kopf: Symbol, Temperatur, Ort + Beschreibung
            var big = WeatherIcon.Create(W.Code, W.IsDay, clean ? 46 : 42);
            var temp = Ui.Text(WeatherService.Deg(W.Temp), clean ? 40 : 36, Ui.White);
            temp.FontWeight = FontWeights.SemiBold;
            temp.Margin = new Thickness(10, -4, 14, 0);
            var today = W.Days.FirstOrDefault();
            var range = today != null ? $"{WeatherService.Deg(today.Max)} / {WeatherService.Deg(today.Min)}" : "";
            var info = clean
                ? Ui.VStack(2,
                    Ui.Text(W.Place, 16, Ui.White),
                    Ui.Text(WeatherService.Text(W.Code) + (range != "" ? " · " + range : ""), 13, Ui.Dim, false))
                : Ui.VStack(1,
                    Ui.Text(W.Place, 14, Ui.White),
                    Ui.Text(WeatherService.Text(W.Code) + (today != null ? $" · H {WeatherService.Deg(today.Max)}  T {WeatherService.Deg(today.Min)}" : ""), 12, Ui.Dim, false),
                    Ui.Text($"Gefühlt {WeatherService.Deg(W.Feels)} · Wind {Math.Round(W.Wind)} km/h · {W.Humidity} %", 11.5, Ui.Dim, false));
            info.VerticalAlignment = VerticalAlignment.Center;
            var head = Ui.HStack(0, big, temp, info);
            root.Children.Add(head);


            // Wochentage – antippen öffnet die Stunden dieses Tages
            int nd = Math.Min(W.Days.Count, clean ? 6 : 7);
            var days = new UniformGrid { Columns = Math.Max(1, nd), Rows = 1 };
            foreach (var d in W.Days.Take(nd))
            {
                var dd = d;
                bool isToday = d.Date.Date == DateTime.Today;
                var cell = Ui.VStack(3,
                    Center(Ui.Text(isToday ? "Heute" : d.Date.ToString("ddd", Ui.De).TrimEnd('.'), clean ? 12.5 : 12, isToday ? Ui.White : Ui.Dim, isToday)),
                    Center(WeatherIcon.Create(d.Code, true, clean ? 24 : 22)),
                    Center(Ui.HStack(4, Ui.Text(WeatherService.Deg(d.Max), clean ? 13 : 12.5, Ui.White), Ui.Text(WeatherService.Deg(d.Min), clean ? 12.5 : 12, Ui.Dim, false))));
                days.Children.Add(Tappable(cell, () => OpenDay(dd.Date), "Stunden anzeigen"));
            }
            // Stunden gibt es nur auf Knopfdruck: einen Tag antippen
            root.Children.Add(new Border { CornerRadius = new CornerRadius(16), Background = Ui.Br(Colors.White, 16), Padding = new Thickness(4, 6, 4, 6), Margin = new Thickness(0, 10, 0, 0), Child = days });
            return root;
        }

        // ---------- Tagesansicht: Stunden eines Tages ----------
        DateTime? selectedDay;

        void OpenDay(DateTime d) { selectedDay = d.Date; host.Rebuild(this); }
        void CloseDay() { selectedDay = null; host.Rebuild(this); }

        FrameworkElement BuildDay(WeatherDay day, bool clean)
        {
            var root = new StackPanel { Margin = new Thickness(22, 12, 22, 8) };
            bool isToday = day.Date.Date == DateTime.Today;
            // Kopf: zurück, Tag, Beschreibung, Höchst/Tiefst
            var back = Ui.RoundButton("", 12, CloseDay, 28);
            back.ToolTip = "Zurück zur Übersicht";
            back.VerticalAlignment = VerticalAlignment.Center;
            var title = Ui.VStack(1,
                Ui.Text(isToday ? "Heute" : day.Date.ToString("dddd, d. MMMM", Ui.De), 15, Ui.White),
                Ui.Text(WeatherService.Text(day.Code) + " · " + WeatherService.Deg(day.Max) + " / " + WeatherService.Deg(day.Min) +
                        (day.Rain >= 20 ? " · Regen " + day.Rain + " %" : ""), 12, Ui.Dim, false));
            title.Margin = new Thickness(10, 0, 0, 0);
            title.VerticalAlignment = VerticalAlignment.Center;
            var icon = WeatherIcon.Create(day.Code, true, 30);
            icon.VerticalAlignment = VerticalAlignment.Center;
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(title, 1); Grid.SetColumn(icon, 2);
            head.Children.Add(back); head.Children.Add(title); head.Children.Add(icon);
            root.Children.Add(head);

            // heute: ab jetzt jede Stunde; andere Tage: alle zwei Stunden über den ganzen Tag
            var hrs = isToday
                ? W.AllHours.Where(x => x.Time >= DateTime.Now.AddMinutes(-30) && x.Time.Date == day.Date.Date).Take(12).ToList()
                : W.AllHours.Where(x => x.Time.Date == day.Date.Date && x.Time.Hour % 2 == 0).Take(12).ToList();
            if (isToday && hrs.Count < 6)   // spät am Abend: mit den ersten Stunden von morgen auffüllen
                hrs = W.AllHours.Where(x => x.Time >= DateTime.Now.AddMinutes(-30)).Take(12).ToList();
            var grid = new UniformGrid { Columns = 6, Rows = hrs.Count > 6 ? 2 : 1 };
            foreach (var h in hrs)
            {
                bool now = isToday && h.Time.Hour == DateTime.Now.Hour && h.Time.Date == DateTime.Today;
                var cell = Ui.VStack(1,
                    Center(Ui.Text(now ? "Jetzt" : h.Time.ToString("HH") + " Uhr", 11, now ? Ui.White : Ui.Dim, now)),
                    Center(WeatherIcon.Create(h.Code, h.Day, 18)),
                    Center(Ui.Text(WeatherService.Deg(h.Temp), 12.5, Ui.White)));
                if (!clean && h.Rain >= 30) cell.Children.Add(Center(Ui.Text(h.Rain + " %", 9.5, Ui.Br(Ui.Teal), false)));
                cell.Margin = new Thickness(0, 3, 0, 3);
                grid.Children.Add(cell);
            }
            root.Children.Add(new Border { CornerRadius = new CornerRadius(16), Background = Ui.Br(Colors.White, 16), Padding = new Thickness(4, 6, 4, 6), Margin = new Thickness(0, 10, 0, 0), Child = grid });
            // irgendwo auf die Island tippen → zurück zum Wetter
            var hit = new Border { Background = Brushes.Transparent, Child = root, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "Antippen: zurück zum Wetter" };
            hit.MouseLeftButtonDown += (o, e) => e.Handled = true;
            hit.MouseLeftButtonUp += (o, e) => { e.Handled = true; CloseDay(); };
            return hit;
        }

        /// <summary>Antippbare Fläche mit Hover (für die Wochentage).</summary>
        static Border Tappable(FrameworkElement content, Action tap, string tip)
        {
            var b = new Border { CornerRadius = new CornerRadius(10), Background = Brushes.Transparent, Padding = new Thickness(2, 4, 2, 4), Margin = new Thickness(1, 0, 1, 0), Cursor = System.Windows.Input.Cursors.Hand, Child = content, ToolTip = tip };
            b.MouseEnter += (o, e) => b.Background = Ui.Br(Colors.White, 22);
            b.MouseLeave += (o, e) => b.Background = Brushes.Transparent;
            b.MouseLeftButtonDown += (o, e) => e.Handled = true;
            b.MouseLeftButtonUp += (o, e) => { e.Handled = true; tap(); };
            return b;
        }

        static FrameworkElement Center(FrameworkElement e) { e.HorizontalAlignment = HorizontalAlignment.Center; return e; }

        public override void Update()
        {
            if (W.LastUpdate != shown) { shown = W.LastUpdate; host.Rebuild(this); }
        }
    }

    /// <summary>Widget: Wetter jetzt.</summary>
    sealed class WeatherWidget : Widget
    {
        Border icon;
        TextBlock temp, desc;
        DateTime shown = DateTime.MinValue;
        public override Action Tap => () => H.ShowWeather();
        public override FrameworkElement Build()
        {
            icon = new Border { Width = 30, Height = 30, VerticalAlignment = VerticalAlignment.Center, Child = Ui.Glyph(WeatherActivity.Glyph, 22, Ui.Br(Ui.Teal)) };
            temp = Big("–");
            desc = Ui.Text("", 11.5, Ui.Dim, false);
            desc.TextTrimming = TextTrimming.CharacterEllipsis;
            var texts = Ui.VStack(0, temp, desc);
            texts.Margin = new Thickness(10, 0, 0, 0);
            texts.VerticalAlignment = VerticalAlignment.Center;
            return Ui.HStack(0, icon, texts);
        }
        public override void Update()
        {
            var w = H.Weather;
            if (w.LastUpdate == shown && shown != DateTime.MinValue) return;
            shown = w.LastUpdate;
            if (!w.HasData) { temp.Text = "Wetter"; desc.Text = string.IsNullOrEmpty(w.Error) ? "lädt …" : "Ort einstellen"; return; }
            icon.Child = WeatherIcon.Create(w.Code, w.IsDay, 30);
            temp.Text = WeatherService.Deg(w.Temp);
            desc.Text = WeatherService.Text(w.Code) + " · " + w.Place;
        }
    }
}

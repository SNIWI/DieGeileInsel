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
            var root = new StackPanel { Margin = new Thickness(22, 14, 22, 8) };
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

            // Kopf: Symbol, Temperatur, Ort + Beschreibung
            var big = WeatherIcon.Create(W.Code, W.IsDay, clean ? 50 : 46);
            var temp = Ui.Text(WeatherService.Deg(W.Temp), clean ? 42 : 40, Ui.White);
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

            // nächste Stunden (aufgeräumt: 5 statt 6, ohne Regen-Prozente)
            int n = clean ? 5 : 6;
            var hours = new UniformGrid { Columns = n, Rows = 1 };
            foreach (var h in W.Hours.Take(n))
            {
                bool now = h.Time.Hour == DateTime.Now.Hour && h.Time.Date == DateTime.Today;
                var cell = Ui.VStack(clean ? 3 : 2,
                    Center(Ui.Text(now ? "Jetzt" : h.Time.ToString("ddd", Ui.De).TrimEnd('.') + " " + h.Time.ToString("HH") + (clean ? "" : " Uhr"), clean ? 12.5 : 11, now ? Ui.White : Ui.Dim, now)),   // mit Tagesname (So 19)
                    Center(WeatherIcon.Create(h.Code, h.Day, clean ? 22 : 20)),
                    Center(Ui.Text(WeatherService.Deg(h.Temp), clean ? 14.5 : 13, Ui.White)));
                if (!clean && h.Rain >= 30) cell.Children.Add(Center(Ui.Text(h.Rain + " %", 10, Ui.Br(Ui.Teal), false)));
                hours.Children.Add(cell);
            }
            root.Children.Add(new Border { CornerRadius = new CornerRadius(16), Background = Ui.Br(Colors.White, 16), Padding = new Thickness(4, clean ? 10 : 8, 4, clean ? 10 : 8), Margin = new Thickness(0, clean ? 14 : 10, 0, 0), Child = hours });
            if (clean) return root;   // aufgeräumt: ohne Tageszeile
            // nächste Tage mit vollem Tagesnamen (Montag, Dienstag, …)
            var days = new UniformGrid { Columns = 3, Rows = 1, Margin = new Thickness(0, 7, 0, 0) };
            foreach (var d in W.Days.Skip(1).Take(3))
            {
                var row = Ui.HStack(clean ? 4 : 5,
                    Ui.Text(d.Date.ToString("dddd", Ui.De), clean ? 13 : 12, Ui.Dim, false),
                    WeatherIcon.Create(d.Code, true, clean ? 18 : 16),
                    Ui.Text(WeatherService.Deg(d.Max) + (clean ? "/" : " / ") + WeatherService.Deg(d.Min), clean ? 13 : 12, Ui.White, clean));
                row.HorizontalAlignment = HorizontalAlignment.Center;
                days.Children.Add(row);
            }
            root.Children.Add(days);
            return root;
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

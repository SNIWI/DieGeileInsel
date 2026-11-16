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
            Ui.Row3(Ui.HStack(7, Emoji(W.HasData ? WeatherService.Emoji(W.Code, W.IsDay) : "🌡️", 14),
                              Ui.Text(W.HasData ? WeatherService.Deg(W.Temp) : "Wetter", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Emoji(W.HasData ? WeatherService.Emoji(W.Code, W.IsDay) : "🌡️", 14);

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

            // Kopf: Symbol, Temperatur, Beschreibung
            var big = Emoji(WeatherService.Emoji(W.Code, W.IsDay), 40);
            var temp = Ui.Text(WeatherService.Deg(W.Temp), 40, Ui.White);
            temp.FontWeight = FontWeights.SemiBold;
            temp.Margin = new Thickness(10, -4, 12, 0);
            var today = W.Days.FirstOrDefault();
            var info = Ui.VStack(1,
                Ui.Text(W.Place, 14, Ui.White),
                Ui.Text(WeatherService.Text(W.Code) + (today != null ? $" · H {WeatherService.Deg(today.Max)}  T {WeatherService.Deg(today.Min)}" : ""), 12, Ui.Dim, false),
                Ui.Text($"Gefühlt {WeatherService.Deg(W.Feels)} · Wind {Math.Round(W.Wind)} km/h · {W.Humidity} %", 11.5, Ui.Dim, false));
            info.VerticalAlignment = VerticalAlignment.Center;
            var head = Ui.HStack(0, big, temp, info);
            root.Children.Add(head);

            // nächste Stunden
            var hours = new UniformGrid { Columns = 6, Rows = 1, Margin = new Thickness(0, 10, 0, 0) };
            foreach (var h in W.Hours.Take(6))
            {
                var cell = Ui.VStack(2,
                    Center(Ui.Text(h.Time.Hour == DateTime.Now.Hour && h.Time.Date == DateTime.Today ? "Jetzt" : h.Time.ToString("HH") + " Uhr", 11, Ui.Dim, false)),
                    Center(Emoji(WeatherService.Emoji(h.Code, h.Day), 17)),
                    Center(Ui.Text(WeatherService.Deg(h.Temp), 13, Ui.White)));
                if (h.Rain >= 30) cell.Children.Add(Center(Ui.Text(h.Rain + " %", 10, Ui.Br(Ui.Teal), false)));
                hours.Children.Add(cell);
            }
            root.Children.Add(new Border { CornerRadius = new CornerRadius(14), Background = Ui.Br(Colors.White, 16), Padding = new Thickness(4, 8, 4, 8), Margin = new Thickness(0, 10, 0, 0), Child = hours });

            // nächste Tage
            var days = new UniformGrid { Columns = 3, Rows = 1, Margin = new Thickness(0, 8, 0, 0) };
            foreach (var d in W.Days.Skip(1).Take(3))
            {
                var row = Ui.HStack(5,
                    Ui.Text(d.Date.ToString("ddd", Ui.De), 12, Ui.Dim, false),
                    Emoji(WeatherService.Emoji(d.Code), 13),
                    Ui.Text(WeatherService.Deg(d.Max) + " / " + WeatherService.Deg(d.Min), 12, Ui.White, false));
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
        TextBlock icon, temp, desc;
        DateTime shown = DateTime.MinValue;
        public override Action Tap => () => H.ShowWeather();
        public override FrameworkElement Build()
        {
            icon = Ui.Glyph("🌡️", 26, Ui.White, Ui.EmojiFont);
            icon.VerticalAlignment = VerticalAlignment.Center;
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
            icon.Text = WeatherService.Emoji(w.Code, w.IsDay);
            temp.Text = WeatherService.Deg(w.Temp);
            desc.Text = WeatherService.Text(w.Code) + " · " + w.Place;
        }
    }
}

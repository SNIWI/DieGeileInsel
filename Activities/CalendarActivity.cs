using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicIsland
{
    /// <summary>Seite „Heute“: Wetter und die nächsten Termine von heute und morgen.</summary>
    public sealed class CalendarActivity : Activity
    {
        public const string Glyph = "";
        const int MaxRows = 3;
        readonly IslandWindow host;
        string shownKey = "";

        public CalendarActivity(IslandWindow h)
        {
            host = h;
            Id = "calendar";
            Priority = 1;
        }

        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(420, 240);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph(Glyph, 14, Ui.Br(Ui.Red)), Ui.Text("Heute", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph(Glyph, 14, Ui.Br(Ui.Red));

        string StateKey()
        {
            var list = host.Calendar.Upcoming(MaxRows);
            return string.Join(";", list.Select(e => e.Key)) + "|" + DateTime.Now.ToString("HHmm") + "|" + host.Calendar.HasCalendars
                   + "|" + host.Weather.LastUpdate.Ticks + "|" + host.Weather.Error;
        }

        /// <summary>Seite „Heute“: links das Wetter, rechts die Termine.</summary>
        public override FrameworkElement BuildExpanded()
        {
            shownKey = StateKey();
            var grid = new Grid { Margin = new Thickness(20, 14, 20, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var weather = WeatherColumn();
            var cal = CalendarColumn();
            Grid.SetColumn(cal, 2);
            grid.Children.Add(weather);
            grid.Children.Add(cal);
            return grid;
        }

        // ---------- Wetter (links) ----------
        FrameworkElement WeatherColumn()
        {
            var w = host.Weather;
            var card = new Border { CornerRadius = new CornerRadius(18), Background = Ui.Br(Colors.White, 16), Padding = new Thickness(14, 10, 14, 10), Cursor = Cursors.Hand };
            card.MouseLeftButtonUp += (s, e) => { e.Handled = true; _ = w.RefreshAsync(true); };
            card.ToolTip = "Antippen zum Aktualisieren";
            var p = new StackPanel();
            card.Child = p;
            if (!w.HasData)
            {
                p.Children.Add(Ui.Text("Wetter", 14, Ui.White));
                var msg = Ui.Text(string.IsNullOrEmpty(w.Error) ? "Wird geladen …" : w.Error, 12, Ui.Dim, false);
                msg.TextWrapping = TextWrapping.Wrap;
                msg.Margin = new Thickness(0, 6, 0, 6);
                p.Children.Add(msg);
                p.Children.Add(Ui.Link("Stadt eintragen", () => host.OpenSetup("weather"), 12.5));
                return card;
            }

            var icon = Ui.Glyph(WeatherService.Emoji(w.Code, w.IsDay), 30, Ui.White, Ui.EmojiFont);
            icon.VerticalAlignment = VerticalAlignment.Center;
            var temp = Ui.Text(WeatherService.Deg(w.Temp), 32, Ui.White);
            temp.FontWeight = FontWeights.SemiBold;
            temp.Margin = new Thickness(8, -4, 0, 0);
            p.Children.Add(Ui.HStack(0, icon, temp));
            var today = w.Days.FirstOrDefault();
            var place = Ui.Text(w.Place, 13, Ui.White, true, true);
            var desc = Ui.Text(WeatherService.Text(w.Code) + (today != null ? $" · {WeatherService.Deg(today.Max)} / {WeatherService.Deg(today.Min)}" : ""), 11.5, Ui.Dim, false);
            desc.TextTrimming = TextTrimming.CharacterEllipsis;
            p.Children.Add(place);
            p.Children.Add(desc);

            var hours = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Rows = 1, Margin = new Thickness(-4, 8, -4, 0) };
            foreach (var h in w.Hours.Take(4))
            {
                var cell = Ui.VStack(1,
                    Center(Ui.Text(h.Time.ToString("HH"), 10.5, Ui.Dim, false)),
                    Center(Ui.Glyph(WeatherService.Emoji(h.Code, h.Day), 14, Ui.White, Ui.EmojiFont)),
                    Center(Ui.Text(WeatherService.Deg(h.Temp), 11.5, Ui.White)));
                hours.Children.Add(cell);
            }
            p.Children.Add(hours);
            return card;
        }

        static FrameworkElement Center(FrameworkElement e) { e.HorizontalAlignment = HorizontalAlignment.Center; return e; }

        // ---------- Termine (rechts) ----------
        FrameworkElement CalendarColumn()
        {
            var cal = host.Calendar;
            var root = new StackPanel();

            var today = Ui.Text(DateTime.Now.ToString("dddd, d. MMMM", Ui.De), 12, Ui.Br(Ui.Red));
            var title = Ui.Text("Termine", 17, Ui.White);
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var refresh = Ui.RoundButton("", 12.5, () => { _ = cal.RefreshAsync(); }, 28, Brushes.Transparent, Ui.Dim);
            refresh.ToolTip = "Aktualisieren";
            var set = Ui.RoundButton("", 12.5, () => host.OpenSetup("calendar"), 28, Brushes.Transparent, Ui.Dim);
            set.ToolTip = "Kalender-Einstellungen";
            var btns = Ui.HStack(2, refresh, set);
            btns.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(btns, 1);
            head.Children.Add(Ui.VStack(0, today, title));
            head.Children.Add(btns);
            root.Children.Add(head);

            var list = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            if (!cal.HasCalendars)
            {
                list.Children.Add(Hint("Noch kein Kalender verknüpft."));
                var l = Ui.Link("Kalender verknüpfen", () => host.OpenSetup("calendar"), 13);
                l.Margin = new Thickness(2, 4, 0, 0);
                list.Children.Add(l);
            }
            else
            {
                var items = cal.Upcoming(MaxRows);
                if (items.Count == 0) list.Children.Add(Hint(cal.LastSync == default ? "Wird geladen …" : "Keine Termine heute und morgen. 🎉"));
                foreach (var e in items) list.Children.Add(Row(e));
            }
            root.Children.Add(list);
            return root;
        }

        static TextBlock Hint(string t)
        {
            var x = Ui.Text(t, 13, Ui.Dim, false);
            x.Margin = new Thickness(2, 6, 0, 0);
            return x;
        }

        public static string When(CalEvent e)
        {
            var now = DateTime.Now;
            if (e.Start <= now && e.End > now) return e.AllDay ? "Heute" : "Läuft · bis " + e.End.ToString("HH:mm");
            string day = e.Start.Date == DateTime.Today ? "" : e.Start.Date == DateTime.Today.AddDays(1) ? "Morgen " : e.Start.ToString("ddd ", Ui.De);
            if (e.AllDay) return (day.Length == 0 ? "Heute" : day.Trim()) + " · ganztägig";
            var mins = (e.Start - now).TotalMinutes;
            if (mins < 60) return "in " + Math.Max(1, (int)Math.Ceiling(mins)) + " Min";
            return day + e.Start.ToString("HH:mm") + "–" + e.End.ToString("HH:mm");
        }

        Border Row(CalEvent e)
        {
            bool running = e.Start <= DateTime.Now && e.End > DateTime.Now && !e.AllDay;
            var bar = new Border { Width = 3, CornerRadius = new CornerRadius(1.5), Background = Ui.Br(running ? Ui.Green : Ui.Red), Margin = new Thickness(0, 6, 10, 6) };
            var t = Ui.Text(e.Title, 13.5, Ui.White);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            string sub = When(e) + (string.IsNullOrEmpty(e.Location) || e.Link != null && e.Location.Contains("http") ? "" : " · " + e.Location.Split('\n')[0]);
            var st = Ui.Text(sub, 11.5, Ui.Dim, false);
            st.TextTrimming = TextTrimming.CharacterEllipsis;
            var texts = Ui.VStack(1, t, st);
            texts.VerticalAlignment = VerticalAlignment.Center;
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(texts, 1);
            g.Children.Add(bar);
            g.Children.Add(texts);
            if (e.Link != null)
            {
                var join = new Border
                {
                    Height = 26,
                    CornerRadius = new CornerRadius(13),
                    Background = Ui.Br(Ui.Green),
                    Padding = new Thickness(11, 0, 11, 0),
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = Ui.Text("Beitreten", 12, Brushes.Black)
                };
                ((TextBlock)join.Child).VerticalAlignment = VerticalAlignment.Center;
                var link = e.Link;
                Ui.Pressable(join, 0.94);
                join.MouseLeftButtonDown += (s, a) => a.Handled = true;
                join.MouseLeftButtonUp += (s, a) => { a.Handled = true; IslandWindow.Open(link); };
                Grid.SetColumn(join, 2);
                g.Children.Add(join);
            }
            return new Border { Height = 42, Margin = new Thickness(0, 0, 0, 2), Child = g, Background = Brushes.Transparent };
        }

        public override void Update()
        {
            if (StateKey() != shownKey) { shownKey = StateKey(); host.Rebuild(this); }
        }
    }

    /// <summary>Widget: nächster Termin.</summary>
    sealed class CalendarWidget : Widget
    {
        TextBlock title, when;
        string key = "";
        public override Action Tap => () => H.ShowCalendar();
        public override FrameworkElement Build()
        {
            title = Ui.Text("", 13, Ui.White);
            when = Ui.Text("", 11.5, Ui.Dim, false);
            title.TextTrimming = when.TextTrimming = TextTrimming.CharacterEllipsis;
            var head = Ui.HStack(5, Ui.Glyph(CalendarActivity.Glyph, 11, Ui.Br(Ui.Red)), Ui.Text("Nächster Termin", 11.5, Ui.Dim, false));
            return Ui.VStack(3, head, title, when);
        }
        public override void Update()
        {
            var e = H.Calendar.Upcoming(1).FirstOrDefault(x => !x.AllDay) ?? H.Calendar.Upcoming(1).FirstOrDefault();
            string k = e == null ? (H.Calendar.HasCalendars ? "none" : "nocal") : e.Key + DateTime.Now.ToString("HHmm");
            if (k == key) return;
            key = k;
            if (e == null)
            {
                title.Text = H.Calendar.HasCalendars ? "Keine Termine" : "Kein Kalender";
                when.Text = H.Calendar.HasCalendars ? "heute und morgen" : "Antippen zum Verknüpfen";
                return;
            }
            title.Text = e.Title;
            when.Text = CalendarActivity.When(e);
        }
    }
}

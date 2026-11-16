using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicIsland
{
    /// <summary>Ein Widget für eigene Seiten (Kachel 176 × 74, zwei nebeneinander, zwei Reihen).</summary>
    public abstract class Widget
    {
        protected IslandWindow H;
        public abstract FrameworkElement Build();
        public virtual void Update() { }
        /// <summary>Antippen der ganzen Kachel (optional).</summary>
        public virtual Action Tap => null;
        public Widget Init(IslandWindow h) { H = h; return this; }

        protected static TextBlock Title(string t) => Ui.Text(t, 11.5, Ui.Dim, false);
        protected static TextBlock Big(string t, double size = 22) => Ui.Text(t, size, Ui.White, true, true);
    }

    public sealed class WidgetInfo
    {
        public string Key, Name, Glyph;
        public Func<Widget> Create;
    }

    /// <summary>Alle Widgets, mit denen du Seiten bestücken kannst.</summary>
    public static class Widgets
    {
        public const int MaxPerPage = 4;

        public static readonly WidgetInfo[] All =
        {
            new WidgetInfo { Key = "clock",     Name = "Uhr",            Glyph = "\uE823", Create = () => new ClockWidget() },
            new WidgetInfo { Key = "battery",   Name = "Akku",           Glyph = "\uE83F", Create = () => new BatteryWidget() },
            new WidgetInfo { Key = "media",     Name = "Musik",          Glyph = "\uE8D6", Create = () => new MediaWidget() },
            new WidgetInfo { Key = "volume",    Name = "Lautstärke",     Glyph = "\uE767", Create = () => new VolumeWidget() },
            new WidgetInfo { Key = "cpu",       Name = "Leistung",       Glyph = "\uE9D9", Create = () => new CpuWidget() },
            new WidgetInfo { Key = "disk",      Name = "Speicher",       Glyph = "\uEDA2", Create = () => new DiskWidget() },
            new WidgetInfo { Key = "airpods",   Name = "AirPods",        Glyph = "\uE7F6", Create = () => new AirPodsWidget() },
            new WidgetInfo { Key = "discord",   Name = "Discord",        Glyph = "\uE8BD", Create = () => new DiscordWidget() },
            new WidgetInfo { Key = "clipboard", Name = "Zwischenablage", Glyph = "\uE77F", Create = () => new ClipboardWidget() },
            new WidgetInfo { Key = "shortcuts", Name = "Kurzbefehle",    Glyph = "\uE8A9", Create = () => new ShortcutsWidget() },
            new WidgetInfo { Key = "timer",     Name = "Timer",          Glyph = "\uE916", Create = () => new TimerWidget() },
            new WidgetInfo { Key = "todo",      Name = "To Do",          Glyph = "\uE73A", Create = () => new TodoWidget() },
            new WidgetInfo { Key = "calendar",  Name = "Kalender",       Glyph = "\uE787", Create = () => new CalendarWidget() },
            new WidgetInfo { Key = "downloads", Name = "Downloads",      Glyph = "\uE896", Create = () => new DownloadsWidget() },
            new WidgetInfo { Key = "weather",   Name = "Wetter",         Glyph = WeatherActivity.Glyph, Create = () => new WeatherWidget() },
            new WidgetInfo { Key = "cat",       Name = "Katze",         Glyph = "\U0001F431", Create = () => new CatWidget() },
        };

        public static WidgetInfo Get(string key) => All.FirstOrDefault(w => w.Key == key);
    }

    /// <summary>Eine eigene Seite: Name oben, darunter bis zu 4 Widgets im 2×2-Raster.</summary>
    public sealed class WidgetPageActivity : Activity
    {
        public const double CellW = 184, CellH = 80;
        readonly IslandWindow host;
        public readonly string PageId;
        readonly List<Widget> live = new List<Widget>();

        public WidgetPageActivity(IslandWindow h, string pageId)
        {
            host = h;
            PageId = pageId;
            Id = "w:" + pageId;
            Priority = 1;
        }

        WidgetPageConfig Cfg => host.Settings.WidgetPages?.FirstOrDefault(p => p.Id == PageId);

        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(420, 240);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph("\uF0E2", 14, Ui.AccentBrush), Ui.Text(Cfg?.Name ?? "Widgets", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph("\uF0E2", 14, Ui.AccentBrush);

        public override FrameworkElement BuildExpanded()
        {
            live.Clear();
            var cfg = Cfg;
            var root = new StackPanel { Margin = new Thickness(22, 12, 22, 6) };
            root.Children.Add(Ui.Row3(Ui.Text(cfg?.Name ?? "Widgets", 14, Ui.White), null,
                Ui.Link("Bearbeiten", () => host.OpenSetup("pages"), 12.5), new Thickness(2, 0, 2, 8)));

            var keys = cfg?.Widgets ?? new List<string>();
            if (keys.Count == 0)
            {
                var t = Ui.Text("Noch keine Widgets auf dieser Seite.", 13, Ui.Dim, false);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.Margin = new Thickness(0, 40, 0, 6);
                root.Children.Add(t);
                var l = Ui.Link("Widgets hinzufügen", () => host.OpenSetup("pages"), 13);
                l.HorizontalAlignment = HorizontalAlignment.Center;
                root.Children.Add(l);
                return root;
            }

            var grid = new WrapPanel { Width = CellW * 2 + 8, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var key in keys.Take(Widgets.MaxPerPage))
            {
                var info = Widgets.Get(key);
                if (info == null) continue;
                Widget w;
                FrameworkElement content;
                try
                {
                    w = info.Create().Init(host);
                    content = w.Build();
                }
                catch (Exception ex) { Program.Log(ex); continue; }
                live.Add(w);
                var card = new Border
                {
                    Width = CellW,
                    Height = CellH,
                    CornerRadius = new CornerRadius(22),
                    Background = Ui.Card,
                    Padding = new Thickness(14, 12, 14, 12),
                    Margin = new Thickness(0, 0, live.Count % 2 == 1 ? 8 : 0, 8),
                    Child = content
                };
                var tap = w.Tap;
                if (tap != null)
                {
                    card.Cursor = Cursors.Hand;
                    card.MouseEnter += (s, e) => card.Background = Ui.CardHover;
                    card.MouseLeave += (s, e) => card.Background = Ui.Card;
                    Ui.Pressable(card, 0.97);
                    card.MouseLeftButtonDown += (s, e) => e.Handled = true;
                    card.MouseLeftButtonUp += (s, e) => { e.Handled = true; try { tap(); } catch (Exception ex) { Program.Log(ex); } };
                }
                grid.Children.Add(card);
            }
            root.Children.Add(grid);
            Update();
            return root;
        }

        public override void Update()
        {
            foreach (var w in live)
            {
                try { w.Update(); } catch (Exception ex) { Program.Log(ex); }
            }
        }
    }

    // =====================================================================
    //  Die Widgets
    // =====================================================================

    sealed class ClockWidget : Widget
    {
        TextBlock time, date;
        public override FrameworkElement Build()
        {
            time = Big("", ClockFormat.Size(H.Settings, 28));
            time.FontWeight = ClockFormat.Weight(H.Settings);
            date = Title("");
            return Ui.VStack(0, time, date);
        }
        public override void Update()
        {
            var n = DateTime.Now;
            time.Text = ClockFormat.Time(H.Settings, n);
            date.Text = ClockFormat.Date(H.Settings, n);
        }
    }

    sealed class BatteryWidget : Widget
    {
        TextBlock val, sub;
        Bar bar;
        public override FrameworkElement Build()
        {
            val = Big("");
            sub = Title("");
            bar = new Bar(5, Ui.Br(Ui.Green)) { Margin = new Thickness(0, 6, 0, 0) };
            return Ui.VStack(0, Ui.Row3(val, null, sub, new Thickness(0)), bar);
        }
        public override void Update()
        {
            if (!H.HasBattery || H.BatteryPct < 0)
            {
                val.Text = "Netz";
                sub.Text = "kein Akku";
                bar.Value = 1;
                return;
            }
            int p = H.BatteryPct;
            val.Text = p + " %";
            sub.Text = H.OnAc ? "lädt ⚡" : "Akku";
            bar.Value = p / 100.0;
            bar.Fill = Ui.Br(H.OnAc ? Ui.Green : p <= 20 ? Ui.Red : Ui.WhiteC);
        }
    }

    sealed class MediaWidget : Widget
    {
        TextBlock title, artist, play;
        public override Action Tap => () => H.ShowPlayer();
        public override FrameworkElement Build()
        {
            title = Ui.Text("", 13, Ui.White);
            artist = Ui.Text("", 11.5, Ui.Dim, false);
            title.TextTrimming = artist.TextTrimming = TextTrimming.CharacterEllipsis;
            var texts = Ui.VStack(1, Title("Musik"), title, artist);
            var btn = Ui.RoundButton("\uE768", 15, H.Media.PlayPause, 34, Ui.Faint);
            play = (TextBlock)btn.Child;
            btn.VerticalAlignment = VerticalAlignment.Center;
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(btn, 1);
            g.Children.Add(texts);
            g.Children.Add(btn);
            return g;
        }
        public override void Update()
        {
            var m = H.Media;
            bool any = !string.IsNullOrEmpty(m.Title);
            title.Text = any ? m.Title : "Nichts läuft";
            artist.Text = any ? m.Artist : "";
            play.Text = m.Playing ? "\uE769" : "\uE768";
        }
    }

    sealed class VolumeWidget : Widget
    {
        TextBlock val;
        DragBar bar;
        public override FrameworkElement Build()
        {
            val = Big("", 18);
            bar = new DragBar(Ui.AccentBrush, 5) { WheelStep = 0.04, Margin = new Thickness(0, 4, 0, 0) };
            bar.Current = () => H.Audio.Volume ?? 0;
            bar.Changed = f => { H.Audio.SetVolume((float)f); val.Text = Math.Round(f * 100) + " %"; };
            return Ui.VStack(0, Ui.Row3(Title("Lautstärke"), null, val, new Thickness(0)), bar);
        }
        public override void Update()
        {
            if (bar.Dragging) return;
            double v = H.Audio.Volume ?? 0;
            bool muted = H.Audio.Muted == true;
            bar.Bar.Value = muted ? 0 : v;
            val.Text = muted ? "aus" : Math.Round(v * 100) + " %";
        }
    }

    sealed class CpuWidget : Widget
    {
        Bar cpu, ram;
        TextBlock cpuT, ramT;
        public override FrameworkElement Build()
        {
            FrameworkElement Row(string label, out Bar b, out TextBlock t, Color c)
            {
                var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                var l = Title(label);
                b = new Bar(5, Ui.Br(c)) { Margin = new Thickness(0, 0, 8, 0) };
                t = Ui.Text("", 12, Ui.White, true, true);
                t.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(b, 1); Grid.SetColumn(t, 2);
                g.Children.Add(l); g.Children.Add(b); g.Children.Add(t);
                return g;
            }
            return Ui.VStack(2, Row("CPU", out cpu, out cpuT, Ui.Accent), Row("RAM", out ram, out ramT, Ui.Purple));
        }
        public override void Update()
        {
            var s = H.Stats;
            cpu.Value = s.Cpu;
            ram.Value = s.Ram;
            cpuT.Text = Math.Round(s.Cpu * 100) + " %";
            ramT.Text = Math.Round(s.Ram * 100) + " %";
        }
    }

    sealed class DiskWidget : Widget
    {
        TextBlock val;
        Bar bar;
        DateTime next;
        public override Action Tap => () => IslandWindow.Open("ms-settings:storagesense");
        public override FrameworkElement Build()
        {
            val = Big("", 18);
            bar = new Bar(5, Ui.AccentBrush) { Margin = new Thickness(0, 6, 0, 0) };
            return Ui.VStack(0, Ui.Row3(Title("Speicher C:"), null, val, new Thickness(0)), bar);
        }
        public override void Update()
        {
            if (DateTime.Now < next) return;
            next = DateTime.Now.AddSeconds(30);
            try
            {
                var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                var d = new DriveInfo(root);
                double free = d.AvailableFreeSpace / 1073741824.0;
                val.Text = Math.Round(free) + " GB frei";
                bar.Value = 1 - (double)d.AvailableFreeSpace / d.TotalSize;
            }
            catch { val.Text = "–"; }
        }
    }

    sealed class AirPodsWidget : Widget
    {
        TextBlock name, val;
        public override Action Tap => () => H.ShowAirPods();
        public override FrameworkElement Build()
        {
            name = Title("AirPods");
            val = Big("", 16);
            val.TextTrimming = TextTrimming.CharacterEllipsis;
            return Ui.VStack(4, name, val);
        }
        public override void Update()
        {
            var st = H.AirPods?.Current;
            if (st == null)
            {
                name.Text = "AirPods";
                val.Text = H.AirPods?.EndpointName != null ? "Verbunden" : "Nicht in Reichweite";
                return;
            }
            string P(int? v) => v.HasValue ? v.Value + "%" : "–";
            name.Text = st.Model;
            val.Text = st.IsHeadphones ? P(st.Left.Battery ?? st.Right.Battery)
                : $"L {P(st.Left.Battery)}  R {P(st.Right.Battery)}" + (st.CaseBattery.HasValue ? $"  C {P(st.CaseBattery)}" : "");
        }
    }

    sealed class DiscordWidget : Widget
    {
        TextBlock val;
        Border mute;
        public override FrameworkElement Build()
        {
            val = Ui.Text("", 13, Ui.White);
            val.TextTrimming = TextTrimming.CharacterEllipsis;
            mute = Ui.RoundButton("\uE720", 14, () => H.Discord.ToggleMute(), 34, Ui.Faint);
            mute.ToolTip = "Stumm";
            mute.VerticalAlignment = VerticalAlignment.Center;
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var texts = Ui.VStack(3, Title("Discord"), val);
            Grid.SetColumn(mute, 1);
            g.Children.Add(texts);
            g.Children.Add(mute);
            return g;
        }
        public override void Update()
        {
            var d = H.Discord;
            if (!d.Connected) { val.Text = "Nicht verbunden"; mute.Visibility = Visibility.Collapsed; return; }
            mute.Visibility = Visibility.Visible;
            val.Text = d.Channel != null ? d.Channel.Name : "Kein Sprachkanal";
            mute.Background = d.MicOff ? Ui.Br(Ui.Red) : Ui.GlassFill;
        }
    }

    sealed class ClipboardWidget : Widget
    {
        TextBlock val;
        public override Action Tap => () =>
        {
            var item = H.ClipHistory.FirstOrDefault();
            if (item != null) H.UseClip(item, true);
            else H.ShowClipboardHistory();
        };
        public override FrameworkElement Build()
        {
            val = Ui.Text("", 12.5, Ui.White, false);
            val.TextWrapping = TextWrapping.Wrap;
            val.TextTrimming = TextTrimming.CharacterEllipsis;
            val.MaxHeight = 36;
            return Ui.VStack(3, Title("Zuletzt kopiert · antippen = einfügen"), val);
        }
        public override void Update()
        {
            var item = H.ClipHistory.FirstOrDefault();
            val.Text = item == null ? "Noch nichts kopiert" : item.Preview;
        }
    }

    sealed class ShortcutsWidget : Widget
    {
        public override FrameworkElement Build()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            foreach (var sc in (H.Settings.Shortcuts ?? new List<ShortcutConfig>()).Take(4))
            {
                var s = sc;
                var icon = Shortcuts.Icon(s, 34);
                var b = new Border { Child = icon, Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, ToolTip = s.Name, Background = Brushes.Transparent };
                b.MouseLeftButtonDown += (o, e) => e.Handled = true;
                b.MouseLeftButtonUp += (o, e) => { e.Handled = true; Shortcuts.Launch(s.Target); };
                row.Children.Add(b);
            }
            if (row.Children.Count == 0) row.Children.Add(Ui.Text("Keine Kurzbefehle", 12.5, Ui.Dim, false));
            return Ui.VStack(0, Title("Kurzbefehle"), row);
        }
    }

    sealed class TimerWidget : Widget
    {
        public override FrameworkElement Build()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            foreach (var min in new[] { 5, 10, 25 })
            {
                int m = min;
                var b = Ui.RoundButton("", 1, () => H.StartTimer(TimeSpan.FromMinutes(m)), 34, Ui.Br(Ui.Orange, 60));
                b.Width = 44;
                b.CornerRadius = new CornerRadius(17);
                b.Child = Ui.Text(m + "′", 13, Ui.White, true, true);
                ((TextBlock)b.Child).HorizontalAlignment = HorizontalAlignment.Center;
                ((TextBlock)b.Child).VerticalAlignment = VerticalAlignment.Center;
                b.Margin = new Thickness(0, 0, 8, 0);
                row.Children.Add(b);
            }
            return Ui.VStack(0, Title("Timer starten"), row);
        }
    }

    sealed class DownloadsWidget : Widget
    {
        TextBlock name, when;
        DateTime next;
        public override Action Tap => () => H.ShowDownloads();
        public override FrameworkElement Build()
        {
            name = Ui.Text("", 13, Ui.White);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            when = Title("");
            return Ui.VStack(3, Title("Letzter Download"), name, when);
        }
        public override void Update()
        {
            if (DateTime.Now < next) return;
            next = DateTime.Now.AddSeconds(5);
            var f = H.Downloads.Recent(1).FirstOrDefault();
            name.Text = f?.Name ?? "Noch nichts heruntergeladen";
            when.Text = f == null ? "" : f.LastWriteTime.ToString("d. MMM, HH:mm", Ui.De);
        }
    }

    sealed class CatWidget : Widget
    {
        CatView view;
        public override Action Tap => () => view?.Purr();
        public override FrameworkElement Build()
        {
            var cfg = H.Settings.Cats?.FirstOrDefault();
            if (cfg == null) return Ui.VStack(3, Title("Katze"), Ui.Link("Katze aussuchen", () => H.OpenSetup("cats"), 12.5));
            view = new CatView(cfg, 48) { IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
            var texts = Ui.VStack(2, Ui.Text(cfg.Name, 14, Ui.White), Title("antippen zum Streicheln"));
            texts.Margin = new Thickness(10, 0, 0, 0);
            texts.VerticalAlignment = VerticalAlignment.Center;
            return Ui.HStack(0, view, texts);
        }
        public override void Update() => view?.Tick(DateTime.Now);
    }
}

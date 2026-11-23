using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicIsland
{
    /// <summary>Ein Kurzbefehl: Programm, Ordner, Datei, Webseite oder Windows-Link (ms-settings: …).</summary>
    public sealed class ShortcutConfig
    {
        public string Name { get; set; } = "";
        public string Target { get; set; } = "";
        public string Glyph { get; set; } = "";
    }

    public static class Shortcuts
    {
        public const int MaxShown = 10;

        public static List<ShortcutConfig> Defaults() => new List<ShortcutConfig>
        {
            new ShortcutConfig { Name = "Explorer", Target = "explorer.exe" },
            new ShortcutConfig { Name = "Downloads", Target = "shell:Downloads", Glyph = "" },
            new ShortcutConfig { Name = "Screenshot", Target = "ms-screenclip:", Glyph = "" },
            new ShortcutConfig { Name = "Rechner", Target = "calc.exe" },
            new ShortcutConfig { Name = "Task-Manager", Target = "taskmgr.exe" },
            new ShortcutConfig { Name = "Einstellungen", Target = "ms-settings:", Glyph = "" },
        };

        /// <summary>Vollständiger Pfad, falls das Ziel eine Datei / ein Ordner ist, sonst null.</summary>
        public static string Resolve(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return null;
            var t = Environment.ExpandEnvironmentVariables(target.Trim().Trim('"'));
            try
            {
                if (File.Exists(t) || Directory.Exists(t)) return t;
                if (t.Contains(":") || t.Contains("\\") || t.Contains("/")) return null; // URL, Protokoll, fehlender Pfad
                foreach (var dir in new[] { Environment.SystemDirectory, Environment.GetFolderPath(Environment.SpecialFolder.Windows) })
                {
                    var p = Path.Combine(dir, t);
                    if (File.Exists(p)) return p;
                }
            }
            catch { }
            return null;
        }

        public static void Launch(string target)
        {
            var t = Environment.ExpandEnvironmentVariables((target ?? "").Trim().Trim('"'));
            if (t.Length == 0) throw new ArgumentException("Kein Ziel");
            Process.Start(new ProcessStartInfo(t) { UseShellExecute = true });
        }

        static readonly Color[] TileColors = { Ui.Blue, Ui.Orange, Ui.Green, Ui.Purple, Ui.Teal, Ui.Red, Ui.Pink };

        /// <summary>App-Symbol im Apple-Stil: abgerundetes Quadrat mit Programm-Icon oder Symbol.</summary>
        public static FrameworkElement Icon(ShortcutConfig s, double size)
        {
            var tile = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size * 0.225),
                Background = Ui.Br(Color.FromRgb(0x3A, 0x3A, 0x3C))
            };
            var path = Resolve(s.Target);
            var img = path != null ? Native.FileIcon(path) : null;
            if (img != null)
            {
                var image = new Image { Source = img, Width = size * 0.66, Height = size * 0.66, Stretch = Stretch.Uniform };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                tile.Child = image;
            }
            else
            {
                string g = !string.IsNullOrEmpty(s.Glyph) ? s.Glyph
                    : (s.Target ?? "").StartsWith("http", StringComparison.OrdinalIgnoreCase) ? ""
                    : "";
                int h = 0;
                foreach (var ch in s.Name ?? "") h = h * 31 + ch;
                tile.Background = Ui.Br(TileColors[Math.Abs(h % TileColors.Length)]);
                tile.Child = Ui.Glyph(g, size * 0.44, Ui.White);
            }
            return tile;
        }
    }

    /// <summary>Raster mit deinen Kurzbefehlen (Island antippen → Kurzbefehle oder Strg + Alt + K).</summary>
    public sealed class ShortcutsActivity : Activity
    {
        readonly IslandWindow host;

        public ShortcutsActivity(IslandWindow h)
        {
            host = h;
            Id = "shortcuts";
            Priority = 1;
        }

        List<ShortcutConfig> Items => host.Settings.Shortcuts ?? new List<ShortcutConfig>();
        int Rows => Math.Max(1, (Math.Min(Items.Count, Shortcuts.MaxShown) + 4) / 5);

        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(440, 74 + Rows * 78);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph("", 14, Ui.AccentBrush), Ui.Text("Kurzbefehle", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph("", 14, Ui.AccentBrush);

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = Ui.PageMargin };
            root.Children.Add(Ui.PageHead("Kurzbefehle", null, ("\uE70F", "Bearbeiten", () => host.OpenSetup())));

            if (Items.Count == 0)
            {
                var empty = Ui.Text("Noch keine Kurzbefehle. Tippe auf „Bearbeiten“.", 12.5, Ui.Dim, false);
                empty.Margin = new Thickness(0, 14, 0, 0);
                root.Children.Add(empty);
                return root;
            }

            var wp = new WrapPanel { Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var s in Items.Take(Shortcuts.MaxShown))
            {
                var sc = s;
                var cell = new StackPanel { Width = 80, Cursor = Cursors.Hand, Background = Brushes.Transparent };
                var icon = Shortcuts.Icon(sc, 48);
                icon.HorizontalAlignment = HorizontalAlignment.Center;
                cell.Children.Add(icon);
                var t = Ui.Text(sc.Name, 11, Ui.Soft, false);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.Margin = new Thickness(2, 5, 2, 0);
                t.MaxWidth = 76;
                cell.Children.Add(t);
                cell.ToolTip = sc.Target;
                cell.MouseEnter += (o, e) => cell.Opacity = 0.75;
                cell.MouseLeave += (o, e) => cell.Opacity = 1;
                Ui.Pressable(cell, 0.92);
                cell.MouseLeftButtonDown += (o, e) => e.Handled = true;
                cell.MouseLeftButtonUp += (o, e) => { e.Handled = true; host.RunShortcut(sc); };
                wp.Children.Add(cell);
            }
            root.Children.Add(wp);
            return root;
        }
    }

    /// <summary>Die neuesten Downloads mit Symbol, Größe und Alter. Antippen öffnet die Datei.</summary>
    public sealed class DownloadsActivity : Activity
    {
        readonly IslandWindow host;
        List<FileInfo> files = new List<FileInfo>();

        public DownloadsActivity(IslandWindow h)
        {
            host = h;
            Id = "downloads";
            Priority = 1;
        }

        public void Reload() => files = host.Downloads.Recent(5);

        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(430, 70 + Math.Max(1, files.Count) * 46);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph("", 14, Ui.AccentBrush), Ui.Text("Downloads", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph("", 14, Ui.AccentBrush);

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = Ui.PageMargin };
            root.Children.Add(Ui.PageHead("Downloads", files.Count > 0 ? "Neueste Dateien" : null, ("\uE838", "Ordner öffnen", host.OpenDownloadsFolder)));

            var list = new StackPanel();
            if (files.Count == 0)
            {
                var empty = Ui.Text("Noch keine Dateien im Downloads-Ordner", 12.5, Ui.Dim, false);
                empty.Margin = new Thickness(0, 8, 0, 0);
                list.Children.Add(empty);
            }
            foreach (var f in files)
            {
                var fi = f;
                var row = new Border { Height = 46, CornerRadius = new CornerRadius(10), Background = Brushes.Transparent, Cursor = Cursors.Hand, Padding = new Thickness(6, 0, 4, 0) };
                var img = Native.FileIcon(fi.FullName);
                FrameworkElement icon = img != null
                    ? new Image { Source = img, Width = 28, Height = 28, VerticalAlignment = VerticalAlignment.Center }
                    : (FrameworkElement)Ui.Glyph("", 20, Ui.Dim);
                var texts = Ui.VStack(1,
                    Ui.Text(fi.Name, 13, Ui.White, false),
                    Ui.Text(DownloadWatcher.FormatSize(fi.Length) + " · " + DownloadWatcher.Ago(DownloadWatcher.Latest(fi)), 11.5, Ui.Dim, false));
                texts.Margin = new Thickness(10, 0, 8, 0);
                var folder = Ui.RoundButton("", 13, () => host.ShowInFolder(fi.FullName), 32);
                folder.ToolTip = "Im Ordner zeigen";
                row.Child = Ui.Row3(icon, texts, folder, new Thickness(0));
                row.MouseEnter += (o, e) => row.Background = Ui.Br(Colors.White, 22);
                row.MouseLeave += (o, e) => row.Background = Brushes.Transparent;
                row.MouseLeftButtonDown += (o, e) => e.Handled = true;
                Ui.Pressable(row, 0.98);
                row.MouseLeftButtonUp += (o, e) => { e.Handled = true; host.OpenFile(fi.FullName); };
                list.Children.Add(row);
            }
            root.Children.Add(files.Count > 0 ? Ui.Group(list, new Thickness(4)) : (UIElement)list);   // Liste als Karte wie in iOS
            return root;
        }
    }

    /// <summary>
    /// Ton-Panel: Lautstärke, Ausgabegerät wechseln (Lautsprecher ↔ Kopfhörer), Mikrofon
    /// stummschalten, Mikrofon-Lautstärke mit Live-Pegel, Lautstärkemixer.
    /// </summary>
    public sealed class SoundActivity : Activity
    {
        const int MaxDevices = 3;
        readonly IslandWindow host;
        readonly AudioService audio;
        List<AudioDevice> outs = new List<AudioDevice>();
        DragBar vol, micVol;
        Bar micLevel;
        TextBlock volT, volIcon, micT, micIcon;
        Border micBtn;

        public SoundActivity(IslandWindow h, AudioService a)
        {
            host = h;
            audio = a;
            Id = "sound";
            Priority = 1;
        }

        public void Reload() => outs = audio.OutputDevices();

        int Rows => Math.Max(1, Math.Min(MaxDevices, outs.Count));
        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(420, 160); // nur Lautstärke + Mikrofon (Ausgabegerät entfernt)

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph("", 14, Ui.AccentBrush), Ui.Text("Ton", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph("", 14, Ui.AccentBrush);

        static TextBlock Section(string text)
        {
            var t = Ui.Text(text.ToUpper(Ui.De), 11, Ui.Dim, false);
            t.Margin = new Thickness(2, 14, 0, 6);
            return t;
        }

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = Ui.PageMargin };
            root.Children.Add(Ui.PageHead("Ton", null, ("\uE9E9", "Lautstärkemixer", () => IslandWindow.Open("ms-settings:apps-volume")), ("\uE713", "Sound-Einstellungen", () => IslandWindow.Open("ms-settings:sound"))));

            // Lautstärke
            var vg = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            vg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            vg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            vg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            var mute = Ui.RoundButton("", 15, audio.ToggleMute, 34);
            mute.ToolTip = "Stumm";
            volIcon = (TextBlock)mute.Child;
            vol = new DragBar(Ui.AccentBrush, 5) { Margin = new Thickness(10, 0, 10, 0), WheelStep = 0.04 };
            vol.Current = () => audio.Volume ?? 0;
            vol.Changed = f => { audio.SetVolume((float)f); volT.Text = Math.Round(f * 100).ToString(); };
            volT = Ui.Text("", 12.5, Ui.White, true, true);
            volT.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(mute, 0); Grid.SetColumn(vol, 1); Grid.SetColumn(volT, 2);
            vg.Children.Add(mute); vg.Children.Add(vol); vg.Children.Add(volT);
            root.Children.Add(vg);

            // Mikrofon
            var mg = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            mg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            micBtn = Ui.RoundButton("", 15, audio.ToggleMicMute, 34);
            micBtn.ToolTip = "Mikrofon stumm";
            micIcon = (TextBlock)micBtn.Child;
            var micStack = new StackPanel { Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            micVol = new DragBar(Ui.AccentBrush, 3, slim: true) { WheelStep = 0.04 };
            micVol.Current = () => audio.MicVolume ?? 0;
            micVol.Changed = f => { audio.SetMicVolume((float)f); micT.Text = Math.Round(f * 100).ToString(); };
            micLevel = new Bar(2, Ui.Br(Ui.Green)) { Margin = new Thickness(0, 1, 0, 0) };
            micStack.Children.Add(micVol);
            micStack.Children.Add(micLevel);
            micT = Ui.Text("", 12.5, Ui.White, true, true);
            micT.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(micBtn, 0); Grid.SetColumn(micStack, 1); Grid.SetColumn(micT, 2);
            mg.Children.Add(micBtn); mg.Children.Add(micStack); mg.Children.Add(micT);
            root.Children.Add(mg);



            Update();
            return root;
        }

        public override void Update()
        {
            if (vol == null) return;
            double v = audio.Volume ?? 0;
            bool muted = audio.Muted == true;
            if (!vol.Dragging)
            {
                vol.Bar.Value = muted ? 0 : v;
                volT.Text = muted ? "aus" : Math.Round(v * 100).ToString();
            }
            volIcon.Text = muted || v < 0.005 ? "" : v < 0.34 ? "" : v < 0.67 ? "" : "";
            volIcon.Foreground = muted ? Ui.Br(Ui.Red) : Ui.White;

            bool micMuted = audio.MicMuted == true;
            double mv = audio.MicVolume ?? 0;
            if (!micVol.Dragging)
            {
                micVol.Bar.Value = mv;
                micT.Text = audio.MicVolume.HasValue ? Math.Round(mv * 100).ToString() : "–";
            }
            micLevel.Value = micMuted ? 0 : Math.Min(1, Math.Sqrt(audio.MicPeak));
            micBtn.Background = micMuted ? Ui.Br(Ui.Red) : Ui.GlassFill;
            micIcon.Foreground = Ui.White;
        }
    }

    /// <summary>Eine Kachel der Übersicht.</summary>
    public sealed class DashTile
    {
        public string Key, Label, Glyph;
        public Color Tint = Colors.White;
        public bool Emoji;
        public Action<IslandWindow> Run;
    }

    /// <summary>Alle Kacheln, die man in der Übersicht anzeigen kann (Auswahl + Reihenfolge in den Einstellungen).</summary>
    public static class DashTiles
    {
        public const int Max = 4; // eine Reihe in der Übersicht (+ „Mehr“)

        /// <summary>Letzte Kachel der Reihe: alle Funktionen als Menü.</summary>
        public static readonly DashTile More = new DashTile { Key = "more", Label = "Mehr", Glyph = "\uE712", Tint = Ui.WhiteC, Run = h => h.ShowAllFunctions() };

        public static readonly DashTile[] All =
        {
            new DashTile { Key = "shortcuts",  Label = "Kurzbefehle",   Glyph = "", Tint = Ui.Blue,   Run = h => h.ShowShortcuts() },
            new DashTile { Key = "downloads",  Label = "Downloads",     Glyph = "", Tint = Ui.Teal,   Run = h => h.ShowDownloads() },
            new DashTile { Key = "sound",      Label = "Ton",           Glyph = "", Tint = Ui.Pink,   Run = h => h.ShowSound() },
            new DashTile { Key = "timer",      Label = "Timer",         Glyph = "", Tint = Ui.Orange, Run = h => h.ShowTimerMenu() },
            new DashTile { Key = "stopwatch",  Label = "Stoppuhr",      Glyph = "", Tint = Ui.Orange, Run = h => h.ToggleStopwatch() },
            new DashTile { Key = "memo",       Label = "Memo",          Glyph = "", Tint = Ui.Red,    Run = h => h.ToggleMemo() },
            new DashTile { Key = "pomodoro",   Label = "Pomodoro",      Glyph = "\uE916", Tint = Ui.Red,    Run = h => h.StartPomodoro(true, 1) },
            new DashTile { Key = "screenshot", Label = "Screenshot",    Glyph = "", Tint = Ui.Teal,   Run = h => IslandWindow.Open("ms-screenclip:") },
            new DashTile { Key = "player",     Label = "Player",        Glyph = "\uE8D6", Tint = Ui.Pink,   Run = h => h.ShowPlayer() },
            new DashTile { Key = "apps",       Label = "Apps",          Glyph = "\uE7C4", Tint = Ui.Blue,   Run = h => h.ShowApps() },
            new DashTile { Key = "clipboard",  Label = "Zwischenablage", Glyph = "\uE77F", Tint = Ui.Teal,  Run = h => h.ShowClipboardHistory() },
            new DashTile { Key = "brightness", Label = "Helligkeit",    Glyph = "\uE706", Tint = Ui.Yellow, Run = h => h.ShowBrightness() },
            new DashTile { Key = "airpods",    Label = "Bluetooth",     Glyph = "\uE702", Tint = Ui.Blue,   Run = h => h.ShowBluetooth() },
            new DashTile { Key = "weather",    Label = "Wetter",        Glyph = WeatherActivity.Glyph, Tint = Ui.Teal, Run = h => h.ShowWeather() },
            new DashTile { Key = "todo",       Label = "To Do",       Glyph = "\uE73A", Tint = Ui.Blue,   Run = h => h.ShowTodo() },
            new DashTile { Key = "calendar",   Label = "Kalender",      Glyph = "\uE787", Tint = Ui.Red,    Run = h => h.ShowCalendar() },
            new DashTile { Key = "ai",         Label = "KI-Chat",        Glyph = "\uE8BD", Tint = Ui.Purple, Run = h => h.ShowAi(true) },
            new DashTile { Key = "logitech", Label = "Maus & Tastatur", Glyph = "", Tint = Ui.Teal, Run = h => h.ShowLogitech() },
            new DashTile { Key = "claudevoice", Label = "Claude diktieren", Glyph = "", Tint = ClaudeTracker.Orange, Run = h => h.ClaudeVoice() },
            new DashTile { Key = "claudelimits", Label = "Claude-Limits", Glyph = "", Tint = ClaudeTracker.Orange, Run = h => h.ShowClaudeLimits() },
            new DashTile { Key = "ocr",        Label = "Text lesen",    Glyph = "\uE722", Tint = Ui.Purple, Run = h => h.StartOcr() },
            new DashTile { Key = "lock",       Label = "Sperren",       Glyph = "", Tint = Ui.WhiteC, Run = h => h.LockPc() },
            new DashTile { Key = "cats",       Label = "Katzen",        Glyph = "\U0001F431", Emoji = true, Run = h => h.OpenSetup("cats") },
            new DashTile { Key = "demos",      Label = "Demos",         Glyph = "", Tint = Ui.Purple, Run = h => h.ShowDemoMenu() },
            new DashTile { Key = "settings",   Label = "Einstellungen", Glyph = "", Tint = Ui.Gray,   Run = h => h.OpenSetup() },
            new DashTile { Key = "help",       Label = "Hilfe",         Glyph = "", Tint = Ui.Blue,   Run = h => h.ShowHelp() },
            new DashTile { Key = "quit",       Label = "Beenden",       Glyph = "", Tint = Ui.Red,    Run = h => h.Quit() },
        };

        public static List<string> DefaultKeys() =>
            new List<string> { "player", "shortcuts", "apps", "clipboard", "downloads", "sound", "brightness", "airpods", "timer", "lock", "settings", "quit" };

        public static DashTile Get(string key) => All.FirstOrDefault(t => t.Key == key);

        public static List<DashTile> Selected(Settings s) =>
            (s.DashboardTiles ?? DefaultKeys()).Select(Get).Where(t => t != null).Distinct().Take(Max).ToList();

        /// <summary>Abgerundetes Quadrat mit farbigem Symbol (wie im Kontrollzentrum).</summary>
        public static Border Icon(DashTile t, double size, Brush bg = null) => new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.29),
            Background = bg ?? Ui.Faint,
            Child = t.Emoji ? EmojiGlyph(t.Glyph, size * 0.42) : Ui.Glyph(t.Glyph, size * 0.38, Ui.Br(t.Tint))
        };

        // Emoji sitzen in ihrer Schrift tiefer als Symbole – feste Zeilenhöhe, damit sie mittig stehen
        static TextBlock EmojiGlyph(string g, double size)
        {
            var t = Ui.Glyph(g, size, Ui.White, Ui.EmojiFont);
            t.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            t.LineHeight = size * 1.05;
            t.Margin = new Thickness(0, -size * 0.06, 0, 0);
            return t;
        }
    }

    /// <summary>Zwischenablage-Verlauf: Antippen fügt den Eintrag direkt in die aktive App ein.</summary>
    public sealed class ClipboardActivity : Activity
    {
        const int MaxRows = 5;
        readonly IslandWindow host;

        public ClipboardActivity(IslandWindow h)
        {
            host = h;
            Id = "clipboard";
            Priority = 1;
        }

        int Rows => Math.Max(1, Math.Min(MaxRows, host.ClipHistory.Count));
        public override double CompactWidth => 220;
        public override Size ExpandedSize => new Size(430, 74 + Rows * 44);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph("", 14, Ui.Br(Ui.Teal)), Ui.Text("Zwischenablage", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph("", 14, Ui.Br(Ui.Teal));

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = Ui.PageMargin };
            root.Children.Add(Ui.PageHead("Zwischenablage", host.ClipHistory.Count > 0 ? host.ClipHistory.Count + " Einträge" : null, ("\uE74D", "Leeren", host.ClipHistory.Count > 0 ? host.ClearClipHistory : (Action)null)));

            var list = new StackPanel();
            if (host.ClipHistory.Count == 0)
            {
                var empty = Ui.Text("Noch nichts kopiert", 12.5, Ui.Dim, false);
                empty.Margin = new Thickness(0, 12, 0, 12);
                list.Children.Add(empty);
            }
            foreach (var c in host.ClipHistory.Take(MaxRows))
            {
                var item = c;
                var row = new Border { Height = 44, CornerRadius = new CornerRadius(10), Background = Brushes.Transparent, Cursor = Cursors.Hand, Padding = new Thickness(8, 0, 4, 0) };
                FrameworkElement icon;
                if (item.Image != null)
                {
                    var img = new Image { Source = item.Image, Width = 30, Height = 30, Stretch = Stretch.UniformToFill };
                    img.Clip = new RectangleGeometry(new Rect(0, 0, 30, 30), 6, 6);
                    icon = img;
                }
                else icon = Ui.Glyph(item.Glyph, 16, Ui.Dim);
                var texts = Ui.VStack(1, Ui.Text(item.Preview, 13, Ui.White, false), Ui.Text(DownloadWatcher.Ago(item.At), 11, Ui.Dim, false));
                texts.Margin = new Thickness(10, 0, 8, 0);
                var copy = Ui.RoundButton("", 12, () => host.UseClip(item, false), 30);
                copy.ToolTip = "Nur kopieren";
                row.Child = Ui.Row3(icon, texts, copy, new Thickness(0));
                row.ToolTip = "Einfügen";
                row.MouseEnter += (o, e) => row.Background = Ui.Br(Colors.White, 22);
                row.MouseLeave += (o, e) => row.Background = Brushes.Transparent;
                row.MouseLeftButtonDown += (o, e) => e.Handled = true;
                Ui.Pressable(row, 0.98);
                row.MouseLeftButtonUp += (o, e) => { e.Handled = true; host.UseClip(item, true); };
                list.Children.Add(row);
            }
            root.Children.Add(host.ClipHistory.Count > 0 ? Ui.Group(list, new Thickness(4)) : (UIElement)list);
            return root;
        }
    }

    /// <summary>App-Switcher: alle offenen Fenster als Symbole, Antippen holt das Fenster nach vorne.</summary>
    public sealed class AppSwitcherActivity : Activity
    {
        readonly IslandWindow host;
        List<WindowInfo> windows = new List<WindowInfo>();

        public AppSwitcherActivity(IslandWindow h)
        {
            host = h;
            Id = "apps";
            Priority = 1;
        }

        public void Reload() => windows = WindowList.Open(host.Handle, 12);

        int Rows => Math.Max(1, (windows.Count + 3) / 4);
        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(430, 62 + Rows * 78);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph("", 14, Ui.AccentBrush), Ui.Text("Apps", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph("", 14, Ui.AccentBrush);

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = Ui.PageMargin };
            root.Children.Add(Ui.PageHead("Offene Apps", "Strg + Alt + A"));
            var wp = new WrapPanel { Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            if (windows.Count == 0) wp.Children.Add(Ui.Text("Keine offenen Fenster", 12.5, Ui.Dim, false));
            foreach (var w in windows)
            {
                var win = w;
                var cell = new StackPanel { Width = 96, Margin = new Thickness(0, 0, 0, 4), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = win.Title };
                var tile = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(14), Background = Ui.Faint, HorizontalAlignment = HorizontalAlignment.Center };
                var img = win.ExePath != null ? Native.FileIcon(win.ExePath) : null;
                if (img != null)
                {
                    var image = new Image { Source = img, Width = 32, Height = 32 };
                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                    tile.Child = image;
                }
                else tile.Child = Ui.Glyph("", 20, Ui.Dim);
                var scale = new ScaleTransform(1, 1);
                tile.RenderTransformOrigin = new Point(0.5, 0.5);
                tile.RenderTransform = scale;
                var lbl = Ui.Text(win.Title, 11, Ui.Soft, false);
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                lbl.Margin = new Thickness(4, 5, 4, 0);
                lbl.MaxWidth = 94;
                cell.Children.Add(tile);
                cell.Children.Add(lbl);
                cell.MouseEnter += (o, e) => tile.Background = Ui.Br(Colors.White, 60);
                cell.MouseLeave += (o, e) => { tile.Background = Ui.Faint; scale.ScaleX = scale.ScaleY = 1; };
                cell.MouseLeftButtonDown += (o, e) => { e.Handled = true; scale.ScaleX = scale.ScaleY = 0.9; };
                cell.MouseLeftButtonUp += (o, e) => { e.Handled = true; host.SwitchTo(win.Handle); };
                wp.Children.Add(cell);
            }
            root.Children.Add(wp);
            return root;
        }
    }

    /// <summary>Helligkeit aller Bildschirme (externe Monitore per DDC/CI, Laptop per Windows).</summary>
    public sealed class BrightnessActivity : Activity
    {
        readonly IslandWindow host;
        List<DisplayInfo> displays;
        bool loading;

        public BrightnessActivity(IslandWindow h)
        {
            host = h;
            Id = "brightness";
            Priority = 1;
        }

        public async void Reload()
        {
            if (loading) return;
            loading = true;
            try { displays = await Brightness.LoadAsync(); }
            catch (Exception ex) { Program.Log(ex); displays = new List<DisplayInfo>(); }
            loading = false;
            host.Rebuild(this);
        }

        int Rows => Math.Max(1, displays?.Count ?? 1);
        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(420, 96 + Rows * 56);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph("", 14, Ui.Br(Ui.Yellow)), Ui.Text("Helligkeit", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph("", 14, Ui.Br(Ui.Yellow));

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = Ui.PageMargin };
            root.Children.Add(Ui.PageHead("Helligkeit", null, ("\uE708", "Nachtmodus", () => IslandWindow.Open("ms-settings:nightlight"))));

            if (displays == null)
            {
                var t = Ui.Text("Bildschirme werden gesucht …", 12.5, Ui.Dim, false);
                t.Margin = new Thickness(0, 16, 0, 0);
                root.Children.Add(t);
                return root;
            }
            if (displays.Count == 0)
            {
                var t = Ui.Text("Kein Bildschirm erlaubt das Regeln. Bei Monitoren muss „DDC/CI“ im Monitor-Menü an sein.", 12, Ui.Dim, false);
                t.TextWrapping = TextWrapping.Wrap;
                t.TextTrimming = TextTrimming.None;
                t.Margin = new Thickness(0, 14, 0, 0);
                root.Children.Add(t);
                var link = Ui.Link("Anzeige-Einstellungen öffnen", () => IslandWindow.Open("ms-settings:display"), 12.5);
                link.Margin = new Thickness(0, 10, 0, 0);
                root.Children.Add(link);
                return root;
            }
            foreach (var d in displays)
            {
                var disp = d;
                var name = Ui.Text(disp.Name, 11.5, Ui.Dim, false);
                name.Margin = new Thickness(0, 14, 0, 2);
                root.Children.Add(name);
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                var low = Ui.Glyph("", 13, Ui.Dim);
                var bar = new DragBar(Ui.Br(Ui.Yellow), 6) { Margin = new Thickness(10, 0, 10, 0), WheelStep = 0.05 };
                var pct = Ui.Text(Math.Round(disp.Fraction * 100) + " %", 12.5, Ui.White, true, true);
                pct.HorizontalAlignment = HorizontalAlignment.Right;
                bar.Bar.Value = disp.Fraction;
                bar.Current = () => disp.Fraction;
                bar.Changed = f => { Brightness.Set(disp, f); pct.Text = Math.Round(f * 100) + " %"; };
                Grid.SetColumn(low, 0); Grid.SetColumn(bar, 1); Grid.SetColumn(pct, 2);
                g.Children.Add(low); g.Children.Add(bar); g.Children.Add(pct);
                root.Children.Add(g);
            }
            return root;
        }
    }
}

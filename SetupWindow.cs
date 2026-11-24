using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DynamicIsland
{
    /// <summary>
    /// Einstellungen im Stil der Apple-Systemeinstellungen: Seitenleiste links, gruppierte
    /// Listen rechts, Schalter, Segment-Auswahl, Systemfarben. Änderungen gelten sofort.
    /// </summary>
    public sealed class SetupWindow : Window
    {
        static readonly SolidColorBrush BgBrush = Ui.Br(Color.FromRgb(0x1C, 0x1C, 0x1E));
        static readonly SolidColorBrush SideBrush = Ui.Br(Color.FromRgb(0x26, 0x26, 0x28));
        static readonly SolidColorBrush CardBrush = Ui.Br(Color.FromRgb(0x2C, 0x2C, 0x2E));
        static readonly SolidColorBrush FieldBrush = Ui.Br(Color.FromRgb(0x3A, 0x3A, 0x3C));
        static readonly SolidColorBrush SegSelBrush = Ui.Br(Color.FromRgb(0x63, 0x63, 0x66));
        static readonly SolidColorBrush SepBrush = Ui.Br(Color.FromRgb(0x45, 0x45, 0x48));
        static readonly SolidColorBrush Secondary = Ui.Br(Color.FromRgb(0x98, 0x98, 0x9F));
        static readonly SolidColorBrush BlueBrush = Ui.Br(Ui.Blue);
        static readonly SolidColorBrush RedBrush = Ui.Br(Ui.Red);

        sealed class Page
        {
            public string Group, Key, Title, Subtitle, Glyph;
            public Color Tint;
            public bool Emoji;
            /// <summary>Echtes App-Logo statt Glyph (Discord, Claude, Teams).</summary>
            public Func<double, FrameworkElement> Logo;
        }

        /// <summary>Farbiges Quadrat mit Symbol bzw. Logo für die Seitenleiste und die Suche.</summary>
        static Border PageIcon(Page p)
        {
            FrameworkElement child;
            if (p.Emoji) child = Ui.Glyph(p.Glyph, 16, Ui.White, Ui.EmojiFont);
            else if (p.Logo != null)
            {
                try { child = p.Logo(p.Key == "teams" ? 24 : 15); }
                catch { child = Ui.Glyph(p.Glyph, 13, Ui.White); }
            }
            else child = Ui.Glyph(p.Glyph, 13, Ui.White);
            child.HorizontalAlignment = HorizontalAlignment.Center;
            child.VerticalAlignment = VerticalAlignment.Center;
            return new Border
            {
                Width = 24, Height = 24, CornerRadius = new CornerRadius(6), ClipToBounds = true,
                Background = p.Emoji ? Brushes.Transparent : Ui.Br(p.Tint),
                Child = child
            };
        }

        // Reihenfolge = Seitenleiste; Group = Überschrift darüber (wie in den Systemeinstellungen am Mac)
        static readonly Page[] Pages =
        {
            new Page { Group = "Island",            Key = "general",   Title = "Allgemein",    Glyph = "\uE713", Tint = Ui.Gray,   Subtitle = "Größe, Position, Aussehen, Ausblenden und Start" },
            new Page { Group = "Island",            Key = "overview",  Title = "Übersicht",    Glyph = "\uE80F", Tint = Ui.Blue,   Subtitle = "Uhr und Kacheln der Startseite" },
            new Page { Group = "Island",            Key = "pages",     Title = "Seiten & Widgets", Glyph = "\uF0E2", Tint = Ui.Teal, Subtitle = "Seiten zum Wischen sortieren, entfernen und mit Widgets bestücken" },
            new Page { Group = "Island",            Key = "keys",      Title = "Tastenkürzel", Glyph = "\uE765", Tint = Ui.Gray,   Subtitle = "Funktionen von überall aufrufen" },
            new Page { Group = "Hinweise & Effekte", Key = "alerts",   Title = "Hinweise",     Glyph = "\uEA8F", Tint = Ui.Red,    Subtitle = "Design, was oben kurz angezeigt wird und in welcher Reihenfolge" },
            new Page { Group = "Hinweise & Effekte", Key = "anim",     Title = "Animationen",  Glyph = "\uE945", Tint = Ui.Orange, Subtitle = "Licht-Effekt und was pro Ereignis passiert" },
            new Page { Group = "Persönlich",        Key = "cats",      Title = "Katzen",       Glyph = "\U0001F431", Emoji = true, Subtitle = "Deine Begleiter neben der Island" },
            new Page { Group = "Persönlich",        Key = "obsidian",  Title = "Obsidian",     Glyph = "", Tint = Obsidian.Purple, Subtitle = "Texte, Links und Dateien auf die Island ziehen – landen in deiner Notiz" },
            new Page { Group = "Persönlich",        Key = "shortcuts", Title = "Kurzbefehle",  Glyph = "\uE8A9", Tint = Ui.Purple, Subtitle = "Programme, Ordner und Webseiten mit einem Klick" },
            new Page { Group = "Medien & Geräte",   Key = "spotify",   Title = "Spotify",      Glyph = "\uE8D6", Tint = Color.FromRgb(0x1D, 0xB9, 0x54), Subtitle = "Eingebauter Player mit deinem Spotify-Konto" },
            new Page { Group = "Medien & Geräte",   Key = "discord",   Title = "Discord",      Glyph = "\uE8F2", Logo = s => Logos.Discord(s, 1, Colors.White), Tint = DiscordVoiceActivity.Blurple, Subtitle = "Nachrichten und Sprach-Overlay" },
            new Page { Group = "Medien & Geräte",   Key = "teams",     Title = "Teams",        Glyph = "\uE716", Logo = s => Logos.Teams(s), Tint = Logos.TeamsPurple, Subtitle = "Besprechungen steuern: Mikro, Kamera, Hand, Reaktionen" },
            new Page { Group = "Medien & Geräte",   Key = "airpods",   Title = "Kopfh\u00F6rer",    Glyph = "\uE7F6", Tint = Color.FromRgb(0x63, 0x63, 0x66), Subtitle = "Akku und Wiedergabeger\u00E4t \u2013 AirPods-Funktionen auf Wunsch" },
            new Page { Group = "Planen",            Key = "calendar",  Title = "Kalender",     Glyph = "\uE787", Tint = Ui.Red,    Subtitle = "Termine anzeigen, erinnern und ansagen" },
            new Page { Group = "Planen",            Key = "todo",      Title = "To Do & Erinnerungen", Glyph = "\uE73A", Tint = Ui.Blue, Subtitle = "Microsoft To Do – und Apple-Erinnerungen über dein Outlook-Konto" },
            new Page { Group = "Planen",            Key = "weather",   Title = "Wetter",       Glyph = WeatherActivity.Glyph, Tint = Ui.Teal, Subtitle = "Wetter jetzt, die nächsten Stunden und Tage" },
            new Page { Group = "KI",                Key = "ai",        Title = "KI",           Glyph = "\uE8BD", Tint = Ui.Purple, Subtitle = "Gemini-Chat, Text-Drop-Zone und Texterkennung" },
            new Page { Group = "KI",                Key = "claude",    Title = "Claude",       Glyph = "\uE945", Logo = s => Logos.Claude(s, 1, Colors.White), Tint = ClaudeTracker.Orange, Subtitle = "Sehen, was Claude Code gerade macht – und wie lange schon" },
        };

        /// <summary>Seiten in der Seitenleiste – „Claude“ nur, wenn Claude Code auf dem PC installiert ist.</summary>
        static System.Collections.Generic.IEnumerable<Page> VisiblePages =>
            Pages.Where(p => p.Key != "claude" || ClaudeTracker.ClaudeInstalled());

        readonly IslandWindow host;
        readonly Settings s;
        readonly StackPanel nav = new StackPanel { Margin = new Thickness(10, 16, 10, 16) };
        readonly ScrollViewer content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        string page;
        StackPanel catList, shortcutList;
        readonly HashSet<CatConfig> openCats = new HashSet<CatConfig>();
        TextBlock discordStatus, notifStatus, spotifyStatus;
        TextBlock teamsStatus;
        TextBox spotifyIdBox;
        TextBox clientIdBox;
        PasswordBox secretBox;
        AppleSwitch discordSwitch;

        public SetupWindow(IslandWindow h, string startPage = null)
        {
            host = h;
            s = h.Settings;
            Title = "Dynamic Island – Einstellungen";
            Width = 880;
            Height = 660;
            MinWidth = 720;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = BgBrush;
            Foreground = Ui.White;
            FontFamily = Ui.TextFont;
            Topmost = true;
            UseLayoutRounding = true;
            SourceInitialized += (o, e) => Native.UseDarkTitleBar(new WindowInteropHelper(this).Handle);
            ContentRendered += (o, e) => { Activate(); Topmost = false; };
            host.Discord.StatusChanged += OnDiscordStatus;
            host.Teams.StatusChanged += OnTeamsStatus;
            host.Spotify.StatusChanged += OnSpotifyStatus;
            host.Todo.StatusChanged += OnTodoStatus;
            Closed += (o, e) =>
            {
                host.Discord.StatusChanged -= OnDiscordStatus;
                host.Teams.StatusChanged -= OnTeamsStatus;
                host.Spotify.StatusChanged -= OnSpotifyStatus;
                host.Todo.StatusChanged -= OnTodoStatus;
                SaveSpotifyId();
                SaveDiscordFields();
                s.Save();
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            // Seitenleiste: oben die Suche, darunter die Seiten (bzw. die Treffer)
            var sideGrid = new Grid();
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var searchBar = BuildSearchBox();
            var navScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = nav };
            Grid.SetRow(navScroll, 1);
            sideGrid.Children.Add(searchBar);
            sideGrid.Children.Add(navScroll);
            nav.Margin = new Thickness(10, 6, 10, 16);
            var side = new Border { Background = SideBrush, Child = sideGrid };
            // Strg+F = suchen, Esc = Suche leeren
            PreviewKeyDown += (o, e) =>
            {
                if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { searchBox.Focus(); searchBox.SelectAll(); e.Handled = true; }
                else if (e.Key == Key.Escape && searchBox.Text.Length > 0) { searchBox.Text = ""; e.Handled = true; }
            };
            Grid.SetColumn(content, 1);
            grid.Children.Add(side);
            grid.Children.Add(content);
            Content = grid;
            ShowPage(startPage ?? "general");
        }

        // =================================================================
        //  Navigation
        // =================================================================
        public void ShowPage(string key)
        {
            if (page == "discord") SaveDiscordFields();
            if (page == "spotify") SaveSpotifyId();
            bool same = key == page;
            double offset = same ? content.VerticalOffset : 0;
            page = VisiblePages.Any(p => p.Key == key) ? key : "general";
            BuildNav();

            var pg = Pages.First(p => p.Key == page);
            var root = new StackPanel { Margin = new Thickness(30, 22, 30, 32), MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
            var title = Ui.Text(pg.Title, 28, Ui.White);
            title.FontWeight = FontWeights.Bold;
            root.Children.Add(title);
            var sub = Ui.Text(pg.Subtitle, 13, Secondary, false);
            sub.Margin = new Thickness(0, 2, 0, 0);
            root.Children.Add(sub);

            discordStatus = null;
            teamsStatus = null;
            notifStatus = null;
            spotifyStatus = null;
            spotifyIdBox = null;
            clientIdBox = null;
            secretBox = null;
            FillPage(page, root);
            content.Content = root;
            if (same) Dispatcher.BeginInvoke(new Action(() => content.ScrollToVerticalOffset(offset)), DispatcherPriority.Loaded);
            else content.ScrollToTop();
        }

        void FillPage(string key, StackPanel root)
        {
            switch (key)
            {
                case "general": BuildGeneral(root); break;
                case "anim": BuildAnim(root); break;
                case "overview": BuildOverview(root); break;
                case "alerts": BuildAlerts(root); break;
                case "cats": BuildCats(root); break;
                case "shortcuts": BuildShortcuts(root); break;
                case "spotify": BuildSpotify(root); break;
                case "discord": BuildDiscord(root); break;
                case "teams": BuildTeams(root); break;
                case "airpods": BuildAirPods(root); break;
                case "pages": BuildPages(root); break;
                case "keys": BuildKeys(root); break;
                case "ai": BuildAi(root); break;
                case "claude": BuildClaude(root); break;
                case "weather": BuildWeather(root); break;
                case "calendar": BuildCalendar(root); break;
                case "todo": BuildTodo(root); break;
                case "obsidian": BuildObsidian(root); break;
            }
        }

        // =================================================================
        //  Suche (oben in der Seitenleiste)
        // =================================================================
        TextBox searchBox;

        /// <summary>Suche von außen setzen (z. B. lokale API /settings?q=…).</summary>
        public void Search(string q) { searchBox.Text = q ?? ""; searchBox.Focus(); searchBox.CaretIndex = searchBox.Text.Length; }
        Dictionary<string, List<string>> searchIndex;

        FrameworkElement BuildSearchBox()
        {
            searchBox = Field("", 13, false);
            searchBox.Margin = new Thickness(6, 0, 0, 0);
            var hint = Ui.Text("Suchen", 13, Secondary, false);
            hint.IsHitTestVisible = false;
            hint.Margin = new Thickness(8, 0, 0, 0);
            hint.VerticalAlignment = VerticalAlignment.Center;
            var clear = Ui.Glyph("", 10, Secondary);
            clear.Cursor = Cursors.Hand;
            clear.Visibility = Visibility.Collapsed;
            clear.Margin = new Thickness(4, 0, 2, 0);
            clear.MouseLeftButtonUp += (o, e) => { searchBox.Text = ""; searchBox.Focus(); };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = Ui.Glyph("", 12, Secondary);
            Grid.SetColumn(searchBox, 1); Grid.SetColumn(hint, 1); Grid.SetColumn(clear, 2);
            g.Children.Add(icon); g.Children.Add(hint); g.Children.Add(searchBox); g.Children.Add(clear);
            searchBox.TextChanged += (o, e) =>
            {
                bool empty = searchBox.Text.Length == 0;
                hint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
                clear.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
                BuildNav();
            };
            searchBox.KeyDown += (o, e) =>
            {
                if (e.Key != Key.Enter) return;
                var first = SearchResults(searchBox.Text.Trim()).FirstOrDefault();
                if (first.page != null) OpenResult(first.page.Key, first.hits.FirstOrDefault());
                e.Handled = true;
            };
            return new Border
            {
                Margin = new Thickness(12, 14, 12, 6), Height = 30, CornerRadius = new CornerRadius(8),
                Background = FieldBrush, Padding = new Thickness(9, 0, 6, 0), Child = g
            };
        }

        static string Norm(string t) => (t ?? "").ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");

        /// <summary>Texte aller Seiten einmal sammeln (Seiten werden dafür unsichtbar aufgebaut).</summary>
        void EnsureIndex()
        {
            if (searchIndex != null) return;
            searchIndex = new Dictionary<string, List<string>>();
            foreach (var p in VisiblePages)
            {
                var texts = new List<string>();
                try
                {
                    var tmp = new StackPanel();
                    FillPage(p.Key, tmp);
                    Collect(tmp, texts);
                }
                catch (Exception ex) { Program.Log(ex); }
                searchIndex[p.Key] = texts.Distinct().ToList();
            }
            // die unsichtbar aufgebauten Seiten haben Verweise (Listen, Statusfelder) überschrieben → aktuelle Seite neu
            var keep = page;
            page = null;
            ShowPage(keep);
        }

        static void Collect(DependencyObject d, List<string> into)
        {
            if (d is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text)) into.Add(tb.Text.Trim());
            foreach (var c in LogicalTreeHelper.GetChildren(d))
                if (c is DependencyObject dc) Collect(dc, into);
        }

        List<(Page page, List<string> hits)> SearchResults(string q)
        {
            var res = new List<(Page, List<string>)>();
            if (q.Length == 0) return res;
            EnsureIndex();
            var words = Norm(q).Split(new[] { (char)32 }, StringSplitOptions.RemoveEmptyEntries);
            bool All(string t) { var n = Norm(t); return words.All(n.Contains); }
            foreach (var p in VisiblePages)
            {
                bool head = All(p.Title + " " + p.Subtitle + " " + p.Group);
                var hits = (searchIndex.TryGetValue(p.Key, out var l) ? l : new List<string>())
                    .Where(All).OrderBy(t => t.Length).Take(3).ToList();
                if (head || hits.Count > 0) res.Add((p, hits));
            }
            // Treffer im Seitennamen zuerst
            return res.OrderByDescending(r => Norm(r.Item1.Title).Contains(words[0])).ToList();
        }

        static string Snippet(string text, string q)
        {
            text = text.Replace("\n", " ");
            if (text.Length <= 60) return text;
            int i = Norm(text).IndexOf(Norm(q).Split((char)32)[0], StringComparison.Ordinal);
            int start = Math.Max(0, Math.Min(text.Length - 60, i - 20));
            if (start > 0) { int sp = text.IndexOf((char)32, start); if (sp >= 0 && sp < i) start = sp + 1; }   // am Wortanfang beginnen
            return (start > 0 ? "…" : "") + text.Substring(start, Math.Min(60, text.Length - start)).Trim() + (start + 60 < text.Length ? "…" : "");
        }

        /// <summary>Seite öffnen und die Fundstelle kurz hervorheben.</summary>
        void OpenResult(string key, string hit)
        {
            ShowPage(key);
            if (string.IsNullOrEmpty(hit)) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var t = FindText(content.Content as DependencyObject, hit);
                if (t == null) return;
                t.BringIntoView();
                var b = new SolidColorBrush(Color.FromArgb(0, 0x0A, 0x84, 0xFF));
                t.Background = b;
                var a = new ColorAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1800) };
                a.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(120, 0x0A, 0x84, 0xFF), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
                a.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(120, 0x0A, 0x84, 0xFF), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1100))));
                a.KeyFrames.Add(new LinearColorKeyFrame(Color.FromArgb(0, 0x0A, 0x84, 0xFF), KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1800))));
                b.BeginAnimation(SolidColorBrush.ColorProperty, a);
            }), DispatcherPriority.Loaded);
        }

        static TextBlock FindText(DependencyObject d, string text)
        {
            if (d == null) return null;
            if (d is TextBlock tb && tb.Text.Trim() == text) return tb;
            foreach (var c in LogicalTreeHelper.GetChildren(d))
                if (c is DependencyObject dc) { var r = FindText(dc, text); if (r != null) return r; }
            return null;
        }

        void BuildSearchResults(string q)
        {
            EnsureIndex();          // baut beim ersten Mal die Seite neu (und damit auch diese Liste) → danach sauber neu füllen
            nav.Children.Clear();
            var res = SearchResults(q);
            if (res.Count == 0)
            {
                var none = Ui.Text("Nichts gefunden", 12.5, Secondary, false);
                none.Margin = new Thickness(10, 8, 0, 0);
                nav.Children.Add(none);
                return;
            }
            foreach (var (p, hits) in res)
            {
                var pg = p;
                var icon = PageIcon(p);
                var lines = new StackPanel();
                lines.Children.Add(Ui.Text(p.Title, 13.5, Ui.White, true));
                foreach (var h in hits)
                {
                    var t = Ui.Text(Snippet(h, q), 11.5, Secondary, false);
                    t.TextTrimming = TextTrimming.CharacterEllipsis;
                    lines.Children.Add(t);
                }
                lines.VerticalAlignment = VerticalAlignment.Center;
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                icon.VerticalAlignment = VerticalAlignment.Top;
                icon.Margin = new Thickness(0, 2, 10, 0);
                Grid.SetColumn(lines, 1);
                g.Children.Add(icon); g.Children.Add(lines);
                var row = new Border
                {
                    CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 2),
                    Cursor = Cursors.Hand, Background = p.Key == page ? Ui.Br(Colors.White, 14) : Brushes.Transparent, Child = g
                };
                var bg = row.Background;
                row.MouseEnter += (o, e) => row.Background = Ui.Br(Colors.White, 20);
                row.MouseLeave += (o, e) => row.Background = bg;
                row.MouseLeftButtonUp += (o, e) => OpenResult(pg.Key, hits.FirstOrDefault());
                nav.Children.Add(row);
            }
        }

        void BuildNav()
        {
            nav.Children.Clear();
            string q = searchBox?.Text.Trim() ?? "";
            if (q.Length > 0) { BuildSearchResults(q); return; }
            string group = null;
            foreach (var p in VisiblePages)
            {
                if (p.Group != group)
                {
                    group = p.Group;
                    var gh = Ui.Text(group.ToUpperInvariant(), 10.5, Secondary, true);
                    gh.Margin = new Thickness(10, nav.Children.Count == 0 ? 0 : 14, 0, 5);
                    nav.Children.Add(gh);
                }
                var pg = p;
                bool sel = p.Key == page;
                var icon = PageIcon(p);
                var row = new Border
                {
                    Height = 36,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 0, 8, 0),
                    Margin = new Thickness(0, 0, 0, 2),
                    Cursor = Cursors.Hand,
                    Background = sel ? BlueBrush : Brushes.Transparent,
                    Child = Ui.HStack(10, icon, Ui.Text(p.Title, 13.5, Ui.White, sel))
                };
                row.MouseEnter += (o, e) => { if (pg.Key != page) row.Background = Ui.Br(Colors.White, 20); };
                row.MouseLeave += (o, e) => { if (pg.Key != page) row.Background = Brushes.Transparent; };
                row.MouseLeftButtonUp += (o, e) => ShowPage(pg.Key);
                nav.Children.Add(row);
            }
        }

        void Apply()
        {
            s.Save();
            host.ApplySettings();
        }

        // =================================================================
        //  Allgemein
        // =================================================================
        static readonly double[] Delays = { 2, 3, 5, 10 };
        static readonly double[] Scales = { 0.9, 1, 1.15, 1.3 };
        static readonly double[] PageScales = { 0.7, 0.8, 0.9, 1 };
        static readonly double[] PageWides = { 1.0, 1.15, 1.3, 1.45 };
        static readonly double[] PageFonts = { 1.0, 1.2, 1.4, 1.6 };
        static readonly string[] PageFontNames = { "Normal", "Groß", "Größer", "Sehr groß" };

        static int Nearest(double[] values, double v)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++)
                if (Math.Abs(values[i] - v) < Math.Abs(values[best] - v)) best = i;
            return best;
        }

        void BuildGeneral(StackPanel root)
        {
            // ---------- Größe & Position ----------
            root.Children.Add(Header("Größe & Position"));
            var size = new StackPanel();
            size.Children.Add(SegmentRow("Große Island", new[] { "Sehr klein", "Klein", "Etwas kleiner", "Normal" }, Nearest(PageScales, s.PageScale),
                i => { s.PageScale = PageScales[i]; Apply(); }));
            size.Children.Add(Divider());
            size.Children.Add(SegmentRow("Breite", new[] { "Normal", "Breit", "Breiter", "Extra breit" }, Nearest(PageWides, s.PageWide),
                i => { s.PageWide = PageWides[i]; Apply(); }));
            size.Children.Add(Divider());
            size.Children.Add(SegmentRow("Alles skalieren", new[] { "Klein", "Normal", "Groß", "Sehr groß" }, Nearest(Scales, s.Scale),
                i => { s.Scale = Scales[i]; Apply(); }));
            size.Children.Add(Divider());
            int pos = s.PosY > 1 ? -1 : s.PosX < -1 ? 0 : s.PosX > 1 ? 2 : 1;
            size.Children.Add(SegmentRow("Position", new[] { "Links", "Mitte", "Rechts" }, pos, i => host.PlaceTop(i)));
            size.Children.Add(Divider());
            size.Children.Add(ToggleRow("Island bleibt genau in der Mitte", s.IslandFixedCenter, v => { s.IslandFixedCenter = v; Apply(); }, out _));
            root.Children.Add(Card(size));
            root.Children.Add(Footer("Frei verschieben: rechte Maustaste auf der Island gedrückt halten und ziehen – auch auf einen anderen Bildschirm. Rastet nur in der Mitte ein. " +
                                     "„Genau in der Mitte“: Die Island rutscht nicht zur Seite, wenn rechts daneben der kleine Kreis erscheint. Aus: Island und Kreis werden zusammen zentriert."));

            // ---------- Seiten: Schrift + Stil ----------
            root.Children.Add(Header("Seiten"));
            var pages = new StackPanel();
            pages.Children.Add(SegmentRow("Schrift in den Seiten", PageFontNames, Nearest(PageFonts, s.PageFont),
                i => { s.PageFont = PageFonts[i]; Apply(); host.RefreshPage(); }));
            pages.Children.Add(Divider());
            pages.Children.Add(SegmentRow("Stil der Seiten", new[] { "Aufgeräumt", "Ausführlich" }, s.PageStyle == "full" ? 1 : 0,
                i => { s.PageStyle = i == 1 ? "full" : "clean"; Apply(); host.RefreshPage(); }));
            root.Children.Add(Card(pages));
            root.Children.Add(Footer("Die Schrift wächst, die Island bleibt gleich groß. „Aufgeräumt“ zeigt weniger Einzelheiten, dafür größer (z. B. Wetter ohne Tage, Kalender mit 3 Terminen). " +
                                     "Jede Seite kann unter „Seiten & Widgets“ eigene Werte bekommen (Knopf Aa)."));

            // ---------- Aussehen ----------
            root.Children.Add(Header("Aussehen"));
            var look = new StackPanel();
            look.Children.Add(ColorRow("Akzentfarbe", AccentPalette, s.AccentColor ?? "#0A84FF", v => { s.AccentColor = v; Apply(); Later(() => ShowPage("general")); }));
            look.Children.Add(Divider());
            look.Children.Add(ToggleRow("Seitensymbole farbig", s.NavIconsColor, v => { s.NavIconsColor = v; Apply(); }, out _));
            look.Children.Add(Divider());
            look.Children.Add(ToggleRow("Im Leerlauf als Pille zeigen", s.IdlePill, v => { s.IdlePill = v; Apply(); }, out _));
            look.Children.Add(Divider());
            look.Children.Add(SegmentRow("Regler-Knöpfe", new[] { "Liquid Glass", "Flach" }, s.Material == "flat" ? 1 : 0,
                i => { s.Material = i == 1 ? "flat" : "glass"; Apply(); Later(() => ShowPage("general")); }));
            if (s.Material != "flat")
            {
                look.Children.Add(Divider());
                var tints = new[] { "clear", "sapphire", "smoke" };
                look.Children.Add(SegmentRow("Glasfarbe", new[] { "Kristall", "Saphir", "Rauch" }, Math.Max(0, Array.IndexOf(tints, s.GlassTint ?? "clear")),
                    i => { s.GlassTint = tints[i]; Apply(); }));
            }
            root.Children.Add(Card(look));
            root.Children.Add(Footer(string.IsNullOrEmpty(LiquidGlass.Problem)
                ? "Seitensymbole: farbig oder alle weiß. Leerlauf: Pille oder dünner Strich. Animationen findest du links unter „Animationen“."
                : "Liquid Glass: " + LiquidGlass.Problem));

            // ---------- Ausblenden ----------
            root.Children.Add(Header("Ausblenden"));
            var hide = new StackPanel();
            hide.Children.Add(ToggleRow("Automatisch nach oben ausblenden", s.AutoHide, v => { s.AutoHide = v; Apply(); }, out _));
            hide.Children.Add(Divider());
            hide.Children.Add(ToggleRow("Auch bei Musik, Timer und Downloads", s.HideWithActivities, v => { s.HideWithActivities = v; Apply(); }, out _));
            hide.Children.Add(Divider());
            hide.Children.Add(SegmentRow("Ausblenden nach", new[] { "2 s", "3 s", "5 s", "10 s" }, Nearest(Delays, s.HideDelay),
                i => { s.HideDelay = Delays[i]; Apply(); }));
            hide.Children.Add(Divider());
            hide.Children.Add(ToggleRow("Bei Vollbild ausblenden (Spiele, Videos)", s.HideInFullscreen, v => { s.HideInFullscreen = v; Apply(); }, out _));
            root.Children.Add(Card(hide));
            root.Children.Add(Footer("Versteckt? Maus an den oberen Bildschirmrand. Hinweise und Anrufe zeigt die Island immer."));

            root.Children.Add(Header("Player"));
            var pl = new StackPanel();
            pl.Children.Add(ToggleRow("Player dauerhaft erreichbar (auch pausiert)", s.KeepPlayer, v => { s.KeepPlayer = v; Apply(); }, out _));
            root.Children.Add(Card(pl));
            root.Children.Add(Footer("Spielt Spotify, wird es bevorzugt angezeigt. In der Übersicht steht immer ein schlichter Mini-Player."));

            root.Children.Add(Header("System"));
            var sys = new StackPanel();
            sys.Children.Add(ToggleRow("Mit Windows starten", Autostart.Enabled, v => Autostart.Set(v), out _));
            root.Children.Add(Card(sys));

            var quit = PressBorder(new Border
            {
                Background = CardBrush,
                CornerRadius = new CornerRadius(10),
                Height = 44,
                Margin = new Thickness(0, 26, 0, 0),
                Child = new TextBlock
                {
                    Text = "Dynamic Island beenden",
                    FontSize = 14.5,
                    FontFamily = Ui.TextFont,
                    Foreground = RedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }, () => host.Quit());
            root.Children.Add(quit);
        }

        // =================================================================
        //  Übersicht (Kacheln auswählen + sortieren)
        // =================================================================
        void BuildOverview(StackPanel root)
        {
            var selected = DashTiles.Selected(s);

            // ---------- Uhr ----------
            root.Children.Add(Header("Uhr"));
            var clock = new StackPanel();
            clock.Children.Add(SegmentRow("Format", new[] { "24 Std.", "12 Std." }, s.Clock24 ? 0 : 1, i => { s.Clock24 = i == 0; Apply(); }));
            clock.Children.Add(Divider());
            clock.Children.Add(ToggleRow("Sekunden anzeigen", s.ClockSeconds, v => { s.ClockSeconds = v; Apply(); }, out _));
            clock.Children.Add(Divider());
            var dateKeys = new[] { "short", "long", "week", "numeric", "off" };
            clock.Children.Add(ChipRow("Datum", new[] { "Sonntag, 15. Nov.", "Sonntag, 15. November", "Mit Kalenderwoche", "15.11.2026", "Kein Datum" },
                Math.Max(0, Array.IndexOf(dateKeys, s.ClockDate ?? "short")), i => { s.ClockDate = dateKeys[i]; Apply(); Later(() => ShowPage("overview")); }));
            clock.Children.Add(Divider());
            var styles = new[] { "bold", "regular", "light" };
            clock.Children.Add(SegmentRow("Schrift", new[] { "Kräftig", "Normal", "Dünn" }, Math.Max(0, Array.IndexOf(styles, s.ClockStyle ?? "bold")),
                i => { s.ClockStyle = styles[i]; Apply(); }));
            clock.Children.Add(Divider());
            var sizes = new[] { "medium", "large", "xl" };
            clock.Children.Add(SegmentRow("Größe", new[] { "Mittel", "Groß", "Sehr groß" }, Math.Max(0, Array.IndexOf(sizes, s.ClockSize ?? "large")),
                i => { s.ClockSize = sizes[i]; Apply(); }));
            clock.Children.Add(Divider());
            clock.Children.Add(ToggleRow("Akkustand neben dem Datum", s.ClockBattery, v => { s.ClockBattery = v; Apply(); }, out _));
            clock.Children.Add(Divider());
            clock.Children.Add(ToggleRow("Mini-Player neben der Uhr", s.DashPlayer, v => { s.DashPlayer = v; Apply(); }, out _));
            root.Children.Add(Card(clock));
            root.Children.Add(Footer("Gilt für die Startseite, die Uhr beim Drüberfahren und das Uhr-Widget. " +
                                     "Mit Sekunden oder 12 Std. wird die Uhr automatisch etwas kleiner, damit sie neben den Player passt."));

            root.Children.Add(Header("Vorschau"));
            var preview = new WrapPanel { Margin = new Thickness(14, 14, 14, 8) };
            foreach (var t in selected)
            {
                var cell = new StackPanel { Width = 76, Margin = new Thickness(0, 0, 0, 8) };
                var icon = DashTiles.Icon(t, 44, FieldBrush);
                icon.HorizontalAlignment = HorizontalAlignment.Center;
                cell.Children.Add(icon);
                var l = Ui.Text(t.Label, 11, Secondary, false);
                l.HorizontalAlignment = HorizontalAlignment.Center;
                l.Margin = new Thickness(0, 4, 0, 0);
                l.MaxWidth = 74;
                cell.Children.Add(l);
                preview.Children.Add(cell);
            }
            if (selected.Count == 0) preview.Children.Add(Ui.Text("Keine Kacheln ausgewählt", 13, Secondary, false));
            root.Children.Add(Card(preview));

            root.Children.Add(Header("Kacheln"));
            var list = new StackPanel();
            var ordered = selected.Concat(DashTiles.All.Where(t => !selected.Contains(t))).ToList();
            foreach (var t in ordered)
            {
                var tile = t;
                bool on = selected.Contains(t);
                int idx = selected.IndexOf(t);
                if (list.Children.Count > 0) list.Children.Add(Divider());

                var g = new Grid { MinHeight = 48, Margin = new Thickness(16, 0, 12, 0) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var icon = DashTiles.Icon(tile, 30, FieldBrush);
                icon.VerticalAlignment = VerticalAlignment.Center;
                var label = Ui.Text(tile.Label, 14.5, on ? Ui.White : Secondary, false);
                label.Margin = new Thickness(12, 0, 8, 0);
                var arrows = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                if (on)
                {
                    var up = Ui.RoundButton("", 12, () => MoveTile(tile.Key, -1), 30, FieldBrush);
                    up.ToolTip = "Nach vorne";
                    up.Opacity = idx > 0 ? 1 : 0.3;
                    var down = Ui.RoundButton("", 12, () => MoveTile(tile.Key, 1), 30, FieldBrush);
                    down.ToolTip = "Nach hinten";
                    down.Opacity = idx < selected.Count - 1 ? 1 : 0.3;
                    down.Margin = new Thickness(6, 0, 0, 0);
                    arrows.Children.Add(up);
                    arrows.Children.Add(down);
                }
                var sw = new AppleSwitch(on, v => SetTile(tile.Key, v));
                Grid.SetColumn(icon, 0); Grid.SetColumn(label, 1); Grid.SetColumn(arrows, 2); Grid.SetColumn(sw, 3);
                g.Children.Add(icon); g.Children.Add(label); g.Children.Add(arrows); g.Children.Add(sw);
                list.Children.Add(g);
            }
            root.Children.Add(Card(list));
            root.Children.Add(Footer($"{DashTiles.Max} Kacheln in einer Reihe – dahinter kommt automatisch „Mehr“ mit allen Funktionen. " +
                                     "Verbundene AirPods bekommen von selbst eine Kachel."));

            var reset = SmallButton("Standard wiederherstellen", () =>
            {
                s.DashboardTiles = DashTiles.DefaultKeys();
                s.Save();
                Later(() => ShowPage("overview"));
            });
            reset.Margin = new Thickness(0, 14, 0, 0);
            reset.HorizontalAlignment = HorizontalAlignment.Left;
            root.Children.Add(reset);
        }

        void SetTile(string key, bool on)
        {
            var keys = DashTiles.Selected(s).Select(t => t.Key).ToList();
            if (on && !keys.Contains(key))
            {
                if (keys.Count >= DashTiles.Max)
                {
                    MessageBox.Show(this, $"Es passen höchstens {DashTiles.Max} Kacheln in die Übersicht.", "Übersicht", MessageBoxButton.OK, MessageBoxImage.Information);
                    Later(() => ShowPage("overview"));
                    return;
                }
                keys.Add(key);
            }
            if (!on) keys.Remove(key);
            s.DashboardTiles = keys;
            s.Save();
            Later(() => ShowPage("overview"));
        }

        void MoveTile(string key, int dir)
        {
            var keys = DashTiles.Selected(s).Select(t => t.Key).ToList();
            int i = keys.IndexOf(key), j = i + dir;
            if (i < 0 || j < 0 || j >= keys.Count) return;
            (keys[i], keys[j]) = (keys[j], keys[i]);
            s.DashboardTiles = keys;
            s.Save();
            Later(() => ShowPage("overview"));
        }

        // =================================================================
        //  Hinweise
        // =================================================================
        void BuildAlerts(StackPanel root)
        {
            // Aussehen der Hinweise (Design + Erscheinen)
            root.Children.Add(Header("Aussehen"));
            var sizeCard = new StackPanel();
            var sizeKeys = AlertLook.Designs.Select(d => d.Key).ToArray();
            sizeCard.Children.Add(SegmentRow("Design", AlertLook.Designs.Select(d => d.Name).ToArray(),
                Math.Max(0, Array.IndexOf(sizeKeys, s.AlertSize ?? "small")),
                i => { s.AlertSize = sizeKeys[i]; Apply(); host.PreviewAlert("claude"); }));
            sizeCard.Children.Add(Divider());
            sizeCard.Children.Add(ButtonRow(SmallButton("Vorschau: Claude", () => host.PreviewAlert("claude")),
                                            SmallButton("Vorschau: Gerät", () => host.PreviewAlert("device")),
                                            SmallButton("Vorschau: Nachricht", () => host.PreviewAlert("message"))));
            sizeCard.Children.Add(Divider());
            var motionKeys = new[] { "slide", "morph", "none" };
            sizeCard.Children.Add(SegmentRow("Erscheinen", new[] { "Schieben", "Tropfen (federnd)", "Ohne" },
                Math.Max(0, Array.IndexOf(motionKeys, s.AlertMotion ?? "slide")),
                i => { s.AlertMotion = motionKeys[i]; Apply(); host.PreviewAlert("claude"); }));
            root.Children.Add(Card(sizeCard));
            root.Children.Add(Footer("Klein: flache Pille mit kleinem Symbol und einer Zeile. Groß: großes Symbol mit Leuchten, zwei Zeilen. " +
                                     "Mitteilung: wie beim iPhone – App-Symbol im abgerundeten Quadrat, Titel mit „jetzt“, darunter der Text. " +
                                     "Minimal: nur das farbige Symbol und eine schmale Zeile. Leuchtend: Symbol im leuchtenden Kreis, farbiger Schimmer und Titel in der Ereignisfarbe. " +
                                     "Schieben: die Island wird sanft länger, der Inhalt gleitet hinein. Tropfen: die Island federt wie beim iPhone. Ohne: einfach einblenden."));

            root.Children.Add(Header("Kurz in der Island anzeigen"));
            var a = new StackPanel();
            void Row(string label, bool value, Action<bool> set)
            {
                if (a.Children.Count > 0) a.Children.Add(Divider());
                a.Children.Add(ToggleRow(label, value, v => { set(v); Apply(); }, out _));
            }
            Row("Lautstärke", s.ShowVolume, v => s.ShowVolume = v);
            Row("Kopieren (Zwischenablage)", s.ShowClipboard, v => s.ShowClipboard = v);
            Row("Zwischenablage-Verlauf merken (Strg + Alt + V)", s.ClipHistory, v => { s.ClipHistory = v; if (!v) host.ClearClipHistory(); });
            Row("Nachrichten-Verlauf merken (nur bis zum Beenden)", s.MessageHistory, v => { s.MessageHistory = v; if (!v) host.ClearMsgHistory(); });
            Row("Feststelltaste", s.ShowCapsLock, v => s.ShowCapsLock = v);
            Row("Geräte verbinden (Kopfhörer, Controller, Festplatten) mit Akku", s.ShowBluetooth, v => s.ShowBluetooth = v);
            Row("Logitech-Maus und -Tastatur: nur bei wenig Akku melden", s.LogitechEnabled, v => { s.LogitechEnabled = v; Later(() => ShowPage("alerts")); });
            if (s.LogitechEnabled)
            {
                var warnSteps = new[] { 10, 15, 20, 30 };
                a.Children.Add(Divider());
                a.Children.Add(SegmentRow("Melden unter", warnSteps.Select(x => x + " %").ToArray(), Math.Max(0, Array.IndexOf(warnSteps, s.LogitechWarnAt)), i => { s.LogitechWarnAt = warnSteps[i]; Apply(); }));
            }
            Row("Kamera- und Mikrofon-Punkte", s.ShowPrivacy, v => s.ShowPrivacy = v);
            Row("Laufende Downloads", s.ShowDownloads, v =>
            {
                s.ShowDownloads = v;
                if (v) host.Downloads.Start();
                else host.Downloads.Stop();
            });
            root.Children.Add(Card(a));
            root.Children.Add(Footer("Downloads aus Chrome, Edge, Firefox und Opera mit Größe und Geschwindigkeit. Ordner: " +
                                     (host.Downloads.Folder ?? Native.DownloadsFolder())));

            root.Children.Add(Header("Mitteilungen & Anrufe"));
            var nt = new StackPanel();
            nt.Children.Add(ToggleRow("Windows-Mitteilungen in der Island", s.MirrorNotifications, async v =>
            {
                s.MirrorNotifications = v;
                s.Save();
                if (v) await host.Notifications.InitAsync();
                else host.Notifications.Disable();
                if (notifStatus != null) notifStatus.Text = "Status: " + host.Notifications.Status;
            }, out _));
            nt.Children.Add(Divider());
            nt.Children.Add(ToggleRow("Antworten direkt absenden (WhatsApp, Discord-DMs)", s.ReplyAutoSend, v => { s.ReplyAutoSend = v; s.Save(); }, out _));
            nt.Children.Add(Divider());
            notifStatus = StatusText("Status: " + host.Notifications.Status);
            nt.Children.Add(notifStatus);
            nt.Children.Add(Divider());
            nt.Children.Add(ButtonRow(SmallButton("Windows-Einstellungen öffnen", () => IslandWindow.Open("ms-settings:privacy-notifications"))));
            root.Children.Add(Card(nt));
            root.Children.Add(Footer("Neue Nachrichten von WhatsApp, Discord, Teams und anderen Apps erscheinen oben, eingehende Anrufe größer mit grünem Hörer. " +
                                     "Mit dem Antworten-Knopf schreibst du direkt in der Island: Die App öffnet den Chat (WhatsApp über die Suche, Discord-DMs per Direktlink) " +
                                     "und sendet – oder fügt nur ein, wenn „direkt absenden“ aus ist. Andere Apps: Text wird eingefügt. " +
                                     "Windows muss den Zugriff auf Mitteilungen erlauben."));

            // Prioritätsliste: kommen mehrere Hinweise gleichzeitig, erscheinen sie nacheinander in dieser Reihenfolge
            root.Children.Add(Header("Reihenfolge bei mehreren Hinweisen"));
            var prioList = new StackPanel();
            s.AlertPriority ??= new List<string>();
            foreach (var (key, _) in IslandWindow.AlertCategories)
                if (!s.AlertPriority.Contains(key)) s.AlertPriority.Add(key);
            for (int i = 0; i < s.AlertPriority.Count; i++)
            {
                int idx = i;
                var key = s.AlertPriority[i];
                var name = IslandWindow.AlertCategories.FirstOrDefault(x => x.Key == key).Name ?? key;
                if (i > 0) prioList.Children.Add(Divider());
                var row = new Grid { MinHeight = 40, Margin = new Thickness(16, 0, 10, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var num = Ui.Text((i + 1).ToString(), 13, Secondary, true);
                num.VerticalAlignment = VerticalAlignment.Center;
                var lbl = Ui.Text(name, 14, Ui.White, false);
                lbl.VerticalAlignment = VerticalAlignment.Center;
                var up = Ui.RoundButton("", 11, () => MovePrio(idx, -1), 28, FieldBrush, Ui.White);
                var down = Ui.RoundButton("", 11, () => MovePrio(idx, 1), 28, FieldBrush, Ui.White);
                if (i == 0) up.Opacity = 0.3;
                if (i == s.AlertPriority.Count - 1) down.Opacity = 0.3;
                var arrows = Ui.HStack(6, up, down);
                arrows.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(lbl, 1); Grid.SetColumn(arrows, 2);
                row.Children.Add(num); row.Children.Add(lbl); row.Children.Add(arrows);
                prioList.Children.Add(row);
            }
            root.Children.Add(Card(prioList));
            root.Children.Add(Footer("Kommen mehrere Hinweise gleichzeitig, zeigt die Island sie nacheinander – oben zuerst. " +
                                     "Ein wichtigerer Hinweis unterbricht einen weniger wichtigen; der kommt danach mit seiner Restzeit wieder."));
        }

        // =================================================================
        //  Animationen: pro Ereignis wählen + direkt testen
        // =================================================================
        // Teile einer Animation (frei kombinierbar) und Licht-Effekte (mehrere gleichzeitig)
        static readonly (string Key, string Name)[] AnimPartDefs = { ("app", "App"), ("glow", "Licht"), ("bounce", "Hüpfen") };
        static readonly (string Key, string Name)[] GlowDefs = { ("orbit", "Umlauf"), ("halo", "Schein"), ("shine", "Glanz"), ("ring", "Rahmen") };
        static readonly (string Hex, string Name)[] AnimPalette =
        {
            ("#FF453A", "Rot"), ("#FF9F0A", "Orange"), ("#FFD60A", "Gelb"), ("#30D158", "Grün"), ("#64D2FF", "Türkis"),
            ("#0A84FF", "Blau"), ("#5E5CE6", "Indigo"), ("#BF5AF2", "Lila"), ("#FF375F", "Pink"), ("#F5F5F7", "Weiß"),
        };

        static HashSet<string> SplitSet(string v) =>
            new HashSet<string>((v ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        /// <summary>Werte in fester Reihenfolge (wie in der Auswahl) zusammenfügen.</summary>
        static string JoinSet(IEnumerable<(string Key, string Name)> order, HashSet<string> set) =>
            string.Join(",", order.Select(d => d.Key).Where(set.Contains));

        /// <summary>Mehrfachauswahl als Kapseln: an = blau mit Haken. Nach jedem Klick werden alle Kapseln neu gefärbt.</summary>
        sealed class ChipSet
        {
            public WrapPanel Panel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            public Action Changed;
            readonly List<Action> refresh = new List<Action>();

            public ChipSet((string Key, string Name)[] items, Func<string, bool> isOn, Action<string> click)
            {
                foreach (var (key, name) in items)
                {
                    var k = key;
                    var check = Ui.Glyph("", 10, Ui.White);
                    check.Margin = new Thickness(0, 0, 5, 0);
                    var txt = Ui.Text(name, 12.5, Ui.White, false);
                    var chip = new Border
                    {
                        CornerRadius = new CornerRadius(14), Padding = new Thickness(11, 5, 12, 5), Margin = new Thickness(0, 0, 6, 6),
                        Cursor = Cursors.Hand, Child = Ui.HStack(0, check, txt)
                    };
                    void Paint()
                    {
                        bool on = isOn(k);
                        chip.Background = on ? BlueBrush : FieldBrush;
                        check.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                        txt.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
                        txt.Foreground = on ? Ui.White : Secondary;
                    }
                    refresh.Add(Paint);
                    chip.MouseEnter += (o, e) => chip.Opacity = 0.85;
                    chip.MouseLeave += (o, e) => chip.Opacity = 1;
                    chip.MouseLeftButtonUp += (o, e) =>
                    {
                        e.Handled = true;
                        try { click(k); } catch (Exception ex) { Program.Log(ex); }
                        foreach (var r in refresh) r();
                        Changed?.Invoke();
                    };
                    Panel.Children.Add(chip);
                    Paint();
                }
            }
        }

        /// <summary>Zeile mit kleiner Beschriftung links und Kapseln rechts daneben.</summary>
        static FrameworkElement ChipLine(string label, ChipSet chips, double indent = 16)
        {
            var g = new Grid { Margin = new Thickness(indent, 0, 12, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var l = Ui.Text(label, 12.5, Secondary, false);
            l.VerticalAlignment = VerticalAlignment.Top;
            l.Margin = new Thickness(0, 6, 0, 0);
            Grid.SetColumn(chips.Panel, 1);
            g.Children.Add(l); g.Children.Add(chips.Panel);
            return g;
        }

        static string DescribeAnim(HashSet<string> parts) =>
            parts.Count == 0 ? "Aus" : string.Join(" + ", AnimPartDefs.Where(d => parts.Contains(d.Key)).Select(d => d.Name));

        void BuildAnim(StackPanel root)
        {
            s.AnimModes ??= new Dictionary<string, string>();
            s.GlowStyles ??= new Dictionary<string, string>();

            // ---------- Licht-Effekte (Standard für alle Ereignisse) ----------
            root.Children.Add(Header("Licht-Effekte"));
            var glowCard = new StackPanel { Margin = new Thickness(0, 12, 0, 6) };
            var stdChips = new ChipSet(GlowDefs, k => SplitSet(s.GlowStyle ?? "orbit").Contains(k), k =>
            {
                var set = SplitSet(s.GlowStyle ?? "orbit");
                if (!set.Remove(k)) set.Add(k);
                if (set.Count == 0) return;                   // mindestens ein Effekt bleibt an
                s.GlowStyle = JoinSet(GlowDefs, set);
                s.Save();
                host.Glow(Ui.Accent);
            });
            glowCard.Children.Add(ChipLine("Standard", stdChips));
            root.Children.Add(Card(glowCard));
            root.Children.Add(Footer("Mehrere Effekte laufen gleichzeitig. Umlauf: Licht läuft einmal um die Island. Schein: weicher Schimmer dahinter. " +
                                     "Glanz: Reflex zieht über die Island. Rahmen: Rand leuchtet auf. Die Farbe kommt vom jeweiligen Ereignis."));

            root.Children.Add(Header("Umlauf"));
            var orbitCard = new StackPanel();
            var orbitLooks = new[] { "comet", "line" };
            orbitCard.Children.Add(SegmentRow("Look", new[] { "Komet", "Streifen" },
                Math.Max(0, Array.IndexOf(orbitLooks, s.OrbitLook ?? "comet")),
                i => { s.OrbitLook = orbitLooks[i]; s.Save(); host.Glow(Ui.Accent, "orbit"); }));
            orbitCard.Children.Add(Divider());
            var orbitEnds = new[] { "soft", "close" };
            orbitCard.Children.Add(SegmentRow("Ende", new[] { "Weich auslaufen", "Rahmen schließen" },
                Math.Max(0, Array.IndexOf(orbitEnds, s.OrbitEnd ?? "soft")),
                i => { s.OrbitEnd = orbitEnds[i]; s.Save(); host.Glow(Ui.Accent, "orbit"); }));
            orbitCard.Children.Add(Divider());
            orbitCard.Children.Add(ButtonRow(SmallButton("Vorschau Umlauf", () => host.Glow(Ui.Accent, "orbit")),
                                             SmallButton("Vorschau Standard", () => host.Glow(Ui.Accent))));
            root.Children.Add(Card(orbitCard));
            root.Children.Add(Footer("Komet: heller Kopf mit weichem Schweif. Streifen: gleichmäßig hell. " +
                                     "Weich auslaufen: bremst ab und verglüht. Rahmen schließen: der Rand leuchtet kurz auf und verblasst."));

            // ---------- Pro Ereignis: eine Karte je Ereignis ----------
            root.Children.Add(Header("Pro Ereignis"));
            root.Children.Add(Footer("App: Die Island wird länger und zeigt groß das App-Symbol mit Inhalt. Licht: die Licht-Effekte. " +
                                     "Hüpfen: kleiner Federhüpfer. Alles lässt sich kombinieren. Farbpunkt antippen = eigene Farbe, ▶ = testen."));
            foreach (var (key, name, color) in IslandWindow.AnimEvents)
            {
                var k = key; var c = color;
                string Mode() => s.AnimModes.TryGetValue(k, out var m) ? m : "off";
                HashSet<string> Over() => s.GlowStyles.TryGetValue(k, out var g) ? SplitSet(g) : new HashSet<string>();

                var card = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
                // Kopf: Farbpunkt, Name, Zusammenfassung, Testen
                var head = new Grid { MinHeight = 48, Margin = new Thickness(16, 0, 10, 0) };
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var dot = new Border
                {
                    Width = 18, Height = 18, CornerRadius = new CornerRadius(9),
                    Background = Ui.Br(host.EventColor(k, c)), BorderBrush = Ui.Br(Colors.White, 90), BorderThickness = new Thickness(1.5),
                    Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand, ToolTip = "Farbe ändern"
                };
                dot.MouseLeftButtonUp += (o, e) =>
                {
                    var cm = new ContextMenu();
                    void Pick(string hex)
                    {
                        s.AnimColors ??= new Dictionary<string, string>();
                        if (hex == null) s.AnimColors.Remove(k); else s.AnimColors[k] = hex;
                        s.Save();
                        dot.Background = Ui.Br(host.EventColor(k, c));
                        host.TestEvent(k, c);
                    }
                    var std = new MenuItem { Header = "Standardfarbe" };
                    std.Click += (o2, e2) => Pick(null);
                    cm.Items.Add(std);
                    cm.Items.Add(new Separator());
                    foreach (var (hex, label) in AnimPalette)
                    {
                        var h = hex;
                        CatStyle.TryColor(h, out var col);
                        var mi = new MenuItem { Header = label, Icon = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = Ui.Br(col) } };
                        mi.Click += (o2, e2) => Pick(h);
                        cm.Items.Add(mi);
                    }
                    cm.PlacementTarget = dot;
                    cm.IsOpen = true;
                };
                var title = Ui.HStack(0, dot, Ui.Text(name, 14, Ui.White, true));
                title.VerticalAlignment = VerticalAlignment.Center;
                var summary = Ui.Text(DescribeAnim(IslandWindow.ParseAnim(Mode())), 12.5, Secondary, false);
                summary.VerticalAlignment = VerticalAlignment.Center;
                summary.Margin = new Thickness(0, 0, 10, 0);
                var test = Ui.RoundButton("", 12, () => host.TestEvent(k, c), 30, FieldBrush, Ui.White);
                test.ToolTip = "Testen";
                test.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(summary, 1); Grid.SetColumn(test, 2);
                head.Children.Add(title); head.Children.Add(summary); head.Children.Add(test);
                card.Children.Add(head);

                // Animation: App / Licht / Hüpfen
                var animChips = new ChipSet(AnimPartDefs, p => IslandWindow.ParseAnim(Mode()).Contains(p), p =>
                {
                    var set = IslandWindow.ParseAnim(Mode());
                    if (!set.Remove(p)) set.Add(p);
                    s.AnimModes[k] = set.Count == 0 ? "off" : JoinSet(AnimPartDefs, set);
                    s.Save();
                    if (set.Count > 0) host.TestEvent(k, c);
                });
                card.Children.Add(ChipLine("Animation", animChips, 44));

                // Licht: Standard oder eigene Effekte (nur sichtbar, wenn Licht an ist)
                var lightDefs = new[] { ("", "Standard") }.Concat(GlowDefs).ToArray();
                var lightChips = new ChipSet(lightDefs, g => g == "" ? Over().Count == 0 : Over().Contains(g), g =>
                {
                    if (g == "") s.GlowStyles.Remove(k);
                    else
                    {
                        var set = Over();
                        if (!set.Remove(g)) set.Add(g);
                        if (set.Count == 0) s.GlowStyles.Remove(k); else s.GlowStyles[k] = JoinSet(GlowDefs, set);
                    }
                    s.Save();
                    host.TestEvent(k, c);
                });
                var lightLine = ChipLine("Licht", lightChips, 44);
                card.Children.Add(lightLine);
                void Sync()
                {
                    var parts = IslandWindow.ParseAnim(Mode());
                    summary.Text = DescribeAnim(parts);
                    lightLine.Visibility = parts.Contains("glow") ? Visibility.Visible : Visibility.Collapsed;
                }
                animChips.Changed = Sync;
                Sync();
                card.Children.Add(new Border { Height = 6 });
                var cardBox = Card(card);
                cardBox.Margin = new Thickness(cardBox.Margin.Left, cardBox.Margin.Top, cardBox.Margin.Right, cardBox.Margin.Bottom + 8);
                root.Children.Add(cardBox);
            }

            // ---------- Farben pro App / Gerät ----------
            s.AppColors ??= new Dictionary<string, string>();
            s.DeviceColors ??= new Dictionary<string, string>();
            s.SeenApps ??= new List<string>();
            s.SeenDevices ??= new List<string>();
            root.Children.Add(Header("Farbe pro App"));
            root.Children.Add(Card(NamedColorList(s.SeenApps, s.AppColors, "App-Name, z. B. WhatsApp", "message")));
            root.Children.Add(Footer("Gilt für Nachrichten dieser App: Licht und Rand der Mitteilung. Apps erscheinen hier, sobald von ihnen eine Nachricht kam, " +
                                     "oder du trägst sie selbst ein. Ohne eigene Farbe gilt die Farbe von „Neue Nachricht“."));
            root.Children.Add(Header("Farbe pro Gerät"));
            root.Children.Add(Card(NamedColorList(s.SeenDevices, s.DeviceColors, "Gerätename, z. B. soundcore Q20i", "device")));
            root.Children.Add(Footer("Gilt, wenn sich das Gerät verbindet: Licht und Symbol. Dazu zählen Bluetooth-Geräte, Controller und USB-Sticks " +
                                     "(Name = Name des Laufwerks). Ohne eigene Farbe gilt die Farbe von „Gerät verbunden“."));

            root.Children.Add(Header("Schnell einstellen"));
            var quick = new StackPanel();
            quick.Children.Add(ButtonRow(
                SmallButton("Alle: App + Licht", () => SetAllAnim("app,glow")),
                SmallButton("Alle: App + Licht + Hüpfen", () => SetAllAnim("app,glow,bounce")),
                SmallButton("Alle: nur Licht", () => SetAllAnim("glow")),
                SmallButton("Alle aus", () => SetAllAnim("off"), true)));
            root.Children.Add(Card(quick));
        }

        /// <summary>Liste mit Farbpunkt pro App bzw. Gerät, dazu ein Feld zum selbst Eintragen.</summary>
        FrameworkElement NamedColorList(List<string> seen, Dictionary<string, string> map, string hint, string evt)
        {
            var list = new StackPanel();
            // eingetragene Farben auch dann zeigen, wenn der Name (noch) nicht in der Liste steht
            var names = seen.Concat(map.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
            var fallback = host.EventColor(evt, evt == "device" ? Ui.Purple : Ui.Blue);
            foreach (var name in names)
            {
                var n = name;
                var custom = IslandWindow.NamedColor(map, n);
                var row = new Grid { MinHeight = 46, Margin = new Thickness(16, 0, 10, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var dot = new Border
                {
                    Width = 18, Height = 18, CornerRadius = new CornerRadius(9),
                    Background = Ui.Br(custom ?? fallback), BorderBrush = Ui.Br(Colors.White, (byte)(custom.HasValue ? 160 : 60)),
                    BorderThickness = new Thickness(1.5), Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "Farbe ändern"
                };
                dot.MouseLeftButtonUp += (o, e) =>
                {
                    var cm = new ContextMenu();
                    void Pick(string hex)
                    {
                        map.Keys.Where(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase)).ToList().ForEach(k => map.Remove(k));
                        if (hex != null) map[n] = hex;
                        s.Save();
                        if (hex != null && CatStyle.TryColor(hex, out var col)) host.Glow(col, host.GlowStyleFor(evt));
                        Later(() => ShowPage("anim"));
                    }
                    var std = new MenuItem { Header = "Standardfarbe" };
                    std.Click += (o2, e2) => Pick(null);
                    cm.Items.Add(std);
                    cm.Items.Add(new Separator());
                    foreach (var (hex, label) in AnimPalette)
                    {
                        var h = hex;
                        CatStyle.TryColor(h, out var col);
                        var mi = new MenuItem { Header = label, Icon = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = Ui.Br(col) } };
                        mi.Click += (o2, e2) => Pick(h);
                        cm.Items.Add(mi);
                    }
                    cm.PlacementTarget = dot;
                    cm.IsOpen = true;
                };
                var label = Ui.HStack(0, dot, Ui.Text(n, 14, Ui.White, false),
                    Ui.Text(custom.HasValue ? "" : "  Standard", 12, Ui.Dim, false));
                label.VerticalAlignment = VerticalAlignment.Center;
                var test = Ui.RoundButton("", 12, () => host.Glow(IslandWindow.NamedColor(map, n) ?? fallback, host.GlowStyleFor(evt)), 30, FieldBrush, Ui.White);
                test.ToolTip = "Testen";
                test.VerticalAlignment = VerticalAlignment.Center;
                var del = Ui.RoundButton("", 11, () =>
                {
                    seen.RemoveAll(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase));
                    map.Keys.Where(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase)).ToList().ForEach(k => map.Remove(k));
                    s.Save();
                    Later(() => ShowPage("anim"));
                }, 30, FieldBrush, Ui.Dim);
                del.ToolTip = "Aus der Liste entfernen";
                del.Margin = new Thickness(6, 0, 0, 0);
                del.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(test, 1); Grid.SetColumn(del, 2);
                row.Children.Add(label); row.Children.Add(test); row.Children.Add(del);
                list.Children.Add(row);
                list.Children.Add(Divider());
            }
            // selbst eintragen
            var box = Field("", 14, false);
            box.ToolTip = hint;
            var placeholder = Ui.Text(hint, 14, Ui.Dim, false);
            placeholder.IsHitTestVisible = false;
            placeholder.Margin = new Thickness(2, 0, 0, 0);
            box.TextChanged += (o, e) => placeholder.Visibility = box.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
            var boxGrid = new Grid();
            boxGrid.Children.Add(placeholder);
            boxGrid.Children.Add(box);
            void AddName()
            {
                var t = box.Text.Trim();
                if (t.Length == 0) return;
                if (!seen.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase))) seen.Add(t);
                s.Save();
                Later(() => ShowPage("anim"));
            }
            box.KeyDown += (o, e) => { if (e.Key == Key.Enter) AddName(); };
            var add = new Grid { Margin = new Thickness(16, 8, 12, 8) };
            add.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            add.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var wrap = FieldWrap(new ContentControl { Content = boxGrid, Focusable = false });
            var btn = SmallButton("Hinzufügen", AddName);
            btn.Margin = new Thickness(8, 0, 0, 0);
            btn.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(btn, 1);
            add.Children.Add(wrap); add.Children.Add(btn);
            list.Children.Add(add);
            return list;
        }

        void MovePrio(int i, int dir)
        {
            var l = s.AlertPriority;
            int j = i + dir;
            if (l == null || j < 0 || j >= l.Count) return;
            (l[i], l[j]) = (l[j], l[i]);
            s.Save();
            Later(() => ShowPage("alerts"));
        }

        void SetAllAnim(string mode)
        {
            s.AnimModes ??= new Dictionary<string, string>();
            foreach (var (key, _, _) in IslandWindow.AnimEvents) s.AnimModes[key] = mode;
            s.Save();
            Later(() => ShowPage("anim"));
        }

        // =================================================================
        //  Wetter
        // =================================================================
        /// <summary>Seite der Island an- oder abschalten (neue Seiten kommen direkt hinter die Übersicht).</summary>
        void SetIslandPage(string key, bool on)
        {
            s.Pages ??= new List<string> { "dashboard" };
            if (on && !s.Pages.Contains(key))
            {
                int i = s.Pages.IndexOf("dashboard");
                s.Pages.Insert(i >= 0 ? i + 1 : 0, key);
            }
            else if (!on) s.Pages.Remove(key);
            s.Save();
            host.ApplySettings();
        }

        void BuildWeather(StackPanel root)
        {
            root.Children.Add(Header("In der Island"));
            var vis = new StackPanel();
            vis.Children.Add(ToggleRow("Eigene Seite „Wetter“", s.Pages?.Contains("weather") == true, v => SetIslandPage("weather", v), out _));
            vis.Children.Add(Divider());
            vis.Children.Add(ToggleRow("In der kleinen Ansicht neben der Uhr", s.WeatherEnabled, v => { s.WeatherEnabled = v; Apply(); }, out _));
            root.Children.Add(Card(vis));

            root.Children.Add(Header("Ort"));
            var g = new StackPanel();
            var city = Field(s.WeatherCity ?? "", 14, false);
            city.MaxLength = 60;
            g.Children.Add(PlainField("Stadt", city));
            g.Children.Add(Divider());
            g.Children.Add(ToggleRow("Ohne Stadt: Windows-Standort benutzen", s.WeatherUseLocation, v => { s.WeatherUseLocation = v; s.Save(); }, out _));
            g.Children.Add(Divider());
            var w = host.Weather;
            var status = StatusText(w.HasData ? $"{w.Place}: {WeatherService.Deg(w.Temp)}, {WeatherService.Text(w.Code)} (Stand {w.LastUpdate:HH:mm})"
                                              : string.IsNullOrEmpty(w.Error) ? "Noch nicht geladen." : w.Error);
            g.Children.Add(status);
            g.Children.Add(Divider());
            g.Children.Add(ButtonRow(FilledSmall("Übernehmen & laden", async () =>
            {
                s.WeatherCity = city.Text.Trim();
                s.Save();
                status.Text = "Lädt …";
                await w.RefreshAsync(true);
                status.Text = w.HasData && string.IsNullOrEmpty(w.Error)
                    ? $"{w.Place}: {WeatherService.Deg(w.Temp)}, {WeatherService.Text(w.Code)}"
                    : w.Error;
            })));
            root.Children.Add(Card(g));
            root.Children.Add(Footer("Stadt leer + Schalter an = Windows-Standort (Windows fragt einmal nach Erlaubnis). Das Wetter kommt kostenlos von Open-Meteo; " +
                                     "dafür wird nur der Ort (bzw. die ungefähren Koordinaten) übertragen. Aktualisiert sich alle 15 Minuten."));
        }

        // =================================================================
        //  Claude Code
        // =================================================================
        void BuildClaude(StackPanel root)
        {
            root.Children.Add(Header("Anzeige"));
            var g = new StackPanel();
            g.Children.Add(ToggleRow("Claude in der Island zeigen", s.ClaudeEnabled, v => { s.ClaudeEnabled = v; Apply(); }, out _));
            g.Children.Add(Divider());
            bool installed = ClaudeTracker.HooksInstalled();
            var last = host.Claude.LastEvent;
            var status = StatusText(!installed ? "Noch nicht mit Claude Code verbunden."
                : last == default ? "Verbunden. Sobald Claude arbeitet, erscheint es oben in der Island."
                : "Verbunden – letztes Signal um " + last.ToString("HH:mm:ss") + ".");
            g.Children.Add(status);
            g.Children.Add(Divider());
            g.Children.Add(ButtonRow(
                FilledSmall(installed ? "Neu einrichten" : "Mit Claude Code verbinden", () =>
                {
                    var err = ClaudeTracker.InstallHooks();
                    status.Text = err == null ? "Verbunden ✓ – gilt ab der nächsten Nachricht an Claude." : "Fehler: " + err;
                }),
                SmallButton("Verbindung trennen", () =>
                {
                    var err = ClaudeTracker.RemoveHooks();
                    status.Text = err == null ? "Getrennt." : "Fehler: " + err;
                }, true)));
            root.Children.Add(Card(g));
            root.Children.Add(Footer("„Verbinden“ trägt in Claude Code (Datei .claude\\settings.json in deinem Benutzerordner) kleine Hooks ein. " +
                                     "Bei jedem Schritt schickt Claude damit eine Nachricht an die Island – nur auf deinem PC (localhost), nichts geht ins Internet. " +
                                     "Die Pille zeigt, was Claude gerade tut (z. B. „Bearbeitet Cats.cs“) und wie lange die Aufgabe schon läuft. " +
                                     "Antippen zeigt Details: Dauer des aktuellen Schritts, Anzahl der Aktionen und deine Nachricht. " +
                                     "Gelb = Claude wartet auf dich, grün = fertig."));
            root.Children.Add(Header("Nutzungs-Limits"));
            var lc = new StackPanel();
            lc.Children.Add(ToggleRow("Warnen bei 80 % und 95 %", s.ClaudeLimitWarn, v => { s.ClaudeLimitWarn = v; Apply(); }, out _));
            lc.Children.Add(Divider());
            lc.Children.Add(StatusText(ClaudeTracker.LimitsLine()
                ?? (ClaudeTracker.LimitsInstalled() ? "Eingerichtet – die Werte kommen mit der nächsten Antwort von Claude Code im Terminal. Die Claude-App selbst schickt keine Limits."
                    : ClaudeTracker.ForeignStatusLine() ? "Du hast schon eine eigene Statuszeile in Claude Code. Häng dort an: curl -s -m 1 --data-binary @- http://localhost:7843/claude-limits"
                    : "Die Statuszeile für die Limits fehlt noch – oben „Neu einrichten“ klicken.")));
            lc.Children.Add(Divider());
            lc.Children.Add(ButtonRow(SmallButton("In der Island zeigen", () => host.ShowClaudeLimits())));
            root.Children.Add(Card(lc));
            root.Children.Add(Footer("Die Limits (5 Stunden und Woche, nur mit Pro/Max-Abo) schickt Claude Code über seine Statuszeile – „Verbinden“ trägt sie mit ein, falls du noch keine eigene hast. " +
                                     "Sie stehen auch in den Details der Claude-Pille und auf der Kachel „Claude-Limits“. Mit der Kachel „Claude diktieren“ sprichst du deine Nachricht an Claude (Windows-Spracheingabe, Win+H)."));
        }

        // =================================================================
        //  Katzen
        // =================================================================
        void BuildCats(StackPanel root)
        {
            root.Children.Add(Header("Anzeigen"));
            var show = new StackPanel();
            var modes = new[] { "expanded", "always", "off" };
            int mode = Math.Max(0, Array.IndexOf(modes, s.CatsMode));
            show.Children.Add(SegmentRow("Katzen zeigen", new[] { "In der Island", "Daneben", "Aus" }, mode, i => { s.CatsMode = modes[i]; Changed(); }));
            show.Children.Add(Divider());
            show.Children.Add(ToggleRow("Bei „Daneben“: sichtbar bleiben, wenn die Island wegfährt", s.CatsStayVisible, v => { s.CatsStayVisible = v; Changed(); }, out _));
            root.Children.Add(Card(show));
            root.Children.Add(Footer("„In der Island“: Sobald du die Island groß machst, sitzen deine Katzen seitlich in der Island auf ihrem Kratzbaum. " +
                                     "„Daneben“: Sie sitzen immer rechts neben der Island."));

            root.Children.Add(Header("Verhalten"));
            var act = new StackPanel();
            act.Children.Add(ToggleRow("Im Schlaf zusammenrollen", s.CatsCurl, v => { s.CatsCurl = v; Changed(); }, out _));
            act.Children.Add(Divider());
            act.Children.Add(ToggleRow("Herumlaufen", s.CatsWalk, v => { s.CatsWalk = v; Changed(); }, out _));
            act.Children.Add(Divider());
            var moodNow = Ui.Text("", 12, Ui.Dim, false);
            void ShowMoodNow()
            {
                var now = DateTime.Now;
                var (_, why) = CatView.WeatherEnergy();
                string m = CatView.MoodFor("calm", now);
                moodNow.Text = s.CatsMoodAuto
                    ? "Gerade: " + CatView.TimeName(now) + (why != null ? ", " + why : "") + " → " + (m == "playful" ? "eher verspielt" : m == "sleepy" ? "eher verschlafen" : "ruhig")
                    : "Aus – jede Katze bleibt bei ihrem Charakter";
            }
            act.Children.Add(ToggleRow("Laune nach Tageszeit und Wetter", s.CatsMoodAuto, v => { s.CatsMoodAuto = v; Changed(); ShowMoodNow(); }, out _));
            ShowMoodNow();
            moodNow.Margin = new Thickness(16, 0, 16, 10);
            act.Children.Add(moodNow);
            root.Children.Add(Card(act));
            root.Children.Add(Footer("Eingerollt schlafen sie mit dem Schwanz um den Körper und strecken sich beim Aufwachen. " +
                                     "Beim Herumlaufen trippeln sie ab und zu hin und her (neben der Island oder unten am Kratzbaum) und wechseln am Kratzbaum die Plätze – verspielte öfter, verschlafene seltener. " +
                                     "Mit Laune nach Tageszeit und Wetter sind sie nachts und mittags verschlafen, nachmittags und bei Sonne verspielt, bei Regen, Gewitter oder Hitze eher müde."));

            root.Children.Add(Header("Deine Katzen"));
            catList = new StackPanel();
            root.Children.Add(Card(catList));
            root.Children.Add(Footer("Bis zu 4 Katzen. Sie blinzeln, wedeln mit dem Schwanz, schlafen ein, wenn nichts los ist, und schnurren, wenn du sie anklickst. " +
                                     "Mit einem Foto wird es deine eigene Katze."));
            BuildCatRows();
        }

        // =================================================================
        //  Kurzbefehle
        // =================================================================
        void BuildShortcuts(StackPanel root)
        {
            root.Children.Add(Header("Deine Kurzbefehle"));
            shortcutList = new StackPanel();
            root.Children.Add(Card(shortcutList));
            root.Children.Add(Footer($"Öffnen: Übersicht → Kurzbefehle oder Strg + Alt + K. Die ersten {Shortcuts.MaxShown} werden angezeigt. " +
                                     "Ziel kann ein Programm, ein Ordner, eine Datei oder eine Webseite (https://…) sein."));
            BuildShortcutRows();
        }

        // =================================================================
        //  Spotify
        // =================================================================
        void BuildSpotify(StackPanel root)
        {
            root.Children.Add(Header("Verbindung"));
            var sp = new StackPanel();
            sp.Children.Add(CopyRow("Redirect URI", SpotifyService.RedirectUri));
            sp.Children.Add(Divider());
            spotifyIdBox = Field(s.SpotifyClientId ?? "", 14, false);
            sp.Children.Add(PlainField("Client-ID", spotifyIdBox));
            sp.Children.Add(Divider());
            spotifyStatus = StatusText(host.Spotify.Connected && host.Spotify.Status == "Nicht verbunden" ? "Verbunden" : host.Spotify.Status);
            sp.Children.Add(spotifyStatus);
            sp.Children.Add(Divider());
            sp.Children.Add(ButtonRow(
                FilledSmall(host.Spotify.Connected ? "Neu verbinden" : "Verbinden", async () =>
                {
                    SaveSpotifyId();
                    if (await host.Spotify.LoginAsync()) Later(() => { if (page == "spotify") ShowPage("spotify"); });
                }),
                SmallButton("Developer Dashboard", () => IslandWindow.Open("https://developer.spotify.com/dashboard")),
                SmallButton("Abmelden", () => { host.Spotify.Logout(); Later(() => ShowPage("spotify")); }, true)));
            root.Children.Add(Card(sp));

            root.Children.Add(Header("Aussehen"));
            root.Children.Add(Card(ColorRow("Farbe des Players", SpotifyPalette, s.SpotifyColor ?? "#1ED760",
                v => { s.SpotifyColor = v; Apply(); Later(() => ShowPage("spotify")); })));
            root.Children.Add(Footer("Färbt Spul- und Lautstärkeregler, Herz, Zufall/Wiederholen und die Geräteliste im Spotify-Player."));

            root.Children.Add(Header("Einrichtung"));
            root.Children.Add(Footer(
                "Einmalig einrichten (ca. 2 Minuten):\n" +
                "1. „Developer Dashboard“ öffnen, mit deinem Spotify-Konto anmelden → „Create app“.\n" +
                "2. „App name“: z. B. Island. „App description“: z. B. Dynamic Island. „Website“: leer lassen.\n" +
                "3. „Redirect URIs“: oben bei „Redirect URI“ auf „Kopieren“ klicken, dort einfügen und auf „Add“ klicken. " +
                "Das ist keine Webseite – nur die Adresse, an die Spotify nach dem Anmelden zurück zur Island schickt.\n" +
                "4. Bei „Which API/SDKs are you planning to use?“ „Web API“ anhaken, Bedingungen bestätigen, „Save“.\n" +
                "5. In der neuen App auf „Settings“ → „Client ID“ kopieren, oben einfügen und „Verbinden“ klicken. Im Browser zustimmen.\n\n" +
                "Danach zeigt die Island den eingebauten Spotify-Player – auch wenn die Musik auf Handy oder Lautsprecher läuft. " +
                "Liken und Anzeigen gehen mit jedem Konto. Steuern (Play/Pause, Geräte, Playlists starten) erlaubt Spotify nur mit Premium. " +
                "Es wird kein Passwort gespeichert, nur eine Anmeldung auf diesem PC."));
        }

        static readonly string[] AccentPalette =
            { "#0A84FF", "#5E5CE6", "#BF5AF2", "#FF375F", "#FF453A", "#FF9F0A", "#FFD60A", "#30D158", "#63E6E2", "#40C8E0", "#8E8E93", "#F5F5F7" };
        static readonly string[] SpotifyPalette =
            { "#1ED760", "#30D158", "#63E6E2", "#0A84FF", "#5E5CE6", "#BF5AF2", "#FF375F", "#FF453A", "#FF9F0A", "#FFD60A", "#F5F5F7" };

        /// <summary>Zeile mit Titel und Auswahl-Kapseln darunter (bricht um).</summary>
        static FrameworkElement ChipRow(string label, string[] options, int selected, Action<int> pick)
        {
            var sp = new StackPanel { Margin = new Thickness(16, 12, 12, 6) };
            sp.Children.Add(Ui.Text(label, 14.5, Ui.White, false));
            var chips = Chips(options, selected, pick);
            ((FrameworkElement)chips).Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(chips);
            return sp;
        }

        /// <summary>Zeile mit Titel und runden Farbfeldern darunter.</summary>
        static FrameworkElement ColorRow(string label, string[] colors, string current, Action<string> pick)
        {
            var sp = new StackPanel { Margin = new Thickness(16, 12, 12, 6) };
            sp.Children.Add(Ui.Text(label, 14.5, Ui.White, false));
            var sw = Swatches(colors, current, false, pick);
            ((FrameworkElement)sw).Margin = new Thickness(-4, 8, 0, 0);
            sp.Children.Add(sw);
            return sp;
        }

        // =================================================================
        //  Seiten & Widgets
        // =================================================================
        static readonly (string Key, string Name, string Glyph)[] PageCatalog =
        {
            ("player", "Player", "\uE8D6"), ("sound", "Ton", "\uE767"), ("brightness", "Helligkeit", "\uE706"),
            ("clipboard", "Zwischenablage", "\uE77F"), ("apps", "Apps", "\uE7C4"), ("shortcuts", "Kurzbefehle", "\uE8A9"),
            ("downloads", "Downloads", "\uE896"), ("bluetooth", "Bluetooth / AirPods", "\uE702"), ("ai", "Gemini", "\uE8BD"), ("weather", "Wetter", WeatherActivity.Glyph), ("calendar", "Kalender", "\uE787"), ("todo", "To Do", "\uE73A"),
        };

        (string name, string glyph) PageInfo(string key)
        {
            if (key == "dashboard") return ("Übersicht", "\uE80F");
            if (key.StartsWith("w:"))
            {
                var wp = s.WidgetPages.FirstOrDefault(p => "w:" + p.Id == key);
                return (wp?.Name ?? "Widgets", "\uF0E2");
            }
            foreach (var c in PageCatalog) if (c.Key == key) return (c.Name, c.Glyph);
            return (key, "\uE8A5");
        }

        void PagesChanged()
        {
            s.Save();
            host.ApplySettings();
            Later(() => ShowPage("pages"));
        }

        void BuildPages(StackPanel root)
        {
            s.Pages ??= new List<string> { "dashboard" };
            s.WidgetPages ??= new List<WidgetPageConfig>();

            root.Children.Add(Header("Deine Seiten"));
            var list = new StackPanel();
            for (int i = 0; i < s.Pages.Count; i++)
            {
                if (list.Children.Count > 0) list.Children.Add(Divider());
                list.Children.Add(PageRow(s.Pages[i], i));
            }
            root.Children.Add(Card(list));
            root.Children.Add(Footer("In der großen Island wischst du seitlich durch diese Seiten – in dieser Reihenfolge. Die Übersicht bleibt immer vorne. " +
                                     "Player und AirPods erscheinen nur, wenn etwas läuft bzw. verbunden ist; laufende Timer oder Anrufe kommen automatisch dazu."));

            root.Children.Add(Header("Seite hinzufügen"));
            var add = new WrapPanel { Margin = new Thickness(12, 12, 12, 6) };
            foreach (var c in PageCatalog)
            {
                var key = c.Key;
                if (s.Pages.Contains(key)) continue;
                add.Children.Add(Pill(c.Name, false, () => { s.Pages.Add(key); PagesChanged(); }));
            }
            add.Children.Add(Pill("+ Neue Widget-Seite", true, () =>
            {
                int n = 1;
                while (s.WidgetPages.Any(p => p.Id == n.ToString())) n++;
                var id = n.ToString();
                s.WidgetPages.Add(new WidgetPageConfig { Id = id, Name = "Widgets " + id, Widgets = new List<string> { "clock", "battery" } });
                s.Pages.Add("w:" + id);
                PagesChanged();
            }));
            root.Children.Add(Card(add));

            foreach (var wp in s.WidgetPages.ToList())
            {
                var page = wp;
                root.Children.Add(Header("Widget-Seite · " + page.Name));
                var c = new StackPanel();
                var nameBox = Field(page.Name, 14, false);
                nameBox.MaxLength = 24;
                nameBox.LostFocus += (o, e) =>
                {
                    var t = nameBox.Text.Trim();
                    if (t.Length == 0 || t == page.Name) return;
                    page.Name = t;
                    PagesChanged();
                };
                c.Children.Add(PlainField("Name", nameBox));
                c.Children.Add(Divider());

                var on = new WrapPanel { Margin = new Thickness(12, 12, 12, 6) };
                if (page.Widgets.Count == 0) on.Children.Add(new TextBlock { Text = "Noch keine Widgets", Foreground = Secondary, FontSize = 13, Margin = new Thickness(4, 4, 0, 10) });
                foreach (var key in page.Widgets.ToList())
                {
                    var info = Widgets.Get(key);
                    if (info == null) continue;
                    var k = key;
                    on.Children.Add(Pill(info.Name + "  ✕", true, () => { page.Widgets.Remove(k); PagesChanged(); }));
                }
                c.Children.Add(on);
                c.Children.Add(Divider());

                if (page.Widgets.Count < Widgets.MaxPerPage)
                {
                    var avail = new WrapPanel { Margin = new Thickness(12, 12, 12, 6) };
                    foreach (var info in Widgets.All)
                    {
                        if (page.Widgets.Contains(info.Key)) continue;
                        var k = info.Key;
                        avail.Children.Add(Pill("+ " + info.Name, false, () => { page.Widgets.Add(k); PagesChanged(); }));
                    }
                    c.Children.Add(avail);
                }
                else c.Children.Add(StatusText($"Die Seite ist voll ({Widgets.MaxPerPage} Widgets). Entferne eins, um ein anderes hinzuzufügen."));
                root.Children.Add(Card(c));
            }
            if (s.WidgetPages.Count > 0)
                root.Children.Add(Footer("Bis zu 4 Widgets pro Seite (2 × 2). Oben antippen = Widget entfernen, unten antippen = hinzufügen. " +
                                         "Musik, AirPods, Speicher, Zwischenablage und Katze reagieren auch aufs Antippen in der Island."));
        }

        FrameworkElement PageRow(string key, int index)
        {
            var (name, glyph) = PageInfo(key);
            var g = new Grid { MinHeight = 46, Margin = new Thickness(16, 0, 10, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = Ui.Glyph(glyph, 15, BlueBrush);
            var t = Ui.Text(name, 14.5, Ui.White, false);
            t.Margin = new Thickness(12, 0, 8, 0);
            Grid.SetColumn(t, 1);
            g.Children.Add(icon);
            g.Children.Add(t);
            if (key == "dashboard")
            {
                var fixedT = Ui.Text("immer vorne", 12.5, Secondary, false);
                fixedT.VerticalAlignment = VerticalAlignment.Center;
                var fx = Ui.HStack(8, fixedT, LookButton(key));
                fx.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(fx, 2);
                g.Children.Add(fx);
                return WithLook(key, g);
            }
            var up = Ui.RoundButton("\uE70E", 12, () => Move(index, -1), 30, Brushes.Transparent, index > 1 ? Ui.White : Secondary);
            up.ToolTip = "Nach vorne";
            var down = Ui.RoundButton("\uE70D", 12, () => Move(index, 1), 30, Brushes.Transparent, index < s.Pages.Count - 1 ? Ui.White : Secondary);
            down.ToolTip = "Nach hinten";
            var del = Ui.RoundButton("\uE711", 12, () =>
            {
                s.Pages.RemoveAt(index);
                if (key.StartsWith("w:")) s.WidgetPages.RemoveAll(p => "w:" + p.Id == key);   // komplett entfernen
                PagesChanged();
            }, 30, Ui.Br(Ui.Red, 45), RedBrush);
            del.ToolTip = "Seite entfernen";
            var btns = Ui.HStack(4, LookButton(key), up, down, del);
            btns.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(btns, 2);
            g.Children.Add(btns);
            return WithLook(key, g);
        }

        // ---- eigene Schrift / eigener Stil pro Seite ----
        readonly HashSet<string> openLooks = new HashSet<string>();

        FrameworkElement LookButton(string key)
        {
            bool own = s.PageLooks != null && s.PageLooks.ContainsKey(key);
            var b = Ui.RoundButton("", 12.5, () => { if (!openLooks.Remove(key)) openLooks.Add(key); Later(() => ShowPage("pages")); }, 30,
                                   openLooks.Contains(key) ? Ui.Br(Colors.White, 30) : Brushes.Transparent, own ? BlueBrush : Ui.White);
            b.ToolTip = own ? "Eigene Schrift / eigener Stil (geändert)" : "Schrift und Stil dieser Seite";
            return b;
        }

        FrameworkElement WithLook(string key, FrameworkElement row)
        {
            if (!openLooks.Contains(key)) return row;
            s.PageLooks ??= new Dictionary<string, PageLook>();
            s.PageLooks.TryGetValue(key, out var l);
            var sp = new StackPanel();
            sp.Children.Add(row);
            var box = new StackPanel { Margin = new Thickness(24, 0, 0, 8) };
            int fi = l == null || l.Font <= 0 ? 0 : Nearest(PageFonts, l.Font) + 1;
            box.Children.Add(ChipRow("Schrift", new[] { "Wie alle" }.Concat(PageFontNames).ToArray(), fi, i => SetLook(key, i == 0 ? 0 : PageFonts[i - 1], null)));
            int si = l == null || string.IsNullOrEmpty(l.Style) ? 0 : l.Style == "full" ? 2 : 1;
            box.Children.Add(ChipRow("Stil", new[] { "Wie alle", "Aufgeräumt", "Ausführlich" }, si, i => SetLook(key, null, i == 0 ? "" : i == 1 ? "clean" : "full")));
            sp.Children.Add(box);
            return sp;
        }

        void SetLook(string key, double? font, string style)
        {
            s.PageLooks ??= new Dictionary<string, PageLook>();
            if (!s.PageLooks.TryGetValue(key, out var l)) s.PageLooks[key] = l = new PageLook();
            if (font != null) l.Font = font.Value;
            if (style != null) l.Style = style == "" ? null : style;
            if (l.Font <= 0 && string.IsNullOrEmpty(l.Style)) s.PageLooks.Remove(key);
            Apply();
            host.RefreshPage();
            Later(() => ShowPage("pages"));
        }

        void Move(int index, int dir)
        {
            int j = index + dir;
            if (j < 1 || j >= s.Pages.Count) return;   // die Übersicht (Platz 0) bleibt vorne
            (s.Pages[index], s.Pages[j]) = (s.Pages[j], s.Pages[index]);
            PagesChanged();
        }

        /// <summary>Kleine Kapsel zum Antippen (hinzufügen / entfernen).</summary>
        static FrameworkElement Pill(string text, bool filled, Action click)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 5, 12, 6),
                Margin = new Thickness(0, 0, 6, 6),
                Background = filled ? BlueBrush : FieldBrush,
                Cursor = Cursors.Hand,
                Child = Ui.Text(text, 12.5, Ui.White, false)
            };
            b.MouseEnter += (o, e) => b.Opacity = 0.8;
            b.MouseLeave += (o, e) => b.Opacity = 1;
            b.MouseLeftButtonUp += (o, e) => click();
            return b;
        }

        // =================================================================
        //  AirPods (fest eingebaut, früher „AirPods Companion“)
        // =================================================================
        void BuildAirPods(StackPanel root)
        {
            var eng = host.AirPods;
            // Standard: normale Kopfhörer (auch AirPods) – Akku und Wiedergabegerät über Bluetooth
            root.Children.Add(Header("Kopfhörer"));
            var hp = new StackPanel();
            var phones = host.StandardHeadphones;
            hp.Children.Add(StatusText(phones == null ? "Keine Kopfhörer verbunden."
                : phones.Name + " verbunden" + (phones.Battery.HasValue ? " · Akku " + phones.Battery + " %" : "")));
            hp.Children.Add(Divider());
            hp.Children.Add(ButtonRow(FilledSmall("Kopfhörer-Seite öffnen", () => host.ShowBluetooth())));
            root.Children.Add(Card(hp));
            root.Children.Add(Footer("Verbundene Kopfhörer erscheinen mit Akku in der Island. Beim Verbinden läuft die Animation von „Gerät verbunden“ " +
                                     "(Einstellungen → Animationen) in der Farbe, die du dem Gerät dort gibst."));

            root.Children.Add(Header("AirPods"));
            var c = new StackPanel();
            c.Children.Add(ToggleRow("AirPods-Funktionen (mit WPods)", s.AirPodsEnabled, v => { s.AirPodsEnabled = v; Apply(); Later(() => ShowPage("airpods")); }, out _));
            if (!s.AirPodsEnabled)
            {
                root.Children.Add(Card(c));
                root.Children.Add(Footer("Aus: AirPods zählen als normale Kopfhörer. An: eigene AirPods-Karte mit Akku von links, rechts und Case, " +
                                         "Ohrerkennung, automatisches Umschalten und – mit WPods – Geräuschkontrolle und Konversationserkennung."));
                return;
            }
            c.Children.Add(Divider());
            c.Children.Add(StatusText(AirPodsText()));
            c.Children.Add(Divider());
            c.Children.Add(ButtonRow(
                FilledSmall("Akku anzeigen", () => host.ShowAirPods()),
                SmallButton("Ton auf AirPods", async () => { if (eng != null) await eng.EnsureOutputAsync(); Later(() => { if (page == "airpods") ShowPage("airpods"); }); }),
                SmallButton("Debug-Log", () => new AirPodsDebugWindow(eng) { Owner = this }.Show())));
            root.Children.Add(Card(c));
            root.Children.Add(Footer("Startet automatisch mit der Island – ein eigenes Programm ist nicht mehr nötig. " +
                                     "Öffnest du das Case neben dem PC und verbinden sich die AirPods, zeigt die Island den Akku von links, rechts und Case. " +
                                     "Die AirPods müssen wie gewohnt in den Windows-Bluetooth-Einstellungen gekoppelt sein."));

            // Direkte Steuerung über den WPods-Dienst
            root.Children.Add(Header("Geräuschkontrolle & Konversation (über WPods)"));
            var w = new StackPanel();
            w.Children.Add(ToggleRow("Direkt mit den AirPods sprechen", s.AirPodsAap, v => { s.AirPodsAap = v; Apply(); Later(() => ShowPage("airpods")); }, out _));
            w.Children.Add(Divider());
            w.Children.Add(StatusText("Status: " + (eng?.Aap.StateText ?? "aus")));
            w.Children.Add(Divider());
            w.Children.Add(ButtonRow(
                FilledSmall("WPods herunterladen", () => IslandWindow.Open("https://github.com/FPNDev/wpods/releases/latest")),
                SmallButton("WPods-App schließen & übernehmen", () =>
                {
                    AapLink.CloseWPodsApp();
                    if (eng != null) { eng.Aap.Stop(); if (s.AirPodsEnabled && s.AirPodsAap) eng.Aap.Start(); }
                    Later(() => ShowPage("airpods"));
                }),
                SmallButton("Neu prüfen", () => Later(() => ShowPage("airpods")))));
            root.Children.Add(Card(w));
            root.Children.Add(Footer(
                "Damit schaltet die Island ANC, Transparenz, Adaptiv und Aus um, steuert die Konversationserkennung und zeigt den Akku auf 1 % genau. " +
                "Windows lässt normale Apps nicht an diesen AirPods-Kanal – das übernimmt der Dienst der freien App WPods (Open Source, GPL-3.0).\n\n" +
                "Einrichten: 1. Secure Boot im BIOS ausschalten (BitLocker-Schlüssel bereithalten). 2. WPods-Setup ausführen – es schaltet den Windows-Testmodus ein " +
                "und installiert Dienst und Treiber. 3. Neu starten. 4. In WPods einmal verbinden, danach im Tray-Menü von WPods „Start with Windows“ ausschalten: " +
                "Nur ein Programm kann den Kanal gleichzeitig nutzen, und das soll die Island sein (der WPods-Dienst läuft trotzdem).\n\n" +
                "Hinweis: Im Testmodus lädt Windows auch andere test-signierte Treiber, unten rechts steht „Testmodus“, und manche Spiele mit Anti-Cheat starten nicht. " +
                "Rückgängig: WPods deinstallieren und den Testmodus ausschalten lassen."));

            root.Children.Add(Header("Automatik"));
            var a = new StackPanel();
            a.Children.Add(ToggleRow("Pausieren beim Herausnehmen", s.AirPodsAutoPause, v => { s.AirPodsAutoPause = v; Apply(); }, out _));
            a.Children.Add(Divider());
            a.Children.Add(ToggleRow("Weiterspielen beim Einsetzen", s.AirPodsAutoResume, v => { s.AirPodsAutoResume = v; Apply(); }, out _));
            a.Children.Add(Divider());
            a.Children.Add(ToggleRow("Ton automatisch auf die AirPods legen", s.AirPodsAutoSwitch, v => { s.AirPodsAutoSwitch = v; Apply(); }, out _));
            a.Children.Add(Divider());
            a.Children.Add(ToggleRow("Auch für Anrufe (Teams, Discord)", s.AirPodsSwitchCommunications, v => { s.AirPodsSwitchCommunications = v; Apply(); }, out _));
            a.Children.Add(Divider());
            a.Children.Add(ToggleRow("Akku-Karte beim Öffnen des Case", s.AirPodsPopup, v => { s.AirPodsPopup = v; Apply(); }, out _));
            a.Children.Add(Divider());
            a.Children.Add(ToggleRow("Warnung bei schwachem Akku (20 % / 10 %)", s.AirPodsLowBattery, v => { s.AirPodsLowBattery = v; Apply(); }, out _));
            a.Children.Add(Divider());
            var ranges = new[] { -55, -65, -75 };
            int ri = s.AirPodsRssi >= -58 ? 0 : s.AirPodsRssi <= -70 ? 2 : 1;
            a.Children.Add(SegmentRow("Reichweite", new[] { "Nah", "Normal", "Weit" }, ri, i => { s.AirPodsRssi = ranges[i]; Apply(); }));
            a.Children.Add(Divider());
            var nameBox = Field(s.AirPodsNameFilter ?? "AirPods", 14, false);
            nameBox.LostFocus += (o, e) =>
            {
                var t = nameBox.Text.Trim();
                s.AirPodsNameFilter = t.Length == 0 ? "AirPods" : t;
                Apply();
            };
            a.Children.Add(PlainField("Name in Windows", nameBox));
            root.Children.Add(Card(a));
            root.Children.Add(Footer(
                "„Reichweite“: „Nah“ nimmt nur AirPods direkt am PC – wichtig, wenn andere AirPods in der Nähe sind. " +
                "„Name in Windows“: Teil des Namens, unter dem deine AirPods als Audiogerät heißen (z. B. wenn du sie umbenannt hast).\n\n" +
                "Pausieren passiert erst, wenn ein AirPod 1,5 Sekunden lang sicher draußen ist, und nur, wenn der Ton gerade über die AirPods läuft. " +
                "Pakete von fremden AirPods (anderer Akkustand) werden ignoriert." +
                (eng != null && eng.Aap.Connected ? "" :
                "\n\nOhne WPods-Verbindung (siehe oben) gehen Geräuschkontrolle, Konversationserkennung und genaue Prozente nicht – dann nur 10er-Schritte.")));
        }

        string AirPodsText()
        {
            var eng = host.AirPods;
            if (eng == null || !s.AirPodsEnabled || !eng.Running) return "Status: aus";
            var st = eng.Current;
            static string P(PodInfo p) => (p.Battery.HasValue ? p.Battery + " %" : "–") + (p.Charging ? " ⚡" : "") + (p.InEar ? " (Ohr)" : "");
            string a = st == null
                ? "Status: nicht in Reichweite – öffne das Case neben dem PC"
                : $"{st.Model}: L {P(st.Left)} · R {P(st.Right)}" + (st.CaseBattery.HasValue ? $" · Case {st.CaseBattery} %" : "");
            return a + " · Ton: " + (eng.EndpointName ?? "nicht verbunden");
        }

        void SaveSpotifyId()
        {
            if (spotifyIdBox == null) return;
            var id = spotifyIdBox.Text.Trim();
            if (id != (s.SpotifyClientId ?? ""))
            {
                s.SpotifyClientId = id;
                s.SpotifyAccessToken = "";
                s.SpotifyRefreshToken = "";
            }
            s.Save();
        }

        void OnSpotifyStatus()
        {
            if (spotifyStatus != null) spotifyStatus.Text = host.Spotify.Status;
        }

        // =================================================================
        //  Discord
        // =================================================================
        void BuildDiscord(StackPanel root)
        {
            root.Children.Add(Header("Verbindung"));
            var dc = new StackPanel();
            dc.Children.Add(ToggleRow("Mit Discord verbinden", s.DiscordEnabled, v =>
            {
                SaveDiscordFields();
                s.DiscordEnabled = v;
                s.Save();
                if (v) host.Discord.Restart();
                else host.Discord.Stop();
            }, out discordSwitch));
            dc.Children.Add(Divider());
            dc.Children.Add(CopyRow("Redirect", DiscordService.RedirectUri));
            dc.Children.Add(Divider());
            clientIdBox = Field(s.DiscordClientId ?? "", 14, false);
            dc.Children.Add(PlainField("Client-ID", clientIdBox));
            dc.Children.Add(Divider());
            secretBox = new PasswordBox
            {
                Password = s.DiscordClientSecret ?? "",
                FontSize = 14,
                Foreground = Ui.White,
                Background = Brushes.Transparent,
                CaretBrush = Ui.White,
                SelectionBrush = BlueBrush,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            dc.Children.Add(PlainField("Secret", secretBox));
            dc.Children.Add(Divider());
            discordStatus = StatusText(host.Discord.Status);
            dc.Children.Add(discordStatus);
            dc.Children.Add(Divider());
            dc.Children.Add(ButtonRow(
                FilledSmall("Verbinden", () =>
                {
                    SaveDiscordFields();
                    s.DiscordEnabled = true;
                    s.Save();
                    discordSwitch?.Set(true);
                    host.Discord.Restart();
                }),
                SmallButton("Developer Portal", () => IslandWindow.Open("https://discord.com/developers/applications")),
                SmallButton("Status kopieren", () => { try { Clipboard.SetText(host.Discord.Status ?? ""); } catch { } }),
                SmallButton("Abmelden", () =>
                {
                    s.DiscordAccessToken = "";
                    s.DiscordRefreshToken = "";
                    s.DiscordEnabled = false;
                    s.Save();
                    discordSwitch?.Set(false);
                    host.Discord.Stop();
                }, true)));
            root.Children.Add(Card(dc));
            root.Children.Add(Footer(
                "Einmalig einrichten (ca. 2 Minuten):\n" +
                "1. „Developer Portal“ öffnen → „New Application“ (Name z. B. „Island“).\n" +
                "2. Links „OAuth2“ → „Add Redirect“ → oben bei „Redirect“ auf „Kopieren“ klicken, einfügen, „Save Changes“.\n" +
                "3. „Client ID“ kopieren und oben einfügen. „Reset Secret“ → Secret kopieren und einfügen.\n" +
                "4. Discord muss laufen. „Verbinden“ klicken – in Discord erscheint ein Fenster „Autorisieren“, dort bestätigen.\n" +
                "Klappt es nicht, steht der Grund oben im Status. Mit „Status kopieren“ kannst du ihn mir schicken.\n\n" +
                "Danach siehst du neue Nachrichten, und in Sprachkanälen und Anrufen zeigt die Island wie das Discord-Overlay, wer spricht. " +
                "Secret und Anmeldung werden nur auf diesem PC gespeichert."));
        }

        void BuildTeams(StackPanel root)
        {
            root.Children.Add(Header("Verbindung"));
            var tc = new StackPanel();
            tc.Children.Add(ToggleRow("Mit Teams verbinden", s.TeamsEnabled, v =>
            {
                s.TeamsEnabled = v;
                s.Save();
                if (v) host.Teams.Restart();
                else host.Teams.Stop();
            }, out var teamsSwitch));
            tc.Children.Add(Divider());
            teamsStatus = StatusText(host.Teams.Status);
            tc.Children.Add(teamsStatus);
            tc.Children.Add(Divider());
            tc.Children.Add(ButtonRow(
                FilledSmall("Koppeln", () =>
                {
                    if (!s.TeamsEnabled)
                    {
                        s.TeamsEnabled = true;
                        s.Save();
                        teamsSwitch?.Set(true);
                        host.Teams.Restart();
                    }
                    host.Teams.Pair();
                }),
                SmallButton("Kopplung aufheben", () => host.Teams.Unpair(), true)));
            root.Children.Add(Card(tc));
            root.Children.Add(Footer(
                "Einmalig einrichten:\n" +
                "1. In Teams: Einstellungen → Datenschutz → „Drittanbieter-App-API“ → „API verwalten“ → „API aktivieren“.\n" +
                "2. Oben „Mit Teams verbinden“ einschalten.\n" +
                "3. Einer Besprechung beitreten und hier auf „Koppeln“ klicken – in Teams auf „Zulassen“ tippen.\n\n" +
                "Danach erscheint die Teams-Seite nur, solange du in einer Besprechung bist: Mikro, Kamera, Hand heben, Chat, Weichzeichnen, " +
                "Reaktionen und Verlassen. Strg + Alt + M schaltet in Besprechungen das Teams-Mikro. Teams-Teilnehmer gibt die Schnittstelle nicht heraus."));
        }

        // =================================================================
        //  Tastenkürzel
        // =================================================================
        // =================================================================
        //  Microsoft To Do (+ Apple-Erinnerungen)
        // =================================================================
        TextBlock todoStatus;
        bool todoWasConnected;

        void OnTodoStatus(string t)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (todoStatus != null) todoStatus.Text = t;
                // nach erfolgreicher Anmeldung die Seite mit den Listen neu zeigen
                if (page == "todo" && host.Todo.Connected && !todoWasConnected && host.Todo.Lists.Count > 0) ShowPage("todo");
            }));
        }

        void BuildObsidian(StackPanel root)
        {
            var vaults = Obsidian.FindVaults();
            root.Children.Add(Header("Ablegen in Obsidian"));
            var c = new StackPanel();
            c.Children.Add(ToggleRow("Auf die Island ziehen = in Obsidian speichern", s.ObsidianEnabled, v => { s.ObsidianEnabled = v; Apply(); }, out _));
            c.Children.Add(Divider());
            // Vault: aus Obsidians Liste oder selbst gewählt
            var names = new System.Collections.Generic.List<string> { "Zuletzt geöffneter" };
            names.AddRange(vaults.Select(v => v.Name));
            bool custom = !string.IsNullOrWhiteSpace(s.ObsidianVault) && !vaults.Any(v => string.Equals(v.Path, s.ObsidianVault, StringComparison.OrdinalIgnoreCase));
            if (custom) names.Add(System.IO.Path.GetFileName(s.ObsidianVault.TrimEnd('\\', '/')));
            int sel = string.IsNullOrWhiteSpace(s.ObsidianVault) ? 0
                : custom ? names.Count - 1
                : 1 + vaults.FindIndex(v => string.Equals(v.Path, s.ObsidianVault, StringComparison.OrdinalIgnoreCase));
            c.Children.Add(ChipRow("Vault", names.ToArray(), sel, i =>
            {
                s.ObsidianVault = i == 0 ? "" : i - 1 < vaults.Count ? vaults[i - 1].Path : s.ObsidianVault;
                Apply();
                Later(() => ShowPage("obsidian"));
            }));
            c.Children.Add(Divider());
            var tgt = new[] { "folder", "daily", "note" };
            c.Children.Add(SegmentRow("Ziel", new[] { "Inbox-Ordner", "Tagesnotiz", "Feste Notiz" }, Math.Max(0, Array.IndexOf(tgt, s.ObsidianTarget)),
                i => { s.ObsidianTarget = tgt[i]; Apply(); Later(() => ShowPage("obsidian")); }));
            if (s.ObsidianTarget == "folder")
            {
                c.Children.Add(Divider());
                string vpf = Obsidian.VaultPath(s);
                var fb = Field(string.IsNullOrWhiteSpace(s.ObsidianFolder) && vpf != null ? Obsidian.InboxFolder(s, vpf) : s.ObsidianFolder ?? "", 14, false);
                fb.TextChanged += (o, e) => { s.ObsidianFolder = fb.Text.Trim(); s.Save(); };
                c.Children.Add(PlainField("Ordner (Text wird je eine neue Notiz, Dateien landen direkt hier)", fb));
            }
            if (s.ObsidianTarget == "note")
            {
                c.Children.Add(Divider());
                var nb = Field(s.ObsidianNote ?? "Eingang", 14, false);
                nb.TextChanged += (o, e) => { s.ObsidianNote = nb.Text.Trim(); s.Save(); };
                c.Children.Add(PlainField("Notiz (z. B. Eingang oder Ordner/Ideen)", nb));
            }
            c.Children.Add(Divider());
            c.Children.Add(ToggleRow("Uhrzeit vor jeden Eintrag", s.ObsidianTime, v => { s.ObsidianTime = v; Apply(); }, out _));
            c.Children.Add(Divider());
            string vp = Obsidian.VaultPath(s);
            c.Children.Add(StatusText(vp == null
                ? "Kein Vault gefunden – Obsidian einmal öffnen oder unten einen Ordner wählen."
                : "Landet in: " + System.IO.Path.GetFileName(vp.TrimEnd('\\', '/')) + " / " + Obsidian.NoteRelPath(s, vp)));
            c.Children.Add(Divider());
            c.Children.Add(ButtonRow(
                SmallButton("Ordner wählen …", () =>
                {
                    var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Obsidian-Vault wählen" };
                    if (dlg.ShowDialog(this) == true) { s.ObsidianVault = dlg.FolderName; Apply(); Later(() => ShowPage("obsidian")); }
                }),
                SmallButton("Notiz öffnen", () => Obsidian.OpenNote(s))));
            root.Children.Add(Card(c));
            root.Children.Add(Footer("Zieh markierten Text, einen Link, ein Bild oder eine Datei auf die Island. Text und Links werden als Punkt an die Notiz angehängt, " +
                                     "Dateien in den Anhang-Ordner deines Vaults kopiert und verlinkt (Bilder, PDFs und Audio direkt eingebettet). " +
                                     "Ist die KI-Drop-Zone an, erscheint dort zusätzlich die Karte „In Obsidian“. Die Tagesnotiz nutzt Ordner und Datumsformat aus Obsidian."));
        }

        void BuildTodo(StackPanel root)
        {
            var todo = host.Todo;
            todoWasConnected = todo.Connected && todo.Lists.Count > 0;

            root.Children.Add(Header("Verbindung"));
            var c = new StackPanel();
            var idBox = Field(s.TodoClientId ?? "", 14, false);
            idBox.TextChanged += (o, e) => { s.TodoClientId = idBox.Text.Trim(); s.Save(); };
            c.Children.Add(PlainField("Client-ID", idBox));
            c.Children.Add(Divider());
            var code = Ui.Text("", 26, Ui.White, true, true);
            code.Margin = new Thickness(16, 10, 16, 0);
            code.Visibility = Visibility.Collapsed;
            c.Children.Add(code);
            todoStatus = StatusText(todo.Connected && todo.Status == "Nicht verbunden" ? "Verbunden" : todo.Status);
            c.Children.Add(todoStatus);
            c.Children.Add(Divider());
            c.Children.Add(ButtonRow(
                FilledSmall(todo.Connected ? "Neu anmelden" : "Anmelden", async () =>
                {
                    s.TodoClientId = idBox.Text.Trim();
                    s.Save();
                    try
                    {
                        var dc = await todo.BeginLoginAsync();
                        code.Text = dc.UserCode;
                        code.Visibility = Visibility.Visible;
                        try { Clipboard.SetText(dc.UserCode); } catch { }
                        todoStatus.Text = "Code kopiert – im Browser einfügen und mit deinem Microsoft-Konto anmelden. Warte …";
                        IslandWindow.Open(dc.Url);
                    }
                    catch (Exception ex) { todoStatus.Text = ex.Message; }
                }),
                SmallButton("App registrieren", () => IslandWindow.Open("https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade")),
                SmallButton("Abmelden", () => { todo.Logout(); Later(() => ShowPage("todo")); }, true)));
            root.Children.Add(Card(c));
            root.Children.Add(Footer(
                "Einmalig einrichten (ca. 3 Minuten):\n" +
                "1. „App registrieren“ → „Neue Registrierung“ → Name z. B. Dynamic Island → Kontotypen: „Konten in einem beliebigen Organisationsverzeichnis und persönliche Microsoft-Konten“ → Registrieren.\n" +
                "2. Die „Anwendungs-ID (Client-ID)“ kopieren und oben eintragen.\n" +
                "3. Links „Authentifizierung“ → ganz unten „Öffentliche Clientflows zulassen“ auf Ja → Speichern.\n" +
                "4. „Anmelden“ klicken, den Code im Browser einfügen, mit deinem Microsoft-Konto anmelden und zustimmen."));

            if (todo.Connected && todo.Lists.Count > 0)
            {
                root.Children.Add(Header("Anzeigen"));
                var l = new StackPanel();
                var lists = todo.Lists;
                int li = Math.Max(0, lists.FindIndex(x => x.Id == todo.MainList?.Id));
                l.Children.Add(ChipRow("Liste (neue Aufgaben landen hier)", lists.Select(x => x.Name).ToArray(), li,
                    i => { s.TodoListId = lists[i].Id; Apply(); _ = todo.RefreshAsync(); }));
                l.Children.Add(Divider());
                l.Children.Add(ToggleRow("Aufgaben aus allen Listen zeigen", s.TodoShowAll, v => { s.TodoShowAll = v; Apply(); _ = todo.RefreshAsync(); }, out _));
                root.Children.Add(Card(l));
            }

            root.Children.Add(Header("Erinnerungen"));
            var r = new StackPanel();
            r.Children.Add(ToggleRow("Erinnerungen in der Island zeigen", s.TodoReminders, v => { s.TodoReminders = v; Apply(); }, out _));
            r.Children.Add(Divider());
            r.Children.Add(ToggleRow("Erinnerungen laut ansagen", s.TodoSpeak, v => { s.TodoSpeak = v; Apply(); }, out _));
            r.Children.Add(Divider());
            r.Children.Add(InfoRow("Neue Aufgabe von überall", "Strg + Alt + N"));
            root.Children.Add(Card(r));

            root.Children.Add(Header("Apple Erinnerungen & Notizen"));
            root.Children.Add(Card(StatusText(
                "Apple bietet für Erinnerungen und Notizen keine Schnittstelle für Windows. Der einfachste Weg:\n\n" +
                "Erinnerungen: Auf dem iPhone unter Einstellungen → Apps → Erinnerungen → Accounts dein Outlook-/Microsoft-Konto hinzufügen " +
                "und „Erinnerungen“ einschalten. Listen in diesem Konto sind dieselben wie in Microsoft To Do – was du am iPhone anlegst, " +
                "zeigt und meldet die Island automatisch.\n\n" +
                "Notizen: Apple Notizen gibt es unter Windows nur im Browser (icloud.com/notes).")));
            root.Children.Add(ButtonRow(SmallButton("iCloud Notizen öffnen", () => IslandWindow.Open("https://www.icloud.com/notes"))));
        }

        // =================================================================
        //  Kalender
        // =================================================================
        static readonly int[] RemindOptions = { 0, 5, 10, 15, 30 };

        void BuildCalendar(StackPanel root)
        {
            var cal = host.Calendar;
            root.Children.Add(Header("Deine Kalender"));
            var c = new StackPanel();
            var urls = cal.Urls;
            for (int i = 0; i < urls.Count; i++)
            {
                int at = i;
                string shown;
                try { var u = new Uri(CalendarService.Normalize(urls[i])); shown = u.Host + (u.AbsolutePath.Length > 24 ? u.AbsolutePath.Substring(0, 24) + "…" : u.AbsolutePath); }
                catch { shown = urls[i].Length > 40 ? urls[i].Substring(0, 40) + "…" : urls[i]; }
                var row = new Grid { MinHeight = 44, Margin = new Thickness(16, 0, 12, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var lbl = Ui.Text(shown, 13.5, Ui.White, false);
                lbl.TextTrimming = TextTrimming.CharacterEllipsis;
                var del = SmallButton("Entfernen", () =>
                {
                    var l = cal.Urls;
                    l.RemoveAt(at);
                    cal.Urls = l;
                    s.Save();
                    _ = cal.RefreshAsync();
                    Later(() => ShowPage("calendar"));
                }, true);
                del.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(del, 1);
                row.Children.Add(lbl);
                row.Children.Add(del);
                c.Children.Add(row);
                c.Children.Add(Divider());
            }
            var box = Field("", 14, false);
            c.Children.Add(PlainField("iCal-Adresse", box));
            c.Children.Add(Divider());
            var status = StatusText(cal.Status);
            c.Children.Add(status);
            c.Children.Add(Divider());
            c.Children.Add(ButtonRow(
                FilledSmall("Hinzufügen", async () =>
                {
                    var u = CalendarService.Normalize(box.Text);
                    if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
                    {
                        status.Text = "Bitte die vollständige iCal-Adresse einfügen (beginnt mit https:// oder webcal://).";
                        return;
                    }
                    var l = cal.Urls;
                    if (!l.Contains(u)) l.Add(u);
                    cal.Urls = l;
                    s.Save();
                    status.Text = "Lade …";
                    await cal.RefreshAsync();
                    Later(() => { if (page == "calendar") ShowPage("calendar"); });
                }),
                SmallButton("Jetzt aktualisieren", async () => { status.Text = "Lade …"; await cal.RefreshAsync(); status.Text = cal.Status; }),
                SmallButton("Termine zeigen", () => host.ShowCalendar())));
            root.Children.Add(Card(c));
            root.Children.Add(Footer(
                "Google Kalender: calendar.google.com → Einstellungen → deinen Kalender wählen → „Geheime Adresse im iCal-Format“ kopieren.\n" +
                "Outlook: outlook.com → Einstellungen → Kalender → Freigegebene Kalender → „Kalender veröffentlichen“ → ICS-Link.\n" +
                "iCloud: Kalender teilen → „Öffentlicher Kalender“ → Link (webcal://…).\n" +
                "Die Adressen werden verschlüsselt gespeichert und alle 10 Minuten neu geladen."));

            root.Children.Add(Header("Erinnerungen"));
            var r = new StackPanel();
            r.Children.Add(ToggleRow("Termine in der Island anzeigen", s.CalendarEnabled, v => { s.CalendarEnabled = v; Apply(); }, out _));
            r.Children.Add(Divider());
            r.Children.Add(ToggleRow("Eigene Seite „Kalender“", s.Pages?.Contains("calendar") == true, v => SetIslandPage("calendar", v), out _));
            r.Children.Add(Divider());
            r.Children.Add(ToggleRow("Wetter links auf der Kalender-Seite", s.CalendarWithWeather, v => { s.CalendarWithWeather = v; Apply(); }, out _));
            r.Children.Add(Divider());
            int ri = Array.IndexOf(RemindOptions, s.CalendarRemind);
            r.Children.Add(ChipRow("Vorher erinnern", new[] { "Aus", "5 Min", "10 Min", "15 Min", "30 Min" }, ri < 0 ? 2 : ri,
                i => { s.CalendarRemind = RemindOptions[i]; Apply(); }));
            r.Children.Add(Divider());
            r.Children.Add(ToggleRow("Bei Beginn nochmal melden", s.CalendarAtStart, v => { s.CalendarAtStart = v; Apply(); }, out _));
            r.Children.Add(Divider());
            r.Children.Add(ToggleRow("Termine laut ansagen", s.CalendarSpeak, v => { s.CalendarSpeak = v; Apply(); }, out _));
            r.Children.Add(Divider());
            r.Children.Add(ButtonRow(SmallButton("Ansage testen", () =>
            {
                var n = cal.Next;
                _ = Speaker.SpeakAsync(n != null
                    ? $"Dein nächster Termin: {n.Title}, {(n.Start.Date == DateTime.Today ? "heute" : n.Start.Date == DateTime.Today.AddDays(1) ? "morgen" : n.Start.ToString("dddd", Ui.De))} um {n.Start:HH:mm} Uhr."
                    : "Das ist eine Test-Ansage der Dynamic Island.");
            })));
            root.Children.Add(Card(r));
            root.Children.Add(Footer("Die Ansage nutzt die Windows-Sprachausgabe. Fehlt eine deutsche Stimme: Windows-Einstellungen → Zeit und Sprache → Sprache → Deutsch → Sprachausgabe."));
        }

        // =================================================================
        //  KI
        // =================================================================
        static readonly string[] AiModels = { GeminiService.FastModel, GeminiService.SmartModel };
        static readonly string[] AiLanguages = { "Deutsch", "Englisch", "Französisch", "Spanisch", "Italienisch", "Türkisch" };

        void BuildAi(StackPanel root)
        {
            var prov = host.Gemini.Current;
            root.Children.Add(Header("Anbieter"));
            var g = new StackPanel();
            g.Children.Add(ToggleRow("KI-Funktionen in der Island", s.AiEnabled, v => { s.AiEnabled = v; Apply(); }, out _));
            g.Children.Add(Divider());
            var provs = GeminiService.Providers;
            g.Children.Add(ChipRow("KI", provs.Select(p => p.Name).ToArray(), Math.Max(0, Array.IndexOf(provs, prov)), i =>
            {
                s.AiProvider = provs[i].Key;
                host.Gemini.History.Clear();   // neuer Anbieter → neuer Chat
                Apply();
                Later(() => ShowPage("ai"));
            }));
            g.Children.Add(Divider());
            var key = new PasswordBox
            {
                Password = host.Gemini.Key ?? "",
                FontSize = 14,
                Foreground = Ui.White,
                Background = Brushes.Transparent,
                CaretBrush = Ui.White,
                SelectionBrush = BlueBrush,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            key.PasswordChanged += (o, e) => { host.Gemini.Key = key.Password; s.Save(); };
            g.Children.Add(PlainField(prov.Name + "-API-Schlüssel", key));
            g.Children.Add(Divider());
            var status = StatusText(host.Gemini.HasKey ? "Schlüssel gespeichert (verschlüsselt)." : "Noch kein Schlüssel eingetragen.");
            g.Children.Add(status);
            g.Children.Add(Divider());
            g.Children.Add(ButtonRow(
                FilledSmall("Verbindung testen", async () =>
                {
                    host.Gemini.Key = key.Password;
                    s.Save();
                    status.Text = "Teste …";
                    status.Text = await host.Gemini.TestAsync();
                }),
                SmallButton("Schlüssel holen", () => IslandWindow.Open(prov.KeyPage)),
                SmallButton("Entfernen", () => { host.Gemini.Key = null; s.Save(); Later(() => ShowPage("ai")); }, true)));
            root.Children.Add(Card(g));
            root.Children.Add(Footer((prov.Key == "gemini"
                                         ? "Den Gemini-Schlüssel gibt es kostenlos in Google AI Studio („Create API key“). "
                                         : "Den Schlüssel bekommst du unter „Schlüssel holen“ – " + prov.Name + " rechnet die Nutzung über dein Konto dort ab (meist Guthaben nötig). ") +
                                     "Jeder Schlüssel wird mit Windows verschlüsselt gespeichert und ist nur in deinem Benutzerkonto lesbar. " +
                                     "Deine Fragen und gezogenen Texte gehen zur Beantwortung an " + prov.Name + ". Du kannst mehrere Schlüssel hinterlegen und oben umschalten."));

            root.Children.Add(Header("Antworten"));
            var a = new StackPanel();
            if (prov.Key == "gemini")
            {
                int mi = Array.IndexOf(AiModels, s.GeminiModel);
                a.Children.Add(ChipRow("Modell", new[] { "Schnell (3.5 Flash-Lite)", "Klüger (3.6 Flash)" }, mi < 0 ? 0 : mi,
                    i => { s.GeminiModel = AiModels[i]; Apply(); }));
            }
            else
            {
                var mb = Field(host.Gemini.Model, 14, false);
                mb.TextChanged += (o, e) => { host.Gemini.Model = mb.Text; s.Save(); };
                a.Children.Add(PlainField("Modell (leer = " + prov.Model + ")", mb));
            }
            a.Children.Add(Divider());
            int li = Array.IndexOf(AiLanguages, s.AiTranslateTo);
            a.Children.Add(ChipRow("Übersetzen nach", AiLanguages, li < 0 ? 0 : li,
                i => { s.AiTranslateTo = AiLanguages[i]; Apply(); }));
            root.Children.Add(Card(a));

            root.Children.Add(Header("Bedienung"));
            var b = new StackPanel();
            b.Children.Add(ToggleRow("Mittlere Maustaste auf der Island liest Text", s.AiMiddleClickOcr, v => { s.AiMiddleClickOcr = v; Apply(); }, out _));
            b.Children.Add(Divider());
            b.Children.Add(InfoRow("Mini-Chat", "Strg + Alt + G"));
            b.Children.Add(Divider());
            b.Children.Add(InfoRow("Text vom Bildschirm", "Strg + Alt + T"));
            b.Children.Add(Divider());
            b.Children.Add(InfoRow("Text zusammenfassen / übersetzen", "Markieren und auf die Island ziehen"));
            root.Children.Add(Card(b));
            root.Children.Add(Footer("Die Texterkennung läuft offline mit Windows – erkannter Text landet automatisch in der Zwischenablage. " +
                                     "Einfache Rechnungen wie 23 * 19 rechnet die Island selbst, ohne Gemini."));
        }

        void BuildKeys(StackPanel root)
        {
            root.Children.Add(Header("Überall verfügbar"));
            var hk = new StackPanel();
            void AddKey(string what, string keys)
            {
                if (hk.Children.Count > 0) hk.Children.Add(Divider());
                hk.Children.Add(InfoRow(what, keys));
            }
            AddKey("Übersicht öffnen", "Strg + Alt + Leertaste");
            AddKey("Kurzbefehle", "Strg + Alt + K");
            AddKey("Downloads", "Strg + Alt + D");
            AddKey("Zwischenablage-Verlauf", "Strg + Alt + V");
            AddKey("Offene Apps", "Strg + Alt + A");
            AddKey("Mikro an / aus (Teams oder Discord)", "Strg + Alt + M");
            AddKey("Gemini fragen", "Strg + Alt + G");
            AddKey("Text vom Bildschirm lesen", "Strg + Alt + T");
            AddKey("Neue Aufgabe (To Do)", "Strg + Alt + N");
            root.Children.Add(Card(hk));

            root.Children.Add(Header("Auf der Island"));
            var g = new StackPanel();
            void AddG(string what, string how)
            {
                if (g.Children.Count > 0) g.Children.Add(Divider());
                g.Children.Add(InfoRow(what, how));
            }
            AddG("Entfalten", "Antippen");
            AddG("Alle Funktionen", "Nochmal tippen oder halten");
            AddG("Nächste / vorige Seite", "In der großen Island seitlich wischen");
            AddG("Menü", "Rechtsklick");
            AddG("Verschieben", "Rechte Maustaste ziehen");
            AddG("Seite wechseln (große Island)", "Mausrad");
            AddG("Aktivität ausblenden / zurück", "Wischen ← / →");
            root.Children.Add(Card(g));
        }

        // =================================================================
        //  Segment-Auswahl (wie UISegmentedControl)
        // =================================================================
        static FrameworkElement Segmented(string[] labels, int selected, Action<int> pick)
        {
            var outer = new Border { Background = FieldBrush, CornerRadius = new CornerRadius(8), Padding = new Thickness(2), VerticalAlignment = VerticalAlignment.Center };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var segs = new List<Border>();
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var seg = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 5, 12, 5),
                    Cursor = Cursors.Hand,
                    Background = i == selected ? SegSelBrush : Brushes.Transparent,
                    Child = Ui.Text(labels[i], 12.5, Ui.White, i == selected)
                };
                seg.MouseLeftButtonUp += (o, e) =>
                {
                    for (int j = 0; j < segs.Count; j++)
                    {
                        segs[j].Background = j == idx ? SegSelBrush : Brushes.Transparent;
                        ((TextBlock)segs[j].Child).FontWeight = j == idx ? FontWeights.SemiBold : FontWeights.Normal;
                    }
                    try { pick(idx); } catch (Exception ex) { Program.Log(ex); }
                };
                segs.Add(seg);
                sp.Children.Add(seg);
            }
            outer.Child = sp;
            return outer;
        }

        /// <summary>Zeile mit einem festen Text (z. B. Adresse) und „Kopieren“-Button.</summary>
        static FrameworkElement CopyRow(string label, string value)
        {
            var g = new Grid { MinHeight = 46, Margin = new Thickness(16, 0, 12, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var l = Ui.Text(label, 14.5, Ui.White, false);
            var v = new TextBox
            {
                Text = value,
                IsReadOnly = true,
                FontSize = 13.5,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = Secondary,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                SelectionBrush = BlueBrush
            };
            TextBlock btnText = null;
            var btn = SmallButton("Kopieren", () =>
            {
                try { Clipboard.SetText(value); } catch { }
                if (btnText != null) btnText.Text = "Kopiert ✓";
            });
            btnText = btn.Child as TextBlock;
            btn.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(v, 1);
            Grid.SetColumn(btn, 2);
            g.Children.Add(l);
            g.Children.Add(v);
            g.Children.Add(btn);
            return g;
        }

        static FrameworkElement SegmentRow(string label, string[] labels, int selected, Action<int> pick)
        {
            var g = new Grid { MinHeight = 48, Margin = new Thickness(16, 0, 12, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var t = Ui.Text(label, 14.5, Ui.White, false);
            var seg = Segmented(labels, selected, pick);
            Grid.SetColumn(seg, 1);
            g.Children.Add(t);
            g.Children.Add(seg);
            return g;
        }

        // =================================================================
        //  Katzen-Zeilen + Kurzbefehl-Zeilen
        // =================================================================
        void BuildCatRows()
        {
            catList.Children.Clear();
            if (catList == null) return;
            foreach (var cfg in s.Cats.ToList())
            {
                if (catList.Children.Count > 0) catList.Children.Add(Divider());
                catList.Children.Add(CatRow(cfg));
            }
            if (s.Cats.Count < 4)
            {
                if (catList.Children.Count > 0) catList.Children.Add(Divider());
                var add = new Border { Background = Brushes.Transparent, Cursor = Cursors.Hand, MinHeight = 48, Padding = new Thickness(16, 0, 16, 0) };
                add.Child = Ui.HStack(8, Ui.Glyph("", 14, BlueBrush), Ui.Text("Katze hinzufügen", 14.5, BlueBrush, false));
                add.MouseEnter += (o, e) => add.Opacity = 0.7;
                add.MouseLeave += (o, e) => add.Opacity = 1;
                add.MouseLeftButtonUp += (o, e) => AddCat();
                catList.Children.Add(add);
            }
        }

        void AddCat()
        {
            if (s.Cats.Count >= 4) return;
            var name = CatStyle.Names.FirstOrDefault(n => !s.Cats.Any(c => c.Name == n)) ?? "Katze";
            var breed = CatStyle.Breeds.FirstOrDefault(b => !s.Cats.Any(c => c.Breed == b.Key)) ?? CatStyle.Breeds[0];
            s.Cats.Add(new CatConfig { Name = name, Breed = breed.Key });
            Changed(true);
        }

        FrameworkElement CatRow(CatConfig cfg)
        {
            var root = new StackPanel { Margin = new Thickness(16, 12, 12, 12) };

            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var view = new CatView(cfg, 40) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var preview = new Border
            {
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(14),
                Background = FieldBrush,
                Child = view,
                Cursor = Cursors.Hand,
                ToolTip = "Anklicken zum Streicheln"
            };
            preview.MouseLeftButtonUp += (o, e) => view.Purr();

            var name = Field(cfg.Name, 16, true);
            name.MaxLength = 20;
            name.TextChanged += (o, e) => cfg.Name = string.IsNullOrWhiteSpace(name.Text) ? "Katze" : name.Text.Trim();
            name.LostFocus += (o, e) => Changed();
            var nameWrap = FieldWrap(name);
            nameWrap.Margin = new Thickness(12, 0, 10, 0);
            nameWrap.VerticalAlignment = VerticalAlignment.Center;

            var del = Ui.RoundButton("", 14, () => { s.Cats.Remove(cfg); Changed(true); }, 36, Ui.Br(Ui.Red, 45), RedBrush);
            del.ToolTip = "Katze entfernen";
            del.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetColumn(preview, 0);
            Grid.SetColumn(nameWrap, 1);
            Grid.SetColumn(del, 2);
            top.Children.Add(preview);
            top.Children.Add(nameWrap);
            top.Children.Add(del);
            root.Children.Add(top);

            bool hasPhoto = CatStyle.LoadPhoto(cfg.PhotoPath) != null;
            bool showsPhoto = hasPhoto && cfg.UsePhoto;

            root.Children.Add(SmallLabel("Aussehen"));
            var gallery = new WrapPanel();
            foreach (var b in CatStyle.Breeds)
            {
                var breed = b;
                bool sel = !showsPhoto && string.Equals(cfg.Breed, b.Key, StringComparison.OrdinalIgnoreCase);
                var tile = new StackPanel { Width = 66, Margin = new Thickness(0, 0, 2, 6), Cursor = Cursors.Hand, Background = Brushes.Transparent, ToolTip = b.Name };
                var box = new Border
                {
                    Width = 50,
                    Height = 50,
                    CornerRadius = new CornerRadius(12),
                    Background = FieldBrush,
                    BorderThickness = new Thickness(2),
                    BorderBrush = sel ? BlueBrush : Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new CatView(new CatConfig { Breed = b.Key, Name = b.Name }, 34)
                    {
                        IsHitTestVisible = false,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                tile.Children.Add(box);
                var lbl = Ui.Text(b.Name, 11, sel ? Ui.White : Secondary, sel);
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                lbl.Margin = new Thickness(0, 4, 0, 0);
                lbl.MaxWidth = 66;
                tile.Children.Add(lbl);
                tile.MouseEnter += (o, e) => box.Opacity = 0.8;
                tile.MouseLeave += (o, e) => box.Opacity = 1;
                tile.MouseLeftButtonUp += (o, e) =>
                {
                    cfg.Breed = breed.Key;
                    cfg.UsePhoto = false;
                    // Vorlage = frischer Look: alte eigene Farben/Muster würden die Vorlage sonst überdecken
                    cfg.FurColor = cfg.EyeColor = cfg.Pattern = "";
                    Changed(true);
                };
                gallery.Children.Add(tile);
            }
            root.Children.Add(gallery);

            var photoButtons = new System.Collections.Generic.List<FrameworkElement>
            {
                SmallButton(hasPhoto ? "Anderes Foto …" : "Eigenes Foto …", () => PickPhoto(cfg))
            };
            if (hasPhoto)
            {
                photoButtons.Add(SmallButton(cfg.UsePhoto ? "Zeichnung zeigen" : "Foto zeigen", () => { cfg.UsePhoto = !cfg.UsePhoto; Changed(true); }));
                photoButtons.Add(SmallButton("Foto entfernen", () => { cfg.PhotoPath = ""; cfg.UsePhoto = false; Changed(true); }, true));
            }
            bool open = openCats.Contains(cfg);
            photoButtons.Insert(0, FilledSmall(open ? "Fertig" : "Anpassen …", () =>
            {
                if (!openCats.Remove(cfg)) openCats.Add(cfg);
                Later(BuildCatRows);
            }));
            var pr = ButtonRow(photoButtons.ToArray());
            pr.Margin = new Thickness(0, 2, 0, 0);
            root.Children.Add(pr);
            if (open) root.Children.Add(CatCustomizer(cfg, showsPhoto));
            return root;
        }

        // ---------- Katzen anpassen: Farben, Muster, Zubehör, Größe, Charakter ----------
        FrameworkElement CatCustomizer(CatConfig cfg, bool photo)
        {
            var p = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            if (!photo)
            {
                p.Children.Add(SmallLabel("Fellfarbe"));
                p.Children.Add(Swatches(CatStyle.FurColors, cfg.FurColor, true, v => { cfg.FurColor = v; Changed(true); }));
                p.Children.Add(SmallLabel("Augenfarbe"));
                p.Children.Add(Swatches(CatStyle.EyeColors, cfg.EyeColor, true, v => { cfg.EyeColor = v; Changed(true); }));

                var patKeys = new[] { "", "Plain", "Tabby", "Points", "Tuxedo", "Calico" };
                int pi = Math.Max(0, Array.FindIndex(patKeys, k => string.Equals(k, cfg.Pattern ?? "", StringComparison.OrdinalIgnoreCase)));
                p.Children.Add(SmallLabel("Muster"));
                p.Children.Add(Chips(new[] { "Wie Vorlage", "Einfarbig", "Getigert", "Abzeichen", "Smoking", "Glückskatze" }, pi,
                    i => { cfg.Pattern = patKeys[i]; Changed(true); }));

                p.Children.Add(SmallLabel("Fell"));
                p.Children.Add(Chips(new[] { "Kurzhaar", "Flauschig" }, cfg.Fluffy ? 1 : 0, i => { cfg.Fluffy = i == 1; Changed(true); }));
            }

            p.Children.Add(SmallLabel("Zubehör"));
            var acc = new WrapPanel();
            foreach (var (key, label) in CatStyle.Accessories)
            {
                var k = key;
                bool sel = string.Equals(cfg.Accessory ?? "none", key, StringComparison.OrdinalIgnoreCase);
                var demo = new CatConfig
                {
                    Breed = cfg.Breed, FurColor = cfg.FurColor, EyeColor = cfg.EyeColor, Pattern = cfg.Pattern, Fluffy = cfg.Fluffy,
                    PhotoPath = cfg.PhotoPath, UsePhoto = cfg.UsePhoto, Accessory = key, AccessoryColor = cfg.AccessoryColor
                };
                var tile = new StackPanel { Width = 62, Margin = new Thickness(0, 0, 2, 6), Cursor = Cursors.Hand, Background = Brushes.Transparent, ToolTip = label };
                var box = new Border
                {
                    Width = 48,
                    Height = 48,
                    CornerRadius = new CornerRadius(12),
                    Background = FieldBrush,
                    BorderThickness = new Thickness(2),
                    BorderBrush = sel ? BlueBrush : Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new CatView(demo, 32) { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                tile.Children.Add(box);
                var lbl = Ui.Text(label, 10.5, sel ? Ui.White : Secondary, sel);
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                lbl.Margin = new Thickness(0, 4, 0, 0);
                tile.Children.Add(lbl);
                tile.MouseEnter += (o, e) => box.Opacity = 0.8;
                tile.MouseLeave += (o, e) => box.Opacity = 1;
                tile.MouseLeftButtonUp += (o, e) => { cfg.Accessory = k; Changed(true); };
                acc.Children.Add(tile);
            }
            p.Children.Add(acc);
            if ((cfg.Accessory ?? "none") != "none")
            {
                p.Children.Add(SmallLabel("Farbe vom Zubehör"));
                p.Children.Add(Swatches(CatStyle.AccentColors, cfg.AccessoryColor, false, v => { cfg.AccessoryColor = v; Changed(true); }));
            }

            var sizes = new[] { 0.85, 1.0, 1.15 };
            p.Children.Add(SmallLabel("Größe"));
            p.Children.Add(Chips(new[] { "Klein", "Normal", "Groß" }, Nearest(sizes, cfg.Size <= 0 ? 1 : cfg.Size), i => { cfg.Size = sizes[i]; Changed(true); }));

            var moods = new[] { "playful", "calm", "sleepy" };
            p.Children.Add(SmallLabel("Charakter"));
            p.Children.Add(Chips(new[] { "Verspielt", "Ruhig", "Verschlafen" }, Math.Max(0, Array.IndexOf(moods, cfg.Mood ?? "calm")),
                i => { cfg.Mood = moods[i]; Changed(true); }));

            var br = ButtonRow(
                SmallButton("Zufällig", () => { RandomizeCat(cfg); Changed(true); }),
                SmallButton("Zurücksetzen", () =>
                {
                    cfg.FurColor = cfg.EyeColor = cfg.Pattern = "";
                    cfg.Accessory = "none";
                    cfg.Fluffy = false;
                    cfg.Size = 1;
                    cfg.Mood = "calm";
                    Changed(true);
                }, true));
            br.Margin = new Thickness(0, 8, 0, 0);
            p.Children.Add(br);
            return p;
        }

        static readonly Random CatRnd = new Random();

        static void RandomizeCat(CatConfig c)
        {
            T Pick<T>(T[] a) => a[CatRnd.Next(a.Length)];
            c.Breed = Pick(CatStyle.Breeds).Key;
            c.FurColor = CatRnd.Next(3) == 0 ? "" : Pick(CatStyle.FurColors);
            c.EyeColor = Pick(CatStyle.EyeColors);
            c.Pattern = Pick(new[] { "", "Plain", "Tabby", "Points", "Tuxedo", "Calico" });
            c.Fluffy = CatRnd.Next(3) == 0;
            c.Accessory = Pick(CatStyle.Accessories).Key;
            c.AccessoryColor = Pick(CatStyle.AccentColors);
            c.Mood = Pick(new[] { "playful", "calm", "sleepy" });
        }

        /// <summary>Runde Farbfelder wie in iOS. Mit „auto“ steht vorne ein Feld für „wie die Vorlage“.</summary>
        static FrameworkElement Swatches(string[] colors, string current, bool auto, Action<string> pick)
        {
            var wp = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            var list = new List<string>();
            if (auto) list.Add("");
            list.AddRange(colors);
            foreach (var hex in list)
            {
                var h = hex;
                bool sel = string.Equals(hex, current ?? "", StringComparison.OrdinalIgnoreCase);
                Brush fill;
                if (hex == "")
                    fill = new LinearGradientBrush(Color.FromRgb(0xFF, 0x9F, 0x0A), Color.FromRgb(0x5E, 0x5C, 0xE6), 45);
                else
                    fill = CatStyle.TryColor(hex, out var c) ? Ui.Br(c) : (Brush)Brushes.Gray;
                var dot = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    Background = fill,
                    BorderBrush = Ui.Br(Colors.White, 60),
                    BorderThickness = new Thickness(0.5)
                };
                var ring = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(16),
                    BorderThickness = new Thickness(2),
                    BorderBrush = sel ? Ui.White : Brushes.Transparent,
                    Background = Brushes.Transparent,
                    Margin = new Thickness(0, 0, 4, 4),
                    Cursor = Cursors.Hand,
                    ToolTip = hex == "" ? "Wie die Vorlage" : hex,
                    Child = dot
                };
                ring.MouseEnter += (o, e) => dot.Opacity = 0.8;
                ring.MouseLeave += (o, e) => dot.Opacity = 1;
                ring.MouseLeftButtonUp += (o, e) => pick(h);
                wp.Children.Add(ring);
            }
            return wp;
        }

        /// <summary>Auswahl-Kapseln, die bei Platzmangel umbrechen.</summary>
        static FrameworkElement Chips(string[] labels, int selected, Action<int> pick)
        {
            var wp = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                bool sel = i == selected;
                var chip = new Border
                {
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(12, 5, 12, 6),
                    Margin = new Thickness(0, 0, 6, 6),
                    Background = sel ? BlueBrush : FieldBrush,
                    Cursor = Cursors.Hand,
                    Child = Ui.Text(labels[i], 12.5, Ui.White, sel)
                };
                chip.MouseEnter += (o, e) => chip.Opacity = 0.8;
                chip.MouseLeave += (o, e) => chip.Opacity = 1;
                chip.MouseLeftButtonUp += (o, e) => pick(idx);
                wp.Children.Add(chip);
            }
            return wp;
        }

        void PickPhoto(CatConfig cfg)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Foto von " + cfg.Name + " wählen",
                Filter = "Bilder|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Alle Dateien|*.*"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var dir = System.IO.Path.Combine(Program.DataDir, "Katzen");
                System.IO.Directory.CreateDirectory(dir);
                var dest = System.IO.Path.Combine(dir, Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(dlg.FileName));
                System.IO.File.Copy(dlg.FileName, dest, true);
                if (CatStyle.LoadPhoto(dest) == null)
                {
                    try { System.IO.File.Delete(dest); } catch { }
                    MessageBox.Show(this, "Dieses Bild lässt sich nicht öffnen. Probiere ein JPG oder PNG.", "Foto", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                cfg.PhotoPath = dest;
                cfg.UsePhoto = true;
            }
            catch (Exception ex) { Program.Log(ex); }
            Changed(true);
        }

        void Changed(bool rebuild = false)
        {
            s.Save();
            host.ApplySettings();
            if (rebuild) Later(BuildCatRows);
        }

        // =================================================================
        //  Kurzbefehle
        // =================================================================
        void BuildShortcutRows()
        {
            if (shortcutList == null) return;
            shortcutList.Children.Clear();
            foreach (var sc in s.Shortcuts.ToList())
            {
                var item = sc;
                if (shortcutList.Children.Count > 0) shortcutList.Children.Add(Divider());
                var g = new Grid { Margin = new Thickness(16, 8, 12, 8), MinHeight = 40 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var icon = Shortcuts.Icon(item, 32);
                icon.VerticalAlignment = VerticalAlignment.Center;
                var texts = Ui.VStack(1, Ui.Text(item.Name, 14.5, Ui.White, false), Ui.Text(item.Target, 12, Secondary, false));
                texts.Margin = new Thickness(12, 0, 8, 0);
                var del = Ui.RoundButton("", 13, () =>
                {
                    s.Shortcuts.Remove(item);
                    s.Save();
                    Later(BuildShortcutRows);
                }, 32, Ui.Br(Ui.Red, 45), RedBrush);
                del.ToolTip = "Entfernen";
                del.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(icon, 0);
                Grid.SetColumn(texts, 1);
                Grid.SetColumn(del, 2);
                g.Children.Add(icon);
                g.Children.Add(texts);
                g.Children.Add(del);
                shortcutList.Children.Add(g);
            }

            if (shortcutList.Children.Count > 0) shortcutList.Children.Add(Divider());
            var form = new StackPanel { Margin = new Thickness(16, 6, 12, 12) };
            form.Children.Add(SmallLabel("Neuer Kurzbefehl"));
            var nameBox = Field("", 14, false);
            var targetBox = Field("", 14, false);
            form.Children.Add(LabeledField("Name", nameBox));
            var tf = LabeledField("Ziel", targetBox);
            tf.Margin = new Thickness(0, 6, 0, 0);
            form.Children.Add(tf);
            var row = ButtonRow(
                SmallButton("Programm wählen …", () =>
                {
                    var dlg = new OpenFileDialog
                    {
                        Title = "Programm oder Datei wählen",
                        Filter = "Programme und Verknüpfungen|*.exe;*.lnk;*.bat;*.url|Alle Dateien|*.*"
                    };
                    if (dlg.ShowDialog(this) != true) return;
                    targetBox.Text = dlg.FileName;
                    if (string.IsNullOrWhiteSpace(nameBox.Text)) nameBox.Text = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                }),
                FilledSmall("Hinzufügen", () =>
                {
                    var n = nameBox.Text.Trim();
                    var t = targetBox.Text.Trim().Trim('"');
                    if (t.Length == 0) return;
                    if (n.Length == 0) n = t;
                    s.Shortcuts.Add(new ShortcutConfig { Name = n, Target = t });
                    s.Save();
                    Later(BuildShortcutRows);
                }));
            row.Margin = new Thickness(0, 8, 0, 0);
            form.Children.Add(row);
            shortcutList.Children.Add(form);
        }

        // =================================================================
        //  Discord
        // =================================================================
        void SaveDiscordFields()
        {
            if (clientIdBox == null || secretBox == null) return;
            var id = clientIdBox.Text.Trim();
            var secret = secretBox.Password.Trim();
            if (id != (s.DiscordClientId ?? "") || secret != (s.DiscordClientSecret ?? ""))
            {
                // neue Anwendung → alte Anmeldung passt nicht mehr
                s.DiscordAccessToken = "";
                s.DiscordRefreshToken = "";
            }
            s.DiscordClientId = id;
            s.DiscordClientSecret = secret;
        }

        void OnTeamsStatus()
        {
            if (teamsStatus != null) teamsStatus.Text = host.Teams.Status;
        }

        void OnDiscordStatus()
        {
            if (discordStatus != null) discordStatus.Text = host.Discord.Status;
        }

        // =================================================================
        //  Bausteine im Apple-Stil
        // =================================================================
        void Later(Action a) => Dispatcher.BeginInvoke(a);

        static TextBlock Header(string text)
        {
            var t = Ui.Text(text.ToUpper(Ui.De), 12, Secondary, false);
            t.Margin = new Thickness(16, 26, 16, 7);
            return t;
        }

        static TextBlock Footer(string text)
        {
            var t = Ui.Text(text, 12, Secondary, false);
            t.TextWrapping = TextWrapping.Wrap;
            t.TextTrimming = TextTrimming.None;
            t.Margin = new Thickness(16, 7, 16, 0);
            t.LineHeight = 17;
            return t;
        }

        static TextBlock StatusText(string text)
        {
            var t = Ui.Text(text, 13, Secondary, false);
            t.TextWrapping = TextWrapping.Wrap;
            t.TextTrimming = TextTrimming.None;
            t.Margin = new Thickness(16, 12, 16, 12);
            return t;
        }

        static TextBlock SmallLabel(string text)
        {
            var t = Ui.Text(text, 12, Secondary, false);
            t.Margin = new Thickness(0, 12, 0, 6);
            return t;
        }

        static Border Card(UIElement content) => new Border
        {
            Background = CardBrush,
            CornerRadius = new CornerRadius(10),
            Child = content
        };

        static Border Divider() => new Border
        {
            Height = 1,
            Background = SepBrush,
            Margin = new Thickness(16, 0, 0, 0),
            SnapsToDevicePixels = true
        };

        static FrameworkElement ToggleRow(string label, bool value, Action<bool> changed, out AppleSwitch sw)
        {
            var g = new Grid { MinHeight = 48, Margin = new Thickness(16, 0, 12, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var t = Ui.Text(label, 14.5, Ui.White, false);
            t.Margin = new Thickness(0, 0, 12, 0);
            sw = new AppleSwitch(value, changed);
            Grid.SetColumn(sw, 1);
            g.Children.Add(t);
            g.Children.Add(sw);
            return g;
        }

        static FrameworkElement InfoRow(string left, string right)
        {
            var g = new Grid { MinHeight = 44, Margin = new Thickness(16, 0, 16, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var l = Ui.Text(left, 14.5, Ui.White, false);
            var r = Ui.Text(right, 14, Secondary, false);
            Grid.SetColumn(r, 1);
            g.Children.Add(l);
            g.Children.Add(r);
            return g;
        }

        static TextBox Field(string text, double size, bool bold) => new TextBox
        {
            Text = text ?? "",
            FontSize = size,
            FontFamily = Ui.TextFont,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = Ui.White,
            Background = Brushes.Transparent,
            CaretBrush = Ui.White,
            SelectionBrush = BlueBrush,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        static Border FieldWrap(Control box) => new Border
        {
            Background = FieldBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 6, 8, 6),
            Child = box
        };

        static FrameworkElement LabeledField(string label, Control box)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var l = Ui.Text(label, 13.5, Secondary, false);
            var w = FieldWrap(box);
            Grid.SetColumn(w, 1);
            g.Children.Add(l);
            g.Children.Add(w);
            return g;
        }

        static FrameworkElement PlainField(string label, Control box)
        {
            var g = new Grid { MinHeight = 46, Margin = new Thickness(16, 0, 12, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var l = Ui.Text(label, 14.5, Ui.White, false);
            Grid.SetColumn(box, 1);
            g.Children.Add(l);
            g.Children.Add(box);
            return g;
        }

        static FrameworkElement ButtonRow(params FrameworkElement[] buttons)
        {
            var wp = new WrapPanel { Margin = new Thickness(16, 10, 16, 10) };
            foreach (var b in buttons)
            {
                b.Margin = new Thickness(0, 0, 8, 4);
                wp.Children.Add(b);
            }
            return wp;
        }

        static Border PressBorder(Border b, Action onClick)
        {
            b.Cursor = Cursors.Hand;
            b.MouseEnter += (o, e) => b.Opacity = 0.8;
            b.MouseLeave += (o, e) => b.Opacity = 1;
            b.MouseLeftButtonDown += (o, e) => { e.Handled = true; b.Opacity = 0.6; };
            b.MouseLeftButtonUp += (o, e) =>
            {
                e.Handled = true;
                b.Opacity = 0.8;
                try { onClick?.Invoke(); } catch (Exception ex) { Program.Log(ex); }
            };
            return b;
        }

        static Border SmallButton(string text, Action onClick, bool destructive = false) => PressBorder(new Border
        {
            Background = FieldBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 7, 12, 7),
            Child = Ui.Text(text, 13, destructive ? RedBrush : BlueBrush, false)
        }, onClick);

        static Border FilledSmall(string text, Action onClick) => PressBorder(new Border
        {
            Background = BlueBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 7, 14, 7),
            Child = Ui.Text(text, 13, Ui.White, true)
        }, onClick);

        static Border FilledButton(string text, Action onClick)
        {
            var t = Ui.Text(text, 15, Ui.White, true);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            return PressBorder(new Border
            {
                Background = BlueBrush,
                CornerRadius = new CornerRadius(12),
                Height = 48,
                Child = t
            }, onClick);
        }
    }

    /// <summary>Schalter wie in iOS (51 × 31, grün wenn an).</summary>
    public sealed class AppleSwitch : Border
    {
        static readonly SolidColorBrush OffBrush = Ui.Br(Color.FromRgb(0x39, 0x39, 0x3D));
        static readonly SolidColorBrush OnBrush = Ui.Br(Ui.Green);
        readonly TranslateTransform knobX = new TranslateTransform();
        readonly Action<bool> changed;
        bool on;

        public AppleSwitch(bool value, Action<bool> onChanged)
        {
            changed = onChanged;
            on = value;
            Width = 51;
            Height = 31;
            CornerRadius = new CornerRadius(15.5);
            Cursor = Cursors.Hand;
            Focusable = true;
            VerticalAlignment = VerticalAlignment.Center;
            Child = new Ellipse
            {
                Width = 27,
                Height = 27,
                Fill = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2, 0, 0, 0),
                RenderTransform = knobX,
                Effect = new DropShadowEffect { BlurRadius = 5, ShadowDepth = 1, Direction = 270, Opacity = 0.35, Color = Colors.Black }
            };
            Apply(false);
            MouseLeftButtonUp += (o, e) => { e.Handled = true; Toggle(); };
            KeyDown += (o, e) =>
            {
                if (e.Key == Key.Space || e.Key == Key.Enter) { e.Handled = true; Toggle(); }
            };
        }

        public bool IsOn => on;

        /// <summary>Setzt den Schalter, ohne die Aktion auszulösen.</summary>
        public void Set(bool value)
        {
            if (on == value) return;
            on = value;
            Apply(true);
        }

        void Toggle()
        {
            on = !on;
            Apply(true);
            try { changed?.Invoke(on); }
            catch (Exception ex) { Program.Log(ex); }
        }

        void Apply(bool animate)
        {
            Background = on ? OnBrush : OffBrush;
            double x = on ? 20 : 0;
            if (animate && !Ui.ReduceMotion)
                knobX.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(x, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else
            {
                knobX.BeginAnimation(TranslateTransform.XProperty, null);
                knobX.X = x;
            }
        }
    }
}

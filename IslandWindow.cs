using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace DynamicIsland
{
    /// <summary>
    /// Das eigentliche Fenster: ein großes, unsichtbares Fenster oben in der Mitte,
    /// in dem die schwarze Pille (und der kleine Kreis) leben. Durchsichtige Bereiche
    /// lassen Klicks durch, man merkt das Fenster also nicht.
    /// </summary>
    public sealed class IslandWindow : Window
    {
        public readonly Settings Settings = Settings.Load();
        public readonly AudioService Audio = new AudioService();
        public readonly MediaService Media;
        public readonly GeminiService Gemini;
        public readonly CalendarService Calendar;
        public readonly TodoService Todo;
        public readonly ClaudeTracker Claude;
        public readonly WeatherService Weather;
        readonly GameDownloadWatcher Games = new GameDownloadWatcher();
        readonly Dictionary<string, GenericActivity> gameActs = new Dictionary<string, GenericActivity>();
        AiActivity ai;
        public readonly SysStats Stats = new SysStats();

        public bool HasBattery;
        public int BatteryPct = -1;
        public bool OnAc;

        public static string MemoFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sprachmemos");

        readonly List<Activity> activities = new List<Activity>();
        readonly HashSet<string> hidden = new HashSet<string>();
        readonly Stack<string> hiddenStack = new Stack<string>();
        readonly ClockActivity clock;
        readonly DashboardActivity dashboard;
        readonly LocalApi api = new LocalApi();
        readonly BtScanner bt = new BtScanner();
        MediaActivity mediaAct;
        string pinnedId;

        // Darstellung
        Grid rootGrid, contentHost, bubbleContent;
        Border pill, bubble;
        FrameworkElement camDot, micDot, currentContent;
        readonly ScaleTransform pillScale = new ScaleTransform(1, 1);
        readonly TranslateTransform pillShift = new TranslateTransform();
        // verschiebt die ganze Reihe: X hält sie am Bildschirmrand sichtbar, Y fährt sie nach oben weg
        readonly TranslateTransform rowMove = new TranslateTransform();
        StackPanel rowPanel;
        Activity currentAct, bubbleAct, expandedAct;
        DateTime expandedSince;
        string currentKey = "";
        double targetW, targetH;
        bool bubbleShown;

        // Eingabe
        IntPtr hwnd;
        bool hovering, mouseDown, longPressed, swiped;
        Point downPos;
        readonly DispatcherTimer tick, holdTimer, collapseTimer;
        int tickCount;
        bool fsHidden;

        // Verschieben (rechte Maustaste ziehen) + automatisches Wegfahren
        double HideAfterSeconds => Math.Max(1, Settings.HideDelay);
        bool rightDown, dragging, menuOpen, tucked;
        Point dragStartCursor;
        double dragStartX, dragStartY, nudgeTarget, rowWidth = 160;
        DateTime lastActive = DateTime.Now;

        // Katzen, Discord, Downloads, Mitteilungen, Kurzbefehle
        public readonly DiscordService Discord;
        /// <summary>AirPods-Funktionen (früher eigene App „AirPods Companion“) – startet mit der Island.</summary>
        public AirPodsEngine AirPods;
        public readonly SpotifyService Spotify;
        SpotifyActivity spotifyAct;
        string lastSpotifyTrack;
        public readonly DownloadWatcher Downloads;
        public readonly NotificationMirror Notifications = new NotificationMirror();
        readonly List<CatView> catViews = new List<CatView>();
        readonly TranslateTransform tuckIsland = new TranslateTransform(), tuckCats = new TranslateTransform();
        readonly Dictionary<string, GenericActivity> dlActs = new Dictionary<string, GenericActivity>();
        StackPanel catsPanel, islandPart;
        Border catsSpacer;
        bool catsHover;
        DateTime lastInteraction = DateTime.Now;
        DiscordVoiceActivity voiceAct;
        SetupWindow setupWin;
        static readonly Random Rnd = new Random();

        // Zwischenablage-Verlauf (nur im Arbeitsspeicher)
        public readonly List<ClipItem> ClipHistory = new List<ClipItem>();
        DateTime ignoreClipUntil;
        public IntPtr Handle => hwnd;

        // Katzen in der großen Island (seitlich, mit Kratzbaum)
        const double CatColW = 84;
        // Alle Seiten der großen Island haben dieselbe Größe
        const double PageW = 420, PageH = 232; // alle Seiten gleich groß – mit Luft wie bei iOS
        const double NavTop = 8;  // oben: kleine Seitensymbole (statt Punkten unten); der Inhalt beginnt etwas tiefer
        const double PageNat = 240; // Entwurfsgröße der Seiten (ExpandedSize)
        /// <summary>Verkleinerung der großen Island (Einstellungen → Allgemein → Große Island).</summary>
        double PageScale => Math.Max(0.6, Math.Min(1.0, Settings.PageScale <= 0 ? 1 : Settings.PageScale));
        /// <summary>Zusätzliche Breite der Seiten (Einstellungen → Allgemein → Breite). Der Inhalt bekommt mehr Platz, wird nicht verzerrt.</summary>
        double PageWide => Math.Max(1.0, Math.Min(1.5, Settings.PageWide));
        double PageWEff => PageW * PageWide;
        readonly List<CatView> stripCats = new List<CatView>();
        bool CatStrip => Settings.CatsMode == "expanded" && Settings.Cats != null && Settings.Cats.Count > 0;

        // Controller + Player
        readonly bool[] padConnected = new bool[4];
        bool padsInit;
        DateTime mediaGoneSince = DateTime.MinValue;

        static readonly string[] PetLines =
        {
            "Prrrr …", "mag das sehr", "will mehr Streicheleinheiten", "blinzelt dich zufrieden an",
            "macht den Milchtritt", "reibt den Kopf an deiner Hand"
        };

        public IslandWindow()
        {
            Title = "Dynamic Island";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = 1000;
            Height = 580;
            MoveTo(Settings.PosX, Settings.PosY, false); // gespeicherte Position

            Media = new MediaService(Dispatcher);
            Gemini = new GeminiService(Settings);
            Calendar = new CalendarService(Settings);
            Todo = new TodoService(Settings);
            Claude = new ClaudeTracker(this);
            Weather = new WeatherService(Settings);
            Discord = new DiscordService(Settings);
            Spotify = new SpotifyService(Settings);
            Downloads = new DownloadWatcher(Dispatcher);
            clock = new ClockActivity(this) { Host = this };
            dashboard = new DashboardActivity(this) { Host = this };
            LiquidGlass.Configure(Settings.Material != "flat", Settings.GlassTint);
            Ui.Accent = Ui.ParseColor(Settings.AccentColor, Ui.Blue);
            Ui.SpotifyAccent = Ui.ParseColor(Settings.SpotifyColor, Color.FromRgb(0x1E, 0xD7, 0x60));
            BuildVisuals();

            tick = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(50) };
            tick.Tick += (s, e) => OnTick();
            holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
            holdTimer.Tick += (s, e) =>
            {
                holdTimer.Stop();
                if (mouseDown && !swiped) { longPressed = true; OnLongPress(); }
            };
            collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
            collapseTimer.Tick += (s, e) =>
            {
                collapseTimer.Stop();
                if (typing || (expandedAct is AiActivity x && x.Sticky)) return; // beim Tippen / KI-Ergebnis offen lassen
                if (!hovering && !catsHover) Collapse();
            };

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
        }

        // =================================================================
        //  Aufbau
        // =================================================================
        void BuildVisuals()
        {
            rootGrid = new Grid();
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 0, 0),
                RenderTransform = rowMove
            };
            rowPanel = row;

            pill = new Border
            {
                Background = Brushes.Black,
                Width = 124,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransformOrigin = new Point(0.5, 0)
            };
            var tg = new TransformGroup();
            tg.Children.Add(pillScale);
            tg.Children.Add(pillShift);
            pill.RenderTransform = tg;

            // Schatten als eigene Fläche HINTER der Pille: Läge der Effekt auf der Pille selbst, müsste WPF bei jeder
            // Bewegung im Inhalt (Wischen, Animationen) die ganze Seite neu durch den Schatten-Filter rendern → Ruckeln.
            var pillShadow = new Border
            {
                Background = Brushes.Black,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                RenderTransformOrigin = new Point(0.5, 0),
                RenderTransform = tg,
                IsHitTestVisible = false,
                Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 3, Direction = 270, Opacity = 0.5, Color = Colors.Black }
            };
            pillShadow.SetBinding(WidthProperty, new System.Windows.Data.Binding("Width") { Source = pill });
            pillShadow.SetBinding(HeightProperty, new System.Windows.Data.Binding("Height") { Source = pill });
            pillShadow.SetBinding(Border.CornerRadiusProperty, new System.Windows.Data.Binding("CornerRadius") { Source = pill });

            var inner = new Grid();
            contentHost = new Grid();
            inner.Children.Add(contentHost);
            pill.Child = inner;
            pill.SizeChanged += (s, e) =>
            {
                double w = pill.ActualWidth, h = pill.ActualHeight;
                double r = Math.Min(h / 2, 44); // groß: runde Ecken wie die iPhone-Island
                pill.CornerRadius = new CornerRadius(r);
                inner.Clip = new RectangleGeometry(new Rect(0, 0, w, h), r, r);
            };
            targetW = 124;
            targetH = 30;

            bubble = new Border
            {
                Background = Brushes.Black,
                Width = 0,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Opacity = 0,
                VerticalAlignment = VerticalAlignment.Top,
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Direction = 270, Opacity = 0.5, Color = Colors.Black }
            };
            bubbleContent = new Grid { ClipToBounds = true };
            bubble.Child = bubbleContent;
            bubble.MouseLeftButtonDown += (s, e) => e.Handled = true;
            bubble.MouseEnter += (s, e) => { lastInteraction = DateTime.Now; SetBubbleWide(true); };
            bubble.MouseLeave += (s, e) => SetBubbleWide(false);
            bubble.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (bubbleAct != null) { pinnedId = bubbleAct.Id; expandedAct = null; Relayout(); }
            };

            camDot = Ui.Dot(Ui.Green, 8);
            micDot = Ui.Dot(Ui.Orange, 8);
            camDot.Visibility = Visibility.Collapsed;
            micDot.Visibility = Visibility.Collapsed;
            micDot.Margin = new Thickness(4, 0, 0, 0);
            var dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(7, 14, 0, 0) };
            dots.Children.Add(camDot);
            dots.Children.Add(micDot);

            // Pille, Kreis und Punkte fahren zusammen nach oben weg …
            islandPart = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, RenderTransform = tuckIsland };
            var pillStack = new Grid { VerticalAlignment = VerticalAlignment.Top };
            pillStack.Children.Add(pillShadow);
            pillStack.Children.Add(pill);
            islandPart.Children.Add(pillStack);
            pillStackRef = pillStack;
            islandPart.Children.Add(bubble);
            islandPart.Children.Add(dots);

            // … die Katzen sitzen rechts daneben. Links hält ein unsichtbarer Platzhalter
            // gleicher Breite die Pille in der Mitte.
            catsSpacer = new Border { Width = 0, IsHitTestVisible = false };
            catsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 4, 0, 0),
                RenderTransform = tuckCats
            };
            catsPanel.MouseEnter += (s, e) => { catsHover = true; lastInteraction = DateTime.Now; };
            catsPanel.MouseLeave += (s, e) =>
            {
                catsHover = false;
                if (expandedAct != null && !hovering) collapseTimer.Start();
            };
            catsPanel.SizeChanged += (s, e) => UpdateCatsSpacer();

            row.Children.Add(catsSpacer);
            row.Children.Add(islandPart);
            row.Children.Add(catsPanel);
            rootGrid.Children.Add(row);
            Content = rootGrid;
            ApplyScale();

            // Seiten wischen wie bei iOS: Inhalt folgt der Maus 1:1, am Rand mit Gummiband-Widerstand,
            // beim Loslassen entscheidet die Wurfrichtung (Schwung) – nicht nur die Strecke.
            pill.PreviewMouseLeftButtonDown += (s, e) =>
            {
                swipeTracking = expandedAct != null && !IsInside<DragBar>(e.OriginalSource as DependencyObject)
                                && !IsInside<TextBox>(e.OriginalSource as DependencyObject);
                swipeStart = e.GetPosition(contentHost);
                swiped = false;
                pageDrag = false;
                swipeSamples.Clear();
            };
            pill.PreviewMouseMove += (s, e) =>
            {
                if (!swipeTracking || e.LeftButton != MouseButtonState.Pressed) return;
                var p = e.GetPosition(contentHost);
                double dx = p.X - swipeStart.X, dy = p.Y - swipeStart.Y;
                if (!pageDrag)
                {
                    // ~10 px Schwelle, dann klar waagerecht → Seite greifen
                    if (Math.Abs(dx) < 10 || Math.Abs(dx) < Math.Abs(dy) * 1.3) return;
                    pageDrag = swiped = true;
                    holdTimer.Stop();
                    // von der aktuellen Position weiter (falls die Seite gerade noch zurückfedert)
                    double curX = (currentContent?.RenderTransform as TranslateTransform)?.X ?? 0;
                    swipeStart = new Point(swipeStart.X - curX, swipeStart.Y);
                    pill.CaptureMouse();
                    dx = p.X - swipeStart.X;
                    ShowPeeks();
                }
                swipeSamples.Add((Environment.TickCount64, p.X));
                if (swipeSamples.Count > 8) swipeSamples.RemoveAt(0);
                MovePage(dx);
            };
            pill.PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (swipeTracking && swiped)
                {
                    e.Handled = true; // kein Klick auf eine Kachel auslösen
                    bool wasDrag = pageDrag;
                    pageDrag = false;
                    if (wasDrag) EndPageDrag(e.GetPosition(contentHost).X - swipeStart.X);
                    if (mouseDown) mouseDown = false;
                    pill.ReleaseMouseCapture();
                }
                pageDrag = false;
                swipeTracking = false;
            };
            pill.MouseLeftButtonDown += Pill_Down;
            // Mittlere Maustaste auf der Island: Text vom Bildschirm lesen
            pill.MouseDown += (s, e) =>
            {
                if (e.ChangedButton != MouseButton.Middle || !Settings.AiEnabled || !Settings.AiMiddleClickOcr) return;
                e.Handled = true;
                StartOcr();
            };
            HookDragDrop();
            pill.MouseMove += Pill_Move;
            pill.MouseLeftButtonUp += Pill_Up;
            pill.LostMouseCapture += (s, e) =>
            {
                if (pageDrag) { pageDrag = false; swipeTracking = false; SnapPageBack(0); }
                if (mouseDown) { mouseDown = false; holdTimer.Stop(); SpringBack(); }
                if (rightDown) { rightDown = false; EndDrag(); }
            };
            pill.MouseEnter += (s, e) =>
            {
                hovering = true;
                lastInteraction = DateTime.Now;
                foreach (var c in catViews) c.Wake();
                collapseTimer.Stop();
                UpdateHoverScale();
                Relayout();
            };
            pill.MouseLeave += (s, e) =>
            {
                hovering = false;
                UpdateHoverScale();
                if (expandedAct != null) collapseTimer.Start();
                Relayout();
            };
            // Rechte Maustaste: kurz klicken = Menü, gedrückt ziehen = Island verschieben
            pill.MouseRightButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (mouseDown) return;
                rightDown = true;
                dragging = false;
                dragStartCursor = CursorDip();
                dragStartX = Settings.PosX;
                dragStartY = Settings.PosY;
                var mr = MonRect;
                grabDx = dragStartCursor.X - (mr.Left + mr.Width / 2 + Settings.PosX);
                grabDy = dragStartCursor.Y - (mr.Top + Settings.PosY);
                pill.CaptureMouse();
            };
            pill.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (!rightDown) return;
                rightDown = false;
                bool wasDrag = dragging;
                pill.ReleaseMouseCapture();
                EndDrag();
                if (!wasDrag) ShowMenu();
            };
            // Mausrad: wechselt in der großen Island die Seite (statt Lautstärke).
            // Regler (Lautstärke/Helligkeit) und lange Listen bekommen das Rad weiter, bis die Liste am Ende ist.
            pill.PreviewMouseWheel += (s, e) =>
            {
                if (expandedAct == null) { e.Handled = true; return; }
                var src = e.OriginalSource as DependencyObject;
                if (FindUp<DragBar>(src) is DragBar db && db.WheelStep > 0) return;
                if (FindUp<ScrollViewer>(src) is ScrollViewer sv && sv.ScrollableHeight > 0.5)
                {
                    bool canScroll = e.Delta > 0 ? sv.VerticalOffset > 0.5 : sv.VerticalOffset < sv.ScrollableHeight - 0.5;
                    if (canScroll) return;
                }
                e.Handled = true;
                WheelPage(e.Delta);
            };
        }

        void OnSourceInitialized(object sender, EventArgs e)
        {
            hwnd = new WindowInteropHelper(this).Handle;
            Native.MakeToolWindow(hwnd);
            try { Native.AddClipboardFormatListener(hwnd); } catch { }
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            RegisterHotkeys();
            Deactivated += (o, ev) => EndTyping(false);
        }

        IntPtr WndProc(IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x031D) OnClipboard();                                   // WM_CLIPBOARDUPDATE
            else if (msg == 0x0021) { handled = true; return new IntPtr(typing ? 1 : 3); } // WM_MOUSEACTIVATE → nur beim Tippen aktivieren
            else if (msg == 0x0312) { handled = true; OnHotkey(wParam.ToInt32()); } // WM_HOTKEY
            return IntPtr.Zero;
        }

        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = Calendar.RefreshAsync();
            _ = Todo.RefreshAsync();
            Stats.Sample();
            PollPower();
            PollUsb();
            PollPrivacy();
            lastCaps = Native.CapsLock;
            Media.KeepPlayer = Settings.KeepPlayer;
            RefreshCats();
            HookServices();
            NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, e) => OnDisplaysChanged();
            api.Start(OnApi);
            tick.Start();
            Relayout();

            bool noCats = Settings.Cats.Count == 0;
            Alert("hello", noCats ? 8 : 6, g =>
            {
                g.Banner = true;
                g.Glyph = "✨";
                g.GlyphFont = Ui.EmojiFont;
                g.Accent = Ui.Yellow;
                g.Title = "Dynamic Island ist aktiv";
                g.Subtitle = noCats ? "Neu: Tippen und Katzen aussuchen" : "Antippen = entfalten · Halten = alle Funktionen";
                if (noCats) g.TapAction = () => { OpenSetup(); return true; };
            });

            await Media.InitAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            tick.Stop();
            foreach (var a in activities.ToArray())
            {
                try { a.OnRemoved(); } catch { }
            }
            try { Native.RemoveClipboardFormatListener(hwnd); } catch { }
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            api.Stop();
            for (int i = 1; i <= 6; i++) { try { Native.UnregisterHotKey(hwnd, i); } catch { } }
            try { Discord.Stop(); } catch { }
            try { AirPods?.Dispose(); } catch { }
            try { Downloads.Stop(); } catch { }
            Settings.Save();
            base.OnClosed(e);
        }

        // =================================================================
        //  Aktivitäten verwalten
        // =================================================================
        public void Add(Activity a)
        {
            a.Host = this;
            var existing = activities.FirstOrDefault(x => x.Id == a.Id);
            if (existing != null) RemoveInternal(existing);
            activities.Add(a);
            hidden.Remove(a.Id);
            CatsNotice(a);
            if (!(a is MediaActivity) && !(a is SpotifyActivity))
                lastActive = DateTime.Now; // kurz zeigen, wenn etwas Neues startet (Musik nur beim Überspringen)
            if (currentKey.Contains(a.Id)) currentKey = "";
            Relayout();
        }

        void RemoveInternal(Activity a)
        {
            activities.Remove(a);
            hidden.Remove(a.Id);
            if (expandedAct == a) expandedAct = null;
            if (a == mediaAct) mediaAct = null;
            try { a.OnRemoved(); } catch (Exception ex) { Program.Log(ex); }
        }

        /// <summary>Island kurz zeigen (z. B. Claude ist fertig oder wartet auf dich).</summary>
        public void Nudge()
        {
            lastActive = DateTime.Now;
            Relayout();
        }

        /// <summary>Baut die Ansicht einer Aktivität neu (z. B. wenn sich ihre Buttons ändern).</summary>
        public void Rebuild(Activity a)
        {
            if (a == currentAct) currentKey = "";
            if (a == bubbleAct) bubbleAct = null;
            Relayout();
        }

        /// <summary>Kurzer Hinweis. Gibt es schon einen mit derselben id, wird er aktualisiert.</summary>
        public GenericActivity Alert(string id, double seconds, Action<GenericActivity> setup)
        {
            var g = activities.FirstOrDefault(x => x.Id == id) as GenericActivity;
            bool isNew = g == null || !g.IsAlert;
            if (isNew) g = new GenericActivity { Id = id, IsAlert = true, Expandable = false };
            setup(g);
            g.ExpiresAt = DateTime.Now.AddSeconds(seconds);
            if (isNew) Add(g);
            else { g.Refresh(); Relayout(); }
            return g;
        }

        // =================================================================
        //  Layout + Animation
        // =================================================================
        public void Relayout()
        {
            var now = DateTime.Now;
            foreach (var a in activities.ToArray())
            {
                bool expired = a.ExpiresAt.HasValue && now >= a.ExpiresAt.Value;
                if (expired && hovering && a == currentAct && !a.Finished)
                {
                    a.ExpiresAt = now.AddSeconds(1); // Hinweis bleibt, solange die Maus drauf ist
                    expired = false;
                }
                if (a.Finished || expired) RemoveInternal(a);
            }

            var visible = activities.Where(a => !hidden.Contains(a.Id)).ToList();
            var alert = visible.Where(a => a.IsAlert).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
            var live = visible.Where(a => !a.IsAlert)
                              .OrderByDescending(a => a.Id == pinnedId ? int.MaxValue : a.Priority)
                              .ThenBy(a => a.CreatedAt)
                              .ToList();
            // Manche Aktivitäten (Claude) wohnen lieber im kleinen Kreis – außer du hast sie angetippt (angepinnt)
            var bubblePref = live.FirstOrDefault(a => a.PreferBubble && a.Id != pinnedId);
            if (bubblePref != null) live.Remove(bubblePref);
            Activity primary = alert ?? live.FirstOrDefault();
            Activity secondary = bubblePref ?? (alert != null ? live.FirstOrDefault() : live.Skip(1).FirstOrDefault());

            if (expandedAct != null && expandedAct != dashboard &&
                (!activities.Contains(expandedAct) || hidden.Contains(expandedAct.Id)))
                expandedAct = null;
            if (expandedAct != null && alert != null && alert.CreatedAt > expandedSince)
                expandedAct = null;

            Activity show;
            bool expanded = false;
            double w, h;
            if (expandedAct != null)
            {
                show = expandedAct;
                expanded = true;
                w = (PageWEff + (CatStrip ? CatColW : 0)) * PageScale; // Platz für Katzen + Kratzbaum
                h = PageH * PageScale;
                secondary = null;
            }
            else if (primary != null) { show = primary; w = primary.CompactWidth; h = primary.CompactHeight; }
            else if (hovering) { show = clock; w = clock.CompactWidth; h = clock.CompactHeight; }
            else
            {
                show = null;
                if (Settings.IdlePill) { w = 124; h = 30; }
                else { w = 84; h = 8; }
            }

            string key = show == null
                ? "idle"
                : (expanded ? "E:" : "C:") + show.Id + ":" + w.ToString(CultureInfo.InvariantCulture) + "x" + h.ToString(CultureInfo.InvariantCulture);
            if (key != currentKey)
            {
                currentKey = key;
                SetContent(show, expanded, w, h);
            }
            AnimateSize(w, h);
            rowWidth = w + (secondary != null ? 44 : 0) + 30 + 2 * (catsSpacer?.Width ?? 0);
            PlaceCats();
            UpdateNudge(!dragging);

            if (secondary != null)
            {
                if (bubbleAct != secondary)
                {
                    bubbleAct = secondary;
                    if (bubbleWide)
                    {
                        bubbleWide = false;
                        bubble.BeginAnimation(WidthProperty, new DoubleAnimation(36, TimeSpan.FromMilliseconds(300)));
                    }
                    bubbleContent.Children.Clear();
                    FrameworkElement m = null;
                    try { m = secondary.BuildMinimal(); } catch (Exception ex) { Program.Log(ex); }
                    if (m != null)
                    {
                        m.HorizontalAlignment = HorizontalAlignment.Center;
                        m.VerticalAlignment = VerticalAlignment.Center;
                        bubbleContent.Children.Add(m);
                    }
                }
                ShowBubble(true);
            }
            else
            {
                bubbleAct = null;
                ShowBubble(false);
            }
        }

        void SetContent(Activity a, bool expanded, double w, double h)
        {
            // Dieselbe Seite nur neu aufgebaut (z. B. jemand schaltet sich stumm)? Dann ohne Überblenden
            // tauschen – sonst blitzt die Island kurz schwarz auf.
            bool sameView = a != null && a == currentAct && expanded == contentWasExpanded && slideDir == 0 && currentContent != null;
            currentAct = a;
            FrameworkElement el = null;
            if (a != null)
            {
                try { el = expanded ? a.BuildExpanded() : a.BuildCompact(); }
                catch (Exception ex) { Program.Log(ex); }
            }

            var old = currentContent;
            if (old != null && sameView && el != null)
            {
                contentHost.Children.Remove(old);
                old = null;
            }
            // Wischen: die neue Seite gleitet nahtlos herein (genau von dort, wo die Vorschau gerade war),
            // die alte gleitet ganz hinaus – ohne Ausblenden, also nie ein schwarzes Loch dazwischen.
            bool sliding = slideDir != 0 && expanded && old != null && !Ui.ReduceMotion;
            double pageWLocal = Math.Max(1, (contentHost.ActualWidth > 1 ? contentHost.ActualWidth : w) / PageScale);
            var usedPeek = slideDir > 0 ? peekNext : slideDir < 0 ? peekPrev : null;
            double slideStart = usedPeek?.RenderTransform is TranslateTransform ptr ? ptr.X : slideDir * pageWLocal;
            ClearPeeks();
            if (old != null)
            {
                old.IsHitTestVisible = false;
                var gone = old;
                if (sliding)
                {
                    var otr = old.RenderTransform as TranslateTransform;
                    if (otr == null || otr.IsFrozen) { otr = new TranslateTransform(otr?.X ?? 0, 0); old.RenderTransform = otr; }
                    var oa = new DoubleAnimation(otr.X, -slideDir * pageWLocal, TimeSpan.FromMilliseconds(420)) { EasingFunction = new SpringEase { Damping = 1.0 } };
                    oa.Completed += (s, e) => { contentHost.Children.Remove(gone); CachePage(gone); };
                    otr.BeginAnimation(TranslateTransform.XProperty, oa);
                }
                else
                {
                    var fo = new DoubleAnimation(0, TimeSpan.FromMilliseconds(110));
                    fo.Completed += (s, e) => { contentHost.Children.Remove(gone); CachePage(gone); };
                    old.BeginAnimation(OpacityProperty, fo);
                }
            }
            currentContent = null;

            if (el != null)
            {
                double ps = expanded ? PageScale : 1;
                var wrap = new Grid
                {
                    Width = w / ps,
                    Height = h / ps,
                    LayoutTransform = expanded && ps < 0.999 ? new ScaleTransform(ps, ps) : null,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0
                };
                if (expanded && a != null)
                {
                    // Inhalt in die einheitliche Seitengröße einpassen: Seiten oben, kleine Aktivitäten mittig,
                    // zu lange Listen scrollen (Mausrad)
                    var nat = a.ExpandedSize;
                    bool pageLike = a == dashboard || IsPanel(a) || a is MediaActivity || a is SpotifyActivity || a is DiscordVoiceActivity;
                    // Height zählt ohne Rand – sonst ragte z. B. die Kachelreihe der Startseite unten aus der Island
                    // Seiten bekommen den Platz unter den Seitensymbolen (unten keine Punkte mehr)
                    double room = PageH - NavTop;
                    el.Height = Math.Max(0, Math.Min(nat.Height, room) - el.Margin.Top - el.Margin.Bottom);
                    if (nat.Height > PageNat + 0.5)
                    {
                        el = new ScrollViewer
                        {
                            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            Content = el
                        };
                    }
                    else
                    {
                        el.VerticalAlignment = pageLike ? VerticalAlignment.Top : VerticalAlignment.Center;
                        if (!pageLike)
                        {
                            el.Width = Math.Min(nat.Width, PageW);
                            el.HorizontalAlignment = HorizontalAlignment.Center;
                        }
                    }
                }

                bool side = expanded && CatStrip;
                if (side)
                {
                    wrap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    wrap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CatColW) });
                    var col = BuildCatStrip();
                    Grid.SetColumn(col, 1);
                    wrap.Children.Add(col);
                }
                else stripCats.Clear();
                if (expanded)
                {
                    wrap.RowDefinitions.Add(new RowDefinition { Height = new GridLength(NavTop) });
                    wrap.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    Grid.SetRow(el, 1);
                    foreach (UIElement c in wrap.Children) Grid.SetRowSpan(c, 2);
                }
                wrap.Children.Add(el);
                if (expanded) AddPageDots(wrap, side ? 2 : 1);
                contentHost.Children.Add(wrap);

                if (expanded) wrap.Tag = CurrentPageKey();   // für die Vorschau beim nächsten Wischen merken

                // seitlich wischen: neue Seite gleitet herein (sichtbar ab dem ersten Bild, kein Einblenden)
                if (sliding)
                {
                    var tr = new TranslateTransform(slideStart, 0);
                    wrap.RenderTransform = tr;
                    tr.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(slideStart, 0, TimeSpan.FromMilliseconds(420))
                    {
                        EasingFunction = new SpringEase { Damping = 1.0 }
                    });
                }
                else if (expanded && !contentWasExpanded && !Ui.ReduceMotion)
                {
                    // Apple-Stil: Inhalt wächst beim Aufklappen leicht aus der Island heraus
                    var sc = new ScaleTransform(0.94, 0.94);
                    wrap.RenderTransformOrigin = new Point(0.5, 0);
                    wrap.RenderTransform = sc;
                    var ga = new DoubleAnimation(1, TimeSpan.FromMilliseconds(420))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(old != null ? 80 : 40),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    sc.BeginAnimation(ScaleTransform.ScaleXProperty, ga);
                    sc.BeginAnimation(ScaleTransform.ScaleYProperty, ga);
                }
                if (sameView || sliding) wrap.Opacity = 1;
                else wrap.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(240))
                {
                    BeginTime = TimeSpan.FromMilliseconds(old != null ? 110 : 60)
                });
                currentContent = wrap;
            }
            contentWasExpanded = expanded && el != null;
        }
        bool contentWasExpanded;

        void AnimateSize(double w, double h)
        {
            if (Math.Abs(w - targetW) < 0.5 && Math.Abs(h - targetH) < 0.5) return;
            targetW = w;
            targetH = h;
            var d = TimeSpan.FromMilliseconds(580);
            pill.BeginAnimation(WidthProperty, new DoubleAnimation(w, d) { EasingFunction = EaseFor(pill.ActualWidth, w) });
            pill.BeginAnimation(HeightProperty, new DoubleAnimation(h, d) { EasingFunction = EaseFor(pill.ActualHeight, h) });
        }

        // Feder-Animation, außer sie würde beim Schrumpfen unter 0 schwingen
        static IEasingFunction EaseFor(double from, double to) =>
            (from - to) * 0.08 >= to - 1
                ? (IEasingFunction)new CubicEase { EasingMode = EasingMode.EaseOut }
                : new SpringEase();

        bool bubbleWide;

        /// <summary>Kreis beim Drüberfahren breit machen (z. B. Claude: was gerade passiert + Dauer).</summary>
        void SetBubbleWide(bool wide)
        {
            if (wide && (bubbleAct == null || bubbleAct.BubbleHoverWidth <= 0 || !bubbleShown)) wide = false;
            if (wide == bubbleWide) return;
            bubbleWide = wide;
            FrameworkElement m = null;
            try { m = wide ? bubbleAct.BuildBubbleHover() : bubbleAct?.BuildMinimal(); } catch (Exception ex) { Program.Log(ex); }
            bubbleContent.Children.Clear();
            if (m != null)
            {
                m.HorizontalAlignment = wide ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                m.VerticalAlignment = VerticalAlignment.Center;
                bubbleContent.Children.Add(m);
            }
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            bubble.BeginAnimation(WidthProperty, new DoubleAnimation(wide ? bubbleAct.BubbleHoverWidth : 36, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease });
        }

        void ShowBubble(bool show)
        {
            if (show == bubbleShown) return;
            bubbleShown = show;
            bubbleWide = false;
            var d = TimeSpan.FromMilliseconds(400);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            bubble.BeginAnimation(WidthProperty, new DoubleAnimation(show ? 36 : 0, d) { EasingFunction = ease });
            bubble.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1 : 0, d));
            bubble.BeginAnimation(MarginProperty, new ThicknessAnimation(new Thickness(show ? 8 : 0, 0, 0, 0), d) { EasingFunction = ease });
        }

        void UpdateHoverScale()
        {
            double s = hovering && expandedAct == null ? 1.04 : 1.0;
            var a = new DoubleAnimation(s, TimeSpan.FromMilliseconds(250)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            pillScale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            pillScale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }

        void SpringBack() =>
            pillShift.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(0, TimeSpan.FromMilliseconds(450)) { EasingFunction = new SpringEase() });

        // =================================================================
        //  Gesten: Tippen · Gedrückt halten · Wischen
        // =================================================================
        void Pill_Down(object sender, MouseButtonEventArgs e)
        {
            mouseDown = true;
            longPressed = false;
            swiped = false;
            downPos = e.GetPosition(this);
            holdTimer.Stop();
            holdTimer.Start();
            pill.CaptureMouse();
            e.Handled = true;
        }

        void Pill_Move(object sender, MouseEventArgs e)
        {
            if (rightDown)
            {
                var c = CursorDip();
                double mx = c.X - dragStartCursor.X, my = c.Y - dragStartCursor.Y;
                if (!dragging && (Math.Abs(mx) > 6 || Math.Abs(my) > 6))
                {
                    dragging = true;
                    if (expandedAct != null) Collapse();
                }
                if (dragging) DragTo();
                return;
            }
            if (!mouseDown || swiped) return;
            double dx = e.GetPosition(this).X - downPos.X;
            if (Math.Abs(dx) > 6) holdTimer.Stop();
            pillShift.BeginAnimation(TranslateTransform.XProperty, null);
            pillShift.X = Math.Max(-40, Math.Min(40, dx * 0.35));
            if (Math.Abs(dx) > 70)
            {
                swiped = true;
                OnSwipe(dx < 0 ? -1 : 1);
                SpringBack();
            }
        }

        void Pill_Up(object sender, MouseButtonEventArgs e)
        {
            if (!mouseDown) return;
            mouseDown = false;
            holdTimer.Stop();
            pill.ReleaseMouseCapture();
            var p = e.GetPosition(this);
            bool moved = Math.Abs(p.X - downPos.X) > 8 || Math.Abs(p.Y - downPos.Y) > 8;
            SpringBack();
            if (!longPressed && !swiped && !moved) OnTap();
            e.Handled = true;
        }

        /// <summary>
        /// Antippen: kleine Island → entfalten. Entfaltet → nochmal tippen öffnet die Übersicht
        /// mit allen Funktionen. Übersicht → nochmal tippen schließt.
        /// </summary>
        void OnTap()
        {
            if (expandedAct != null)
            {
                if (expandedAct != dashboard) Expand(dashboard);
                else Collapse();
                return;
            }
            var a = currentAct;
            if (a == null || a == clock) { Expand(dashboard); return; }
            if (a.IsAlert)
            {
                try { a.OnTap(); } catch (Exception ex) { Program.Log(ex); }
                a.Finished = true;
                Relayout();
                return;
            }
            if (a.CanExpand) { Expand(a); return; }
            try { a.OnTap(); } catch (Exception ex) { Program.Log(ex); }
        }

        /// <summary>Gedrückt halten: immer direkt zur Übersicht mit allen Funktionen.</summary>
        void OnLongPress()
        {
            if (expandedAct == dashboard) return;
            Expand(dashboard);
        }

        void OnSwipe(int dir)
        {
            if (expandedAct != null) { SwipePage(dir < 0 ? 1 : -1); return; }
            if (dir < 0)
            {
                // nach links: Aktivität ausblenden
                var a = currentAct;
                if (a == null || a == clock) return;
                if (a.IsAlert) a.Finished = true;
                else
                {
                    hidden.Add(a.Id);
                    hiddenStack.Push(a.Id);
                    if (pinnedId == a.Id) pinnedId = null;
                }
            }
            else
            {
                // nach rechts: zuletzt ausgeblendete zurückholen
                while (hiddenStack.Count > 0)
                {
                    var id = hiddenStack.Pop();
                    if (hidden.Remove(id)) { pinnedId = id; break; }
                }
            }
            Relayout();
        }

        /// <summary>Panels (Kurzbefehle, Downloads, Ton, Hilfe) verschwinden, sobald man sie verlässt.</summary>
        static bool IsPanel(Activity a) => a is ShortcutsActivity || a is DownloadsActivity || a is SoundActivity
                                            || a is ClipboardActivity || a is AppSwitcherActivity || a is BrightnessActivity
                                            || a is SpotifyListActivity || a is AirPodsActivity || a is BluetoothActivity || a is HeadphonesActivity || a is AiActivity || a is CalendarActivity || a is TodoActivity || a is WidgetPageActivity || a?.Id == "help";

        void Expand(Activity a)
        {
            if (a != expandedAct) EndTyping(true);
            if (expandedAct != null && expandedAct != a && IsPanel(expandedAct)) expandedAct.Finished = true;
            expandedAct = a;
            expandedSince = DateTime.Now;
            collapseTimer.Stop();
            Relayout();
            UpdateHoverScale();
        }

        public void Collapse()
        {
            EndTyping(true);
            if (expandedAct == null) return;
            if (IsPanel(expandedAct)) expandedAct.Finished = true;
            expandedAct = null;
            Relayout();
            UpdateHoverScale();
        }

        // =================================================================
        //  Position (verschieben) + automatisch nach oben wegfahren
        // =================================================================
        // ---- Bildschirm der Island (auch zweiter Monitor) ----
        Native.Monitor monitor;

        /// <summary>WPF-Einheiten pro Bildschirm-Pixel (Windows-Skalierung).</summary>
        static double DipPerPx
        {
            get
            {
                var p = Native.PrimaryMonitor();
                double pw = p.Bounds.Right - p.Bounds.Left;
                return pw > 0 ? SystemParameters.PrimaryScreenWidth / pw : 1;
            }
        }

        Native.Monitor CurrentMonitor()
        {
            if (monitor != null) return monitor;
            var all = Native.Monitors();
            monitor = all.FirstOrDefault(m => !string.IsNullOrEmpty(Settings.MonitorName) && m.Name == Settings.MonitorName)
                      ?? all.FirstOrDefault(m => m.Primary) ?? Native.PrimaryMonitor();
            return monitor;
        }

        /// <summary>Fläche des Bildschirms der Island in WPF-Einheiten.</summary>
        Rect MonRect
        {
            get
            {
                var m = CurrentMonitor();
                double k = DipPerPx;
                double w = m.Bounds.Right - m.Bounds.Left, h = m.Bounds.Bottom - m.Bounds.Top;
                if (w <= 0 || h <= 0) return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
                return new Rect(m.Bounds.Left * k, m.Bounds.Top * k, w * k, h * k);
            }
        }

        /// <summary>Bildschirme an-/abgesteckt oder umgestellt → Island neu platzieren.</summary>
        void OnDisplaysChanged()
        {
            monitor = null;
            Dispatcher.BeginInvoke(new Action(() => { MoveTo(Settings.PosX, Settings.PosY, false); Relayout(); }));
        }

        /// <summary>Mausposition in WPF-Einheiten (berücksichtigt die Windows-Skalierung).</summary>
        Point CursorDip()
        {
            if (!Native.GetCursorPos(out var p)) return new Point(-1, -1);
            double k = DipPerPx;
            return new Point(p.X * k, p.Y * k);
        }

        /// <summary>Setzt die Island an eine neue Stelle (x = Abstand zur Mitte, y = Abstand von oben) auf ihrem Bildschirm.</summary>
        void MoveTo(double x, double y, bool snap)
        {
            var r = MonRect;
            double maxX = Math.Max(0, r.Width / 2 - 70);
            double maxY = Math.Max(0, r.Height - 260);   // große Island soll noch auf den Bildschirm passen
            if (double.IsNaN(x)) x = 0;
            if (double.IsNaN(y)) y = 0;
            x = Math.Max(-maxX, Math.Min(maxX, x));
            y = Math.Max(0, Math.Min(maxY, y));
            if (snap && Math.Abs(x) < 18) x = 0; // rastet nur in der Mitte ein – sonst frei verschiebbar
            Settings.PosX = x;
            Settings.PosY = y;
            Left = r.Left + (r.Width - Width) / 2 + x;
            Top = r.Top + y;
            UpdateNudge(false);
        }

        double grabDx, grabDy;

        /// <summary>Beim Ziehen: Island folgt der Maus, auch auf einen anderen Bildschirm.</summary>
        void DragTo()
        {
            if (!Native.GetCursorPos(out var p)) return;
            var m = Native.MonitorAt(p.X, p.Y);
            if (m != null && (monitor == null || m.Name != monitor.Name))
            {
                monitor = m;
                Settings.MonitorName = m.Primary ? "" : m.Name;
            }
            var c = CursorDip();
            var r = MonRect;
            MoveTo(c.X - grabDx - (r.Left + r.Width / 2), c.Y - grabDy - r.Top, true);
        }

        void EndDrag()
        {
            if (!dragging) return;
            dragging = false;
            lastActive = DateTime.Now;
            Settings.Save();
        }

        /// <summary>Schiebt die Reihe so weit nach innen, dass sie am Bildschirmrand nicht abgeschnitten wird.</summary>
        /// <summary>Größe der Island (Einstellungen → Allgemein → Größe).</summary>
        public double Scale => Math.Max(0.8, Math.Min(1.5, Settings.Scale <= 0 ? 1 : Settings.Scale));

        public void ApplyScale()
        {
            if (rootGrid == null) return;
            rootGrid.LayoutTransform = Math.Abs(Scale - 1) < 0.01 ? Transform.Identity : new ScaleTransform(Scale, Scale);
            UpdateNudge(false);
        }

        void UpdateNudge(bool animated)
        {
            double sw = MonRect.Width;
            double sc = Scale;
            double lim = Math.Max(0, sw / 2 - rowWidth * sc / 2 - 8);
            double x = Settings.PosX;
            double n = Math.Max(-lim, Math.Min(lim, x)) - x; // in Bildschirm-Pixeln
            if (Math.Abs(n - nudgeTarget) < 0.5 && animated) return;
            nudgeTarget = n;
            if (animated)
                rowMove.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(n / sc, TimeSpan.FromMilliseconds(420)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else
            {
                rowMove.BeginAnimation(TranslateTransform.XProperty, null);
                rowMove.X = n / sc;
            }
        }

        /// <summary>Ist die Maus dort, wo die Island sitzt (bzw. am oberen Rand darüber)?</summary>
        bool CursorInZone()
        {
            var c = CursorDip();
            var r = MonRect;
            double cx = r.Left + r.Width / 2 + Settings.PosX + nudgeTarget;
            if (Math.Abs(c.X - cx) > Math.Max(targetW, 130) * Scale / 2 + 16) return false;
            double cy = c.Y - r.Top;
            if (Settings.PosY < 1) return cy >= 0 && cy <= 4; // oben: Maus an den Bildschirmrand
            return cy >= Settings.PosY && cy <= Settings.PosY + targetH * Scale + 14;
        }

        /// <summary>
        /// Was die Island sichtbar hält: Hinweise, Discord-Anrufe und laufende Sprachmemos.
        /// Musik, Timer und Downloads blenden sich nach kurzer Zeit mit aus (abschaltbar).
        /// </summary>
        bool KeepsVisible(Activity a) =>
            !Settings.HideWithActivities || a.IsAlert || a is VoiceMemoActivity;

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        ContextMenu openCm;

        /// <summary>Liegt der Mauszeiger wirklich über diesem Element? (echte Bildschirmkoordinaten)</summary>
        bool CursorOver(FrameworkElement el)
        {
            try
            {
                if (el == null || !el.IsVisible || PresentationSource.FromVisual(el) == null || !Native.GetCursorPos(out var p)) return false;
                var a = el.PointToScreen(new Point(0, 0));
                var b = el.PointToScreen(new Point(el.ActualWidth, el.ActualHeight));
                return p.X >= Math.Min(a.X, b.X) - 2 && p.X <= Math.Max(a.X, b.X) + 2 && p.Y >= Math.Min(a.Y, b.Y) - 2 && p.Y <= Math.Max(a.Y, b.Y) + 2;
            }
            catch { return false; }
        }

        /// <summary>
        /// Hängende Zustände aufräumen – sonst blieb die Island manchmal für immer sichtbar
        /// (z. B. Maus außerhalb losgelassen, MouseLeave verpasst, Menü ohne „Closed“).
        /// </summary>
        void ClearStuckFlags()
        {
            if (mouseDown && (GetAsyncKeyState(0x01) & 0x8000) == 0) mouseDown = false;
            if (rightDown && (GetAsyncKeyState(0x02) & 0x8000) == 0) rightDown = false;
            if (menuOpen && (openCm == null || !openCm.IsOpen)) menuOpen = false;
            if (hovering && !CursorOver(pill) && !CursorOver(bubble))
            {
                hovering = false;
                if (expandedAct != null && !catsHover) collapseTimer.Start();   // große Island klappt dann auch wieder zu
            }
            if (catsHover && !CursorOver(catsPanel)) catsHover = false;
        }

        bool prevLb, prevRb;

        /// <summary>Klick irgendwo neben die große Island: sofort zuklappen und – wenn nichts läuft – wegfahren.</summary>
        void CheckClickOutside()
        {
            short ls = GetAsyncKeyState(0x01), rs = GetAsyncKeyState(0x02);
            bool lb = (ls & 0x8000) != 0, rb = (rs & 0x8000) != 0;
            // auch ganz kurze Klicks zwischen zwei Ticks mitnehmen (Bit 0 = „seit dem letzten Abfragen gedrückt“)
            bool press = (lb && !prevLb) || (rb && !prevRb) || (!lb && (ls & 1) != 0) || (!rb && (rs & 1) != 0);
            prevLb = lb; prevRb = rb;
            if (!press || expandedAct == null || menuOpen || tucked) return;
            if ((DateTime.Now - expandedSince).TotalMilliseconds < 300) return;
            if (CursorOver(pill) || CursorOver(bubble) || CursorOver(catsPanel)) return;
            hovering = false;
            collapseTimer.Stop();
            Collapse();
            lastActive = DateTime.MinValue;   // Auto-Ausblenden greift sofort
        }

        void UpdateAutoHide()
        {
            CheckClickOutside();
            if (fsHidden) { SetTucked(true); return; }
            if (tickCount % 10 == 0) ClearStuckFlags();
            bool busy = !Settings.AutoHide || hovering || mouseDown || rightDown || menuOpen || expandedAct != null
                        || catsHover || setupWin != null
                        || activities.Any(a => !hidden.Contains(a.Id) && KeepsVisible(a))
                        || CursorInZone();
            if (busy) lastActive = DateTime.Now;
            SetTucked(!busy && (DateTime.Now - lastActive).TotalSeconds >= HideAfterSeconds);
        }

        void SetTucked(bool t)
        {
            if (t == tucked) return;
            tucked = t;
            AnimateTuck(islandPart, tuckIsland, t);
            PlaceCats();
        }

        const int TuckMs = 340;
        static IEasingFunction TuckEase(bool hide) =>
            new CubicEase { EasingMode = hide ? EasingMode.EaseIn : EasingMode.EaseOut };

        void AnimateTuck(FrameworkElement el, TranslateTransform tr, bool hide)
        {
            el.IsHitTestVisible = !hide;
            // Auftauchen = exakt rückwärts abgespieltes Wegfahren: gleiche Dauer, gespiegelte Kurve
            var d = TimeSpan.FromMilliseconds(Ui.ReduceMotion ? 1 : TuckMs);
            tr.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(hide ? -(targetH + 40) : 0, d) { EasingFunction = TuckEase(hide) });
            el.BeginAnimation(OpacityProperty,
                new DoubleAnimation(hide ? 0 : 1, d) { EasingFunction = TuckEase(hide) });
        }

        // =================================================================
        //  Katzen
        // =================================================================
        /// <summary>Baut die Katzen neben der Island neu auf (nach Änderungen in den Einstellungen).</summary>
        public void RefreshCats()
        {
            catViews.Clear();
            catsPanel.Children.Clear();
            if (Settings.CatsMode == "always" && Settings.Cats != null)
            {
                foreach (var cfg in Settings.Cats.Take(4))
                {
                    var v = new CatView(cfg, 30);
                    if (catViews.Count > 0) v.Margin = new Thickness(5, 0, 0, 0);
                    v.MouseLeftButtonDown += (s, e) => e.Handled = true;
                    v.MouseLeftButtonUp += (s, e) => { e.Handled = true; PetCat(v); };
                    v.MouseRightButtonUp += (s, e) => { e.Handled = true; ShowMenu(); };
                    catViews.Add(v);
                    catsPanel.Children.Add(v);
                }
            }
            catsPanel.Visibility = catViews.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            catPlaced = false;
            PlaceCats();
            UpdateCatsSpacer();
        }

        bool catPlaced, catShown;
        double catY;

        /// <summary>
        /// Katzen ein- oder ausblenden. Standard: Sie tauchen auf, wenn die Island groß wird,
        /// und setzen sich unten neben sie. Wählbar: immer / wenn groß / aus.
        /// </summary>
        void PlaceCats()
        {
            if (catsPanel == null) return;
            bool wanted = catViews.Count > 0 && Settings.CatsMode == "always";
            bool show = wanted && !fsHidden && !(tucked && !Settings.CatsStayVisible);
            double y = show
                ? (expandedAct != null ? Math.Max(0, targetH - 34) : 0) // groß: unten neben der Island sitzen
                : (tucked ? -(targetH + 40) : 0);
            if (catPlaced && show == catShown && Math.Abs(y - catY) < 0.5) return;
            bool appearing = show && (!catPlaced || !catShown);
            catPlaced = true;
            catShown = show;
            catY = y;
            catsPanel.IsHitTestVisible = show;
            if (!show) catsHover = false;
            var cd = TimeSpan.FromMilliseconds(Ui.ReduceMotion ? 1 : TuckMs);
            tuckCats.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(y, cd) { EasingFunction = TuckEase(!show) });
            catsPanel.BeginAnimation(OpacityProperty,
                new DoubleAnimation(show ? 1 : 0, cd) { EasingFunction = TuckEase(!show) });
            if (appearing)
            {
                foreach (var c in catViews) c.Wake();
                catViews[Rnd.Next(catViews.Count)].Hop();
            }
        }

        /// <summary>Seitliche Spalte der großen Island: hoher Kratzbaum mit deinen Katzen.</summary>
        CatView topCat;          // sitzt oben auf dem Kratzbaum und schläft dort
        DateTime topCatAwake;    // zuletzt geweckt (Streicheln) – kurz danach schläft sie wieder ein
        const double TopCatNapAfter = 6;

        FrameworkElement BuildCatStrip()
        {
            stripCats.Clear();
            topCat = null;
            var c = new Canvas
            {
                Width = 64,
                Height = 112,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 16)
            };
            c.Children.Add(new CatTower());
            var cats = Settings.Cats.Take(4).ToList();
            for (int i = 0; i < cats.Count; i++)
            {
                var v = new CatView(cats[i], 26);
                v.MouseLeftButtonDown += (s, e) => e.Handled = true;
                v.MouseLeftButtonUp += (s, e) => { e.Handled = true; PetCat(v); };
                Canvas.SetLeft(v, CatTower.Spots[i].X);
                Canvas.SetTop(v, CatTower.Spots[i].Y);
                c.Children.Add(v);
                stripCats.Add(v);
            }
            if (stripCats.Count > 0)
            {
                // die Katze ganz oben döst: nach dem Aufklappen noch kurz wach, dann schläft sie ein
                topCat = stripCats[0];
                topCatAwake = DateTime.Now.AddSeconds(-(TopCatNapAfter - 1.5));
                var others = stripCats.Skip(1).ToList();
                if (others.Count == 0) return c;
                var hopper = others[Rnd.Next(others.Count)];
                Dispatcher.BeginInvoke(new Action(hopper.Hop), DispatcherPriority.Background);
            }
            return c;
        }

        // =================================================================
        //  Seiten der großen Island: seitlich wischen statt Buttons
        // =================================================================
        static readonly string[] PanelKeys = { "shortcuts", "apps", "clipboard", "sound", "brightness", "downloads", "ai", "calendar", "todo", "weather" };
        int slideDir;
        Point swipeStart;
        bool swipeTracking, pageDrag;
        readonly List<(long t, double x)> swipeSamples = new List<(long, double)>();
        double slideFrom;   // Startversatz der neuen Seite (nach einem Wisch näher an der Hand)

        bool HasNeighbor(int dir)
        {
            var keys = PageKeys();
            int i = keys.IndexOf(CurrentPageKey() ?? "dashboard");
            int j = (i < 0 ? 0 : i) + dir;
            return j >= 0 && j < keys.Count;
        }

        /// <summary>Gummiband: je weiter über den Rand, desto weniger folgt der Inhalt.</summary>
        static double Rubberband(double over, double dim, double c = 0.55) => over * dim * c / (dim + c * Math.Abs(over));

        // ---- Vorschau der Nachbarseiten beim Wischen (statt Schwarz) ----
        readonly Dictionary<string, FrameworkElement> pageCache = new Dictionary<string, FrameworkElement>();
        FrameworkElement peekPrev, peekNext;
        int peekGen;

        double PageWLocal => Math.Max(1, contentHost.ActualWidth / PageScale);

        /// <summary>Zuletzt angezeigte Fassung einer Seite aufheben – sie dient beim Wischen als Vorschau.</summary>
        void CachePage(FrameworkElement wrap)
        {
            if (wrap?.Tag is string key && !string.IsNullOrEmpty(key)) pageCache[key] = wrap;
        }

        void ClearPeeks()
        {
            foreach (var p in new[] { peekPrev, peekNext })
            {
                if (p == null) continue;
                (p.RenderTransform as TranslateTransform)?.BeginAnimation(TranslateTransform.XProperty, null);
                contentHost.Children.Remove(p);
            }
            peekPrev = peekNext = null;
        }

        void ShowPeeks()
        {
            ClearPeeks();
            peekGen++;
            var keys = PageKeys();
            int i = keys.IndexOf(CurrentPageKey() ?? "dashboard");
            if (i < 0) return;
            double wl = PageWLocal;
            if (i + 1 < keys.Count) peekNext = MakePeek(keys[i + 1], wl);
            if (i - 1 >= 0) peekPrev = MakePeek(keys[i - 1], -wl);
        }

        FrameworkElement MakePeek(string key, double x)
        {
            FrameworkElement el;
            if (pageCache.TryGetValue(key, out var cached) && cached.Parent == null)
            {
                el = cached;
                el.BeginAnimation(OpacityProperty, null);
                el.Opacity = 1;
            }
            else el = PeekPlaceholder(key);
            el.IsHitTestVisible = false;
            el.RenderTransform = new TranslateTransform(x, 0);
            contentHost.Children.Insert(0, el);
            return el;
        }

        /// <summary>Seite noch nie geöffnet: Symbol + Name als Vorschau.</summary>
        FrameworkElement PeekPlaceholder(string key)
        {
            double ps = PageScale;
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var g = Ui.Glyph(PageGlyph(key) ?? "", 30, Ui.Br(Colors.White, 150));
            g.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(g);
            var t = Ui.Text(PageTitle(key), 15, Ui.Br(Colors.White, 170));
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(t);
            return new Grid
            {
                Width = contentHost.ActualWidth / ps,
                Height = contentHost.ActualHeight / ps,
                LayoutTransform = ps < 0.999 ? new ScaleTransform(ps, ps) : null,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Black,
                Children = { stack }
            };
        }

        void MovePeeks(double x)
        {
            double wl = PageWLocal;
            if (peekNext?.RenderTransform is TranslateTransform n) n.X = x + wl;
            if (peekPrev?.RenderTransform is TranslateTransform p) p.X = x - wl;
        }

        void MovePage(double dx)
        {
            if (currentContent == null) return;
            if (!(currentContent.RenderTransform is TranslateTransform tr) || tr.IsFrozen)
                currentContent.RenderTransform = tr = new TranslateTransform();
            tr.BeginAnimation(TranslateTransform.XProperty, null);
            int dir = dx < 0 ? 1 : -1;                         // nach links ziehen = nächste Seite
            tr.X = HasNeighbor(dir) ? dx : Rubberband(dx, PageW);
            MovePeeks(tr.X);                                   // Nachbarseite schaut schon herein
        }

        /// <summary>Geschwindigkeit der letzten ~90 ms (DIP pro Sekunde).</summary>
        double SwipeVelocity()
        {
            if (swipeSamples.Count < 2) return 0;
            var last = swipeSamples[swipeSamples.Count - 1];
            var first = swipeSamples.FirstOrDefault(x => last.t - x.t <= 90);
            if (first.t == 0 || last.t - first.t < 8) first = swipeSamples[Math.Max(0, swipeSamples.Count - 3)];
            double ms = Math.Max(1, last.t - first.t);
            return (last.x - first.x) / ms * 1000;
        }

        void EndPageDrag(double dx)
        {
            double v = SwipeVelocity();
            // Apple: Schwung projizieren (wo würde die Bewegung auslaufen?) und danach entscheiden
            const double d = 0.99;
            double projected = dx + v / 1000 * d / (1 - d);
            int dir = projected < 0 ? 1 : -1;
            if (Math.Abs(projected) > PageW * 0.22 && HasNeighbor(dir))
            {
                double cur = (currentContent?.RenderTransform as TranslateTransform)?.X ?? 0;
                slideFrom = dir * Math.Max(40, Math.Min(140, 120 - Math.Abs(cur) * 0.3));
                SwipePage(dir);
                slideFrom = 0;
                // Seite hat sich doch nicht gewechselt (z. B. öffnet nur eine App) → zurückfedern
                if (peekNext != null || peekPrev != null) SnapPageBack(v);
            }
            else SnapPageBack(v);
        }

        /// <summary>Zurückfedern von der aktuellen Position – kritisch gedämpft, ohne Nachschwingen.</summary>
        void SnapPageBack(double velocity)
        {
            if (currentContent == null) return;
            var tr = currentContent.RenderTransform as TranslateTransform;
            if (tr == null) return;
            double from = tr.X;
            var dur = TimeSpan.FromMilliseconds(Ui.ReduceMotion ? 120 : 380);
            IEasingFunction ease = new SpringEase { Damping = Math.Abs(velocity) > 600 ? 0.8 : 1.0 };
            tr.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(from, 0, dur) { EasingFunction = ease });
            // Vorschauseiten gleiten mit zurück und verschwinden danach
            double wl = PageWLocal;
            int gen = peekGen;
            if (peekNext?.RenderTransform is TranslateTransform n) n.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(n.X, wl, dur) { EasingFunction = ease });
            if (peekPrev?.RenderTransform is TranslateTransform p)
            {
                var pa = new DoubleAnimation(p.X, -wl, dur) { EasingFunction = ease };
                p.BeginAnimation(TranslateTransform.XProperty, pa);
            }
            var cleanup = new DispatcherTimer { Interval = dur + TimeSpan.FromMilliseconds(30) };
            cleanup.Tick += (s, e) => { cleanup.Stop(); if (gen == peekGen && !pageDrag) ClearPeeks(); };
            cleanup.Start();
        }

        /// <summary>Hast du im Player-Wechsler eine andere App als Spotify gewählt?</summary>
        bool OtherSourcePinned => Media.PinnedAppId != null && MediaService.NameOf(Media.PinnedAppId) != "Spotify";

        Activity PlayerAct()
        {
            bool sp = spotifyAct != null && activities.Contains(spotifyAct);
            bool md = mediaAct != null && activities.Contains(mediaAct);
            if (md && (OtherSourcePinned || !sp)) return mediaAct;
            return sp ? spotifyAct : null;
        }

        /// <summary>Player-Wechsler: alle Apps, die gerade Medien haben – antippen wechselt den Player.</summary>
        public async void ShowPlayerSources()
        {
            var sources = await Media.SourcesAsync();
            // Spotify über den eingebauten Player auch dann anbieten, wenn Windows es gerade nicht meldet
            if (Spotify.Connected && !sources.Any(x => x.Name == "Spotify"))
                sources.Insert(0, new MediaService.Source { AppId = "Spotify.exe", Name = "Spotify", Title = Spotify.Track?.Title ?? "", Playing = Spotify.Playing });
            var cm = new ContextMenu();
            cm.Items.Add(MHint("Player wechseln"));
            if (sources.Count == 0) cm.Items.Add(MHint("Gerade spielt nichts"));
            var shown = PlayerAct();
            foreach (var src in sources.OrderByDescending(x => x.Playing))
            {
                var s = src;
                bool isSp = s.Name == "Spotify";
                bool current = isSp ? shown == spotifyAct || (shown == mediaAct && Media.AppName == "Spotify") : shown == mediaAct && s.Current;
                string what = string.IsNullOrWhiteSpace(s.Title) ? "" : " – " + Short(s.Title + (string.IsNullOrWhiteSpace(s.Artist) ? "" : " · " + s.Artist), 42);
                var mi = MItem((s.Playing ? "▶  " : "❚❚  ") + s.Name + what, () => PickPlayer(s));
                mi.IsChecked = current;
                cm.Items.Add(mi);
            }
            if (Media.PinnedAppId != null)
            {
                cm.Items.Add(new Separator());
                cm.Items.Add(MItem("Automatisch (was gerade spielt)", () => { Media.Select(null); SyncMedia(); OpenPage("player"); }));
            }
            OpenMenu(cm);
        }

        void PickPlayer(MediaService.Source s)
        {
            bool isSp = s.Name == "Spotify";
            if (isSp && Spotify.Connected)
            {
                Media.Select(null);           // Spotify = eingebauter Player
                SyncSpotify();
                var p = spotifyAct != null && activities.Contains(spotifyAct) ? spotifyAct : null;
                if (p != null) { pinnedId = p.Id; Expand(p); return; }
            }
            Media.Select(s.AppId);
            _ = Media.RefreshPropsAsync();
            SyncMedia();
            if (mediaAct != null && activities.Contains(mediaAct)) { pinnedId = mediaAct.Id; Expand(mediaAct); }
        }

        /// <summary>
        /// Deine Seiten (Einstellungen → Seiten: sortieren, entfernen, Widget-Seiten anlegen).
        /// Laufende Aktivitäten (Timer, Anruf …) kommen automatisch hinter Übersicht/Player.
        /// </summary>
        List<string> PageKeys()
        {
            var keys = new List<string>();
            foreach (var k in Settings.Pages ?? new List<string> { "dashboard" })
            {
                if (string.IsNullOrEmpty(k) || keys.Contains(k)) continue;
                if (k == "player") { if (PlayerAct() != null || Spotify.Connected) keys.Add(k); }
                else if (k == "airpods" || k == "bluetooth") { if (!keys.Contains("bluetooth")) keys.Add("bluetooth"); }
                else if (k.StartsWith("w:")) { if (Settings.WidgetPages.Any(p => "w:" + p.Id == k)) keys.Add(k); }
                else if (k == "ai") { if (Settings.AiEnabled) keys.Add(k); }
                else if (k == "dashboard" || PanelKeys.Contains(k)) keys.Add(k);
            }
            if (!keys.Contains("dashboard")) keys.Insert(0, "dashboard");
            var live = activities.Where(x => !x.IsAlert && x.CanExpand && !IsPanel(x) && !hidden.Contains(x.Id)
                                             && !(x is MediaActivity) && !(x is SpotifyActivity) && !(x is SpotifyListActivity))
                                 .Select(a => "live:" + a.Id).ToList();
            int at = keys.Contains("player") ? keys.IndexOf("player") + 1 : keys.IndexOf("dashboard") + 1;
            keys.InsertRange(at, live);
            return keys;
        }

        // =================================================================
        //  Kalender: Termine anzeigen, erinnern und ansagen
        // =================================================================
        DateTime calDay;
        readonly HashSet<string> calDone = new HashSet<string>();

        public void ShowCalendar()
        {
            var a = activities.OfType<CalendarActivity>().FirstOrDefault() ?? new CalendarActivity(this) { Host = this };
            a.ExpiresAt = DateTime.Now.AddSeconds(60);
            if (activities.Contains(a)) Rebuild(a);
            else Add(a);
            Expand(a);
        }

        void CheckCalendar()
        {
            if (!Settings.CalendarEnabled || !Calendar.HasCalendars) return;
            var now = DateTime.Now;
            foreach (var e in Calendar.Events.ToList())
            {
                if (e.AllDay) continue;
                double mins = (e.Start - now).TotalMinutes;
                int remind = Settings.CalendarRemind;
                if (remind > 0 && mins > 0.5 && mins <= remind && calDone.Add("r|" + e.Key))
                {
                    int m = Math.Max(1, (int)Math.Round(mins));
                    CalendarAlert(e, $"in {m} Min · {e.Start:HH:mm}");
                    ShowCalendarCountdown(e);
                    if (Settings.CalendarSpeak) _ = Speaker.SpeakAsync($"Erinnerung: In {m} {(m == 1 ? "Minute" : "Minuten")} beginnt {e.Title}.");
                }
                if (Settings.CalendarAtStart && mins <= 0.5 && mins > -2 && calDone.Add("s|" + e.Key))
                {
                    CalendarAlert(e, "beginnt jetzt" + (e.End > e.Start ? " · bis " + e.End.ToString("HH:mm") : ""));
                    if (Settings.CalendarSpeak) _ = Speaker.SpeakAsync($"Jetzt beginnt {e.Title}.");
                }
            }
        }

        // ---- Wetter ----
        public void ShowWeather()
        {
            // Wetter wohnt jetzt mit dem Kalender auf der Seite „Heute“
            _ = Weather.RefreshAsync();
            ShowCalendar();
        }

        // ---- Microsoft To Do ----
        public void ShowTodo(bool focus = false)
        {
            var a = activities.OfType<TodoActivity>().FirstOrDefault() ?? new TodoActivity(this) { Host = this };
            a.ExpiresAt = DateTime.Now.AddSeconds(60);
            if (activities.Contains(a)) Rebuild(a);
            else Add(a);
            Expand(a);
            if (focus) Dispatcher.BeginInvoke(new Action(a.FocusInput), DispatcherPriority.Loaded);
        }

        void CheckTodo()
        {
            if (!Todo.Connected || !Settings.TodoReminders) return;
            var now = DateTime.Now;
            foreach (var t in Todo.Tasks.ToList())
            {
                if (!t.Reminder.HasValue) continue;
                var r = t.Reminder.Value;
                if (now < r || (now - r).TotalMinutes > 2) continue;
                if (!calDone.Add("t|" + t.Id + "|" + r.ToString("yyyyMMddHHmm"))) continue;
                var task = t;
                Alert("todo-alert", 12, g =>
                {
                    g.Banner = true;
                    g.Glyph = TodoActivity.Glyph;
                    g.Accent = Ui.Accent;
                    g.Title = task.Title;
                    g.Subtitle = "Erinnerung · Microsoft To Do";
                    g.CompactW = 380;
                    g.TapAction = () => { ShowTodo(); return true; };
                });
                if (Settings.TodoSpeak) _ = Speaker.SpeakAsync("Erinnerung: " + task.Title + ".");
            }
        }

        void CalendarAlert(CalEvent e, string when)
        {
            string loc = string.IsNullOrEmpty(e.Location) || e.Location.Contains("http") ? "" : " · " + e.Location.Split('\n')[0];
            Alert("cal-alert", 10, g =>
            {
                g.Banner = true;
                g.Glyph = CalendarActivity.Glyph;
                g.Accent = Ui.Red;
                g.Title = e.Title;
                g.Subtitle = when + loc;
                g.CompactW = 380;
                g.Buttons.Clear();
                var link = e.Link;
                g.TapAction = link != null ? () => { Open(link); return true; } : (Func<bool>)(() => { ShowCalendar(); return true; });
            });
        }

        /// <summary>Live-Aktivität bis kurz nach Beginn: Countdown in der kleinen Island.</summary>
        void ShowCalendarCountdown(CalEvent e)
        {
            string id = "cal-" + e.Key;
            if (activities.Any(x => x.Id == id)) return;
            var g = new GenericActivity
            {
                Id = id,
                Host = this,
                Priority = 30,
                Glyph = CalendarActivity.Glyph,
                Accent = Ui.Red,
                LeftText = e.Title,
                CompactW = 300,
                Title = e.Title,
                Subtitle = e.Start.ToString("HH:mm") + "–" + e.End.ToString("HH:mm") + (string.IsNullOrEmpty(e.Location) ? "" : " · " + e.Location.Split('\n')[0]),
                RightColor = Ui.Red,
                ExpiresAt = e.Start.AddMinutes(5)
            };
            g.Ticker = x =>
            {
                double m = (e.Start - DateTime.Now).TotalMinutes;
                x.RightText = m > 0 ? "in " + Math.Max(1, (int)Math.Ceiling(m)) + " Min" : "jetzt";
            };
            if (e.Link != null)
            {
                var link = e.Link;
                g.Buttons.Add(new ActionButton("\uE714", Ui.Br(Ui.Green), () => Open(link)));
            }
            g.Buttons.Add(new ActionButton(CalendarActivity.Glyph, Ui.Br(Colors.White, 40), ShowCalendar));
            g.Ticker(g);
            Add(g);
        }

        // =================================================================
        //  KI: Gemini-Chat, Text-Drop-Zone, Texterkennung
        // =================================================================
        AiActivity AiAct => ai ??= new AiActivity(this) { Host = this };

        /// <summary>KI-Seite öffnen (focus = gleich ins Eingabefeld tippen).</summary>
        public void ShowAi(bool focus = false)
        {
            var a = AiAct;
            a.Finished = false;
            a.ExpiresAt = null;
            if (!activities.Contains(a)) Add(a);
            if (expandedAct != a) Expand(a);
            lastActive = DateTime.Now;
            if (focus) Dispatcher.BeginInvoke(new Action(a.FocusInput), DispatcherPriority.Loaded);
        }

        public void CloseAi()
        {
            if (expandedAct is AiActivity) Collapse();
            else EndTyping(true);
        }

        // ---- Tippen: die Island ist sonst ein Fenster, das nie den Fokus bekommt ----
        bool typing;
        IntPtr typingPrev;
        public bool IsTyping => typing;

        public void BeginTyping(TextBox tb)
        {
            if (!typing)
            {
                typingPrev = AiNative.GetForegroundWindow();
                AiNative.SetNoActivate(hwnd, false);
                typing = true;
            }
            AiNative.SetForegroundWindow(hwnd);
            Activate();
            tb.Focus();
            Keyboard.Focus(tb);
        }

        /// <summary>Tippen beenden. restore = vorheriges Fenster wieder nach vorn (nur wenn wir noch vorne sind).</summary>
        public void EndTyping(bool restore)
        {
            if (!typing) return;
            typing = false;
            bool weAreFront = AiNative.GetForegroundWindow() == hwnd;
            AiNative.SetNoActivate(hwnd, true);
            Keyboard.ClearFocus();
            if (restore && weAreFront && typingPrev != IntPtr.Zero && typingPrev != hwnd) AiNative.SetForegroundWindow(typingPrev);
            typingPrev = IntPtr.Zero;
        }

        /// <summary>Bildschirm einfrieren → Bereich wählen → Text erkennen → in der Island zeigen.</summary>
        public async void StartOcr()
        {
            if (ocrRunning) return;
            ocrRunning = true;
            try
            {
                Collapse();
                double op = Opacity;
                Opacity = 0;                      // Island nicht mit aufnehmen
                await Task.Delay(180);
                System.Windows.Media.Imaging.BitmapSource shot;
                try { shot = ScreenOcr.CapturePrimary(); }
                finally { Opacity = op; }
                var region = await ScreenOcr.PickRegionAsync(shot);
                if (region == null) return;
                AiAct.ShowBusy("Text wird erkannt …");
                string text;
                try { text = await ScreenOcr.RecognizeAsync(region); }
                catch (Exception ex) { AiAct.ShowResult("Texterkennung", ex.Message, false); return; }
                if (string.IsNullOrWhiteSpace(text)) AiAct.ShowResult("Texterkennung", "Im Bereich wurde kein Text gefunden.", false);
                else
                {
                    try { Clipboard.SetText(text); } catch { }
                    AiAct.ShowResult("Erkannter Text · kopiert", text, true);
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            finally { ocrRunning = false; }
        }
        bool ocrRunning;

        // ---- Text auf die Island ziehen ----
        DispatcherTimer dragLeaveTimer;
        bool dragOpened;

        void HookDragDrop()
        {
            AllowDrop = true;
            pill.AllowDrop = true;
            dragLeaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            dragLeaveTimer.Tick += (s, e) =>
            {
                dragLeaveTimer.Stop();
                if (CursorOver(pill)) return;
                ai?.EndDrop();
                if (dragOpened && expandedAct is AiActivity) Collapse();
                dragOpened = false;
            };
            DragEnter += (s, e) =>
            {
                if (!Settings.AiEnabled || !AiActivity.HasText(e.Data)) { e.Effects = DragDropEffects.None; return; }
                e.Effects = DragDropEffects.Copy;
                dragLeaveTimer.Stop();
                lastActive = DateTime.Now;
                if (!(expandedAct is AiActivity)) dragOpened = true;
                ShowAi();
                AiAct.BeginDrop();
            };
            DragOver += (s, e) =>
            {
                if (!Settings.AiEnabled || !AiActivity.HasText(e.Data)) { e.Effects = DragDropEffects.None; e.Handled = true; return; }
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                dragLeaveTimer.Stop();
            };
            DragLeave += (s, e) => dragLeaveTimer.Start();
            Drop += (s, e) =>
            {
                // nicht auf eine der vier Karten fallen gelassen → Text zeigen, Aktion per Chip wählen
                dragLeaveTimer.Stop();
                dragOpened = false;
                var t = AiActivity.DroppedText(e.Data);
                if (string.IsNullOrWhiteSpace(t)) { ai?.EndDrop(); return; }
                AiAct.ShowResult("Text", t, true);
            };
        }

        static class AiNative
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
            [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);

            public static void SetNoActivate(IntPtr h, bool on)
            {
                if (h == IntPtr.Zero) return;
                const int GWL_EXSTYLE = -20;
                const long WS_EX_NOACTIVATE = 0x08000000;
                long ex = (long)GetWindowLongPtr(h, GWL_EXSTYLE);
                ex = on ? ex | WS_EX_NOACTIVATE : ex & ~WS_EX_NOACTIVATE;
                SetWindowLongPtr(h, GWL_EXSTYLE, new IntPtr(ex));
            }
        }

        /// <summary>Eigene Widget-Seite öffnen.</summary>
        public void ShowWidgetPage(string id)
        {
            var a = activities.OfType<WidgetPageActivity>().FirstOrDefault(x => x.PageId == id) ?? new WidgetPageActivity(this, id);
            a.ExpiresAt = DateTime.Now.AddSeconds(60);
            if (activities.Contains(a)) Rebuild(a);
            else Add(a);
            Expand(a);
        }

        /// <summary>„Mehr“ in der Übersicht: alle Funktionen als Menü.</summary>
        public void ShowAllFunctions()
        {
            var cm = new ContextMenu();
            foreach (var t in DashTiles.All)
            {
                var tile = t;
                cm.Items.Add(MItem(tile.Label, () => tile.Run(this)));
            }
            cm.Items.Add(new Separator());
            cm.Items.Add(MItem("Seiten & Widgets bearbeiten …", () => OpenSetup("pages")));
            OpenMenu(cm);
        }

        string CurrentPageKey()
        {
            var a = expandedAct;
            if (a == null) return null;
            if (a == dashboard) return "dashboard";
            if (a is MediaActivity || a is SpotifyActivity || a is SpotifyListActivity) return "player";
            if (a is ShortcutsActivity) return "shortcuts";
            if (a is AppSwitcherActivity) return "apps";
            if (a is ClipboardActivity) return "clipboard";
            if (a is SoundActivity) return "sound";
            if (a is BrightnessActivity) return "brightness";
            if (a is DownloadsActivity) return "downloads";
            if (a is AirPodsActivity || a is BluetoothActivity || a is HeadphonesActivity) return "bluetooth";
            if (a is WidgetPageActivity w) return "w:" + w.PageId;
            if (a is AiActivity) return "ai";
            if (a is CalendarActivity) return "calendar";
            if (a is TodoActivity) return "todo";
            if (a is WeatherActivity) return "weather";
            return "live:" + a.Id;
        }

        void SwipePage(int dir)
        {
            var keys = PageKeys();
            int i = Math.Max(0, keys.IndexOf(CurrentPageKey() ?? "dashboard"));
            int j = i + dir;
            if (j < 0 || j >= keys.Count)
            {
                // am Ende: kurz federn
                pillShift.BeginAnimation(TranslateTransform.XProperty, null);
                pillShift.X = -dir * 14;
                SpringBack();
                return;
            }
            slideDir = dir;
            try { OpenPage(keys[j]); }
            finally { slideDir = 0; }
        }

        void OpenPage(string key)
        {
            switch (key)
            {
                case "dashboard": Expand(dashboard); break;
                case "player": ShowPlayer(); break;
                case "shortcuts": ShowShortcuts(); break;
                case "apps": ShowApps(); break;
                case "clipboard": ShowClipboardHistory(); break;
                case "sound": ShowSound(); break;
                case "brightness": ShowBrightness(); break;
                case "downloads": ShowDownloads(); break;
                case "ai": ShowAi(); break;
                case "calendar": ShowCalendar(); break;
                case "todo": ShowTodo(); break;
                case "weather": ShowWeather(); break;
                case "airpods":
                case "bluetooth": ShowBluetooth(); break;
                default:
                    if (key.StartsWith("w:")) { ShowWidgetPage(key.Substring(2)); break; }
                    var a = activities.FirstOrDefault(x => "live:" + x.Id == key);
                    if (a != null) Expand(a);
                    break;
            }
        }

        /// <summary>
        /// Oben in der großen Island: kleine Symbole für jede Seite (die aktuelle hell hinterlegt).
        /// Antippen springt direkt zur Seite.
        /// </summary>
        void AddPageDots(Grid wrap, int columns)
        {
            var keys = PageKeys();
            if (keys.Count < 2) return;
            int idx = keys.IndexOf(CurrentPageKey() ?? "");
            var nav = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 0, 0)
            };
            for (int i = 0; i < keys.Count; i++)
            {
                int at = i;
                string key = keys[i];
                bool cur = i == idx;
                UIElement icon;
                string g = PageGlyph(key);
                var liveAct = key.StartsWith("live:") ? activities.FirstOrDefault(x => "live:" + x.Id == key) : null;
                // entweder alle Symbole farbig oder alle weiß (Einstellungen → Allgemein)
                bool colored = Settings.NavIconsColor;
                double op = cur ? 1 : colored ? 0.6 : 0.45;
                Color? mono = colored ? (Color?)null : Colors.White;
                Color tint = colored ? PageColor(key, liveAct) : Colors.White;
                var brush = Ui.Br(tint, (byte)(op * 255));
                if (liveAct is ClaudeActivity) icon = Logos.Claude(12, op, mono);
                else if (liveAct is DiscordVoiceActivity) icon = Logos.Discord(13, op, mono);
                else if (g != null) icon = Ui.Glyph(g, 10, brush);
                else icon = new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, Fill = brush };
                var cell = new Border
                {
                    Child = icon,
                    Height = 17,
                    MinWidth = 20,
                    Padding = new Thickness(5, 0, 5, 0),
                    Margin = new Thickness(1, 0, 1, 0),
                    CornerRadius = new CornerRadius(8.5),
                    Background = cur ? Ui.Br(Colors.White, 38) : Brushes.Transparent,
                    Cursor = cur ? null : Cursors.Hand,
                    ToolTip = PageTitle(key)
                };
                if (icon is FrameworkElement fe) { fe.HorizontalAlignment = HorizontalAlignment.Center; fe.VerticalAlignment = VerticalAlignment.Center; }
                if (!cur)
                {
                    cell.MouseEnter += (s, e) => cell.Background = Ui.Br(Colors.White, 22);
                    cell.MouseLeave += (s, e) => cell.Background = Brushes.Transparent;
                }
                Ui.Pressable(cell, 0.88);
                cell.MouseLeftButtonDown += (s, e) => e.Handled = true;
                cell.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    if (at == idx) return;
                    slideDir = at > idx ? 1 : -1;
                    try { OpenPage(key); } catch (Exception ex) { Program.Log(ex); }
                    finally { slideDir = 0; }
                };
                nav.Children.Add(cell);
            }
            Grid.SetRowSpan(nav, 2);
            if (columns > 1) Grid.SetColumnSpan(nav, columns);
            wrap.Children.Add(nav);
        }

        /// <summary>Farbe eines Seitensymbols (wie die Kacheln der Übersicht).</summary>
        static Color PageColor(string key, Activity live)
        {
            switch (key)
            {
                case "dashboard": return Ui.Blue;
                case "player": return Ui.SpotifyAccent;
                case "bluetooth": return Ui.Blue;
                case "sound": return Ui.Pink;
                case "brightness": return Ui.Yellow;
                case "downloads": return Ui.Blue;
                case "clipboard": return Ui.Teal;
                case "shortcuts": return Ui.Purple;
                case "apps": return Ui.Blue;
                case "ai": return Ui.Purple;
                case "calendar": return Ui.Red;
                case "todo": return Ui.Blue;
                case "weather": return Ui.Teal;
            }
            if (key.StartsWith("w:")) return Ui.Teal;
            if (live is ClaudeActivity) return Logos.ClaudeOrange;
            if (live is DiscordVoiceActivity) return Logos.DiscordBlurple;
            if (live is GenericActivity ga) return ga.Accent;
            if (live is TimerActivity) return Ui.Orange;
            return Ui.WhiteC;
        }

        // =================================================================
        //  „Fertig“-Animation (Claude fertig, Timer abgelaufen, Download fertig)
        // =================================================================
        Grid pillStackRef;

        /// <summary>Ereignisse mit Animation (einzeln an-/abschaltbar unter Einstellungen → Allgemein → Animationen).</summary>
        public static readonly (string Key, string Name, Color Color)[] AnimEvents =
        {
            ("claude",   "Claude ist fertig",       Ui.Green),
            ("timer",    "Timer abgelaufen",        Ui.Orange),
            ("download", "Download / Spiel fertig", Ui.Green),
            ("copy",     "Kopieren",                Ui.Teal),
            ("message",  "Neue Nachricht",          Ui.Blue),
            ("device",   "Gerät verbunden",         Ui.Purple),
        };

        /// <summary>Animationsart „App im Vordergrund“ für dieses Ereignis an?</summary>
        bool SpotlightOn(string evt) => AnimMode(evt) == "app" && !fsHidden && !Ui.ReduceMotion;

        /// <summary>Gewählte Animation für ein Ereignis (Einstellungen → Animationen).</summary>
        public string AnimMode(string evt) =>
            evt != null && Settings.AnimModes != null && Settings.AnimModes.TryGetValue(evt, out var m) ? m ?? "off" : "off";

        /// <summary>Island wird länger: großes App-Symbol, daneben der Inhalt (springt hinein, gleitet nach).</summary>
        public void Spotlight(Func<FrameworkElement> icon, Color c, string title, string sub, double seconds = 3.5, Func<bool> tap = null)
        {
            expandedAct = null;
            var s = new SpotlightActivity(icon, c, title, sub, seconds, tap) { Host = this };
            Add(s);
            foreach (var cat in catViews.Concat(stripCats)) cat.Hop();
        }

        /// <summary>Claude ist fertig: App im Vordergrund oder die gewählte Animation.</summary>
        public void ClaudeFinished(ClaudeActivity a)
        {
            if (SpotlightOn("claude"))
            {
                string sub = (string.IsNullOrEmpty(a.Project) ? "" : a.Project + " · ") + "nach " + ClaudeActivity.Dur(a.DoneAfter);
                Spotlight(() => Logos.Claude(22), Logos.ClaudeOrange, "Claude ist fertig", sub, 4.5,
                          () => { pinnedId = a.Id; Relayout(); return true; });
            }
            else Celebrate(Ui.Green, "claude");
        }

        /// <summary>Vorschau eines Ereignisses aus den Einstellungen (Animations-Tester).</summary>
        public void TestEvent(string key, Color c)
        {
            var mode = AnimMode(key);
            if (mode == "off") mode = "app";                       // „Aus“ testen: trotzdem etwas zeigen
            if (mode != "app") { Celebrate(c, key, true, mode); return; }
            switch (key)
            {
                case "claude": Spotlight(() => Logos.Claude(22), Logos.ClaudeOrange, "Claude ist fertig", "Dyamicisland+ · nach 3:12"); break;
                case "timer": Spotlight(() => Ui.Glyph("", 18, Ui.Br(Ui.Orange)), Ui.Orange, "Timer abgelaufen", DateTime.Now.ToString("HH:mm")); break;
                case "download": Spotlight(() => Ui.Glyph("", 18, Ui.Br(Ui.Green)), Ui.Green, "Download fertig", "Beispiel.zip · 24,3 MB"); break;
                case "copy": Spotlight(() => Ui.Glyph("", 18, Ui.Br(Ui.Teal)), Ui.Teal, "Kopiert", "Beispieltext aus der Zwischenablage", 2.5); break;
                case "message": Spotlight(() => Logos.Discord(22), Logos.DiscordBlurple, "Luca", "Kommst du heute noch online?"); break;
                case "device": Spotlight(() => Ui.Glyph("", 18, Ui.Br(Ui.Purple)), Ui.Purple, "soundcore Q20i", "Verbunden · Akku 80 %"); break;
                default: Celebrate(c, key, true); break;
            }
        }

        /// <summary>Kurz feiern: farbiges Leuchten um die Island und/oder ein kleiner Hüpfer, die Katzen hüpfen mit.
        /// evt = Ereignis (nur wenn eingeschaltet); test = Vorschau aus den Einstellungen (läuft immer).</summary>
        public void Celebrate(Color c, string evt = null, bool test = false, string forceMode = null)
        {
            var mode = forceMode ?? (evt == null ? "both" : AnimMode(evt));
            if (mode == "app") mode = "bounce";   // ohne eigene Großansicht (z. B. Nachricht-Banner): kleiner Hüpfer
            if (mode == "off" || (Ui.ReduceMotion && !test) || pill == null) return;
            lastActive = DateTime.Now;
            Relayout();
            // erst auftauchen lassen (falls die Island oben versteckt war), dann feiern
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(tucked ? TuckMs + 40 : 30) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                if (mode == "glow" || mode == "both") Glow(c);
                if (mode == "bounce" || mode == "both") Bounce();
                foreach (var cat in catViews.Concat(stripCats)) cat.Hop();
            };
            t.Start();
        }

        void Glow(Color c)
        {
            if (pillStackRef == null) return;
            var sc = new ScaleTransform(1, 1);
            var tg = new TransformGroup();
            tg.Children.Add(sc);
            tg.Children.Add(pillScale);
            tg.Children.Add(pillShift);
            var ring = new Border
            {
                BorderBrush = Ui.Br(c),
                BorderThickness = new Thickness(2.5),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = tg,
                Opacity = 0,
                Effect = new DropShadowEffect { Color = c, BlurRadius = 26, ShadowDepth = 0, Opacity = 1 }
            };
            // folgt der Island, auch wenn sie gerade wächst oder schrumpft
            ring.SetBinding(WidthProperty, new System.Windows.Data.Binding("Width") { Source = pill });
            ring.SetBinding(HeightProperty, new System.Windows.Data.Binding("Height") { Source = pill });
            ring.SetBinding(Border.CornerRadiusProperty, new System.Windows.Data.Binding("CornerRadius") { Source = pill });
            pillStackRef.Children.Add(ring);
            // zweimal aufleuchten und dabei leicht nach außen wachsen
            var fade = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1500) };
            fade.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
            fade.KeyFrames.Add(new EasingDoubleKeyFrame(0.25, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600))));
            fade.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(820))));
            fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1500)), new CubicEase { EasingMode = EasingMode.EaseOut }));
            fade.Completed += (s, e) => pillStackRef.Children.Remove(ring);
            ring.BeginAnimation(OpacityProperty, fade);
            var grow = new DoubleAnimation(1, 1.06, TimeSpan.FromMilliseconds(1500)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.12, TimeSpan.FromMilliseconds(1500)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        void Bounce()
        {
            var k = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(700) };
            double baseS = hovering && expandedAct == null ? 1.04 : 1.0;
            k.KeyFrames.Add(new EasingDoubleKeyFrame(baseS * 1.12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140)), new CubicEase { EasingMode = EasingMode.EaseOut }));
            k.KeyFrames.Add(new EasingDoubleKeyFrame(baseS * 0.95, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)), new SineEase()));
            k.KeyFrames.Add(new EasingDoubleKeyFrame(baseS * 1.04, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480)), new SineEase()));
            k.KeyFrames.Add(new EasingDoubleKeyFrame(baseS, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700)), new SineEase()));
            pillScale.BeginAnimation(ScaleTransform.ScaleXProperty, k);
            pillScale.BeginAnimation(ScaleTransform.ScaleYProperty, k);
        }

        static string PageGlyph(string key)
        {
            switch (key)
            {
                case "dashboard": return "\uE80F";   // Start
                case "player": return "\uE8D6";      // Musik
                case "bluetooth": return "\uE702";
                case "sound": return "\uE767";
                case "brightness": return "\uE706";
                case "downloads": return "\uE896";
                case "clipboard": return "\uE77F";
                case "shortcuts": return "\uECA5";
                case "apps": return "\uE71D";
                case "ai": return "\uE8BD";
                case "calendar": return CalendarActivity.Glyph;
                case "todo": return TodoActivity.Glyph;
                case "weather": return WeatherActivity.Glyph;
            }
            if (key.StartsWith("w:")) return "\uF0E2"; // Widget-Seite
            return null;                              // laufende Aktivität: kleiner Punkt
        }

        string PageTitle(string key)
        {
            switch (key)
            {
                case "dashboard": return "Übersicht";
                case "player": return "Player";
                case "bluetooth": return "Bluetooth";
                case "sound": return "Ton";
                case "brightness": return "Helligkeit";
                case "downloads": return "Downloads";
                case "clipboard": return "Zwischenablage";
                case "shortcuts": return "Verknüpfungen";
                case "apps": return "Apps";
                case "ai": return "Gemini";
                case "calendar": return "Heute";
                case "todo": return "To Do";
                case "weather": return "Wetter";
            }
            if (key.StartsWith("w:")) return Settings.WidgetPages.FirstOrDefault(p => "w:" + p.Id == key)?.Name ?? "Widgets";
            var live = key.StartsWith("live:") ? activities.FirstOrDefault(x => "live:" + x.Id == key) : null;
            if (live is ClaudeActivity) return "Claude";
            if (live is DiscordVoiceActivity) return "Discord";
            return "Aktivität";
        }

        static T FindUp<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return null;
        }

        // Mausrad → Seite wechseln. Touchpads liefern viele kleine Schritte: aufsummieren, dann kurz sperren.
        double wheelAcc;
        DateTime wheelLock;

        void WheelPage(int delta)
        {
            var now = DateTime.Now;
            if (now < wheelLock) return;
            wheelAcc += delta;
            if (Math.Abs(wheelAcc) < 100) return;
            int dir = wheelAcc < 0 ? 1 : -1;   // runter = nächste Seite, hoch = vorige
            wheelAcc = 0;
            wheelLock = now.AddMilliseconds(380);
            lastInteraction = now;
            SwipePage(dir);
        }

        static bool IsInside<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T) return true;
                d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return false;
        }

        void UpdateCatsSpacer()
        {
            double w = catViews.Count > 0 ? catsPanel.ActualWidth + catsPanel.Margin.Left : 0;
            if (Math.Abs(catsSpacer.Width - w) < 0.5) return;
            catsSpacer.Width = w;
            Relayout();
        }

        void PetCat(CatView v)
        {
            lastActive = DateTime.Now;
            lastInteraction = DateTime.Now;
            if (v == topCat) topCatAwake = DateTime.Now;
            v.Purr();
            if (expandedAct != null) return;
            var cfg = v.Config;
            var line = PetLines[Rnd.Next(PetLines.Length)];
            var a = Alert("pet", 2.8, g =>
            {
                g.Banner = true;
                g.Glyph = "\u2665";
                g.GlyphFont = Ui.SymbolFont;
                g.Accent = Ui.Pink;
                g.Title = cfg.Name;
                g.Subtitle = line;
                g.RightFactory = () => CatStyle.Avatar(cfg, 40);
            });
            Rebuild(a);
        }

        /// <summary>Neue Aktivität oder Hinweis: Katzen wachen auf, eine hüpft kurz.</summary>
        void CatsNotice(Activity a)
        {
            lastInteraction = DateTime.Now;
            if (catViews.Count == 0 || a.Id == "pet") return;
            foreach (var c in catViews) c.Wake();
            if (a.IsAlert && a.Id != "volume") catViews[Rnd.Next(catViews.Count)].Hop();
        }

        // =================================================================
        //  Discord, Downloads, Mitteilungen
        // =================================================================
        void HookServices()
        {
            Spotify.Problem += msg => Alert("spotify-err", 3.5, g =>
            {
                g.Glyph = "\uE7BA";
                g.Accent = Ui.Orange;
                g.LeftText = msg;
                g.RightText = null;
                g.CompactW = Math.Min(560, 80 + msg.Length * 7);
            });
            if (Spotify.Connected) _ = Spotify.RefreshAsync();

            Discord.StatusChanged += () =>
            {
                var st = Discord.Status ?? "";
                bool bad = st.StartsWith("Discord hat abgelehnt") || st.StartsWith("Anmeldung") || st.StartsWith("Discord:")
                           || st.StartsWith("Keine") || st.StartsWith("Client-ID");
                if (!bad) return;
                Alert("discord-status", 10, g =>
                {
                    g.Banner = true;
                    g.Glyph = "\uE8BD";
                    g.Accent = Ui.Orange;
                    g.Title = "Discord verbindet nicht";
                    g.Subtitle = st;
                    g.CompactW = 470;
                    g.TapAction = () => { OpenSetup("discord"); return true; };
                });
            };
            Discord.ChannelChanged += OnDiscordChannel;
            // Nur neu aufbauen, wenn jemand kommt oder geht – Stummschalten & Co. ändern nur Abzeichen
            Discord.MembersChanged += () =>
            {
                if (voiceAct == null) return;
                if (voiceAct.NeedsRebuild) Rebuild(voiceAct);
                else voiceAct.Update();
            };
            Discord.SelfVoiceChanged += () => voiceAct?.Update();
            Discord.Notification += (title, body, icon, channel) => ShowMessage("Discord", title, body, icon, channel);
            if (Settings.DiscordEnabled) Discord.Start();
            HookAirPods();

            Downloads.Started += OnDownloadStarted;
            HookGames();
            Downloads.Completed += OnDownloadCompleted;
            Downloads.Cancelled += OnDownloadCancelled;
            if (Settings.ShowDownloads) Downloads.Start();

            Notifications.Received += OnWindowsNotification;
            if (Settings.MirrorNotifications) _ = Notifications.InitAsync();
        }

        void OnDiscordChannel()
        {
            if (Discord.Channel != null)
            {
                if (voiceAct == null || !activities.Contains(voiceAct))
                {
                    voiceAct = new DiscordVoiceActivity(Discord) { StopStream = StopDiscordStream };
                    Add(voiceAct);
                }
                else Rebuild(voiceAct);
            }
            else if (voiceAct != null)
            {
                voiceAct.Finished = true;
                voiceAct = null;
                Relayout();
            }
        }

        static readonly string[] CallWords = { "eingehender anruf", "ruft dich an", "ruft an", "incoming call", "is calling", "calling you", "videoanruf", "sprachanruf" };
        static readonly string[] MissedWords = { "verpasst", "missed" };

        /// <summary>Neue Nachricht oder eingehender Anruf (von Discord oder aus Windows-Mitteilungen).</summary>
        void ShowMessage(string app, string title, string body, string iconUrl, string channelId, string aumid = null)
        {
            var text = ((title ?? "") + " " + (body ?? "")).ToLowerInvariant();
            bool call = CallWords.Any(w => text.Contains(w)) && !MissedWords.Any(w => text.Contains(w));
            bool isDiscord = (app ?? "").IndexOf("discord", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isWhatsApp = (app ?? "").IndexOf("whatsapp", StringComparison.OrdinalIgnoreCase) >= 0;
            Color accent = isDiscord ? DiscordVoiceActivity.Blurple : isWhatsApp ? Ui.Green : Ui.Accent;
            Celebrate(call ? Ui.Green : accent, "message");

            var a = Alert(call ? "call-in" : "msg", call ? 25 : 7, g =>
            {
                g.Banner = true;
                g.Glyph = call ? "\uE717" : "\uE8BD";
                g.GlyphFont = Ui.IconFont;
                g.Accent = call ? Ui.Green : accent;
                g.Title = string.IsNullOrWhiteSpace(title) ? app : title;
                g.Subtitle = string.IsNullOrWhiteSpace(body) ? (call ? "Eingehender Anruf" : app) : body;
                g.CompactW = 430;
                g.Buttons.Clear();
                g.RightFactory = null;
                if (!string.IsNullOrEmpty(iconUrl)) g.RightFactory = () => WebAvatar.Circle(iconUrl, 38);
                if (call && isDiscord && channelId != null && Discord.Connected)
                {
                    g.Buttons.Add(new ActionButton("\uE717", Ui.Br(Ui.Green), () => { Discord.Join(channelId); g.Finished = true; Relayout(); }));
                    g.Buttons.Add(new ActionButton("\uE778", Ui.Br(Ui.Red), () => { g.Finished = true; Relayout(); }));
                }
                else if (!call && !string.IsNullOrWhiteSpace(title))
                {
                    // Antworten direkt aus der Island
                    string sender = title, msg = body, icon = iconUrl, ch = channelId, id = aumid;
                    g.Buttons.Add(new ActionButton("\uE97A", Ui.Br(accent), () =>
                    {
                        g.Finished = true;
                        StartReply(app, sender, msg, icon, ch, id, accent);
                    }));
                }
                // Antippen: zur App, von der die Mitteilung kommt
                g.TapAction = () => { OpenSourceApp(app, aumid, isDiscord, isWhatsApp); g.Finished = true; Relayout(); return true; };
            });
            Rebuild(a);
        }

        /// <summary>Discord-Stream (Go Live) beenden – über den Knopf im Discord-Fenster.</summary>
        async void StopDiscordStream()
        {
            bool ok = await DiscordUi.StopStreamAsync(() => Dispatcher.Invoke(() => Open("discord://")));
            Alert("dc-stream", 3, g =>
            {
                g.Glyph = "";
                g.Accent = ok ? DiscordVoiceActivity.Blurple : Ui.Gray;
                g.LeftText = ok ? "Stream beendet" : "Kein Stream gefunden";
                g.RightText = ok ? "Discord" : "";
                g.CompactW = 260;
            });
        }

        // ---- Antworten aus der Island ----
        void StartReply(string app, string sender, string msg, string icon, string channelId, string aumid, Color accent)
        {
            var r = new ReplyActivity(this, app, sender, msg, icon, channelId, aumid, accent) { Host = this, ExpiresAt = DateTime.Now.AddMinutes(5) };
            Add(r);
            Expand(r);
        }

        static bool IsDiscordApp(string app) => (app ?? "").IndexOf("discord", StringComparison.OrdinalIgnoreCase) >= 0;
        static bool IsWhatsAppApp(string app) => (app ?? "").IndexOf("whatsapp", StringComparison.OrdinalIgnoreCase) >= 0;
        static bool IsDiscordDm(ReplyActivity r) => IsDiscordApp(r.App) && !string.IsNullOrEmpty(r.ChannelId) && !(r.Sender ?? "").Contains("(#");

        /// <summary>Wie wird die Antwort zugestellt? (Hinweis unter dem Eingabefeld)</summary>
        public static string ReplyHint(ReplyActivity r, bool autoSend)
        {
            bool direct = IsWhatsAppApp(r.App) || IsDiscordDm(r);
            if (direct) return autoSend ? "Enter: öffnet den Chat in " + r.App + " und sendet sofort." : "Enter: öffnet den Chat und fügt die Antwort ein – du sendest selbst.";
            if (IsDiscordApp(r.App)) return "Enter: springt in den Kanal und fügt die Antwort ein – du sendest selbst.";
            return "Enter: öffnet " + (string.IsNullOrEmpty(r.App) ? "die App" : r.App) + " und fügt die Antwort ein.";
        }

        /// <summary>
        /// Antwort zustellen: App nach vorn, richtigen Chat öffnen (WhatsApp: Suche, Discord: Direktlink bzw. Schnellsuche),
        /// Text einfügen und – bei WhatsApp / Discord-DMs, wenn erlaubt – absenden.
        /// </summary>
        public async void SendReply(ReplyActivity r, string text)
        {
            r.Finished = true;
            EndTyping(false);
            Collapse();
            string oldClip = null;
            try { if (Clipboard.ContainsText()) oldClip = Clipboard.GetText(); } catch { }
            bool sent = false, pasted = false;
            try
            {
                bool wa = IsWhatsAppApp(r.App), dc = IsDiscordApp(r.App), dm = IsDiscordDm(r);
                bool launched;
                if (dm) { Open("discord://-/channels/@me/" + r.ChannelId); launched = true; }
                else launched = AppActivator.Activate(r.App, r.Aumid);
                if (!launched)
                {
                    if (wa) Open("whatsapp://");
                    else if (dc) Open("discord://");
                    else { Alert("reply", 4, g => { g.Banner = true; g.Glyph = ""; g.Accent = Ui.Orange; g.Title = "App nicht gefunden"; g.Subtitle = r.App; }); return; }
                }
                await Task.Delay(dm ? 1400 : 900);

                if (wa)
                {
                    // Chat über die Suche öffnen
                    ClipSet(r.Sender);
                    WindowList.Key(0x46, true);          // Strg+F
                    await Task.Delay(350);
                    WindowList.Key(0x41, true);          // alles markieren (alte Suche ersetzen)
                    WindowList.SendPaste();
                    await Task.Delay(1100);
                    WindowList.Key(0x0D);                // Enter → erster Treffer
                    await Task.Delay(700);
                }
                else if (dc && !dm)
                {
                    // Server-Kanal: Schnellsuche (Strg+K) mit dem Kanalnamen
                    var s = r.Sender ?? "";
                    int a = s.IndexOf("(#");
                    string chan = a >= 0 ? s.Substring(a + 1).Split(',', ')')[0].Trim() : s;
                    ClipSet(chan);
                    WindowList.Key(0x4B, true);          // Strg+K
                    await Task.Delay(400);
                    WindowList.SendPaste();
                    await Task.Delay(700);
                    WindowList.Key(0x0D);
                    await Task.Delay(600);
                }

                ClipSet(text);
                WindowList.SendPaste();
                pasted = true;
                if ((wa || dm) && Settings.ReplyAutoSend)
                {
                    await Task.Delay(250);
                    WindowList.Key(0x0D);                // absenden
                    sent = true;
                }
                await Task.Delay(400);
            }
            catch (Exception ex) { Program.Log(ex); }
            finally
            {
                if (oldClip != null) ClipSet(oldClip);
            }
            Alert("reply", 3.5, g =>
            {
                g.Banner = true;
                g.Glyph = sent ? "" : "";
                g.Accent = sent ? Ui.Green : Ui.Accent;
                g.Title = sent ? "Gesendet an " + r.Sender : pasted ? "Antwort eingefügt" : "Antwort nicht zugestellt";
                g.Subtitle = sent ? text : pasted ? "Prüfen und mit Enter senden" : r.App;
            });
        }

        static void ClipSet(string s)
        {
            for (int i = 0; i < 3; i++)
            {
                try { Clipboard.SetText(s ?? ""); return; }
                catch { System.Threading.Thread.Sleep(40); }
            }
        }

        /// <summary>Die App öffnen, von der eine Mitteilung kam (offenes Fenster nach vorne, sonst starten).</summary>
        void OpenSourceApp(string app, string aumid, bool isDiscord, bool isWhatsApp)
        {
            if (AppActivator.Activate(app, aumid)) return;
            if (isDiscord) Open("discord://");
            else if (isWhatsApp) Open("whatsapp://");
        }

        void OnWindowsNotification(string app, string title, string body, string aumid)
        {
            if (!Settings.MirrorNotifications || fsHidden) return;
            app ??= "";
            if (app.IndexOf("Dynamic Island", StringComparison.OrdinalIgnoreCase) >= 0) return;
            if (app.IndexOf("discord", StringComparison.OrdinalIgnoreCase) >= 0 && Discord.Connected) return; // kommt direkt von Discord
            ShowMessage(app, title, body, null, null, aumid);
        }

        static string Short(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        // ---- Spiele-Downloads (Steam, Epic) ----
        bool gamesHooked;

        void HookGames()
        {
            if (gamesHooked) return;
            gamesHooked = true;
            Games.Started += d =>
            {
                var g = new GenericActivity
                {
                    Id = "game-" + d.Key,
                    Host = this,
                    Priority = 45,
                    Glyph = "",
                    Accent = Ui.Accent,
                    CompactW = 300,
                    BarInCompact = false
                };
                g.Ticker = a => GameText(a, d);
                GameText(g, d);
                gameActs[d.Key] = g;
                Add(g);
            };
            Games.Progress += d => { if (gameActs.TryGetValue(d.Key, out var g)) { bool had = g.Progress.HasValue; GameText(g, d); if (had != g.Progress.HasValue) Rebuild(g); } };
            Games.Stopped += d => { if (gameActs.TryGetValue(d.Key, out var g)) { g.Finished = true; gameActs.Remove(d.Key); Relayout(); } };
            Games.Completed += d =>
            {
                if (gameActs.TryGetValue(d.Key, out var g)) { g.Finished = true; gameActs.Remove(d.Key); }
                if (SpotlightOn("download"))
                {
                    Spotlight(() => Ui.Glyph("", 18, Ui.Br(Ui.Green)), Ui.Green, d.Name, d.Store + " · bereit zum Spielen", 5);
                    return;
                }
                Celebrate(Ui.Green, "download");
                Alert("game-done", 7, a =>
                {
                    a.Banner = true;
                    a.Glyph = "";
                    a.Accent = Ui.Green;
                    a.Title = d.Name;
                    a.Subtitle = d.Store + " · fertig geladen – bereit zum Spielen";
                    a.CompactW = 410;
                    a.Buttons.Clear();
                    a.RightFactory = () => Ui.Glyph("", 20, Ui.Br(Ui.Green));
                });
            };
        }

        static void GameText(GenericActivity a, GameDownload d)
        {
            var p = d.Progress;
            a.LeftText = Short(d.Name, 20);
            a.RightText = p.HasValue ? Math.Floor(p.Value * 100) + " %" : "lädt";
            a.Title = d.Name;
            string sub = d.Store;
            if (d.Speed > 1024) sub += " · " + DownloadWatcher.FormatSize((long)d.Speed) + "/s";
            if (p.HasValue) sub += " · " + DownloadWatcher.FormatSize(d.Done) + " von " + DownloadWatcher.FormatSize(d.Total);
            if (p.HasValue && d.Speed > 1024 && d.Total > d.Done)
            {
                var left = TimeSpan.FromSeconds((d.Total - d.Done) / d.Speed);
                sub += left.TotalMinutes < 1 ? " · gleich fertig" : left.TotalHours < 1 ? $" · noch {(int)Math.Ceiling(left.TotalMinutes)} Min" : $" · noch {(int)left.TotalHours} Std {left.Minutes} Min";
            }
            if (p.HasValue && (DateTime.Now - d.LastChange).TotalSeconds > 45) sub += " · pausiert?";
            a.Subtitle = sub;
            a.Progress = p;
        }

        void OnDownloadStarted(DownloadItem it)
        {
            var g = new GenericActivity
            {
                Id = "dl-" + it.Key,
                Priority = 40,
                Glyph = "\uE896",
                Accent = Ui.Accent,
                LeftText = Short(it.Name, 22),
                RightText = DownloadWatcher.FormatSize(it.Size),
                RightColor = Ui.WhiteC,
                Title = it.Name,
                Subtitle = "Wird heruntergeladen …",
                CompactW = 300
            };
            g.Buttons.Add(new ActionButton("\uE8B7", Ui.Br(Ui.WhiteC, 40), OpenDownloadsFolder));
            g.Ticker = a =>
            {
                a.LeftText = Short(it.Name, 22);
                a.Title = it.Name;
                a.RightText = DownloadWatcher.FormatSize(it.Size);
                a.Subtitle = it.Speed > 1024 ? "Lädt · " + DownloadWatcher.FormatSize((long)it.Speed) + "/s" : "Wird heruntergeladen …";
            };
            dlActs[it.Key] = g;
            Add(g);
        }

        void OnDownloadCompleted(DownloadItem it)
        {
            if (dlActs.TryGetValue(it.Key, out var act)) { act.Finished = true; dlActs.Remove(it.Key); }
            var path = it.FinalPath;
            if (SpotlightOn("download"))
            {
                Spotlight(() => Ui.Glyph("", 18, Ui.Br(Ui.Green)), Ui.Green, it.Name,
                          "Download fertig · " + DownloadWatcher.FormatSize(it.Size) + " · Tippen zum Öffnen", 5,
                          () => { OpenFile(path); return true; });
                return;
            }
            Celebrate(Ui.Green, "download");
            var a = Alert("dl-done", 7, g =>
            {
                g.Banner = true;
                g.Glyph = "\uE73E";
                g.Accent = Ui.Green;
                g.Title = it.Name;
                g.Subtitle = "Download fertig · " + DownloadWatcher.FormatSize(it.Size) + " · Tippen zum Öffnen";
                g.CompactW = 430;
                g.Buttons.Clear();
                g.Buttons.Add(new ActionButton("\uE8B7", Ui.Br(Ui.WhiteC, 40), () => ShowInFolder(path)));
                g.TapAction = () => { OpenFile(path); return true; };
            });
            Rebuild(a);
        }

        void OnDownloadCancelled(DownloadItem it)
        {
            if (!dlActs.TryGetValue(it.Key, out var act)) return;
            act.Finished = true;
            dlActs.Remove(it.Key);
            Relayout();
        }

        public void OpenFile(string path) => Open(path);

        public void ShowInFolder(string path)
        {
            try { Process.Start("explorer.exe", "/select,\"" + path + "\""); }
            catch (Exception ex) { Program.Log(ex); }
        }

        public void OpenDownloadsFolder() => Open(Downloads.Folder ?? Native.DownloadsFolder());

        public void ShowDownloads()
        {
            var a = activities.OfType<DownloadsActivity>().FirstOrDefault() ?? new DownloadsActivity(this);
            a.Reload();
            a.ExpiresAt = DateTime.Now.AddSeconds(20);
            if (activities.Contains(a)) Rebuild(a);
            else Add(a);
            Expand(a);
        }

        public void ShowShortcuts()
        {
            var a = activities.OfType<ShortcutsActivity>().FirstOrDefault() ?? new ShortcutsActivity(this);
            a.ExpiresAt = DateTime.Now.AddSeconds(20);
            if (activities.Contains(a)) Rebuild(a);
            else Add(a);
            Expand(a);
        }

        public void RunShortcut(ShortcutConfig sc)
        {
            Collapse();
            try { Shortcuts.Launch(sc.Target); }
            catch (Exception ex)
            {
                Program.Log(ex);
                Alert("sc-err", 3, g =>
                {
                    g.Glyph = "\uE783";
                    g.Accent = Ui.Red;
                    g.LeftText = "Nicht gefunden";
                    g.RightText = sc.Name;
                    g.RightColor = Ui.Gray;
                    g.CompactW = 300;
                });
            }
        }

        DateTime playerWanted = DateTime.MinValue;
        /// <summary>Du hast den Player gerade ausdrücklich geöffnet → auch pausiert/ohne Musik zeigen (2 Min.).</summary>
        bool PlayerWanted => (DateTime.Now - playerWanted).TotalMinutes < 2;

        public void ShowPlayer()
        {
            // Früher: lief nichts, öffnete „Zurück zum Player“ wieder die Playlist-Liste (Schleife).
            // Jetzt: Player immer zeigen, auch pausiert oder wenn gerade nichts läuft.
            playerWanted = DateTime.Now;
            if (Spotify.Connected) SyncSpotify();
            SyncMedia();
            var p = PlayerAct();
            if (p != null) { Expand(p); return; }
            if (Spotify.Connected) { ShowSpotifyPlaylists(); return; } // Spotify-Player ließ sich nicht anlegen → Playlists
            Open("spotify:"); // nicht verbunden und nichts läuft → Spotify starten
        }

        void ShowSpotifyList(bool devicesMode)
        {
            var a = activities.OfType<SpotifyListActivity>().FirstOrDefault(x => x.Devices == devicesMode)
                    ?? new SpotifyListActivity(this, Spotify, devicesMode);
            a.ExpiresAt = DateTime.Now.AddSeconds(30);
            if (activities.Contains(a)) Rebuild(a);
            else Add(a);
            Expand(a);
            a.Reload();
        }

        public void ShowSpotifyDevices() => ShowSpotifyList(true);
        public void ShowSpotifyPlaylists() => ShowSpotifyList(false);

        public void ShowApps()
        {
            var a = activities.OfType<AppSwitcherActivity>().FirstOrDefault() ?? new AppSwitcherActivity(this);
            a.Reload();
            a.ExpiresAt = DateTime.Now.AddSeconds(20);
            if (activities.Contains(a)) Rebuild(a);
            else Add(a);
            Expand(a);
        }

        public void SwitchTo(IntPtr window)
        {
            Collapse();
            WindowList.Activate(window);
        }

        public void ShowBrightness()
        {
            var a = activities.OfType<BrightnessActivity>().FirstOrDefault() ?? new BrightnessActivity(this);
            a.ExpiresAt = DateTime.Now.AddSeconds(30);
            if (activities.Contains(a)) Rebuild(a);
            else Add(a);
            Expand(a);
            a.Reload();
        }

        // =================================================================
        //  AirPods
        // =================================================================
        void HookAirPods()
        {
            try
            {
                AirPodsEngine.ImportOldSettings(Settings);
                Settings.Save();
                AirPods = new AirPodsEngine(Settings, Dispatcher);

                // Case neben dem PC geöffnet → Akku-Karte wie am iPhone.
                // Aber nur, wenn eigene AirPods mit diesem PC gekoppelt sind – sonst klappte die Island
                // bei jedem fremden AirPods-Case in der Nähe auf („Bluetooth geht ohne Grund auf“).
                AirPods.CaseOpened += async st =>
                {
                    if (AirPodsConnected || await HasPairedAirPodsAsync()) ShowAirPods(true);
                };
                AirPods.StatusChanged += st =>
                {
                    var a = activities.OfType<AirPodsActivity>().FirstOrDefault();
                    if (a != null && a.Changed()) Rebuild(a);
                    RefreshAirPodsTile();
                };
                AirPods.EndpointChanged += name =>
                {
                    var a = activities.OfType<AirPodsActivity>().FirstOrDefault();
                    if (a != null) Rebuild(a);
                    RefreshAirPodsTile();
                };
                AirPods.Notify += (title, text) => Alert("airpods-low", 6, g =>
                {
                    g.Banner = true;
                    g.Glyph = "\uE7F6";
                    g.Accent = Ui.Red;
                    g.Title = title;
                    g.Subtitle = text;
                    g.CompactW = 380;
                    g.TapAction = () => { ShowAirPods(); return true; };
                });
                AirPods.EarPause += paused => Alert("airpods-ear", 2.5, g =>
                {
                    g.Glyph = "\uE7F6";
                    g.Accent = Ui.WhiteC;
                    g.LeftText = paused ? "AirPod herausgenommen" : "AirPods eingesetzt";
                    g.RightText = paused ? "Pausiert" : "Weiter";
                    g.RightColor = Ui.Gray;
                    g.CompactW = 310;
                });

                if (Settings.AirPodsEnabled)
                {
                    // Die alte separate App würde doppelt pausieren → beenden, die Island übernimmt.
                    var retired = AirPodsEngine.RetireOldApp();
                    if (!string.IsNullOrEmpty(retired)) Program.Log(new Exception("AirPods: " + retired));
                    AirPods.Start();
                }
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        bool? pairedAirPods;
        DateTime pairedAirPodsChecked;

        /// <summary>Sind AirPods mit diesem PC gekoppelt? (alle 10 Minuten neu geprüft)</summary>
        async Task<bool> HasPairedAirPodsAsync()
        {
            if (pairedAirPods.HasValue && (DateTime.Now - pairedAirPodsChecked).TotalMinutes < 10) return pairedAirPods.Value;
            bool found = false;
            try
            {
                var sel = Windows.Devices.Bluetooth.BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                var list = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(sel);
                found = list.Any(d => IsAirPodsName(d.Name));
            }
            catch (Exception ex) { Program.Log(ex); }
            pairedAirPods = found;
            pairedAirPodsChecked = DateTime.Now;
            return found;
        }

        string lastAirKey = "";
        bool btHooked;

        /// <summary>Kachel „AirPods“ in der Übersicht: erscheint beim Verbinden, zeigt den Akku.</summary>
        void RefreshAirPodsTile()
        {
            if (!btHooked) { btHooked = true; bt.Changed += RefreshAirPodsTile; }
            var k = dashboard.AirPodsKey;
            if (k == lastAirKey) return;
            lastAirKey = k;
            Rebuild(dashboard);
        }

        /// <summary>Sind deine AirPods gerade mit dem PC verbunden?</summary>
        public bool AirPodsConnected => AirPods != null && AirPods.Running && (AirPods.EndpointName != null || AirPods.Aap.Connected);

        /// <summary>Verbundene normale Kopfhörer (AirPods ausgenommen, die haben ihre eigene Seite).</summary>
        public BtDev StandardHeadphones => bt.Headphones(IsAirPodsName);

        bool IsAirPodsName(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            var f = Settings.AirPodsNameFilter;
            return n.IndexOf("AirPods", StringComparison.OrdinalIgnoreCase) >= 0
                   || (!string.IsNullOrWhiteSpace(f) && n.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>Bluetooth-Seite: AirPods verbunden → AirPods-Menü, andere Kopfhörer → Kopfhörer-Seite, sonst Bluetooth.</summary>
        public void ShowBluetooth(bool plain = false)
        {
            if (!plain && AirPodsConnected) { ShowAirPods(); return; }
            if (!plain && StandardHeadphones != null) { ShowHeadphones(); return; }
            var a = activities.OfType<BluetoothActivity>().FirstOrDefault() ?? new BluetoothActivity(this);
            a.ExpiresAt = DateTime.Now.AddSeconds(60);
            if (activities.Contains(a)) { a.Reload(); Rebuild(a); }
            else Add(a);
            Expand(a);
        }

        public void ShowHeadphones()
        {
            var a = activities.OfType<HeadphonesActivity>().FirstOrDefault() ?? new HeadphonesActivity(this);
            a.ExpiresAt = DateTime.Now.AddSeconds(30);
            if (activities.Contains(a)) Rebuild(a);
            else Add(a);
            Expand(a);
        }

        /// <summary>AirPods-Karte öffnen. auto = vom Öffnen des Case ausgelöst (schließt sich nach ein paar Sekunden).</summary>
        public void ShowAirPods(bool auto = false)
        {
            var a = activities.OfType<AirPodsActivity>().FirstOrDefault() ?? new AirPodsActivity(this);
            a.ExpiresAt = DateTime.Now.AddSeconds(auto ? 8 : 30);
            if (activities.Contains(a)) Rebuild(a);
            else Add(a);
            if (auto && (setupWin != null || menuOpen)) return;
            Expand(a);
        }

        public void ShowSound()
        {
            var a = activities.OfType<SoundActivity>().FirstOrDefault() ?? new SoundActivity(this, Audio);
            a.Reload();
            a.ExpiresAt = DateTime.Now.AddSeconds(30);
            if (activities.Contains(a)) Rebuild(a);
            else Add(a);
            Expand(a);
        }

        public void SwitchOutput(SoundActivity panel, AudioDevice dev)
        {
            if (dev.IsDefault) return;
            if (Audio.SetDefault(dev.Id))
            {
                panel.Reload();
                panel.ExpiresAt = DateTime.Now.AddSeconds(30);
                Rebuild(panel);
            }
            else
            {
                Alert("snd-err", 3, g =>
                {
                    g.Glyph = "\uE7F5";
                    g.Accent = Ui.Red;
                    g.LeftText = "Wechsel nicht möglich";
                    g.CompactW = 280;
                    g.TapAction = () => { Open("ms-settings:sound"); return true; };
                });
            }
        }

        public void LockPc()
        {
            if (Native.LockDisabledByPolicy)
            {
                Alert("lock", 5, g =>
                {
                    g.Banner = true;
                    g.Glyph = "\uE72E";
                    g.Accent = Ui.Red;
                    g.Title = "Sperren ist in Windows abgeschaltet";
                    g.Subtitle = "Richtlinie „DisableLockWorkstation“ ist gesetzt";
                    g.CompactW = 420;
                });
                return;
            }
            Collapse();
            // kurz warten, bis die Maus losgelassen ist
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                if (!Native.Lock())
                    Alert("lock", 3, g =>
                    {
                        g.Glyph = "\uE72E";
                        g.Accent = Ui.Red;
                        g.LeftText = "Sperren fehlgeschlagen";
                        g.CompactW = 270;
                    });
            };
            t.Start();
        }

        public void OpenSetup() => OpenSetup(null);

        /// <summary>Öffnet die Einstellungen, optional direkt auf einer Seite ("cats", "overview", …).</summary>
        public void OpenSetup(string page)
        {
            try
            {
                if (setupWin != null)
                {
                    if (page != null) setupWin.ShowPage(page);
                    setupWin.Activate();
                    return;
                }
                Collapse();
                setupWin = new SetupWindow(this, page);
                setupWin.Closed += (s, e) =>
                {
                    setupWin = null;
                    Settings.Save();
                    RefreshCats();
                };
                setupWin.Show();
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        // ---- Tastenkürzel ----
        void RegisterHotkeys()
        {
            const uint Alt = 0x1, Ctrl = 0x2, NoRepeat = 0x4000;
            try
            {
                Native.RegisterHotKey(hwnd, 1, Ctrl | Alt | NoRepeat, 0x20); // Leertaste → Übersicht
                Native.RegisterHotKey(hwnd, 2, Ctrl | Alt | NoRepeat, 0x4B); // K → Kurzbefehle
                Native.RegisterHotKey(hwnd, 3, Ctrl | Alt | NoRepeat, 0x44); // D → Downloads
                Native.RegisterHotKey(hwnd, 4, Ctrl | Alt | NoRepeat, 0x4D); // M → Discord-Mikro
                Native.RegisterHotKey(hwnd, 5, Ctrl | Alt | NoRepeat, 0x56); // V → Zwischenablage
                Native.RegisterHotKey(hwnd, 6, Ctrl | Alt | NoRepeat, 0x41); // A → Apps
                Native.RegisterHotKey(hwnd, 7, Ctrl | Alt | NoRepeat, 0x47); // G → Gemini-Chat
                Native.RegisterHotKey(hwnd, 8, Ctrl | Alt | NoRepeat, 0x54); // T → Text vom Bildschirm
                Native.RegisterHotKey(hwnd, 9, Ctrl | Alt | NoRepeat, 0x4E); // N → neue Aufgabe
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        void OnHotkey(int id)
        {
            lastActive = DateTime.Now;
            switch (id)
            {
                case 1:
                    if (expandedAct == dashboard) Collapse();
                    else Expand(dashboard);
                    break;
                case 2: ShowShortcuts(); break;
                case 3: ShowDownloads(); break;
                case 5: ShowClipboardHistory(); break;
                case 6: ShowApps(); break;
                case 7:
                    if (!Settings.AiEnabled) break;
                    if (expandedAct is AiActivity && typing) CloseAi();
                    else ShowAi(true);
                    break;
                case 8: if (Settings.AiEnabled) StartOcr(); break;
                case 9: ShowTodo(true); break;
                case 4:
                    if (!Discord.Connected)
                    {
                        Alert("dmute", 2, g => { g.Glyph = "\uE720"; g.Accent = Ui.Gray; g.LeftText = "Discord nicht verbunden"; g.CompactW = 270; });
                        break;
                    }
                    Discord.ToggleMute();
                    bool m = Discord.MicOff;
                    Alert("dmute", 1.8, g =>
                    {
                        g.Glyph = "\uE720";
                        g.Accent = m ? Ui.Red : Ui.Green;
                        g.LeftText = m ? "Discord stumm" : "Discord-Mikro an";
                        g.CompactW = 240;
                    });
                    break;
            }
        }

        // =================================================================
        //  Takt: alles regelmäßig aktualisieren
        // =================================================================
        void OnTick()
        {
            tickCount++;
            if (tickCount % 100 == 0) { CheckCalendar(); CheckTodo(); }      // alle 5 s: steht ein Termin / eine Erinnerung an?
            if (tickCount % 60 == 0 && Settings.ShowDownloads) _ = Games.PollAsync();  // Spiele-Downloads (Steam/Epic) alle 3 s
            if (tickCount == 40 || tickCount % 1200 == 0) _ = Weather.RefreshAsync(); // Wetter: beim Start, dann prüfen (lädt höchstens alle 15 Min)
            if (tickCount % 2400 == 0 && Todo.Connected) _ = Todo.RefreshAsync(); // To Do alle 2 Min
            if (tickCount % 12000 == 0 || calDay != DateTime.Today)          // alle 10 Min und um Mitternacht neu laden
            {
                calDay = DateTime.Today;
                if (tickCount > 1) _ = Calendar.RefreshAsync();
            }
            UpdateAutoHide();
            if (catViews.Count > 0)
            {
                var now = DateTime.Now;
                foreach (var c in catViews) c.Tick(now);
                if (tickCount % 20 == 0)
                {
                    // eine Minute nichts los → Katzen schlafen ein
                    // (verschlafene Katzen früher, verspielte später – je nach Charakter)
                    double quietFor = (now - lastInteraction).TotalSeconds;
                    bool calm = !hovering && !catsHover && expandedAct == null && !activities.Any(x => x.IsAlert);
                    if (calm) foreach (var c in catViews) if (quietFor > c.SleepAfterSeconds) c.Sleeping = true;
                }
            }
            if (stripCats.Count > 0)
            {
                var nowS = DateTime.Now;
                foreach (var c in stripCats) c.Tick(nowS);
                if (topCat != null && !topCat.Sleeping && (nowS - topCatAwake).TotalSeconds > TopCatNapAfter)
                    topCat.Sleeping = true;
            }
            if (tickCount % 20 == 0) Downloads.Poll();
            if (Spotify.Connected)
            {
                bool active = Spotify.Playing || expandedAct is SpotifyActivity || (DateTime.Now - Spotify.LastPlaying).TotalMinutes < 1;
                if (tickCount % (active ? 40 : 200) == 0) _ = Spotify.RefreshAsync();
                if (tickCount % 5 == 0) SyncSpotify();
            }
            if (tickCount % 40 == 0)
            {
                Media.PickSession();
                if (Settings.ShowBluetooth) PollControllers();
            }
            if (tickCount % 40 == 0 && Settings.MirrorNotifications) _ = Notifications.PollAsync();
            foreach (var a in activities.ToArray())
            {
                try { a.Update(); } catch (Exception ex) { Program.Log(ex); }
            }
            if (currentAct == clock) clock.Update();
            if (currentAct == dashboard) dashboard.Update();

            if (tickCount % 2 == 0) PollAudio();
            if (tickCount % 5 == 0) { Media.Poll(); SyncMedia(); PollCapsLock(); Relayout(); }
            if (tickCount % 20 == 0) { Stats.Sample(); PollFullscreen(); }
            if (tickCount % 30 == 0) PollPrivacy();
            if (tickCount % 40 == 0) { PollPower(); PollUsb(); }
            if (tickCount % 60 == 0) _ = Media.RefreshPropsAsync();
            if (tickCount % 80 == 0)
            {
                Native.KeepTopmost(hwnd);
                // immer abfragen (Kopfhörer-Seite), melden nur wenn eingeschaltet
                _ = bt.PollAsync(Settings.ShowBluetooth ? OnBtConnected : null, Settings.ShowBluetooth ? OnBtDisconnected : (Action<string>)null);
                RefreshAirPodsTile();
            }
        }

        TimeSpan spPrevPos, spPrevDur, mdPrevPos, mdPrevDur;
        DateTime mdNaturalAt = DateTime.MinValue;

        /// <summary>
        /// Wurde übersprungen (oder zurück) – oder lief der vorige Song einfach zu Ende?
        /// Nur beim Überspringen zeigt sich die Island kurz.
        /// </summary>
        static bool WasSkip(TimeSpan prevPos, TimeSpan prevDur) =>
            prevDur <= TimeSpan.Zero || prevPos < prevDur - TimeSpan.FromSeconds(4);

        void SyncSpotify()
        {
            // länger pausiert → Player ausblenden (wie bei anderen Apps)
            bool show = Spotify.Connected && (PlayerWanted && !OtherSourcePinned ||
                        Spotify.HasPlayback && (Spotify.Playing || (DateTime.Now - Spotify.LastPlaying).TotalMinutes < (Settings.KeepPlayer ? 3 : 1)));
            if (show)
            {
                if (spotifyAct == null || !activities.Contains(spotifyAct))
                {
                    spotifyAct = new SpotifyActivity(this, Spotify, Audio);
                    Add(spotifyAct);
                }
                var id = Spotify.Track?.Id ?? Spotify.Track?.Title;
                // Ist die Musik aus, liefert Spotify manchmal kurz keinen Titel – das zählte früher jedes Mal als
                // „übersprungen“ und hielt die Island dauerhaft sichtbar. Jetzt nur echte Titelwechsel bei laufender Musik.
                if (id != null && id != lastSpotifyTrack)
                {
                    if (lastSpotifyTrack != null && Spotify.Playing && WasSkip(spPrevPos, spPrevDur)) lastActive = DateTime.Now; // übersprungen → kurz zeigen
                    lastSpotifyTrack = id;
                }
                spPrevPos = Spotify.Position;
                spPrevDur = Spotify.Duration;
                // Windows meldet schon einen anderen Titel → Spotify sofort neu abfragen
                if (Media.AppName == "Spotify" && !string.IsNullOrEmpty(Media.Title) && Spotify.Track != null &&
                    Media.Title != Spotify.Track.Title && tickCount % 20 == 0)
                    _ = Spotify.RefreshAsync();
            }
            else if (spotifyAct != null && activities.Contains(spotifyAct))
            {
                RemoveInternal(spotifyAct);
                Relayout();
            }
        }

        bool SpotifyShown => spotifyAct != null && activities.Contains(spotifyAct);

        string lastTrackKey = "";

        void SyncMedia()
        {
            // Spotify läuft schon über den eingebauten Player → nicht doppelt anzeigen
            if (SpotifyShown && Media.AppName == "Spotify")
            {
                if (mediaAct != null && activities.Contains(mediaAct)) { RemoveInternal(mediaAct); Relayout(); }
                return;
            }
            // Song lief bis zum Ende und der nächste startet: Position springt von „fast Ende“ auf 0
            var pos = Media.Position;
            if (pos < mdPrevPos - TimeSpan.FromSeconds(3) && !WasSkip(mdPrevPos, mdPrevDur)) mdNaturalAt = DateTime.Now;

            var trackKey = Media.ShouldShow ? Media.Title + "|" + Media.Artist : "";
            if (trackKey != lastTrackKey)
            {
                // nur beim Überspringen kurz zeigen, nicht wenn ein Song normal zu Ende läuft
                bool natural = (DateTime.Now - mdNaturalAt).TotalSeconds < 6 || !WasSkip(mdPrevPos, mdPrevDur);
                if (trackKey.Length > 0 && lastTrackKey.Length > 0 && !natural && Media.Playing) lastActive = DateTime.Now;
                if (trackKey.Length > 0) lastTrackKey = trackKey;   // kurzes „nichts läuft“ nicht als Titelwechsel werten
            }
            mdPrevPos = pos;
            mdPrevDur = Media.Duration;
            if (Media.ShouldShow || (PlayerWanted && Media.HasSession && !string.IsNullOrWhiteSpace(Media.Title)))
            {
                mediaGoneSince = DateTime.MinValue;
                if (mediaAct == null || !activities.Contains(mediaAct))
                {
                    mediaAct = new MediaActivity(Media, Audio) { OpenSound = ShowSound, SwitchSource = ShowPlayerSources };
                    Add(mediaAct);
                }
            }
            else if (mediaAct != null && activities.Contains(mediaAct))
            {
                // erst nach 5 s entfernen – beim Songwechsel ist die Wiedergabe kurz „weg“
                if (mediaGoneSince == DateTime.MinValue) mediaGoneSince = DateTime.Now;
                if ((DateTime.Now - mediaGoneSince).TotalSeconds < 5) return;
                var m = mediaAct;
                RemoveInternal(m);
                Relayout();
            }
        }

        // ---- Lautstärke / Stumm ----
        float? lastVol;
        bool? lastMute, lastMicMute;
        bool audioInit;

        string lastOutName = "";

        void PollAudio()
        {
            Audio.Poll();
            if (!audioInit)
            {
                audioInit = true;
                lastVol = Audio.Volume; lastMute = Audio.Muted; lastMicMute = Audio.MicMuted;
                return;
            }
            bool soundOpen = expandedAct is MediaActivity || expandedAct is SoundActivity;
            // Wechselt Windows das Ausgabegerät (z. B. AirPods verbinden/trennen), springt die Lautstärke –
            // das ist keine Änderung durch dich, also keine Anzeige (sonst blieb die Island ständig sichtbar)
            bool deviceChanged = Audio.OutputName != lastOutName;
            lastOutName = Audio.OutputName;
            if (Settings.ShowVolume && !soundOpen && !deviceChanged && Audio.Volume.HasValue && lastVol.HasValue && Math.Abs(Audio.Volume.Value - lastVol.Value) > 0.004)
                ShowVolumeHud();

            if (!soundOpen && Audio.Muted.HasValue && lastMute.HasValue && Audio.Muted != lastMute)
            {
                bool muted = Audio.Muted.Value;
                Alert("mute", 2.2, g =>
                {
                    g.Glyph = muted ? "" : "";
                    g.Accent = muted ? Ui.Red : Ui.WhiteC;
                    g.LeftText = muted ? "Stumm" : "Ton an";
                    g.RightText = null;
                    g.CompactW = 190;
                });
            }
            if (Audio.MicMuted.HasValue && lastMicMute.HasValue && Audio.MicMuted != lastMicMute)
            {
                bool muted = Audio.MicMuted.Value;
                Alert("micmute", 2.2, g =>
                {
                    g.Glyph = "";
                    g.Accent = muted ? Ui.Red : Ui.Green;
                    g.LeftText = muted ? "Mikrofon stumm" : "Mikrofon an";
                    g.CompactW = 230;
                });
            }
            lastVol = Audio.Volume; lastMute = Audio.Muted; lastMicMute = Audio.MicMuted;
        }

        void ShowVolumeHud()
        {
            double v = Audio.Volume ?? 0;
            bool muted = Audio.Muted == true;
            Alert("volume", 1.6, g =>
            {
                g.Glyph = muted || v < 0.005 ? "" : "";
                g.Accent = Ui.WhiteC;
                g.BarInCompact = true;
                g.Progress = muted ? 0 : v;
                g.RightText = Math.Round(v * 100).ToString(CultureInfo.InvariantCulture);
                g.RightColor = Ui.WhiteC;
                g.CompactW = 280;
                g.TapAction = () => { ShowSound(); return true; };
            });
        }

        // ---- Feststelltaste ----
        bool? lastCaps;

        void PollCapsLock()
        {
            bool caps = Native.CapsLock;
            if (lastCaps.HasValue && caps != lastCaps.Value && Settings.ShowCapsLock)
            {
                Alert("caps", 1.6, g =>
                {
                    g.Glyph = "⇪";
                    g.GlyphFont = Ui.SymbolFont;
                    g.Accent = caps ? Ui.Green : Ui.Gray;
                    g.LeftText = caps ? "Feststelltaste an" : "Feststelltaste aus";
                    g.RightText = null;
                    g.CompactW = 230;
                });
            }
            lastCaps = caps;
        }

        // ---- Zwischenablage ----
        void OnClipboard()
        {
            if (DateTime.Now < ignoreClipUntil) return; // das waren wir selbst
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    string title, sub = null;
                    ClipItem item = null;
                    if (Clipboard.ContainsText())
                    {
                        var t = Clipboard.GetText().Trim().Replace("\r", " ").Replace("\n", " ");
                        if (t.Length == 0) return;
                        // sieht es nach einem Passwort aus? Dann nicht anzeigen.
                        bool secret = !t.Contains(' ') && !t.StartsWith("http") && t.Length >= 8 && t.Length <= 64 &&
                                      t.Any(char.IsDigit) && t.Any(char.IsLetter) && t.Any(c => !char.IsLetterOrDigit(c));
                        title = "Kopiert";
                        sub = secret ? new string('•', Math.Min(t.Length, 10)) : (t.Length > 40 ? t.Substring(0, 40) + "…" : t);
                        item = new ClipItem { Text = Clipboard.GetText(), Secret = secret };
                    }
                    else if (Clipboard.ContainsImage())
                    {
                        title = "Bild kopiert";
                        var img = Clipboard.GetImage();
                        if (img != null)
                        {
                            if (img.CanFreeze) img.Freeze();
                            item = new ClipItem { Image = img };
                        }
                    }
                    else if (Clipboard.ContainsFileDropList())
                    {
                        var files = Clipboard.GetFileDropList().Cast<string>().ToArray();
                        int n = files.Length;
                        title = n == 1 ? "1 Datei kopiert" : $"{n} Dateien kopiert";
                        item = new ClipItem { Files = files, Text = string.Join("\n", files) };
                    }
                    else return;

                    if (item != null && Settings.ClipHistory) AddClip(item);
                    if (!Settings.ShowClipboard) return;

                    if (SpotlightOn("copy"))
                    {
                        Spotlight(() => Ui.Glyph("", 18, Ui.Br(Ui.Teal)), Ui.Teal, title, sub ?? "", 2.2,
                                  () => { ShowClipboardHistory(); return true; });
                        return;
                    }
                    Celebrate(Ui.Teal, "copy");
                    Alert("clip", 1.8, g =>
                    {
                        g.Glyph = "";
                        g.Accent = Ui.Teal;
                        g.LeftText = title;
                        g.RightText = sub;
                        g.RightColor = Color.FromArgb(170, 255, 255, 255);
                        g.CompactW = sub == null ? 210 : 330;
                        g.TapAction = () => { ShowClipboardHistory(); return true; };
                    });
                }
                catch { }
            }), DispatcherPriority.Background);
        }

        void AddClip(ClipItem item)
        {
            if (item.Text != null) ClipHistory.RemoveAll(c => c.Image == null && c.Text == item.Text);
            ClipHistory.Insert(0, item);
            int images = 0;
            ClipHistory.RemoveAll(c => c.Image != null && ++images > 3); // Bilder brauchen viel Speicher
            while (ClipHistory.Count > 25) ClipHistory.RemoveAt(ClipHistory.Count - 1);
            var panel = activities.OfType<ClipboardActivity>().FirstOrDefault();
            if (panel != null) Rebuild(panel);
        }

        /// <summary>Eintrag aus dem Verlauf wieder in die Zwischenablage legen (und einfügen).</summary>
        public void UseClip(ClipItem item, bool paste)
        {
            try
            {
                ignoreClipUntil = DateTime.Now.AddMilliseconds(700);
                if (item.Image != null) Clipboard.SetImage(item.Image);
                else if (item.Files != null)
                {
                    var sc = new StringCollection();
                    sc.AddRange(item.Files);
                    Clipboard.SetFileDropList(sc);
                }
                else Clipboard.SetText(item.Text ?? "");
                ClipHistory.Remove(item);
                ClipHistory.Insert(0, item);
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                return;
            }
            Collapse();
            if (paste)
            {
                // unser Fenster nimmt nie den Fokus → Strg+V landet in der App, in der du gerade warst
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                t.Tick += (s, e) => { t.Stop(); WindowList.SendPaste(); };
                t.Start();
            }
            else Alert("clip", 1.4, g =>
            {
                g.Glyph = "\uE8C8";
                g.Accent = Ui.Teal;
                g.LeftText = "Kopiert";
                g.RightText = null;
                g.TapAction = null;
                g.CompactW = 200;
            });
        }

        public void ClearClipHistory()
        {
            ClipHistory.Clear();
            var panel = activities.OfType<ClipboardActivity>().FirstOrDefault();
            if (panel != null) Rebuild(panel);
        }

        public void ShowClipboardHistory()
        {
            var a = activities.OfType<ClipboardActivity>().FirstOrDefault() ?? new ClipboardActivity(this);
            a.ExpiresAt = DateTime.Now.AddSeconds(30);
            if (activities.Contains(a)) Rebuild(a);
            else Add(a);
            Expand(a);
        }

        // ---- Akku ----
        bool powerInit, lastAc;
        int lastPct = -1;

        void PollPower()
        {
            if (!Native.GetSystemPowerStatus(out var ps)) return;
            HasBattery = ps.BatteryFlag != 128 && ps.BatteryFlag != 255 && ps.BatteryLifePercent != 255;
            BatteryPct = ps.BatteryLifePercent == 255 ? -1 : ps.BatteryLifePercent;
            OnAc = ps.ACLineStatus == 1;
            if (!powerInit) { powerInit = true; lastAc = OnAc; lastPct = BatteryPct; return; }

            if (HasBattery && OnAc != lastAc) ShowChargingAlert(OnAc, BatteryPct);
            if (HasBattery && !OnAc && BatteryPct >= 0 &&
                ((lastPct > 20 && BatteryPct <= 20) || (lastPct > 10 && BatteryPct <= 10)))
            {
                int pct = BatteryPct;
                Alert("lowbat", 6, g =>
                {
                    g.Banner = true;
                    g.Glyph = "";
                    g.Accent = Ui.Red;
                    g.Title = "Akku schwach";
                    g.Subtitle = $"Noch {pct} % – Ladegerät anschließen";
                    g.RightFactory = () => { var b = new BatteryIcon(); b.Set(pct, false); return b; };
                });
            }
            lastAc = OnAc;
            lastPct = BatteryPct;
        }

        public void ShowChargingAlert(bool charging, int pct)
        {
            pct = Math.Max(0, pct);
            Alert("power", 3.5, g =>
            {
                g.Glyph = "";
                g.Accent = charging ? Ui.Green : Ui.Gray;
                g.LeftText = charging ? "Lädt" : "Akkubetrieb";
                g.CompactW = 280;
                g.RightFactory = () =>
                {
                    var b = new BatteryIcon();
                    b.Set(pct, charging);
                    return Ui.HStack(7, Ui.Text($"{pct} %", 13.5, charging ? Ui.Br(Ui.Green) : Ui.White, true, true), b);
                };
            });
        }

        // ---- Kamera / Mikrofon ----
        List<string> lastCam = new List<string>(), lastMic = new List<string>();
        bool privacyInit;

        void PollPrivacy()
        {
            if (!Settings.ShowPrivacy)
            {
                camDot.Visibility = Visibility.Collapsed;
                micDot.Visibility = Visibility.Collapsed;
                return;
            }
            var cam = Privacy.AppsUsing("webcam");
            var mic = Privacy.AppsUsing("microphone");
            camDot.Visibility = cam.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            micDot.Visibility = mic.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            var newCam = cam.Except(lastCam).ToList();
            var newMic = mic.Except(lastMic).ToList();
            if (privacyInit && newCam.Count > 0)
                Alert("cam", 3, g =>
                {
                    g.Banner = true; g.Glyph = ""; g.Accent = Ui.Green;
                    g.Title = "Kamera aktiv"; g.Subtitle = string.Join(", ", newCam);
                });
            else if (privacyInit && newMic.Count > 0)
                Alert("mic", 3, g =>
                {
                    g.Banner = true; g.Glyph = ""; g.Accent = Ui.Orange;
                    g.Title = "Mikrofon aktiv"; g.Subtitle = string.Join(", ", newMic);
                });
            lastCam = cam;
            lastMic = mic;
            privacyInit = true;
        }

        // ---- USB-Sticks ----
        HashSet<string> lastDrives;

        void PollUsb()
        {
            HashSet<string> now;
            try
            {
                now = new HashSet<string>(DriveInfo.GetDrives()
                    .Where(d => (d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed) && d.IsReady)
                    .Select(d => d.Name));
            }
            catch { return; }

            if (lastDrives != null)
            {
                foreach (var d in now.Except(lastDrives))
                {
                    string label = "USB-Stick", info = d;
                    double? used = null;
                    bool hdd = false;
                    try
                    {
                        var di = new DriveInfo(d);
                        hdd = di.DriveType == DriveType.Fixed;
                        label = !string.IsNullOrEmpty(di.VolumeLabel) ? di.VolumeLabel : hdd ? "Externe Festplatte" : "USB-Stick";
                        if (di.TotalSize > 0)
                        {
                            used = 1 - (double)di.AvailableFreeSpace / di.TotalSize;
                            info = $"{d} · {DownloadWatcher.FormatSize(di.AvailableFreeSpace)} frei von {DownloadWatcher.FormatSize(di.TotalSize)}";
                        }
                    }
                    catch { }
                    var path = d;
                    ShowDeviceAlert("usb-in", hdd ? "\uEDA2" : "\uE88E", label, info, used, Ui.Accent, () => { Open(path); return true; });
                }
                foreach (var d in lastDrives.Except(now))
                {
                    var path = d;
                    Alert("usb-out", 2.5, g =>
                    {
                        g.Glyph = ""; g.Accent = Ui.Gray;
                        g.LeftText = "Entfernt"; g.RightText = path; g.RightColor = Ui.Gray;
                        g.CompactW = 230;
                    });
                }
            }
            lastDrives = now;
        }

        // ---- Bluetooth ----
        static string DeviceGlyph(string name, out bool controller)
        {
            var n = (name ?? "").ToLowerInvariant();
            controller = new[] { "controller", "gamepad", "dualsense", "dualshock", "joy-con", "8bitdo", "xbox" }.Any(k => n.Contains(k));
            if (controller) return "\uE7FC";
            if (new[] { "pod", "bud", "head", "ear", "wh-", "wf-", "jbl", "bose", "beats", "sony", "kopfh", "headset", "soundcore", "anker", "jabra", "sennheiser", "q20", "q30", "q35", "q45" }.Any(k => n.Contains(k))) return "\uE7F6";
            if (n.Contains("mouse") || n.Contains("maus")) return "\uE962";
            if (n.Contains("keyboard") || n.Contains("tastatur")) return "\uE765";
            if (n.Contains("phone") || n.Contains("galaxy") || n.Contains("pixel")) return "\uE8EA";
            return "\uE702";
        }

        static Color BatteryColor(int pct) => pct <= 20 ? Ui.Red : pct <= 40 ? Ui.Orange : Ui.Green;

        /// <summary>
        /// Gerät verbunden: Banner mit Symbol und einem Ring, der sich füllt und bis zum
        /// exakten Akkustand (bzw. belegten Speicher) hochzählt.
        /// </summary>
        void ShowDeviceAlert(string id, string glyph, string title, string subtitle, double? fraction, Color ring, Func<bool> tap = null)
        {
            Celebrate(ring, "device");
            var a = Alert(id, 5, g =>
            {
                g.Banner = true;
                g.Glyph = glyph;
                g.GlyphFont = Ui.IconFont;
                g.Accent = Ui.WhiteC;
                g.Title = title;
                g.Subtitle = subtitle;
                g.CompactW = 410;
                g.Buttons.Clear();
                g.TapAction = tap;
                Func<FrameworkElement> ringF = () => new RingGauge(fraction ?? 0, ring, 46, 4);
                Func<FrameworkElement> checkF = () => Ui.Glyph("\uE73E", 20, Ui.Br(Ui.Green));
                g.RightFactory = fraction.HasValue ? ringF : checkF;
            });
            Rebuild(a);
        }

        // Kopfhörer wie AirPods verbinden/trennen sich in der Ruhephase ständig neu – nicht jedes Mal melden
        readonly Dictionary<string, DateTime> btShown = new Dictionary<string, DateTime>();
        readonly Dictionary<string, DateTime> btGone = new Dictionary<string, DateTime>();

        // Kopfhörer (z. B. soundcore) verbinden sich im Standby alle paar Minuten neu. Darum:
        // pro Gerät höchstens alle 3 Stunden melden, und nur wenn es wirklich länger (≥ 20 Min) weg war.
        const double BtRepeatMinutes = 180, BtAwayMinutes = 20;

        bool BtAlertAllowed(string name)
        {
            var now = DateTime.Now;
            bool everGone = btGone.TryGetValue(name, out var gone);
            if (everGone && (now - gone).TotalMinutes < BtAwayMinutes) return false;                       // nur kurz weg gewesen
            if (btShown.TryGetValue(name, out var shown) && (now - shown).TotalMinutes < BtRepeatMinutes) return false; // schon gemeldet
            btShown[name] = now;
            return true;
        }

        void OnBtConnected(string name, int? battery)
        {
            // AirPods meldet die AirPods-Karte selbst – nicht doppelt
            if (IsAirPodsName(name) && AirPods != null && AirPods.Running) return;
            // Controller meldet XInput schon (PollControllers)
            var glyph = DeviceGlyph(name, out bool controller);
            if (controller && activities.Any(x => x.Id == "dev-controller")) return;
            if (!BtAlertAllowed(name ?? "")) return;
            ShowDeviceAlert(controller ? "dev-controller" : "bt-" + name, glyph, name,
                battery.HasValue ? $"Verbunden · Akku {battery.Value} %" : "Verbunden",
                battery.HasValue ? battery.Value / 100.0 : (double?)null,
                BatteryColor(battery ?? 100)); // Antippen schließt nur die Meldung (öffnete sonst im falschen Moment die Bluetooth-Seite)
        }

        /// <summary>Xbox-Controller (USB, Funk-Adapter oder Bluetooth) über XInput.</summary>
        void PollControllers()
        {
            for (int i = 0; i < 4; i++)
            {
                bool c = XInput.Connected(i);
                if (padsInit && c && !padConnected[i] && !activities.Any(x => x.Id == "dev-controller"))
                {
                    int? bat = XInput.Battery(i);
                    ShowDeviceAlert("dev-controller", "\uE7FC", "Xbox Controller",
                        bat.HasValue ? $"Verbunden · Akku ca. {bat.Value} %" : "Verbunden",
                        bat.HasValue ? bat.Value / 100.0 : (double?)null, BatteryColor(bat ?? 100));
                }
                else if (padsInit && !c && padConnected[i])
                {
                    Alert("dev-controller-off", 2.5, g =>
                    {
                        g.Glyph = "\uE7FC";
                        g.Accent = Ui.Gray;
                        g.LeftText = "Controller";
                        g.RightText = "Getrennt";
                        g.RightColor = Ui.Gray;
                        g.CompactW = 280;
                    });
                }
                padConnected[i] = c;
            }
            padsInit = true;
        }

        void OnBtDisconnected(string name)
        {
            if (IsAirPodsName(name) && AirPods != null && AirPods.Running) return;
            btGone[name ?? ""] = DateTime.Now;
            if (btShown.TryGetValue("off:" + name, out var t) && (DateTime.Now - t).TotalMinutes < BtRepeatMinutes) return;
            btShown["off:" + name] = DateTime.Now;
            ShowBtOff(name);
        }

        void ShowBtOff(string name) => Alert("bt-off-" + name, 2.5, g =>
        {
            g.Glyph = ""; g.Accent = Ui.Gray;
            g.LeftText = name; g.RightText = "Getrennt"; g.RightColor = Ui.Gray;
            g.CompactW = 300;
        });

        // ---- Netzwerk ----
        void OnNetworkChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            bool on = e.IsAvailable;
            Dispatcher.BeginInvoke(new Action(() => Alert("net", 2.5, g =>
            {
                g.Glyph = "";
                g.Accent = on ? Ui.Green : Ui.Red;
                g.LeftText = on ? "Online" : "Offline";
                g.RightText = on ? "Verbunden" : "Kein Netzwerk";
                g.RightColor = Color.FromArgb(170, 255, 255, 255);
                g.CompactW = 250;
            })));
        }

        // ---- Vollbild ----
        void PollFullscreen()
        {
            bool fs = Settings.HideInFullscreen && Native.IsForegroundFullscreen(hwnd);
            if (fs == fsHidden) return;
            fsHidden = fs;
            // gleiche Bewegung wie das automatische Wegfahren (nach oben gleiten statt ausblenden)
            if (fs && expandedAct != null) Collapse();
            if (fs) SetTucked(true);
            else { lastActive = DateTime.Now; SetTucked(false); }
            PlaceCats();
        }

        // =================================================================
        //  Funktionen, die Menü / Dashboard / Demos benutzen
        // =================================================================
        public static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception ex) { Program.Log(ex); }
        }

        public void StartTimer(TimeSpan d, string label = "Timer")
        {
            expandedAct = null;
            var t = new TimerActivity(d, label, Ui.Orange);
            t.Completed = () => TimerDone(label, "", Ui.Orange, null);
            Add(t);
            UpdateHoverScale();
        }

        void TimerDone(string label, string glyph, Color c, string emoji)
        {
            Native.Beep();
            if (SpotlightOn("timer"))
            {
                Spotlight(() => emoji != null ? Ui.Glyph(emoji, 18, Ui.White, Ui.EmojiFont) : Ui.Glyph(glyph, 18, Ui.Br(c)), c,
                          label + " abgelaufen", DateTime.Now.ToString("HH:mm") + " · Tippen zum Schließen", 8);
                return;
            }
            Celebrate(c, "timer");
            Alert("timerdone", 8, g =>
            {
                g.Banner = true;
                g.Glyph = emoji ?? glyph;
                g.GlyphFont = emoji != null ? Ui.EmojiFont : Ui.IconFont;
                g.Accent = c;
                g.Title = label + " abgelaufen";
                g.Subtitle = DateTime.Now.ToString("HH:mm") + " · Tippen zum Schließen";
            });
        }

        public void StartPomodoro(bool focus, int round)
        {
            expandedAct = null;
            foreach (var p in activities.OfType<TimerActivity>().Where(x => x.Pomodoro).ToList()) p.Finished = true;
            string label = focus ? $"Fokus {round}" : "Pause";
            var minutes = focus ? 25 : (round % 4 == 0 ? 15 : 5);
            var color = focus ? Ui.Red : Ui.Green;
            var t = new TimerActivity(TimeSpan.FromMinutes(minutes), label, color, "\U0001F345") { Pomodoro = true };
            t.Completed = () =>
            {
                TimerDone(label, null, color, "\U0001F345");
                StartPomodoro(!focus, focus ? round : round + 1);
            };
            Add(t);
            UpdateHoverScale();
        }

        public void ToggleStopwatch()
        {
            expandedAct = null;
            var sw = activities.OfType<StopwatchActivity>().FirstOrDefault();
            if (sw != null) { sw.Finished = true; Relayout(); }
            else Add(new StopwatchActivity());
            UpdateHoverScale();
        }

        public void ToggleMemo()
        {
            expandedAct = null;
            var m = activities.OfType<VoiceMemoActivity>().FirstOrDefault();
            if (m != null) { m.Stop(true); return; }
            var memo = new VoiceMemoActivity(Audio);
            if (!memo.Ok)
            {
                Alert("memo-err", 3, g =>
                {
                    g.Banner = true; g.Glyph = ""; g.Accent = Ui.Red;
                    g.Title = "Aufnahme nicht möglich"; g.Subtitle = "Kein Mikrofon gefunden";
                });
                return;
            }
            Add(memo);
            UpdateHoverScale();
        }

        public void MemoSaved(string path)
        {
            Alert("memo-saved", 4, g =>
            {
                g.Banner = true; g.Glyph = ""; g.Accent = Ui.Green;
                g.Title = "Sprachmemo gespeichert";
                g.Subtitle = Path.GetFileName(path) + " · Tippen zum Öffnen";
                g.TapAction = () => { Open(Path.GetDirectoryName(path)); return true; };
            });
        }

        public void ShowHelp()
        {
            var g = new GenericActivity
            {
                Id = "help",
                Priority = 1,
                Glyph = "",
                Accent = Ui.Accent,
                CompactW = 200,
                LeftText = "Hilfe",
                Title = "So bedienst du die Island",
                Subtitle = "Antippen = entfalten · nochmal = alle Funktionen",
                Detail = "Rechte Maustaste halten + ziehen = verschieben\n" +
                         "Gedrückt halten = alle Funktionen · Rechtsklick = Menü\n" +
                         "Katze anklicken = streicheln\n" +
                         "Strg+Alt+Leertaste = Übersicht · Strg+Alt+K = Kurzbefehle\n" +
                         "Strg+Alt+D = Downloads · Strg+Alt+M = Discord-Mikro\n" +
                         "Versteckt? Maus an den oberen Bildschirmrand\n" +
                         "Wischen ← blendet aus · Wischen → holt zurück\n" +
                         "Mausrad = Seite wechseln · Kreis antippen = tauschen\n" +
                         "Beenden: Rechtsklick → Beenden\n" +
                         $"Eigene Aktivitäten: http://localhost:{LocalApi.Port}/show?title=Hallo" +
                         (api.Running ? "" : " (API aus)"),
                ExpiresAt = DateTime.Now.AddSeconds(30)
            };
            Add(g);
            Expand(g);
        }

        // ---- Lokale API ----
        void OnApi(string path, NameValueCollection q, string body)
        {
            if (path.StartsWith("/claude")) { Claude.Handle(body, q); return; }
            string id = "api-" + (q["id"] ?? "1");
            if (path.StartsWith("/hide"))
            {
                var a = activities.FirstOrDefault(x => x.Id == id);
                if (a != null) { a.Finished = true; Relayout(); }
                return;
            }
            if (!path.StartsWith("/show")) return;

            var g = activities.FirstOrDefault(x => x.Id == id) as GenericActivity;
            bool isNew = g == null;
            if (isNew) g = new GenericActivity { Id = id, Priority = 55 };
            g.Title = q["title"];
            g.Subtitle = q["sub"];
            g.LeftText = q["left"] ?? q["title"];
            g.RightText = q["right"];
            g.Detail = q["detail"];
            if (q["icon"] != null)
            {
                try { g.Glyph = char.ConvertFromUtf32(Convert.ToInt32(q["icon"], 16)); g.GlyphFont = Ui.IconFont; } catch { }
            }
            if (q["emoji"] != null) { g.Glyph = q["emoji"]; g.GlyphFont = Ui.EmojiFont; }
            if (q["color"] != null)
            {
                try
                {
                    var c = q["color"].StartsWith("#") ? q["color"] : "#" + q["color"];
                    g.Accent = (Color)ColorConverter.ConvertFromString(c);
                }
                catch { }
            }
            if (q["progress"] != null && double.TryParse(q["progress"], NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
                g.Progress = Math.Max(0, Math.Min(1, p));
            g.Banner = q["banner"] == "1";
            g.CompactW = q["width"] != null && double.TryParse(q["width"], NumberStyles.Float, CultureInfo.InvariantCulture, out var wv) ? wv : 250;
            if (q["seconds"] != null && double.TryParse(q["seconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out var sec) && sec > 0)
            {
                g.IsAlert = true;
                g.Expandable = false;
                g.ExpiresAt = DateTime.Now.AddSeconds(sec);
            }
            if (isNew) Add(g);
            else Rebuild(g);
        }

        // =================================================================
        //  Menüs (Rechtsklick + Buttons in der Übersicht)
        // =================================================================
        MenuItem MItem(string header, Action act)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += (s, e) => { try { act(); } catch (Exception ex) { Program.Log(ex); } };
            return mi;
        }

        MenuItem MCheck(string header, bool value, Action<bool> set)
        {
            var mi = new MenuItem { Header = header, IsCheckable = true, IsChecked = value, StaysOpenOnClick = true };
            mi.Click += (s, e) => { set(mi.IsChecked); Settings.Save(); Relayout(); };
            return mi;
        }

        static MenuItem MHint(string text) => new MenuItem { Header = text, IsEnabled = false };

        void OpenMenu(ContextMenu cm)
        {
            Ui.StyleMenu(cm);
            cm.PlacementTarget = pill;
            cm.Placement = PlacementMode.Bottom;
            openCm = cm;
            cm.Opened += (s, e) => menuOpen = true;
            cm.Closed += (s, e) => { menuOpen = false; lastActive = DateTime.Now; };
            cm.IsOpen = true;
        }

        void AddTimerItems(ItemCollection items)
        {
            items.Add(MItem("Pomodoro (25 / 5)", () => StartPomodoro(true, 1)));
            items.Add(new Separator());
            foreach (var m in new[] { 1, 3, 5, 10, 15, 30, 60 })
            {
                int min = m;
                items.Add(MItem(min == 60 ? "1 Stunde" : (min == 1 ? "1 Minute" : $"{min} Minuten"), () => StartTimer(TimeSpan.FromMinutes(min))));
            }
        }

        void AddDemoItems(ItemCollection items)
        {
            items.Add(MItem("Navigation", () => Demos.Navigation(this)));
            items.Add(MItem("Flug", () => Demos.Flight(this)));
            items.Add(MItem("Fahrdienst (Uber)", () => Demos.Ride(this)));
            items.Add(MItem("Essenslieferung", () => Demos.Food(this)));
            items.Add(MItem("Sport-Ticker", () => Demos.Sports(this)));
            items.Add(MItem("Eingehender Anruf", () => Demos.Call(this)));
            items.Add(MItem("Bildschirmaufnahme", () => Demos.ScreenRecording(this)));
            items.Add(MItem("Hotspot", () => Demos.Hotspot(this)));
            items.Add(MItem("AirDrop", () => Demos.AirDrop(this)));
            items.Add(new Separator());
            items.Add(MItem("Face ID", () => Demos.FaceId(this)));
            items.Add(MItem("Apple Pay", () => Demos.Pay(this)));
            items.Add(MItem("AirPods verbunden", () => Demos.AirPods(this)));
            items.Add(MItem("Laden", () => Demos.Charging(this)));
            items.Add(MItem("Stummmodus", () => Demos.Silent(this)));
            items.Add(new Separator());
            items.Add(MItem("Alle Demos beenden", () =>
            {
                foreach (var a in activities.Where(x => x.Id.StartsWith("demo")).ToList()) a.Finished = true;
                Relayout();
            }));
        }

        void AddSettingsItems(ItemCollection items)
        {
            items.Add(MItem("Alle Einstellungen …", OpenSetup));
            items.Add(new Separator());
            items.Add(MCheck("Automatisch nach oben ausblenden", Settings.AutoHide, v => { Settings.AutoHide = v; lastActive = DateTime.Now; }));
            items.Add(MCheck("… auch bei Musik, Timer und Downloads", Settings.HideWithActivities, v => { Settings.HideWithActivities = v; lastActive = DateTime.Now; }));
            items.Add(MCheck("Im Leerlauf als Pille zeigen", Settings.IdlePill, v => Settings.IdlePill = v));
            items.Add(MCheck("Bei Vollbild ausblenden (Spiele, Videos)", Settings.HideInFullscreen, v => Settings.HideInFullscreen = v));
            items.Add(new Separator());
            items.Add(MCheck("Lautstärke anzeigen", Settings.ShowVolume, v => Settings.ShowVolume = v));
            items.Add(MCheck("Kopieren anzeigen", Settings.ShowClipboard, v => Settings.ShowClipboard = v));
            items.Add(MCheck("Feststelltaste anzeigen", Settings.ShowCapsLock, v => Settings.ShowCapsLock = v));
            items.Add(MCheck("Bluetooth-Geräte anzeigen", Settings.ShowBluetooth, v => Settings.ShowBluetooth = v));
            items.Add(MCheck("Kamera-/Mikrofon-Punkte", Settings.ShowPrivacy, v => { Settings.ShowPrivacy = v; PollPrivacy(); }));
            items.Add(new Separator());
            items.Add(MCheck("Mit Windows starten", Autostart.Enabled, v => Autostart.Set(v)));
        }

        void AddPositionItems(ItemCollection items)
        {
            double side = MonRect.Width / 2;
            void Place(double x) { MoveTo(x, 0, false); Settings.Save(); Relayout(); }
            items.Add(MItem("Oben links", () => Place(-side)));
            items.Add(MItem("Oben Mitte (Standard)", () => Place(0)));
            items.Add(MItem("Oben rechts", () => Place(side)));
            items.Add(new Separator());
            items.Add(MHint("Frei verschieben: rechte Maustaste halten + ziehen"));
        }

        MenuItem SubMenu(string header, Action<ItemCollection> fill)
        {
            var mi = new MenuItem { Header = header };
            fill(mi.Items);
            return mi;
        }

        // Diese Menüs öffnen auch die Buttons in der Übersicht (Island antippen).
        public void ShowTimerMenu() { var cm = new ContextMenu(); AddTimerItems(cm.Items); OpenMenu(cm); }
        public void ShowDemoMenu() { var cm = new ContextMenu(); AddDemoItems(cm.Items); OpenMenu(cm); }
        public void ShowSettingsMenu() => OpenSetup("general");

        /// <summary>Nach Änderungen im Einstellungsfenster alles neu anwenden.</summary>
        public void ApplySettings()
        {
            Settings.Save();
            PollPrivacy();
            ApplyScale();
            Media.KeepPlayer = Settings.KeepPlayer;
            if (AirPods != null)
            {
                AirPods.SetRssiThreshold(Settings.AirPodsRssi);
                if (Settings.AirPodsEnabled) AirPods.Start(); else AirPods.Stop();
                if (Settings.AirPodsEnabled && Settings.AirPodsAap) AirPods.Aap.Start(); else AirPods.Aap.Stop();
            }
            LiquidGlass.Configure(Settings.Material != "flat", Settings.GlassTint);
            Ui.Accent = Ui.ParseColor(Settings.AccentColor, Ui.Blue);
            Ui.SpotifyAccent = Ui.ParseColor(Settings.SpotifyColor, Color.FromRgb(0x1E, 0xD7, 0x60));
            RefreshCats();
            pageCache.Clear();   // Größe/Seiten können sich geändert haben
            lastActive = DateTime.Now;
            currentKey = "";
            Relayout();
        }

        /// <summary>0 = oben links, 1 = oben Mitte, 2 = oben rechts.</summary>
        public void PlaceTop(int where)
        {
            double side = MonRect.Width / 2;
            MoveTo(where == 0 ? -side : where == 2 ? side : 0, 0, false);
            Settings.Save();
            Relayout();
        }
        public void ShowPositionMenu() { var cm = new ContextMenu(); AddPositionItems(cm.Items); OpenMenu(cm); }

        /// <summary>Beendet die App.</summary>
        public void Quit() => Close();

        void ShowMenu()
        {
            var cm = new ContextMenu();

            cm.Items.Add(MItem("Alle Funktionen", () => Expand(dashboard)));
            cm.Items.Add(MItem("Kurzbefehle", ShowShortcuts));
            cm.Items.Add(MItem("Downloads", ShowDownloads));
            cm.Items.Add(MItem("Ton & Ausgabegerät", ShowSound));
            cm.Items.Add(MItem("Zwischenablage", ShowClipboardHistory));
            cm.Items.Add(MItem("Apps", ShowApps));
            cm.Items.Add(MItem("Helligkeit", ShowBrightness));
            if (Settings.AiEnabled)
            {
                cm.Items.Add(MItem("Gemini fragen", () => ShowAi(true)));
                cm.Items.Add(MItem("Text vom Bildschirm lesen", StartOcr));
            }
            cm.Items.Add(new Separator());

            cm.Items.Add(SubMenu("Timer", AddTimerItems));
            bool hasSw = activities.OfType<StopwatchActivity>().Any();
            cm.Items.Add(MItem(hasSw ? "Stoppuhr beenden" : "Stoppuhr", ToggleStopwatch));
            bool hasMemo = activities.OfType<VoiceMemoActivity>().Any();
            cm.Items.Add(MItem(hasMemo ? "Sprachmemo speichern" : "Sprachmemo", ToggleMemo));
            if (Discord.Connected)
                cm.Items.Add(MItem(Discord.MicOff ? "Discord-Mikro einschalten" : "Discord stummschalten", Discord.ToggleMute));
            cm.Items.Add(new Separator());

            cm.Items.Add(MItem("Einstellungen …", OpenSetup));
            cm.Items.Add(SubMenu("Position", AddPositionItems));
            cm.Items.Add(SubMenu("Demos", AddDemoItems));
            if (hidden.Count > 0)
                cm.Items.Add(MItem($"Ausgeblendete zurückholen ({hidden.Count})", () => { hidden.Clear(); hiddenStack.Clear(); Relayout(); }));
            cm.Items.Add(MItem("Hilfe", ShowHelp));
            cm.Items.Add(new Separator());
            cm.Items.Add(MItem("PC sperren", LockPc));
            cm.Items.Add(MItem("Beenden", Quit));

            OpenMenu(cm);
        }
    }
}

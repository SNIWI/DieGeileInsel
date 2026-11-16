using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace DynamicIsland
{
    // =====================================================================
    //  Musik & Podcasts
    // =====================================================================
    public sealed class MediaActivity : Activity
    {
        readonly MediaService m;
        readonly AudioService audio;
        Image cArt, eArt;
        Visualizer cViz, eViz, mViz;
        TextBlock cTitle;
        TextBlock eTitle, eArtist, eApp, ePos, eDur, ePlay, eVolT, eVolIcon, eShuffleG, eRepeatG;
        DragBar eSeek, eVol;
        Border eShuffle, eRepeat;
        BitmapSource shownArt;
        Color shownAccent;
        bool forceArt = true;
        PlayerUi player;

        /// <summary>Öffnet das Ton-Panel (setzt die Island).</summary>
        public Action OpenSound;
        /// <summary>Player-Wechsler: Spotify, Browser, VLC … (setzt die Island).</summary>
        public Action SwitchSource;

        public MediaActivity(MediaService media, AudioService a)
        {
            m = media;
            audio = a;
            Id = "media";
            Priority = 50;
        }

        public override double CompactWidth => 300;
        public override Size ExpandedSize => new Size(420, 240);

        public override FrameworkElement BuildCompact()
        {
            var art = Ui.ArtBox(24, 6, out cArt);
            cViz = new Visualizer(4, 16, 3, 2.5);
            cTitle = Ui.Text("", 13, Ui.White, true);
            cTitle.Margin = new Thickness(9, 0, 9, 0);
            forceArt = true;
            var g = Ui.Row3(art, cTitle, cViz, new Thickness(7, 0, 15, 0));
            Update();
            return g;
        }

        public override FrameworkElement BuildMinimal()
        {
            mViz = new Visualizer(3, 14, 2.6, 2);
            mViz.SetColor(m.Accent);
            return mViz;
        }

        static Border SmallToggle(string glyph, Action click, out TextBlock g)
        {
            var b = Ui.RoundButton(glyph, 15, click, 36, Brushes.Transparent, Ui.Dim);
            g = (TextBlock)b.Child;
            return b;
        }

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = new Thickness(22, 18, 22, 12) };

            // Cover, Titel, Interpret, App
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            player = new PlayerUi(m.OpenApp, "App öffnen");
            eArt = player.Art;
            eTitle = Ui.Text("", 17, Ui.White);
            eArtist = Ui.Text("", 13, Ui.Dim, false);
            eApp = Ui.Link("", m.OpenApp, 11.5);
            eApp.Margin = new Thickness(0, 3, 0, 0);
            var texts = Ui.VStack(1, eTitle, eArtist, eApp);
            texts.Margin = new Thickness(0, 0, 10, 0);
            eViz = new Visualizer(5, 22, 3.4, 3) { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) };
            FrameworkElement right = eViz;
            if (SwitchSource != null)
            {
                var sw = Ui.RoundButton("", 13, SwitchSource, 32, Brushes.Transparent, Ui.Dim);
                sw.ToolTip = "Player wechseln";
                sw.VerticalAlignment = VerticalAlignment.Top;
                var rs = Ui.HStack(4, sw, eViz);
                rs.VerticalAlignment = VerticalAlignment.Top;
                right = rs;
            }
            Grid.SetColumn(texts, 1); Grid.SetColumn(right, 2);
            top.Children.Add(texts); top.Children.Add(right);
            root.Children.Add(top);

            // Fortschritt: anklicken oder ziehen zum Spulen, Zeiten darunter (wie Apple Music)
            eSeek = new DragBar(Ui.AccentBrush, 5);
            // links die verbleibende Zeit, rechts die Länge des Titels
            eSeek.Changed = f => ePos.Text = "-" + Ui.Fmt(TimeSpan.FromTicks((long)(m.Duration.Ticks * (1 - f))));
            eSeek.Committed = f => m.Seek(f);
            ePos = Ui.Text("0:00", 11, Ui.Dim, false, true);
            eDur = Ui.Text("", 11, Ui.Dim, false, true);
            var seekGroup = new StackPanel();
            seekGroup.Children.Add(eSeek);
            seekGroup.Children.Add(Ui.Row3(ePos, null, eDur, new Thickness(0, -2, 0, 0)));

            // Steuerung: Zufall · Zurück · Play/Pause · Weiter · Wiederholen
            eShuffle = SmallToggle("", m.ToggleShuffle, out eShuffleG);
            eShuffle.ToolTip = "Zufallswiedergabe";
            var prev = Ui.RoundButton("", 18, m.Previous, 44, Brushes.Transparent);
            var play = Ui.RoundButton("", 28, m.PlayPause, 52, Brushes.Transparent);
            ePlay = (TextBlock)play.Child;
            var next = Ui.RoundButton("", 18, m.Next, 44, Brushes.Transparent);
            eRepeat = SmallToggle("", m.CycleRepeat, out eRepeatG);
            eRepeat.ToolTip = "Wiederholen";
            var ctrl = Ui.HStack(14, eShuffle, prev, play, next, eRepeat);
            ctrl.HorizontalAlignment = HorizontalAlignment.Center;

            // Lautstärke: nicht mehr im Player (Mausrad auf der Island bzw. Seite „Ton“) – Elemente bleiben fürs Update bestehen
            var vol = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            vol.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            vol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            vol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            vol.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var mute = Ui.RoundButton("", 14, audio.ToggleMute, 30, Brushes.Transparent, Ui.Dim);
            mute.ToolTip = "Stumm";
            eVolIcon = (TextBlock)mute.Child;
            eVol = new DragBar(Ui.AccentBrush, 4) { Margin = new Thickness(8, 0, 8, 0), WheelStep = 0.04 };
            eVol.Current = () => audio.Volume ?? 0;
            eVol.Changed = f => { audio.SetVolume((float)f); eVolT.Text = Math.Round(f * 100).ToString(); };
            eVolT = Ui.Text("", 11.5, Ui.Dim, false, true);
            eVolT.HorizontalAlignment = HorizontalAlignment.Right;
Grid.SetColumn(mute, 0); Grid.SetColumn(eVol, 1); Grid.SetColumn(eVolT, 2);
            vol.Children.Add(mute); vol.Children.Add(eVol); vol.Children.Add(eVolT);

            root.Children.Remove(top);
            var spread = player.Layout(top, texts, seekGroup, ctrl);


            forceArt = true;
            Update();
            return spread;
        }

        public override void Update()
        {
            if (forceArt || m.Art != shownArt || m.Accent != shownAccent)
            {
                forceArt = false;
                shownArt = m.Art;
                shownAccent = m.Accent;
                if (cArt != null) cArt.Source = m.Art;
                if (eArt != null) eArt.Source = m.Art;
                cViz?.SetColor(m.Accent);
                eViz?.SetColor(m.Accent);
                mViz?.SetColor(m.Accent);
            }
            double lvl = audio.Peak;
            cViz?.Step(lvl, m.Playing);
            eViz?.Step(lvl, m.Playing);
            mViz?.Step(lvl, m.Playing);
            if (cTitle != null) cTitle.Text = m.Title;

            if (eTitle == null) return;
            eTitle.Text = m.Title;
            eArtist.Text = m.Artist;
            eApp.Text = string.IsNullOrEmpty(m.AppName) ? "" : "In " + m.AppName + " öffnen";
            ePlay.Text = m.Playing ? "" : "";
            player?.Animate(m.Title + "|" + m.Artist, m.Playing, m.Accent, ePlay);
            if (!eSeek.Dragging)
            {
                ePos.Text = m.Duration > TimeSpan.Zero ? "-" + Ui.Fmt(m.Duration - m.Position) : "";
                eSeek.Bar.Value = m.Duration.TotalSeconds > 0 ? m.Position.TotalSeconds / m.Duration.TotalSeconds : 0;
            }
            eDur.Text = m.Duration > TimeSpan.Zero ? Ui.Fmt(m.Duration) : "";

            var acc = Ui.Br(m.Accent);
            eShuffle.Visibility = m.CanShuffle ? Visibility.Visible : Visibility.Hidden;
            eRepeat.Visibility = m.CanRepeat ? Visibility.Visible : Visibility.Hidden;
            eShuffleG.Foreground = m.Shuffle ? acc : Ui.Dim;
            eRepeatG.Foreground = m.Repeat != 0 ? acc : Ui.Dim;
            eRepeatG.Text = m.Repeat == 1 ? "" : "";

            double v = audio.Volume ?? 0;
            bool muted = audio.Muted == true;
            if (!eVol.Dragging)
            {
                eVol.Bar.Value = muted ? 0 : v;
                eVolT.Text = muted ? "aus" : Math.Round(v * 100).ToString();
            }
            eVolIcon.Text = muted || v < 0.005 ? "" : v < 0.34 ? "" : v < 0.67 ? "" : "";

        }

        // Antippen entfaltet den Player (die App öffnest du über das Cover oder „In … öffnen“)
        public override bool OnTap() => false;
    }

    // =====================================================================
    //  Timer (auch für Pomodoro)
    // =====================================================================
    public sealed class TimerActivity : Activity
    {
        public readonly string Label;
        public readonly Color Color;
        public readonly string Emoji;
        public Action Completed;
        public bool Pomodoro;

        TimeSpan total;
        DateTime end;
        TimeSpan pausedRemaining;
        bool paused;
        TextBlock cTime, eTime, eSub, ePlay, mText;
        Bar eBar;

        public TimerActivity(TimeSpan duration, string label, Color color, string emoji = null)
        {
            total = duration;
            end = DateTime.Now + duration;
            Label = label;
            Color = color;
            Emoji = emoji;
            Priority = 40;
            Id = "timer-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public TimeSpan Remaining
        {
            get
            {
                var r = paused ? pausedRemaining : end - DateTime.Now;
                return r < TimeSpan.Zero ? TimeSpan.Zero : r;
            }
        }

        TextBlock Icon(double size) => Emoji != null
            ? Ui.Glyph(Emoji, size, Ui.Br(Color), Ui.EmojiFont)
            : Ui.Glyph("", size, Ui.Br(Color));

        public override double CompactWidth => 220;
        public override Size ExpandedSize => new Size(380, 166);

        public override FrameworkElement BuildCompact()
        {
            cTime = Ui.Text("", 14, Ui.Br(Color), true, true);
            var g = Ui.Row3(Ui.HStack(7, Icon(15), Ui.Text(Label, 12.5, Ui.Dim, false)), null, cTime, new Thickness(13, 0, 15, 0));
            Refresh();
            return g;
        }

        public override FrameworkElement BuildMinimal()
        {
            mText = Ui.Text("", 11, Ui.Br(Color), true, true);
            Refresh();
            return mText;
        }

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = new Thickness(20, 18, 20, 14) };
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var circle = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(22), Background = Ui.Br(Color, 55), Child = Icon(20) };
            eSub = Ui.Text("", 12.5, Ui.Dim, false);
            var texts = Ui.VStack(2, Ui.Text(Label, 15.5, Ui.White), eSub);
            texts.Margin = new Thickness(12, 0, 8, 0);
            eTime = Ui.Text("", 32, Ui.Br(Color), true, true);
            Grid.SetColumn(circle, 0); Grid.SetColumn(texts, 1); Grid.SetColumn(eTime, 2);
            top.Children.Add(circle); top.Children.Add(texts); top.Children.Add(eTime);
            root.Children.Add(top);

            eBar = new Bar(5, Ui.Br(Color)) { Margin = new Thickness(0, 14, 0, 0) };
            root.Children.Add(eBar);

            var play = Ui.RoundButton("", 18, TogglePause, 44, Ui.Br(Color, 70));
            ePlay = (TextBlock)play.Child;
            var plus = Ui.RoundButton("+1", 13, AddMinute, 44, Ui.Faint, Ui.White, Ui.TextFont);
            var stop = Ui.RoundButton("", 15, Cancel, 44, Ui.Faint);
            var btns = Ui.HStack(16, plus, play, stop);
            btns.HorizontalAlignment = HorizontalAlignment.Center;
            btns.Margin = new Thickness(0, 12, 0, 0);
            root.Children.Add(btns);
            Refresh();
            return root;
        }

        public override void Update()
        {
            if (!paused && !Finished && DateTime.Now >= end)
            {
                Finished = true;
                Completed?.Invoke();
                return;
            }
            Refresh();
        }

        void Refresh()
        {
            var r = Remaining;
            var txt = Ui.Fmt(r + TimeSpan.FromMilliseconds(999));
            if (cTime != null) cTime.Text = txt;
            if (eTime != null) eTime.Text = txt;
            if (eSub != null) eSub.Text = (paused ? "Pausiert · " : "") + "von " + Ui.Fmt(total);
            if (ePlay != null) ePlay.Text = paused ? "" : "";
            if (eBar != null) eBar.Value = total.TotalSeconds > 0 ? 1 - r.TotalSeconds / total.TotalSeconds : 1;
            if (mText != null) mText.Text = r.TotalMinutes >= 1 ? $"{(int)Math.Ceiling(r.TotalMinutes)}m" : $"{(int)Math.Ceiling(r.TotalSeconds)}s";
        }

        void TogglePause()
        {
            if (paused) { end = DateTime.Now + pausedRemaining; paused = false; }
            else { pausedRemaining = Remaining; paused = true; }
            Refresh();
        }

        void AddMinute()
        {
            if (paused) pausedRemaining += TimeSpan.FromMinutes(1);
            else end = end.AddMinutes(1);
            total += TimeSpan.FromMinutes(1);
            Refresh();
        }

        void Cancel()
        {
            Finished = true;
            Host?.Relayout();
        }
    }

    // =====================================================================
    //  Stoppuhr
    // =====================================================================
    public sealed class StopwatchActivity : Activity
    {
        readonly Stopwatch sw = Stopwatch.StartNew();
        readonly List<TimeSpan> laps = new List<TimeSpan>();
        TextBlock cTime, eTime, eLaps, ePlay;

        public StopwatchActivity()
        {
            Id = "stopwatch";
            Priority = 39;
        }

        static string F(TimeSpan t) => t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds / 10:00}";

        public override double CompactWidth => 230;
        public override Size ExpandedSize => new Size(380, 178);

        public override FrameworkElement BuildCompact()
        {
            cTime = Ui.Text("", 14, Ui.Br(Ui.Orange), true, true);
            var left = Ui.HStack(7, Ui.Glyph("", 15, Ui.Br(Ui.Orange)), Ui.Text("Stoppuhr", 12.5, Ui.Dim, false));
            var g = Ui.Row3(left, null, cTime, new Thickness(13, 0, 15, 0));
            Update();
            return g;
        }

        public override FrameworkElement BuildMinimal() => Ui.Glyph("", 15, Ui.Br(Ui.Orange));

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = new Thickness(20, 16, 20, 14) };
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            eLaps = Ui.Text("", 11.5, Ui.Dim, false, true);
            eLaps.TextTrimming = TextTrimming.None;
            var left = Ui.VStack(3, Ui.Text("Stoppuhr", 15.5, Ui.White), eLaps);
            left.VerticalAlignment = VerticalAlignment.Top;
            eTime = Ui.Text("", 32, Ui.Br(Ui.Orange), true, true);
            Grid.SetColumn(left, 0); Grid.SetColumn(eTime, 1);
            top.Children.Add(left); top.Children.Add(eTime);
            root.Children.Add(top);

            var lap = Ui.RoundButton("", 16, () => { if (sw.IsRunning) laps.Add(sw.Elapsed); }, 46, Ui.Faint);
            var play = Ui.RoundButton("", 19, () => { if (sw.IsRunning) sw.Stop(); else sw.Start(); }, 46, Ui.Br(Ui.Orange, 70));
            ePlay = (TextBlock)play.Child;
            var reset = Ui.RoundButton("", 16, () => { sw.Reset(); laps.Clear(); }, 46, Ui.Faint);
            var close = Ui.RoundButton("", 15, () => { Finished = true; Host?.Relayout(); }, 46, Ui.Faint);
            var btns = Ui.HStack(14, lap, play, reset, close);
            btns.HorizontalAlignment = HorizontalAlignment.Center;
            btns.Margin = new Thickness(0, 16, 0, 0);
            root.Children.Add(btns);
            Update();
            return root;
        }

        public override void Update()
        {
            var t = sw.Elapsed;
            if (cTime != null) cTime.Text = F(t);
            if (eTime != null) eTime.Text = F(t);
            if (ePlay != null) ePlay.Text = sw.IsRunning ? "" : "";
            if (eLaps != null)
            {
                if (laps.Count == 0) eLaps.Text = sw.IsRunning ? "Läuft · Fahne = Runde" : "Pausiert";
                else
                {
                    var lines = new List<string>();
                    for (int i = laps.Count - 1; i >= Math.Max(0, laps.Count - 2); i--)
                    {
                        var d = i == 0 ? laps[0] : laps[i] - laps[i - 1];
                        lines.Add($"Runde {i + 1}   {F(d)}");
                    }
                    eLaps.Text = string.Join("\n", lines);
                }
            }
        }
    }

    // =====================================================================
    //  Sprachmemo (nimmt wirklich auf, speichert als WAV in Dokumente\Sprachmemos)
    // =====================================================================
    public sealed class VoiceMemoActivity : Activity
    {
        const string Alias = "dimemo";
        readonly AudioService audio;
        readonly DateTime started = DateTime.Now;
        public readonly bool Ok;
        bool closed;
        TextBlock cTime, eTime;
        Visualizer cViz, eViz;

        public VoiceMemoActivity(AudioService a)
        {
            audio = a;
            Id = "memo";
            Priority = 45;
            Native.Mci("close " + Alias);
            Ok = Native.Mci("open new type waveaudio alias " + Alias) == 0;
            if (Ok)
            {
                Native.Mci("set " + Alias + " time format ms bitspersample 16 channels 1 samplespersec 44100 bytespersec 88200 alignment 2");
                Ok = Native.Mci("record " + Alias) == 0;
                if (!Ok) Native.Mci("close " + Alias);
            }
            closed = !Ok;
        }

        public override double CompactWidth => 230;
        public override Size ExpandedSize => new Size(390, 172);

        public override FrameworkElement BuildCompact()
        {
            var dot = Ui.Dot(Ui.Red, 9);
            Ui.Pulse(dot);
            cViz = new Visualizer(6, 16, 2.6, 2) { FakeWhenSilent = false };
            cViz.SetColor(Ui.Red);
            cTime = Ui.Text("", 14, Ui.Br(Ui.Red), true, true);
            var g = Ui.Row3(Ui.HStack(8, dot, cViz), null, cTime, new Thickness(14, 0, 15, 0));
            Update();
            return g;
        }

        public override FrameworkElement BuildMinimal()
        {
            var dot = Ui.Dot(Ui.Red, 11);
            Ui.Pulse(dot);
            return dot;
        }

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = new Thickness(20, 16, 20, 14) };
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = Ui.VStack(2, Ui.Text("Sprachmemo", 15.5, Ui.White), Ui.Text("Wird aufgenommen …", 12.5, Ui.Dim, false));
            eTime = Ui.Text("", 30, Ui.Br(Ui.Red), true, true);
            Grid.SetColumn(left, 0); Grid.SetColumn(eTime, 1);
            top.Children.Add(left); top.Children.Add(eTime);
            root.Children.Add(top);

            eViz = new Visualizer(30, 28, 3, 3) { FakeWhenSilent = false, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
            eViz.SetColor(Ui.Red);
            root.Children.Add(eViz);

            var save = Ui.RoundButton("", 18, () => Stop(true), 46, Ui.Br(Ui.Red));
            var discard = Ui.RoundButton("", 16, () => Stop(false), 46, Ui.Faint);
            var btns = Ui.HStack(20, discard, save);
            btns.HorizontalAlignment = HorizontalAlignment.Center;
            btns.Margin = new Thickness(0, 12, 0, 0);
            root.Children.Add(btns);
            Update();
            return root;
        }

        public override void Update()
        {
            var t = Ui.Fmt(DateTime.Now - started);
            if (cTime != null) cTime.Text = t;
            if (eTime != null) eTime.Text = t;
            double lvl = Math.Min(1, audio.MicPeak * 2.5);
            cViz?.Step(lvl, true);
            eViz?.Step(lvl, true);
        }

        public void Stop(bool save)
        {
            if (closed) return;
            closed = true;
            Native.Mci("stop " + Alias);
            string path = null;
            if (save)
            {
                try { Directory.CreateDirectory(IslandWindow.MemoFolder); } catch { }
                path = Path.Combine(IslandWindow.MemoFolder, $"Memo {DateTime.Now:yyyy-MM-dd HH-mm-ss}.wav");
                if (Native.Mci($"save {Alias} \"{path}\"") != 0) path = null;
            }
            Native.Mci("close " + Alias);
            Finished = true;
            if (save)
            {
                if (path != null) Host?.MemoSaved(path);
                else Host?.Alert("memo-err", 3, g =>
                {
                    g.Banner = true; g.Glyph = ""; g.Accent = Ui.Red;
                    g.Title = "Speichern fehlgeschlagen"; g.Subtitle = "Die Aufnahme konnte nicht gespeichert werden";
                });
            }
            Host?.Relayout();
        }

        public override void OnRemoved() => Stop(false);
    }

    // =====================================================================
    //  Uhr (beim Drüberfahren im Leerlauf)
    // =====================================================================
    public sealed class ClockActivity : Activity
    {
        readonly IslandWindow host;
        TextBlock time, right;

        public ClockActivity(IslandWindow h)
        {
            host = h;
            Id = "clock";
        }

        public override double CompactWidth => 230;

        public override FrameworkElement BuildCompact()
        {
            time = Ui.Text("", 13.5, Ui.White, true, true);
            right = Ui.Text("", 12.5, Ui.Dim, false);
            var g = Ui.Row3(Ui.HStack(7, Ui.Glyph("", 13, Ui.Dim), time), null, right, new Thickness(14, 0, 15, 0));
            Update();
            return g;
        }

        public override void Update()
        {
            if (time == null) return;
            var now = DateTime.Now;
            var st = host.Settings;
            time.Text = ClockFormat.Time(st, now);
            right.Text = st.ClockBattery && host.HasBattery && host.BatteryPct >= 0
                ? $"{host.BatteryPct} %{(host.OnAc ? " ⚡" : "")}"
                : ClockFormat.Date(st, now);
        }
    }

    // =====================================================================
    //  Dashboard (Leerlauf + gedrückt halten / antippen)
    // =====================================================================
    public sealed class DashboardActivity : Activity
    {
        readonly IslandWindow host;
        TextBlock time, date, cpuT, ramT, batT;
        Bar cpuB, ramB;
        BatteryIcon bat;
        // Mini-Player
        Image miniArt;
        TextBlock miniTitle, miniArtist, miniPlay;
        BitmapSource miniShownArt;

        public DashboardActivity(IslandWindow h)
        {
            host = h;
            Id = "dashboard";
        }

        // Verbundene AirPods bekommen automatisch eine Kachel (vorne), die zu ihrem Untermenü führt
        bool AirPodsLive => host.AirPods != null && host.AirPods.Running && (host.AirPods.EndpointName != null || host.AirPods.Current != null);
        // andere Bluetooth-Kopfhörer bekommen dieselbe Kachel (führt zu ihrer Seite)
        bool PhonesLive => !AirPodsLive && host.StandardHeadphones != null;
        List<DashTile> Tiles()
        {
            var list = DashTiles.Selected(host.Settings);
            if ((AirPodsLive || PhonesLive) && !list.Any(t => t.Key == "airpods"))
            {
                list.Insert(0, DashTiles.Get("airpods"));
                if (list.Count > DashTiles.Max) list.RemoveAt(list.Count - 1);
            }
            return list;
        }
        int TileRows => Math.Max(1, (Tiles().Count + 3) / 4);
        bool HasPlayer => host.Settings.DashPlayer && host.Media.ShouldShow;
        public override Size ExpandedSize => new Size(420, 240);

        /// <summary>Was die AirPods-Kachel gerade zeigt – ändert es sich, baut die Island die Übersicht neu.</summary>
        public string AirPodsKey => AirPodsLive ? AirPodsLabel() : PhonesLive ? "p:" + PhonesLabel() : "";

        string PhonesLabel()
        {
            var d = host.StandardHeadphones;
            if (d == null) return "";
            if (d.Battery.HasValue) return d.Battery.Value + " %";
            return d.Name.Length > 11 ? d.Name.Substring(0, 10) + "…" : d.Name;
        }

        string AirPodsLabel()
        {
            var st = host.AirPods?.Current;
            if (st == null) return "AirPods ✓";
            if (st.IsHeadphones) return (st.Left.Battery ?? st.Right.Battery)?.ToString() + " %";
            string P(int? v) => v.HasValue ? v.Value.ToString() : "–";
            return $"L {P(st.Left.Battery)} · R {P(st.Right.Battery)}";
        }
        public override FrameworkElement BuildCompact() => new Grid();

        public override FrameworkElement BuildExpanded()
        {
            // oben Uhr + Player, unten die Kachelreihe – dazwischen Luft (wie der iOS-Sperrbildschirm)
            // Uhr/Player und Kachelreihe als ein Block, senkrecht mittig – unten bleibt Platz für die Seitenpunkte
            var root = new Grid { Margin = new Thickness(22, 8, 22, 14) };
            var block = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
            root.Children.Add(block);

            // Uhr + Datum links · Mini-Player rechts (CPU/RAM weggelassen – braucht man hier nicht)
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var cs = host.Settings;
            time = Ui.Text("", ClockFormat.Size(cs, 40), Ui.White, true, true);
            time.FontWeight = ClockFormat.Weight(cs);
            date = Ui.Text("", 13, Ui.Dim, false);
            date.TextTrimming = TextTrimming.CharacterEllipsis;
            if (cs.ClockDate == "off" && !(cs.ClockBattery && host.HasBattery)) date.Visibility = Visibility.Collapsed;
            var left = Ui.VStack(0, time, date);
            if (!HasPlayer)
            {
                // ohne Player steht die Uhr mittig
                left.HorizontalAlignment = HorizontalAlignment.Center;
                time.HorizontalAlignment = date.HorizontalAlignment = HorizontalAlignment.Center;
                time.TextAlignment = date.TextAlignment = TextAlignment.Center;
            }
            miniArt = null; miniTitle = miniArtist = miniPlay = null; miniShownArt = null;
            cpuB = null; ramB = null; cpuT = null; ramT = null; bat = null; batT = null;
            Grid.SetColumn(left, 0);
            top.Children.Add(left);
            if (HasPlayer)
            {
                var right = BuildMiniPlayer();
                Grid.SetColumn(right, 1);
                top.Children.Add(right);
            }
            block.Children.Add(top);

            // Eine Reihe: deine 4 wichtigsten Kacheln + „Mehr“ (alle Funktionen als Menü)
            var grid = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 22, 0, 0) };
            var row = Tiles();
            row.Add(DashTiles.More);
            foreach (var t in row)
            {
                var tile = t;
                bool live = tile.Key == "airpods" && (AirPodsLive || PhonesLive);
                var cell = new StackPanel { Width = 75, Background = Brushes.Transparent, Cursor = Cursors.Hand };
                var icon = DashTiles.Icon(tile, 50);
                if (live)
                {
                    // verbunden: Akku statt Name, grüner Punkt am Symbol
                    var dot = new System.Windows.Shapes.Ellipse
                    {
                        Width = 10, Height = 10, Fill = Ui.Br(Ui.Green), Stroke = Brushes.Black, StrokeThickness = 1.5,
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 3, 3, 0)
                    };
                    icon.Child = null;
                    var glyph = Ui.Glyph("\uE7F6", 19, Ui.White);   // verbunden: Kopfhörer statt Bluetooth-Symbol
                    var layer = new Grid();
                    layer.Children.Add(glyph);
                    layer.Children.Add(dot);
                    icon.Child = layer;
                }
                icon.HorizontalAlignment = HorizontalAlignment.Center;
                var scale = new ScaleTransform(1, 1);
                icon.RenderTransformOrigin = new Point(0.5, 0.5);
                icon.RenderTransform = scale;
                var lbl = Ui.Text(live ? (AirPodsLive ? AirPodsLabel() : PhonesLabel()) : tile.Label, 11, live ? Ui.White : Ui.Dim, false);
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                lbl.MaxWidth = 73;
                lbl.Margin = new Thickness(1, 6, 1, 0);
                cell.Children.Add(icon);
                cell.Children.Add(lbl);
                cell.MouseEnter += (s, e) => icon.Background = Ui.Br(Colors.White, 60);
                bool glass = icon is GlassBorder gb && gb.HasGlass;
                cell.MouseLeave += (s, e) =>
                {
                    icon.Background = Ui.Faint;
                    (icon as GlassBorder)?.Lift(false);
                    Ui.Press(scale, 1, 300, new SpringEase());
                };
                cell.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    (icon as GlassBorder)?.Lift(true);
                    // Glas hebt sich an (iOS 26), sonst leicht eindrücken
                    Ui.Press(scale, glass ? 1.08 : 0.9, 130, new CubicEase { EasingMode = EasingMode.EaseOut });
                };
                cell.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    (icon as GlassBorder)?.Lift(false);
                    Ui.Press(scale, 1, 380, new SpringEase());
                    try { tile.Run(host); } catch (Exception ex) { Program.Log(ex); }
                };
                grid.Children.Add(cell);
            }
            block.Children.Add(grid);
            Update();
            return root;
        }

        /// <summary>Schlichter Player in der Übersicht – immer erreichbar, solange etwas läuft oder pausiert ist.</summary>
        FrameworkElement BuildMiniPlayer()
        {
            var m = host.Media;
            var box = new Border { Background = Ui.Br(Colors.White, 22), CornerRadius = new CornerRadius(14), Height = 56, Width = 220, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(8, 0, 6, 0) };
            var art = Ui.ArtBox(40, 9, out miniArt);
            miniTitle = Ui.Text("", 13, Ui.White);
            miniArtist = Ui.Text("", 11.5, Ui.Dim, false);
            var texts = Ui.VStack(1, miniTitle, miniArtist);
            texts.Margin = new Thickness(10, 0, 8, 0);
            var info = new Grid { Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = "Player öffnen" };
            info.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            info.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(texts, 1);
            info.Children.Add(art);
            info.Children.Add(texts);
            info.MouseLeftButtonDown += (s, e) => e.Handled = true;
            info.MouseLeftButtonUp += (s, e) => { e.Handled = true; host.ShowPlayer(); };
            var prev = Ui.RoundButton("\uE892", 14, m.Previous, 34, Brushes.Transparent);
            var play = Ui.RoundButton("\uE768", 17, m.PlayPause, 38, Brushes.Transparent);
            miniPlay = (TextBlock)play.Child;
            var next = Ui.RoundButton("\uE893", 14, m.Next, 34, Brushes.Transparent);
            // Titel bekommt nur den Platz, der übrig ist – die drei Knöpfe bleiben immer sichtbar
            miniTitle.TextTrimming = miniArtist.TextTrimming = TextTrimming.CharacterEllipsis;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var btns = Ui.HStack(0, prev, play, next);
            Grid.SetColumn(btns, 1);
            row.Children.Add(info);
            row.Children.Add(btns);
            box.Child = row;
            UpdateMini();
            return box;
        }

        void UpdateMini()
        {
            if (miniTitle == null) return;
            var m = host.Media;
            miniTitle.Text = m.Title;
            miniArtist.Text = string.IsNullOrEmpty(m.AppName) ? m.Artist : m.Artist + " · " + m.AppName;
            miniPlay.Text = m.Playing ? "\uE769" : "\uE768";
            if (m.Art != miniShownArt) { miniShownArt = m.Art; miniArt.Source = m.Art; }
        }

        static FrameworkElement StatRow(string label, Color c, out Bar bar, out TextBlock val)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            var l = Ui.Text(label, 11, Ui.Dim, false);
            bar = new Bar(5, Ui.Br(c)) { Margin = new Thickness(4, 0, 8, 0) };
            val = Ui.Text("", 11.5, Ui.White, true, true);
            val.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(l, 0); Grid.SetColumn(bar, 1); Grid.SetColumn(val, 2);
            g.Children.Add(l); g.Children.Add(bar); g.Children.Add(val);
            return g;
        }

        public override void Update()
        {
            if (time == null) return;
            var now = DateTime.Now;
            var cs = host.Settings;
            time.Text = ClockFormat.Time(cs, now);
            date.Text = ClockFormat.Date(cs, now);
            if (cs.ClockBattery && host.HasBattery && host.BatteryPct >= 0)
                date.Text += (date.Text.Length > 0 ? "  ·  " : "") + $"{host.BatteryPct} %{(host.OnAc ? " ⚡" : "")}";
            UpdateMini();
        }
    }

    /// <summary>Uhr nach deinen Einstellungen (24/12 Std., Sekunden, Datum, Schrift, Größe).</summary>
    public static class ClockFormat
    {
        public static string Time(Settings s, DateTime n)
        {
            if (s.Clock24) return n.ToString(s.ClockSeconds ? "HH:mm:ss" : "HH:mm", Ui.De);
            return n.ToString(s.ClockSeconds ? "h:mm:ss" : "h:mm", Ui.De) + (n.Hour < 12 ? " AM" : " PM");
        }

        public static string Date(Settings s, DateTime n) => s.ClockDate switch
        {
            "long" => n.ToString("dddd, d. MMMM", Ui.De),
            "week" => n.ToString("ddd, d. MMM", Ui.De) + " · KW " + ISOWeek.GetWeekOfYear(n),
            "numeric" => n.ToString("dd.MM.yyyy", Ui.De),
            "off" => "",
            _ => n.ToString("ddd, d. MMM", Ui.De),
        };

        public static FontWeight Weight(Settings s) =>
            s.ClockStyle == "light" ? FontWeights.Light : s.ClockStyle == "regular" ? FontWeights.Normal : FontWeights.SemiBold;

        /// <summary>Schriftgröße – mit Sekunden oder AM/PM etwas kleiner, damit es neben den Player passt.</summary>
        public static double Size(Settings s, double large)
        {
            double f = s.ClockSize == "medium" ? 0.8 : s.ClockSize == "xl" ? 1.15 : 1.0;
            if (s.ClockSeconds) f *= 0.8;
            if (!s.Clock24) f *= 0.85;
            return Math.Round(large * f);
        }
    }
}

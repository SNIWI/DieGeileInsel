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
        string cKey;
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
            eSeek = PlayerUi.SeekBar();
            // links die gespielte Zeit, rechts die verbleibende (wie Apple Music); Knopf erst beim Drüberfahren
            eSeek.Changed = f => { ePos.Text = Ui.Fmt(TimeSpan.FromTicks((long)(m.Duration.Ticks * f))); eDur.Text = "-" + Ui.Fmt(TimeSpan.FromTicks((long)(m.Duration.Ticks * (1 - f)))); };
            eSeek.Committed = f => m.Seek(f);
            ePos = Ui.Text("0:00", 11, Ui.Dim, false, true);
            eDur = Ui.Text("", 11, Ui.Dim, false, true);
            var seekGroup = new StackPanel();
            seekGroup.Children.Add(eSeek);
            seekGroup.Children.Add(Ui.Row3(ePos, null, eDur, new Thickness(0, -2, 0, 0)));

            // Steuerung: Zufall · Zurück · Play/Pause · Weiter · Wiederholen
            eShuffle = SmallToggle("", m.ToggleShuffle, out eShuffleG);
            eShuffle.ToolTip = "Zufallswiedergabe";
            Border prev = null, next = null;
            prev = Ui.RoundButton("", 18, () => { player?.Hint(-1); PlayerUi.Nudge(prev, -1); m.Previous(); }, 44, Brushes.Transparent);
            var play = Ui.RoundButton("", 28, m.PlayPause, 52, Brushes.Transparent);
            ePlay = (TextBlock)play.Child;
            next = Ui.RoundButton("", 18, () => { player?.Hint(1); PlayerUi.Nudge(next, 1); m.Next(); }, 44, Brushes.Transparent);
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
            if (cTitle != null)
            {
                string ck = m.Title + "|" + m.Artist;
                if (cKey != null && ck != cKey) PlayerUi.CompactChange(cArt, cTitle);
                cKey = ck;
                cTitle.Text = m.Title;
            }

            if (eTitle == null) return;
            eTitle.Text = m.Title;
            eArtist.Text = m.Artist;
            eApp.Text = string.IsNullOrEmpty(m.AppName) ? "" : "In " + m.AppName + " öffnen";
            player?.Animate(m.Title + "|" + m.Artist, m.Playing, m.Accent, ePlay);
            if (!eSeek.Dragging)
            {
                ePos.Text = m.Duration > TimeSpan.Zero ? Ui.Fmt(m.Position) : "";
                eDur.Text = m.Duration > TimeSpan.Zero ? "-" + Ui.Fmt(m.Duration - m.Position) : "";
                eSeek.Bar.Value = m.Duration.TotalSeconds > 0 ? m.Position.TotalSeconds / m.Duration.TotalSeconds : 0;
            }
            // Livestream/Radio ohne Länge: kein leerer Balken
            var seekVis = m.Duration > TimeSpan.Zero ? Visibility.Visible : Visibility.Hidden;
            if (eSeek.Visibility != seekVis) eSeek.Visibility = seekVis;

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

        // Wie der iPhone-Timer: Ring, der sich leert, große ruhige Ziffern
        TimerRing cRing, mRing, eRing;
        TextBlock eEnd;

        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(420, 200);

        static TextBlock Digits(double size, Brush b)
        {
            var t = Ui.Text("", size, b, true, true);
            t.FontFamily = Ui.TextFont;
            System.Windows.Documents.Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular);   // Ziffern springen nicht hin und her
            return t;
        }

        public override FrameworkElement BuildCompact()
        {
            cRing = new TimerRing(20, 2.6, Color) { VerticalAlignment = VerticalAlignment.Center };
            cTime = Digits(15, Ui.Br(Color));
            var label = Ui.Text(Label, 12.5, Ui.Dim, false, true);
            label.Margin = new Thickness(8, 0, 0, 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            var g = Ui.Row3(Ui.HStack(0, cRing, label), null, cTime, new Thickness(10, 0, 15, 0));
            Refresh();
            return g;
        }

        public override FrameworkElement BuildMinimal()
        {
            mRing = new TimerRing(28, 2.6, Color);
            mText = Digits(10, Ui.Br(Color));
            mText.HorizontalAlignment = HorizontalAlignment.Center;
            mText.VerticalAlignment = VerticalAlignment.Center;
            mRing.Children.Add(mText);
            Refresh();
            return mRing;
        }

        public override FrameworkElement BuildExpanded()
        {
            // links: großer Ring mit der Restzeit, rechts: Name, Endzeit, Knöpfe
            eRing = new TimerRing(132, 7, Color) { VerticalAlignment = VerticalAlignment.Center };
            eTime = Digits(30, Ui.White);
            eTime.HorizontalAlignment = HorizontalAlignment.Center;
            eTime.VerticalAlignment = VerticalAlignment.Center;
            eRing.Children.Add(eTime);

            var icon = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Ui.Br(Color, 55), Child = Icon(15) };
            var title = Ui.Text(Label, 18, Ui.White, true, true);
            title.Margin = new Thickness(10, 0, 0, 0);
            title.VerticalAlignment = VerticalAlignment.Center;
            var head = Ui.HStack(0, icon, title);
            eEnd = Ui.Text("", 13, Ui.Dim, false);
            eEnd.Margin = new Thickness(0, 6, 0, 0);
            eSub = Ui.Text("", 12, Ui.Br(Color), false);
            eSub.Margin = new Thickness(0, 2, 0, 0);

            var play = Ui.RoundButton("", 20, TogglePause, 50, Ui.Br(Color, 75));
            ePlay = (TextBlock)play.Child;
            var plus1 = Ui.RoundButton("+1", 13, AddMinute, 42, Ui.Faint, Ui.White, Ui.TextFont);
            plus1.ToolTip = "1 Minute mehr";
            var plus5 = Ui.RoundButton("+5", 13, () => { for (int i = 0; i < 5; i++) AddMinute(); }, 42, Ui.Faint, Ui.White, Ui.TextFont);
            plus5.ToolTip = "5 Minuten mehr";
            var stop = Ui.RoundButton("", 15, Cancel, 42, Ui.Br(Ui.Red, 60), Ui.Br(Ui.Red));
            stop.ToolTip = "Timer beenden";
            var btns = Ui.HStack(12, plus1, plus5, play, stop);
            btns.Margin = new Thickness(0, 16, 0, 0);

            var right = Ui.VStack(0, head, eEnd, eSub, btns);
            right.VerticalAlignment = VerticalAlignment.Center;
            right.Margin = new Thickness(24, 0, 0, 0);

            var g = new Grid { Margin = new Thickness(24, 12, 24, 12) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(right, 1);
            g.Children.Add(eRing);
            g.Children.Add(right);
            Refresh();
            return g;
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
            double left = total.TotalSeconds > 0 ? Math.Max(0, Math.Min(1, r.TotalSeconds / total.TotalSeconds)) : 0;
            if (cTime != null) cTime.Text = txt;
            if (eTime != null) eTime.Text = txt;
            cRing?.Set(left, paused); mRing?.Set(left, paused); eRing?.Set(left, paused);
            if (eEnd != null) eEnd.Text = paused ? "Pausiert" : "Endet um " + (DateTime.Now + r).ToString("HH:mm") + " Uhr";
            if (eSub != null) eSub.Text = "von " + Ui.Fmt(total);
            if (ePlay != null) ePlay.Text = paused ? "" : "";
            if (eBar != null) eBar.Value = 1 - left;
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

    /// <summary>Ring, der sich leert (Restzeit). Pausiert: blasser.</summary>
    public sealed class TimerRing : Grid
    {
        readonly double size, thick;
        readonly System.Windows.Shapes.Path arc;
        readonly System.Windows.Shapes.Ellipse track;
        readonly Color color;
        double shown = -1;
        bool shownPaused;

        public TimerRing(double size, double thickness, Color color)
        {
            this.size = size; thick = thickness; this.color = color;
            Width = size; Height = size;
            track = new System.Windows.Shapes.Ellipse { Stroke = Ui.Br(color, 50), StrokeThickness = thickness };
            arc = new System.Windows.Shapes.Path { Stroke = Ui.Br(color), StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            Children.Add(track);
            Children.Add(arc);
        }

        public void Set(double fraction, bool paused)
        {
            if (Math.Abs(fraction - shown) < 0.0015 && paused == shownPaused) return;
            shown = fraction; shownPaused = paused;
            arc.Opacity = paused ? 0.45 : 1;
            double r = (size - thick) / 2, c = size / 2;
            if (fraction >= 0.999) { arc.Data = new EllipseGeometry(new Point(c, c), r, r); return; }
            if (fraction <= 0.001) { arc.Data = null; return; }
            double a = fraction * 2 * Math.PI;   // von oben im Uhrzeigersinn
            var start = new Point(c, c - r);
            var end = new Point(c + r * Math.Sin(a), c - r * Math.Cos(a));
            var fig = new PathFigure { StartPoint = start, IsClosed = false };
            fig.Segments.Add(new ArcSegment(end, new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, true));
            arc.Data = new PathGeometry(new[] { fig });
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

        // Steht auch in der Leerlauf-Pille: Uhrzeit links, Wetter (oder Akku/Datum) rechts
        public override double CompactWidth => 200;

        Border wIcon;
        string wKey;

        public override FrameworkElement BuildCompact()
        {
            time = Ui.Text("", 13.5, Ui.White, true, true);
            right = Ui.Text("", 12.5, Ui.Dim, false, true);
            wIcon = new Border { Width = 16, Height = 16 };
            wKey = null;
            wIcon.Margin = new Thickness(0, 0, 5, 0);
            wIcon.VerticalAlignment = VerticalAlignment.Center;
            var g = Ui.Row3(time, null, Ui.HStack(0, wIcon, right), new Thickness(16, 0, 16, 0));
            Update();
            return g;
        }

        public override void Update()
        {
            if (time == null) return;
            var now = DateTime.Now;
            var st = host.Settings;
            time.Text = ClockFormat.Time(st, now);
            var w = host.Weather;
            if (w != null && w.HasData && st.WeatherEnabled)
            {
                wIcon.Visibility = Visibility.Visible;
                // farbiges Symbol – nur neu zeichnen, wenn sich das Wetter ändert
                string k = w.Code + "|" + w.IsDay;
                if (k != wKey) { wKey = k; wIcon.Child = WeatherIcon.Create(w.Code, w.IsDay, 16); }
                right.Text = WeatherService.Deg(w.Temp);
            }
            else
            {
                wIcon.Visibility = Visibility.Collapsed;
                right.Text = st.ClockBattery && host.HasBattery && host.BatteryPct >= 0
                    ? $"{host.BatteryPct} %{(host.OnAc ? " ⚡" : "")}"
                    : ClockFormat.Date(st, now);
            }
        }
    }

    // =====================================================================
    //  Dashboard (Leerlauf + gedrückt halten / antippen)
    // =====================================================================
    public sealed class DashboardActivity : Activity
    {
        readonly IslandWindow host;
        TextBlock time, date;
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
        bool HasPlayer => host.Settings.DashPlayer && host.Media.ShouldShow && !IslandWindow.Demo;
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

        DateTime shownWeather;

        public override FrameworkElement BuildExpanded()
        {
            // Aufbau wie der iPhone-Sperrbildschirm: oben Datum über großer Uhr, rechts ein ruhiges Widget
            // (läuft Musik: Player, sonst Wetter), unten eine gleichmäßige Reihe runder Knöpfe
            var cs = host.Settings;
            var root = new Grid { Margin = new Thickness(26, 10, 26, 12) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ---------- oben: Datum + Uhr · Widget ----------
            var top = new Grid { VerticalAlignment = VerticalAlignment.Center };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            date = Ui.Text("", 13, Ui.Br(Colors.White, 165), false);
            date.TextTrimming = TextTrimming.CharacterEllipsis;
            if (cs.ClockDate == "off" && !(cs.ClockBattery && host.HasBattery)) date.Visibility = Visibility.Collapsed;
            time = Ui.Text("", ClockFormat.Size(cs, 46), Ui.White, true, true);
            time.FontWeight = ClockFormat.Weight(cs);
            time.Margin = new Thickness(-2, -3, 0, 0);
            var left = Ui.VStack(0, date, time);
            left.VerticalAlignment = VerticalAlignment.Center;
            left.HorizontalAlignment = HorizontalAlignment.Left;
            // Uhr antippen → Weltuhr
            left.Background = Brushes.Transparent;
            left.Cursor = Cursors.Hand;
            left.ToolTip = "Weltuhr";
            left.MouseLeftButtonDown += (s, e) => e.Handled = true;
            left.MouseLeftButtonUp += (s, e) => { e.Handled = true; host.ShowWorldClock(); };
            miniArt = null; miniTitle = miniArtist = miniPlay = null; miniShownArt = null;
            top.Children.Add(left);
            var widget = HasPlayer ? BuildMiniPlayer() : WeatherGlance();
            if (widget != null)
            {
                Grid.SetColumn(widget, 1);
                top.Children.Add(widget);
            }
            else
            {
                // nichts daneben: Uhr mittig
                left.HorizontalAlignment = HorizontalAlignment.Center;
                time.HorizontalAlignment = date.HorizontalAlignment = HorizontalAlignment.Center;
                Grid.SetColumnSpan(left, 2);
            }
            root.Children.Add(top);

            // ---------- unten: runde Knöpfe, gleichmäßig über die ganze Breite ----------
            var row = Tiles();
            row.Add(DashTiles.More);
            var grid = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Columns = row.Count, Margin = new Thickness(-8, 12, -8, 0) };
            Grid.SetRow(grid, 1);
            foreach (var t in row) grid.Children.Add(TileCell(t));
            root.Children.Add(grid);
            Update();
            return root;
        }

        /// <summary>Ein runder Knopf mit Beschriftung (wie im Kontrollzentrum).</summary>
        FrameworkElement TileCell(DashTile tile)
        {
            bool live = tile.Key == "airpods" && (AirPodsLive || PhonesLive);
            var cell = new StackPanel { Background = Brushes.Transparent, Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Center };
            const double size = 48;
            var icon = DashTiles.Icon(tile, size);
            icon.CornerRadius = new CornerRadius(size / 2);
            var idle = live ? Ui.Br(Ui.Green, 50) : Ui.Br(Colors.White, 26);
            icon.Background = idle;
            if (live)
            {
                // verbunden: Kopfhörer-Symbol auf grünem Grund, Akku statt Name
                icon.Child = Ui.Glyph("", 19, Ui.Br(Ui.Green));
            }
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            var scale = new ScaleTransform(1, 1);
            icon.RenderTransformOrigin = new Point(0.5, 0.5);
            icon.RenderTransform = scale;
            var lbl = Ui.Text(live ? (AirPodsLive ? AirPodsLabel() : PhonesLabel()) : tile.Label, 11, live ? Ui.White : Ui.Br(Colors.White, 150), false);
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            lbl.TextAlignment = TextAlignment.Center;
            lbl.MaxWidth = 78;
            lbl.Margin = new Thickness(1, 7, 1, 0);
            cell.Children.Add(icon);
            cell.Children.Add(lbl);
            cell.MouseEnter += (s, e) => icon.Background = live ? Ui.Br(Ui.Green, 75) : Ui.Br(Colors.White, 48);
            cell.MouseLeave += (s, e) =>
            {
                icon.Background = idle;
                Ui.Press(scale, 1, 300, new SpringEase());
            };
            cell.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                Ui.Press(scale, 0.9, 130, new CubicEase { EasingMode = EasingMode.EaseOut });
            };
            cell.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                Ui.Press(scale, 1, 380, new SpringEase());
                try { tile.Run(host); } catch (Exception ex) { Program.Log(ex); }
            };
            return cell;
        }

        /// <summary>Wetter rechts neben der Uhr: farbiges Symbol, Temperatur, Beschreibung (antippen: Wetter-Seite).</summary>
        FrameworkElement WeatherGlance()
        {
            var w = host.Weather;
            shownWeather = w?.LastUpdate ?? default;
            if (w == null || !w.HasData || !host.Settings.WeatherEnabled) return null;
            var today = w.Days.FirstOrDefault();
            var temp = Ui.Text(WeatherService.Deg(w.Temp), 30, Ui.White, false);
            temp.FontWeight = FontWeights.Light;
            temp.HorizontalAlignment = HorizontalAlignment.Right;
            var desc = Ui.Text(WeatherService.Text(w.Code) + (today != null ? $" · {WeatherService.Deg(today.Max)} / {WeatherService.Deg(today.Min)}" : ""), 12, Ui.Br(Colors.White, 150), false);
            desc.HorizontalAlignment = HorizontalAlignment.Right;
            // Symbol direkt neben der Temperatur, Beschreibung darunter – alles rechtsbündig
            var icon = WeatherIcon.Create(w.Code, w.IsDay, 30);
            icon.Margin = new Thickness(0, 0, 8, 0);
            var line = Ui.HStack(0, icon, temp);
            line.HorizontalAlignment = HorizontalAlignment.Right;
            var box = Ui.VStack(2, line, desc);
            box.Background = Brushes.Transparent;
            box.Cursor = Cursors.Hand;
            box.VerticalAlignment = VerticalAlignment.Center;
            box.ToolTip = w.Place;
            box.MouseLeftButtonDown += (s, e) => e.Handled = true;
            box.MouseLeftButtonUp += (s, e) => { e.Handled = true; host.ShowWeather(); };
            return box;
        }

        /// <summary>Schlichter Player in der Übersicht – immer erreichbar, solange etwas läuft oder pausiert ist.</summary>
        FrameworkElement BuildMiniPlayer()
        {
            var m = host.Media;
            var box = new Border { Background = Ui.Br(Colors.White, 20), CornerRadius = new CornerRadius(16), Height = 60, Width = 236, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(9, 0, 6, 0) };
            var art = Ui.ArtBox(42, 10, out miniArt);
            miniTitle = Ui.Text("", 13, Ui.White);
            miniArtist = Ui.Text("", 11.5, Ui.Br(Colors.White, 150), false);
            var texts = Ui.VStack(1, miniTitle, miniArtist);
            texts.Margin = new Thickness(10, 0, 6, 0);
            var info = new Grid { Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = "Player öffnen" };
            info.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            info.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(texts, 1);
            info.Children.Add(art);
            info.Children.Add(texts);
            info.MouseLeftButtonDown += (s, e) => e.Handled = true;
            info.MouseLeftButtonUp += (s, e) => { e.Handled = true; host.ShowPlayer(); };
            var play = Ui.RoundButton("", 16, m.PlayPause, 34, Brushes.Transparent);
            miniPlay = (TextBlock)play.Child;
            var next = Ui.RoundButton("", 13, m.Next, 30, Brushes.Transparent);
            // Titel bekommt nur den Platz, der übrig ist – die Knöpfe bleiben immer sichtbar
            miniTitle.TextTrimming = miniArtist.TextTrimming = TextTrimming.CharacterEllipsis;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var btns = Ui.HStack(0, play, next);
            btns.VerticalAlignment = VerticalAlignment.Center;
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
            // neues Wetter: Widget neben der Uhr auffrischen
            if (!HasPlayer && host.Weather != null && host.Weather.HasData && host.Weather.LastUpdate != shownWeather) host.Rebuild(this);
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
            "week" => n.ToString("dddd, d. MMM", Ui.De) + " · KW " + ISOWeek.GetWeekOfYear(n),
            "numeric" => n.ToString("dd.MM.yyyy", Ui.De),
            "off" => "",
            _ => n.ToString("dddd, d. MMM", Ui.De),   // voller Tagesname
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

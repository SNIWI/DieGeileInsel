using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIsland
{
    /// <summary>
    /// Eingebauter Spotify-Player (mit deinem Konto verbunden): Cover, Titel, „Gefällt mir“,
    /// Spulen, Zufall, Wiederholen, Lautstärke des Spotify-Geräts, Gerät wechseln, Playlists.
    /// Funktioniert auch, wenn die Musik auf Handy oder Lautsprecher läuft.
    /// </summary>
    public sealed class SpotifyActivity : Activity
    {
        /// <summary>Farbe des Spotify-Players (in den Einstellungen wählbar, Standard Spotify-Grün).</summary>
        public static Color Green => Ui.SpotifyAccent;
        static SolidColorBrush GreenBrush => Ui.SpotifyBrush;

        readonly IslandWindow host;
        readonly SpotifyService sp;
        readonly AudioService audio;
        Image cArt, eArt;
        Visualizer cViz, eViz, mViz;
        TextBlock cTitle;
        string cKey;
        TextBlock eTitle, eArtist, eDevice, ePos, eDur, ePlay, eHeart, eShuffleG, eRepeatG, eVolT;
        DragBar eSeek, eVol;
        string shownArt;
        Color accent = Green;
        PlayerUi player;

        /// <summary>Spielt Spotify gerade – laut Web API oder laut Windows (falls die API kurz hinterherhinkt).</summary>
        bool IsPlaying => sp.Playing || (host.Media.AppName == "Spotify" && host.Media.Playing);

        public SpotifyActivity(IslandWindow h, SpotifyService s, AudioService a)
        {
            host = h;
            sp = s;
            audio = a;
            Id = "spotify";
            Priority = 51;
        }

        public override double CompactWidth => 300;
        public override Size ExpandedSize => new Size(420, 240);

        public override FrameworkElement BuildCompact()
        {
            var art = Ui.ArtBox(24, 6, out cArt);
            cViz = new Visualizer(4, 16, 3, 2.5);
            cTitle = Ui.Text("", 13, Ui.White, true);
            cTitle.Margin = new Thickness(9, 0, 9, 0);
            shownArt = null;
            var g = Ui.Row3(art, cTitle, cViz, new Thickness(7, 0, 15, 0));
            Update();
            return g;
        }

        public override FrameworkElement BuildMinimal()
        {
            mViz = new Visualizer(3, 14, 2.6, 2);
            mViz.SetColor(accent);
            return mViz;
        }

        static Border Toggle(string glyph, Action click, out TextBlock g, double size = 36)
        {
            var b = Ui.RoundButton(glyph, 15, click, size, Brushes.Transparent, Ui.Dim);
            g = (TextBlock)b.Child;
            return b;
        }

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = new Thickness(22, 18, 22, 12) };

            // Cover + Titel + Gerät + Herz
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            player = new PlayerUi(() => IslandWindow.Open(sp.Track?.Uri ?? "spotify:"), "In Spotify öffnen");
            eArt = player.Art;
            eTitle = Ui.Text("", 17, Ui.White);
            eArtist = Ui.Text("", 13, Ui.Dim, false);
            eDevice = Ui.Link("", host.ShowSpotifyDevices, 11.5);
            eDevice.Foreground = GreenBrush;
            eDevice.Margin = new Thickness(0, 3, 0, 0);
            var texts = Ui.VStack(1, eTitle, eArtist, eDevice);
            texts.Margin = new Thickness(0, 0, 10, 0);
            var heart = Ui.RoundButton("", 17, sp.ToggleLike, 38, Brushes.Transparent);
            heart.ToolTip = "Gefällt mir";
            heart.VerticalAlignment = VerticalAlignment.Center;
            eHeart = (TextBlock)heart.Child;
            var lists = Ui.RoundButton("\uE8FD", 13, host.ShowSpotifyPlaylists, 32, Brushes.Transparent, Ui.Dim);
            lists.ToolTip = "Playlists";
            var switcher = Ui.RoundButton("", 13, host.ShowPlayerSources, 32, Brushes.Transparent, Ui.Dim);
            switcher.ToolTip = "Player wechseln (Spotify, Browser, …)";
            var right = Ui.HStack(2, heart, lists, switcher);
            right.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(texts, 1); Grid.SetColumn(right, 2);
            top.Children.Add(texts); top.Children.Add(right);

            // Fortschritt
            eSeek = PlayerUi.SeekBar();
            // links die gespielte Zeit, rechts die verbleibende (wie Apple Music); Knopf erst beim Drüberfahren
            eSeek.Changed = f => { double ms = sp.Track?.DurationMs ?? 0; ePos.Text = Ui.Fmt(TimeSpan.FromMilliseconds(ms * f)); eDur.Text = "-" + Ui.Fmt(TimeSpan.FromMilliseconds(ms * (1 - f))); };
            eSeek.Committed = f => sp.Seek(f);
            ePos = Ui.Text("0:00", 11, Ui.Dim, false, true);
            eDur = Ui.Text("", 11, Ui.Dim, false, true);
            var seekGroup = new StackPanel();
            seekGroup.Children.Add(eSeek);
            seekGroup.Children.Add(Ui.Row3(ePos, null, eDur, new Thickness(0, -2, 0, 0)));

            // Steuerung
            var shuffle = Toggle("", sp.ToggleShuffle, out eShuffleG);
            shuffle.ToolTip = "Zufallswiedergabe";
            Border prev = null, next = null;
            prev = Ui.RoundButton("", 18, () => { player?.Hint(-1); PlayerUi.Nudge(prev, -1); sp.Previous(); }, 44, Brushes.Transparent);
            var play = Ui.RoundButton("", 28, sp.PlayPause, 52, Brushes.Transparent);
            ePlay = (TextBlock)play.Child;
            next = Ui.RoundButton("", 18, () => { player?.Hint(1); PlayerUi.Nudge(next, 1); sp.Next(); }, 44, Brushes.Transparent);
            var repeat = Toggle("", sp.CycleRepeat, out eRepeatG);
            repeat.ToolTip = "Wiederholen";
            var ctrl = Ui.HStack(14, shuffle, prev, play, next, repeat);
            ctrl.HorizontalAlignment = HorizontalAlignment.Center;

            // Lautstärke des Spotify-Geräts
            var vol = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            vol.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            vol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            vol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            vol.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var vIcon = Ui.Glyph("", 14, Ui.Dim);
            eVol = new DragBar(Ui.SpotifyBrush, 4) { Margin = new Thickness(10, 0, 8, 0), WheelStep = 0.05 };
            eVol.Current = () => (sp.Device?.Volume ?? 50) / 100.0;
            eVol.Changed = f => eVolT.Text = Math.Round(f * 100).ToString();
            eVol.Committed = f => sp.SetVolume(f);
            eVolT = Ui.Text("", 11.5, Ui.Dim, false, true);
            eVolT.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(vIcon, 0); Grid.SetColumn(eVol, 1); Grid.SetColumn(eVolT, 2);
            vol.Children.Add(vIcon); vol.Children.Add(eVol); vol.Children.Add(eVolT);
            // Lautstärke-Regler nicht mehr im Player (Platz) – Elemente bleiben fürs Update bestehen

            shownArt = null;
            Update();
            return player.Layout(top, texts, seekGroup, ctrl);
        }

        void LoadArt()
        {
            var url = sp.Track?.ArtUrl;
            if (url == shownArt) return;
            shownArt = url;
            var img = WebAvatar.Load(url);
            if (cArt != null) cArt.Source = img;
            if (eArt != null) eArt.Source = img;
            // Akzentfarbe aus dem Cover, sobald es geladen ist
            if (img is BitmapImage bi)
            {
                void Apply()
                {
                    try { accent = MediaService.AccentOf(bi); } catch { accent = Green; }
                    if (accent == Colors.White) accent = Green;
                    cViz?.SetColor(accent); eViz?.SetColor(accent); mViz?.SetColor(accent);
                }
                if (bi.IsDownloading) bi.DownloadCompleted += (s, e) => Apply();
                else Apply();
            }
        }

        public override void Update()
        {
            LoadArt();
            bool here = sp.Device == null || sp.Device.Type.Equals("Computer", StringComparison.OrdinalIgnoreCase);
            double lvl = here ? audio.Peak : 0;
            bool playing = IsPlaying;
            cViz?.Step(lvl, playing);
            eViz?.Step(lvl, playing);
            mViz?.Step(lvl, playing);
            if (cTitle != null)
            {
                string ck = (sp.Track?.Id ?? sp.Track?.Title ?? "") + "|" + sp.Track?.Artist;
                if (cKey != null && ck != cKey) PlayerUi.CompactChange(cArt, cTitle);
                cKey = ck;
                cTitle.Text = sp.Track?.Title ?? "";
            }

            if (eTitle == null) return;
            var t = sp.Track;
            eTitle.Text = t?.Title ?? "Nichts läuft";
            eArtist.Text = t?.Artist ?? "";
            eDevice.Text = sp.Device != null ? "▸ " + sp.Device.Name : "Gerät wählen";
            player?.Animate((t?.Id ?? t?.Title ?? "") + "|" + (t?.Artist ?? ""), playing, accent, ePlay);
            eHeart.Text = sp.Liked ? "" : "";
            eHeart.Foreground = sp.Liked ? GreenBrush : Ui.White;
            if (!eSeek.Dragging)
            {
                ePos.Text = sp.Duration > TimeSpan.Zero ? Ui.Fmt(sp.Position) : "";
                eDur.Text = sp.Duration > TimeSpan.Zero ? "-" + Ui.Fmt(sp.Duration - sp.Position) : "";
                eSeek.Bar.Value = sp.Duration.TotalSeconds > 0 ? sp.Position.TotalSeconds / sp.Duration.TotalSeconds : 0;
            }
            eShuffleG.Foreground = sp.Shuffle ? GreenBrush : Ui.Dim;
            eRepeatG.Foreground = sp.Repeat != "off" ? GreenBrush : Ui.Dim;
            eRepeatG.Text = sp.Repeat == "track" ? "" : "";
            if (!eVol.Dragging)
            {
                var v = sp.Device?.Volume;
                eVol.Bar.Value = (v ?? 0) / 100.0;
                eVolT.Text = v.HasValue ? v.Value.ToString() : "–";
            }
        }

        public override bool OnTap() => false;
    }

    /// <summary>Liste der Spotify-Geräte oder Playlists (eine Seite in der großen Island).</summary>
    public sealed class SpotifyListActivity : Activity
    {
        readonly IslandWindow host;
        readonly SpotifyService sp;
        public readonly bool Devices;
        List<SpotifyDevice> devices;
        List<SpotifyPlaylist> playlists;
        const int MaxRows = 5;

        public SpotifyListActivity(IslandWindow h, SpotifyService s, bool devicesMode)
        {
            host = h;
            sp = s;
            Devices = devicesMode;
            Id = devicesMode ? "spotify-devices" : "spotify-playlists";
            Priority = 1;
        }

        public async void Reload()
        {
            try
            {
                if (Devices) devices = await sp.DevicesAsync();
                else playlists = await sp.PlaylistsAsync();
            }
            catch (Exception ex) { Program.Log(ex); }
            host.Rebuild(this);
        }

        int Count => Devices ? devices?.Count ?? 0 : playlists?.Count ?? 0;
        int Rows => Math.Max(1, Math.Min(MaxRows, Count));
        public override double CompactWidth => 210;
        public override Size ExpandedSize => new Size(440, 72 + Rows * 46);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph(Devices ? "" : "", 14, Ui.Br(SpotifyActivity.Green)),
                Ui.Text(Devices ? "Spotify-Geräte" : "Playlists", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        static string DeviceGlyph(string type)
        {
            switch ((type ?? "").ToLowerInvariant())
            {
                case "smartphone": return "";
                case "speaker": return "";
                case "tv": return "";
                case "tablet": return "";
                default: return ""; // Computer
            }
        }

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = new Thickness(20, 16, 20, 10) };
            root.Children.Add(Ui.Row3(Ui.Text(Devices ? "Abspielen auf" : "Deine Playlists", 15.5, Ui.White), null,
                Ui.Link("Zurück zum Player", host.ShowPlayer), new Thickness(0)));
            var list = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

            bool loading = Devices ? devices == null : playlists == null;
            if (loading || Count == 0)
            {
                var t = Ui.Text(loading ? "Wird geladen …" : Devices ? "Kein Gerät gefunden – öffne Spotify auf einem Gerät" : "Keine Playlists", 12.5, Ui.Dim, false);
                t.Margin = new Thickness(0, 12, 0, 0);
                list.Children.Add(t);
            }
            else if (Devices)
            {
                foreach (var d in devices.Take(MaxRows))
                {
                    var dev = d;
                    var name = Ui.Text(dev.Name, 13.5, Ui.White, dev.Active);
                    name.Margin = new Thickness(12, 0, 8, 0);
                    var icon = Ui.Glyph(DeviceGlyph(dev.Type), 16, dev.Active ? Ui.Br(SpotifyActivity.Green) : Ui.Dim);
                    var check = Ui.Glyph(dev.Active ? "" : "", 14, Ui.Br(SpotifyActivity.Green));
                    list.Children.Add(Row(Ui.Row3(icon, name, check, new Thickness(0)), dev.Active, async () =>
                    {
                        if (!dev.Active && dev.Id != null) await sp.TransferAsync(dev.Id);
                        host.ShowPlayer();
                    }));
                }
            }
            else
            {
                foreach (var p in playlists.Take(MaxRows))
                {
                    var pl = p;
                    var img = WebAvatar.Load(pl.ImageUrl);
                    FrameworkElement icon;
                    if (img != null)
                    {
                        var im = new Image { Source = img, Width = 32, Height = 32, Stretch = Stretch.UniformToFill };
                        im.Clip = new RectangleGeometry(new Rect(0, 0, 32, 32), 6, 6);
                        icon = im;
                    }
                    else icon = Ui.Glyph("", 16, Ui.Dim);
                    var texts = Ui.VStack(1, Ui.Text(pl.Name, 13, Ui.White, false), Ui.Text(pl.Tracks + " Titel", 11, Ui.Dim, false));
                    texts.Margin = new Thickness(10, 0, 8, 0);
                    list.Children.Add(Row(Ui.Row3(icon, texts, Ui.Glyph("", 13, Ui.Dim), new Thickness(0)), false, async () =>
                    {
                        if (pl.Uri != null) await sp.PlayContextAsync(pl.Uri);
                        host.ShowPlayer();
                    }));
                }
            }
            root.Children.Add(list);
            return root;
        }

        static Border Row(UIElement content, bool selected, Action click)
        {
            var row = new Border
            {
                Height = 46,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 0, 8, 0),
                Cursor = Cursors.Hand,
                Background = selected ? Ui.Br(Colors.White, 28) : Brushes.Transparent,
                Child = content
            };
            row.MouseEnter += (o, e) => { if (!selected) row.Background = Ui.Br(Colors.White, 18); };
            row.MouseLeave += (o, e) => { if (!selected) row.Background = Brushes.Transparent; };
            row.MouseLeftButtonDown += (o, e) => e.Handled = true;
            row.MouseLeftButtonUp += (o, e) =>
            {
                e.Handled = true;
                try { click(); } catch (Exception ex) { Program.Log(ex); }
            };
            return row;
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace DynamicIsland
{
    /// <summary>
    /// Liest, was gerade auf dem PC läuft (Spotify, YouTube im Browser, Apple Music, VLC …)
    /// über die offizielle Windows-Medien-Schnittstelle und steuert es.
    /// </summary>
    public sealed class MediaService
    {
        readonly Dispatcher ui;
        GlobalSystemMediaTransportControlsSessionManager mgr;
        GlobalSystemMediaTransportControlsSession session;
        string artKey = "";
        bool refreshing;
        TimeSpan start;

        public string Title = "", Artist = "", AppId = "";
        public BitmapSource Art;
        public Color Accent = Colors.White;
        public bool Playing;
        public bool CanShuffle, CanRepeat, Shuffle;
        public int Repeat; // 0 = aus, 1 = Titel, 2 = Liste
        public TimeSpan Position, Duration;
        public DateTime LastPlayingAt = DateTime.MinValue;

        public bool HasSession => session != null;

        /// <summary>Player dauerhaft anzeigen (auch pausiert), sonst nur bis 60 s nach dem Pausieren.</summary>
        public bool KeepPlayer = true;

        // Pausiert die Musik länger (3 Min., ohne „Player behalten“ 1 Min.), verschwindet der Player wieder
        public bool ShouldShow => HasSession && !string.IsNullOrWhiteSpace(Title) &&
                                  (Playing || PinnedAppId != null || (DateTime.Now - LastPlayingAt).TotalMinutes < (KeepPlayer ? 3 : 1));

        public MediaService(Dispatcher d) { ui = d; }

        public async Task InitAsync()
        {
            try
            {
                mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                mgr.CurrentSessionChanged += (s, e) => ui.BeginInvoke(new Action(PickSession));
                mgr.SessionsChanged += (s, e) => ui.BeginInvoke(new Action(PickSession));
                PickSession();
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        static bool IsSpotify(GlobalSystemMediaTransportControlsSession s) =>
            (s?.SourceAppUserModelId ?? "").IndexOf("Spotify", StringComparison.OrdinalIgnoreCase) >= 0;

        static bool IsPlaying(GlobalSystemMediaTransportControlsSession s)
        {
            try { return s?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; }
            catch { return false; }
        }

        /// <summary>
        /// Wählt die passende Wiedergabe: was gerade spielt (Spotify zuerst), sonst Spotify,
        /// sonst die aktuelle Windows-Sitzung. Wird regelmäßig aufgerufen.
        /// </summary>
        /// <summary>Von dir im Player-Wechsler gewählte Quelle (null = automatisch).</summary>
        public string PinnedAppId { get; private set; }

        public sealed class Source { public string AppId, Name, Title, Artist; public bool Playing, Current; }

        /// <summary>Alle Apps, die gerade Medien anbieten (Spotify, Browser-Tabs, VLC …).</summary>
        public async Task<System.Collections.Generic.List<Source>> SourcesAsync()
        {
            var list = new System.Collections.Generic.List<Source>();
            if (mgr == null) return list;
            try
            {
                foreach (var x in mgr.GetSessions())
                {
                    var src = new Source { AppId = x.SourceAppUserModelId ?? "", Playing = IsPlaying(x) };
                    src.Name = NameOf(src.AppId);
                    src.Current = session != null && src.AppId == session.SourceAppUserModelId;
                    try
                    {
                        var p = await x.TryGetMediaPropertiesAsync();
                        src.Title = CleanTitle(p?.Title, out _);
                        src.Artist = string.IsNullOrEmpty(p?.Artist) ? (p?.AlbumArtist ?? "") : p.Artist;
                    }
                    catch { }
                    if (!list.Any(l => l.AppId == src.AppId)) list.Add(src);
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            return list;
        }

        /// <summary>Quelle fest wählen (null = wieder automatisch: was spielt, Spotify zuerst).</summary>
        public void Select(string appId)
        {
            PinnedAppId = string.IsNullOrEmpty(appId) ? null : appId;
            if (mgr == null) return;
            try
            {
                var s = PinnedAppId == null ? null : mgr.GetSessions().FirstOrDefault(x => x.SourceAppUserModelId == PinnedAppId);
                if (s != null) Attach(s);
                else { PinnedAppId = null; PickSession(); }
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        public void PickSession()
        {
            if (mgr == null) return;
            try
            {
                var all = mgr.GetSessions();
                GlobalSystemMediaTransportControlsSession best = null;
                // fest gewählte Quelle hat Vorrang, solange es sie gibt
                if (PinnedAppId != null)
                {
                    best = all.FirstOrDefault(x => x.SourceAppUserModelId == PinnedAppId);
                    if (best == null) PinnedAppId = null;
                    else
                    {
                        if (session == null || best.SourceAppUserModelId != session.SourceAppUserModelId) Attach(best);
                        return;
                    }
                }
                foreach (var x in all) if (IsPlaying(x) && IsSpotify(x)) { best = x; break; }
                if (best == null) foreach (var x in all) if (IsPlaying(x)) { best = x; break; }
                if (best == null && session != null && all.Any(x => x.SourceAppUserModelId == session.SourceAppUserModelId) && !IsPlaying(session))
                    best = all.First(x => x.SourceAppUserModelId == session.SourceAppUserModelId); // pausiert: dabei bleiben
                if (best == null) foreach (var x in all) if (IsSpotify(x)) { best = x; break; }
                if (best == null) best = mgr.GetCurrentSession();
                if (best == null && session == null) return;
                if (best != null && session != null && best.SourceAppUserModelId == session.SourceAppUserModelId) return;
                Attach(best);
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        void Attach(GlobalSystemMediaTransportControlsSession next)
        {
            try
            {
                if (session != null) session.MediaPropertiesChanged -= OnProps;
                session = next;
                if (session != null) session.MediaPropertiesChanged += OnProps;
                AppId = session?.SourceAppUserModelId ?? "";
                artKey = "";
                Poll();
                _ = RefreshPropsAsync();
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        void OnProps(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) =>
            ui.BeginInvoke(new Action(() => { _ = RefreshPropsAsync(); }));

        public async Task RefreshPropsAsync()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                var s = session;
                if (s == null) { Title = Artist = ""; Art = null; artKey = ""; return; }
                var p = await s.TryGetMediaPropertiesAsync();
                if (p == null) return;
                // Beim Songwechsel meldet manche App kurz einen leeren Titel → alten behalten,
                // damit der Player nicht zuklappt.
                if (string.IsNullOrWhiteSpace(p.Title) && !string.IsNullOrWhiteSpace(Title)) return;
                string raw = p.Title ?? "";
                Artist = string.IsNullOrEmpty(p.Artist) ? (p.AlbumArtist ?? "") : p.Artist;
                // Stream oder Datei direkt im Browser geöffnet: Titel ist nur die Adresse („stream.example.com/live/stream.mp3“) → lesbar machen
                Title = CleanTitle(raw, out string host);
                if (host != null && string.IsNullOrEmpty(Artist)) Artist = host;
                var key = raw + "|" + Artist + "|" + AppId;
                if (key != artKey || (Art == null && p.Thumbnail != null))
                {
                    artKey = key;
                    var bmp = await LoadArtAsync(p.Thumbnail);
                    Provider = DetectProvider(AppId, Title);
                    Site = null;
                    // Webseite im Browser: Seite erkennen; ohne echtes Cover (nur Browser-Logo) das Logo der Seite zeigen
                    if (IsBrowser(AppId) && (Provider == null || SiteLogo.LooksLikeIcon(bmp)))
                    {
                        string t = raw;
                        var domain = await Task.Run(() => SiteLogo.FindDomain(t));
                        if (key != artKey) return;   // inzwischen anderer Titel
                        Site = domain;
                        if (domain != null && SiteLogo.LooksLikeIcon(bmp))
                            bmp = await SiteLogo.GetAsync(domain) ?? bmp;
                        if (key != artKey) return;
                    }
                    Art = bmp;
                    Accent = bmp != null ? AccentOf(bmp) : Colors.White;
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            finally { refreshing = false; }
        }

        public void Poll()
        {
            var s = session;
            if (s == null) { Playing = false; return; }
            try
            {
                var pb = s.GetPlaybackInfo();
                Playing = pb != null && pb.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                if (Playing) LastPlayingAt = DateTime.Now;
                var c = pb?.Controls;
                CanShuffle = c != null && c.IsShuffleEnabled;
                CanRepeat = c != null && c.IsRepeatEnabled;
                Shuffle = pb?.IsShuffleActive == true;
                var rm = pb?.AutoRepeatMode;
                Repeat = rm == global::Windows.Media.MediaPlaybackAutoRepeatMode.Track ? 1
                       : rm == global::Windows.Media.MediaPlaybackAutoRepeatMode.List ? 2 : 0;

                var tl = s.GetTimelineProperties();
                if (tl != null)
                {
                    start = tl.StartTime;
                    var dur = tl.EndTime - tl.StartTime;
                    var pos = tl.Position - tl.StartTime;
                    if (Playing && tl.LastUpdatedTime.Year > 2000)
                    {
                        var since = DateTimeOffset.Now - tl.LastUpdatedTime;
                        if (since > TimeSpan.Zero && since < TimeSpan.FromHours(6)) pos += since;
                    }
                    if (dur > TimeSpan.Zero && pos > dur) pos = dur;
                    if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
                    Duration = dur;
                    Position = pos;
                }
            }
            catch { }
        }

        public async void PlayPause() { try { if (session != null) await session.TryTogglePlayPauseAsync(); } catch { } }
        public async void Next() { try { if (session != null) await session.TrySkipNextAsync(); } catch { } }
        public async void Previous() { try { if (session != null) await session.TrySkipPreviousAsync(); } catch { } }

        public async void ToggleShuffle()
        {
            try
            {
                if (session == null) return;
                Shuffle = !Shuffle;
                await session.TryChangeShuffleActiveAsync(Shuffle);
            }
            catch { }
        }

        /// <summary>Aus → Liste → Titel → aus (wie in Spotify).</summary>
        public async void CycleRepeat()
        {
            try
            {
                if (session == null) return;
                Repeat = Repeat == 0 ? 2 : Repeat == 2 ? 1 : 0;
                var mode = Repeat == 1 ? global::Windows.Media.MediaPlaybackAutoRepeatMode.Track
                         : Repeat == 2 ? global::Windows.Media.MediaPlaybackAutoRepeatMode.List
                         : global::Windows.Media.MediaPlaybackAutoRepeatMode.None;
                await session.TryChangeAutoRepeatModeAsync(mode);
            }
            catch { }
        }

        public async void Seek(double fraction)
        {
            try
            {
                fraction = Math.Max(0, Math.Min(1, fraction));
                if (session != null && Duration > TimeSpan.Zero)
                    await session.TryChangePlaybackPositionAsync((start + TimeSpan.FromTicks((long)(Duration.Ticks * fraction))).Ticks);
            }
            catch { }
        }

        static readonly System.Text.RegularExpressions.Regex UrlLike = new System.Text.RegularExpressions.Regex(
            @"^(?:[a-z][a-z0-9+.-]*://)?(?<host>[\w-]+(?:\.[\w-]+)+)(?::\d+)?/\S*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static readonly string[] MediaExt = { ".mp3", ".aac", ".m4a", ".ogg", ".opus", ".flac", ".wav", ".mp4", ".webm", ".mkv", ".m3u8", ".pls" };
        static readonly string[] Generic = { "stream", "live", "listen", "index", "play", "playlist", "radio", "audio", "master", "chunklist", "high", "low", "mid" };

        /// <summary>
        /// Titel, der nur eine Adresse ist, lesbar machen: „…/Mein_Lied.mp3“ → „Mein Lied“, sonst „Livestream“.
        /// host = Server der Adresse (ohne www), sonst null.
        /// </summary>
        public static string CleanTitle(string title, out string host)
        {
            host = null;
            var t = (title ?? "").Trim();
            var m = UrlLike.Match(t);
            if (!m.Success) return t;
            host = m.Groups["host"].Value.ToLowerInvariant();
            if (host.StartsWith("www.")) host = host.Substring(4);
            string path = t.Substring(m.Groups["host"].Index + m.Groups["host"].Length);
            int q = path.IndexOfAny(new[] { '?', '#' });
            if (q >= 0) path = path.Substring(0, q);
            string file = Uri.UnescapeDataString(path.TrimEnd('/').Split('/').LastOrDefault() ?? "");
            string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            string name = MediaExt.Contains(ext) ? System.IO.Path.GetFileNameWithoutExtension(file) : "";
            name = name.Replace('_', ' ').Replace('-', ' ').Trim();
            bool useful = name.Length >= 3 && name.Count(char.IsLetter) >= 3 && !Generic.Contains(name.ToLowerInvariant());
            return useful ? name : "Livestream";
        }

        /// <summary>Erkannter Musikdienst (App oder Browser-Tab), z. B. „YouTube Music“, „Deezer“; null = unbekannt.</summary>
        public string Provider { get; private set; }
        public bool SoundCloud => Provider == "SoundCloud";
        public static readonly Color SoundCloudOrange = Color.FromRgb(0xFF, 0x55, 0x00);
        /// <summary>Markenfarbe des erkannten Dienstes (null = unbekannt).</summary>
        public Color? ProviderColor => Providers.Where(p => p.Name == Provider).Select(p => (Color?)p.Color).FirstOrDefault();

        /// <summary>Domain der Webseite, die im Browser spielt (z. B. „twitch.tv“); null = keine Webseite erkannt.</summary>
        public string Site { get; private set; }

        public string AppName => Provider ?? SiteName ?? NameOf(AppId);

        /// <summary>Name der Webseite; bekannte Dienste in ihrer Schreibweise („YouTube“ statt „Youtube“).</summary>
        string SiteName
        {
            get
            {
                var n = SiteLogo.NameOf(Site);
                if (n == null) return null;
                var known = Providers.Select(p => p.Name).FirstOrDefault(p => p.Replace(" ", "").Equals(n, StringComparison.OrdinalIgnoreCase));
                return known ?? n;
            }
        }

        static readonly string[] Browsers = { "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "arc", "zen" };
        static bool IsBrowser(string appId) { var id = (appId ?? "").ToLowerInvariant(); return Browsers.Any(b => id.Contains(b)); }

        // Musikdienste: Kennung im App-Namen (Desktop-App) und Text im Fenstertitel (Browser-Tab).
        // Reihenfolge zählt: „YouTube Music“ vor „YouTube“.
        static readonly (string Name, string[] Ids, string[] Titles, Color Color)[] Providers =
        {
            ("YouTube Music", new[] { "youtubemusic", "youtube music", "ytmdesktop" }, new[] { "YouTube Music" }, Color.FromRgb(0xFF, 0x00, 0x33)),
            ("SoundCloud",    new[] { "soundcloud" },                     new[] { "SoundCloud" },    Color.FromRgb(0xFF, 0x55, 0x00)),
            ("Apple Music",   new[] { "applemusic", "itunes", "cider" },  new[] { "Apple Music" },   Color.FromRgb(0xFA, 0x2D, 0x48)),
            ("Deezer",        new[] { "deezer" },                         new[] { "Deezer" },        Color.FromRgb(0xA2, 0x38, 0xFF)),
            ("TIDAL",         new[] { "tidal" },                          new[] { "TIDAL" },         Color.FromRgb(0xF2, 0xF2, 0xF2)),
            ("Amazon Music",  new[] { "amazonmusic", "amazon music" },    new[] { "Amazon Music" },  Color.FromRgb(0x25, 0xD1, 0xDA)),
            ("Qobuz",         new[] { "qobuz" },                          new[] { "Qobuz" },         Color.FromRgb(0x2E, 0x8B, 0xFF)),
            ("Bandcamp",      new[] { "bandcamp" },                       new[] { "Bandcamp" },      Color.FromRgb(0x1D, 0xA0, 0xC3)),
            ("Mixcloud",      new[] { "mixcloud" },                       new[] { "Mixcloud" },      Color.FromRgb(0x5A, 0x6E, 0xF5)),
            ("Audiomack",     new[] { "audiomack" },                      new[] { "Audiomack" },     Color.FromRgb(0xFF, 0xA2, 0x00)),
            ("Pandora",       new[] { "pandora" },                        new[] { "Pandora" },       Color.FromRgb(0x36, 0x68, 0xFF)),
            ("Napster",       new[] { "napster" },                        new[] { "Napster" },       Color.FromRgb(0x2D, 0x9C, 0xF0)),
            ("TuneIn",        new[] { "tunein" },                         new[] { "TuneIn" },        Color.FromRgb(0x14, 0xD8, 0xCC)),
            ("Plexamp",       new[] { "plexamp" },                        new string[0],             Color.FromRgb(0xE5, 0xA0, 0x0D)),
            ("VLC",           new[] { "vlc" },                            new string[0],             Color.FromRgb(0xFF, 0x88, 0x00)),
            ("foobar2000",    new[] { "foobar2000" },                     new string[0],             Color.FromRgb(0xC8, 0xC8, 0xC8)),
            ("MusicBee",      new[] { "musicbee" },                       new string[0],             Color.FromRgb(0xF5, 0xC5, 0x18)),
            ("AIMP",          new[] { "aimp" },                           new string[0],             Color.FromRgb(0xFF, 0x8C, 0x1A)),
            ("Winamp",        new[] { "winamp" },                         new string[0],             Color.FromRgb(0xF0, 0x9A, 0x1A)),
            ("YouTube",       new string[0],                              new[] { "YouTube" },       Color.FromRgb(0xFF, 0x00, 0x00)),
        };

        /// <summary>Musikdienst erkennen: erst am App-Namen, im Browser am Fenstertitel des Tabs, in dem der Songtitel steht.</summary>
        static string DetectProvider(string appId, string title)
        {
            var id = (appId ?? "").ToLowerInvariant();
            if (id.Contains("spotify")) return null;   // Spotify hat eigenen Namen/Player
            foreach (var p in Providers)
                if (p.Ids.Any(x => id.Contains(x))) return p.Name;
            if (string.IsNullOrWhiteSpace(title) || !Browsers.Any(b => id.Contains(b))) return null;
            var part = title.Length > 40 ? title.Substring(0, 40) : title;
            try
            {
                foreach (var proc in System.Diagnostics.Process.GetProcesses())
                {
                    try
                    {
                        string pn = proc.ProcessName.ToLowerInvariant();
                        if (!Browsers.Any(b => pn.StartsWith(b))) continue;
                        string wt = proc.MainWindowTitle ?? "";
                        if (wt.IndexOf(part, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        foreach (var p in Providers)
                            if (p.Titles.Any(t => wt.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)) return p.Name;
                    }
                    catch { }
                    finally { proc.Dispose(); }
                }
            }
            catch { }
            return null;
        }

        public static string NameOf(string appId)
        {
            {
                var id = appId ?? "";
                if (id.IndexOf("Spotify", StringComparison.OrdinalIgnoreCase) >= 0) return "Spotify";
                if (id.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) >= 0) return "Google Chrome";
                if (id.IndexOf("MSEdge", StringComparison.OrdinalIgnoreCase) >= 0) return "Microsoft Edge";
                if (id.IndexOf("AppleMusic", StringComparison.OrdinalIgnoreCase) >= 0) return "Apple Music";
                if (id.IndexOf("firefox", StringComparison.OrdinalIgnoreCase) >= 0) return "Firefox";
                if (id.IndexOf("ZuneMusic", StringComparison.OrdinalIgnoreCase) >= 0) return "Medienwiedergabe";
                foreach (var p in Providers)   // bekannte Musik-Apps mit schönem Namen (Deezer, TIDAL, VLC …)
                    if (p.Ids.Any(x => id.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) return p.Name;
                if (id.IndexOf("opera", StringComparison.OrdinalIgnoreCase) >= 0) return "Opera";
                if (id.IndexOf("brave", StringComparison.OrdinalIgnoreCase) >= 0) return "Brave";
                var bang = id.LastIndexOf('!');
                if (bang >= 0) id = id.Substring(bang + 1);
                if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) id = id.Substring(0, id.Length - 4);
                return id;
            }
        }

        /// <summary>Öffnet die App, aus der die Musik kommt.</summary>
        public void OpenApp()
        {
            var id = AppId;
            if (string.IsNullOrEmpty(id)) return;
            try
            {
                string proc = null;
                if (id.IndexOf("Spotify", StringComparison.OrdinalIgnoreCase) >= 0) proc = "Spotify";
                else if (id.Equals("Chrome", StringComparison.OrdinalIgnoreCase)) proc = "chrome";
                else if (id.Equals("MSEdge", StringComparison.OrdinalIgnoreCase)) proc = "msedge";
                else if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) proc = Path.GetFileNameWithoutExtension(id);
                else if (!id.Contains("!")) proc = id;

                if (proc != null)
                    foreach (var p in Process.GetProcessesByName(proc))
                        if (p.MainWindowHandle != IntPtr.Zero) { Native.Focus(p.MainWindowHandle); return; }

                Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\" + id) { UseShellExecute = true });
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        static async Task<BitmapSource> LoadArtAsync(IRandomAccessStreamReference r)
        {
            if (r == null) return null;
            try
            {
                using var ras = await r.OpenReadAsync();
                using var s = ras.AsStreamForRead();
                var ms = new MemoryStream();
                await s.CopyToAsync(ms);
                ms.Position = 0;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 200;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        /// <summary>Kräftigste Farbe des Covers – färbt den Visualizer (wie bei iOS).</summary>
        public static Color AccentOf(BitmapSource src)
        {
            try
            {
                var small = new TransformedBitmap(src, new ScaleTransform(16.0 / src.PixelWidth, 16.0 / src.PixelHeight));
                var conv = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
                int w = conv.PixelWidth, h = conv.PixelHeight;
                var px = new byte[w * h * 4];
                conv.CopyPixels(px, w * 4, 0);
                double r = 0, g = 0, b = 0, sum = 0;
                for (int i = 0; i + 3 < px.Length; i += 4)
                {
                    double pb = px[i] / 255.0, pg = px[i + 1] / 255.0, pr = px[i + 2] / 255.0;
                    double max = Math.Max(pr, Math.Max(pg, pb)), min = Math.Min(pr, Math.Min(pg, pb));
                    double sat = max <= 0 ? 0 : (max - min) / max;
                    double weight = 0.03 + sat * sat * max;
                    r += pr * weight; g += pg * weight; b += pb * weight; sum += weight;
                }
                if (sum <= 0) return Colors.White;
                r /= sum; g /= sum; b /= sum;
                double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
                if (mx < 0.02 || (mx - mn) / mx < 0.15) return Colors.White;
                double k = 0.95 / mx;
                return Color.FromRgb((byte)(Math.Min(1, r * k) * 255), (byte)(Math.Min(1, g * k) * 255), (byte)(Math.Min(1, b * k) * 255));
            }
            catch { return Colors.White; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DynamicIsland
{
    public sealed class SpotifyTrack
    {
        public string Id, Uri, Title = "", Artist = "", Album = "", ArtUrl;
        public int DurationMs;
    }

    public sealed class SpotifyDevice
    {
        public string Id, Name = "", Type = "";
        public bool Active;
        public int? Volume;
    }

    public sealed class SpotifyPlaylist
    {
        public string Uri, Name = "", ImageUrl;
        public int Tracks;
    }

    /// <summary>
    /// Verbindung zu deinem Spotify-Konto über die offizielle Web API (Anmeldung mit PKCE,
    /// kein Secret nötig). Liefert, was gerade läuft – auch auf Handy oder Lautsprecher –
    /// und steuert Wiedergabe, Geräte, Playlists und „Gefällt mir“.
    /// Steuern verlangt bei Spotify ein Premium-Konto; Anzeigen und Liken gehen auch ohne.
    /// </summary>
    public sealed class SpotifyService
    {
        public const string RedirectUri = "http://127.0.0.1:8898/callback";
        const int Port = 8898;
        const string Scopes = "user-read-playback-state user-modify-playback-state user-read-currently-playing " +
                              "user-library-read user-library-modify playlist-read-private playlist-read-collaborative";
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        readonly Settings settings;
        bool refreshing, loggingIn;
        string likedChecked;

        public string Status { get; private set; } = "Nicht verbunden";
        public string UserName { get; private set; }
        public bool Premium { get; private set; } = true;
        public bool Connected => !string.IsNullOrEmpty(settings.SpotifyRefreshToken);

        // aktueller Zustand
        public SpotifyTrack Track { get; private set; }
        public bool Playing { get; private set; }
        public bool Shuffle { get; private set; }
        public string Repeat { get; private set; } = "off"; // off | context | track
        public SpotifyDevice Device { get; private set; }
        public bool Liked { get; private set; }
        public DateTime LastPlaying { get; private set; } = DateTime.MinValue;
        int progressMs;
        DateTime progressAt = DateTime.Now;

        public event Action StatusChanged, StateChanged;
        /// <summary>Fehlermeldung für einen kurzen Hinweis in der Island.</summary>
        public event Action<string> Problem;

        public SpotifyService(Settings s) { settings = s; }

        public bool HasPlayback => Track != null;
        public TimeSpan Duration => TimeSpan.FromMilliseconds(Track?.DurationMs ?? 0);

        public TimeSpan Position
        {
            get
            {
                var p = TimeSpan.FromMilliseconds(progressMs);
                if (Playing) p += DateTime.Now - progressAt;
                if (Track != null && p > Duration) p = Duration;
                return p < TimeSpan.Zero ? TimeSpan.Zero : p;
            }
        }

        void SetStatus(string s)
        {
            Status = s;
            StatusChanged?.Invoke();
        }

        // =================================================================
        //  Anmeldung (Browser → http://127.0.0.1:8898/callback)
        // =================================================================
        public async Task<bool> LoginAsync()
        {
            if (loggingIn) return false;
            var clientId = (settings.SpotifyClientId ?? "").Trim();
            if (clientId.Length == 0)
            {
                SetStatus("Bitte zuerst die Client-ID eintragen");
                return false;
            }
            loggingIn = true;
            TcpListener listener = null;
            try
            {
                var verifier = RandomString(64);
                string challenge;
                using (var sha = SHA256.Create())
                    challenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
                var state = RandomString(16);

                listener = new TcpListener(IPAddress.Loopback, Port);
                listener.Start();
                var url = "https://accounts.spotify.com/authorize?response_type=code" +
                          "&client_id=" + Uri.EscapeDataString(clientId) +
                          "&scope=" + Uri.EscapeDataString(Scopes) +
                          "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
                          "&code_challenge_method=S256&code_challenge=" + challenge +
                          "&state=" + state;
                IslandWindow.Open(url);
                SetStatus("Bitte im Browser bei Spotify anmelden und zustimmen …");

                string code = null, error = null;
                var deadline = DateTime.Now.AddMinutes(3);
                while (code == null && error == null && DateTime.Now < deadline)
                {
                    var wait = deadline - DateTime.Now;
                    if (wait <= TimeSpan.Zero) break;
                    var accept = listener.AcceptTcpClientAsync();
                    if (await Task.WhenAny(accept, Task.Delay(wait)) != accept) break;
                    using var client = await accept;
                    using var stream = client.GetStream();
                    var buf = new byte[16384];
                    var read = stream.ReadAsync(buf, 0, buf.Length);
                    if (await Task.WhenAny(read, Task.Delay(4000)) != read) continue; // leere Vorab-Verbindung des Browsers
                    int n = await read;
                    var request = Encoding.ASCII.GetString(buf, 0, n);
                    var line = request.Split('\n')[0];
                    var parts = line.Split(' ');
                    bool isCallback = parts.Length > 1 && parts[1].StartsWith("/callback", StringComparison.Ordinal);
                    if (isCallback)
                    {
                        var path = parts[1];
                        var q = ParseQuery(path.Contains('?') ? path.Substring(path.IndexOf('?') + 1) : "");
                        q.TryGetValue("error", out error);
                        if (q.TryGetValue("state", out var st) && st == state) q.TryGetValue("code", out code);
                        else if (error == null) error = "state";
                    }
                    var ok = code != null;
                    var html = "<!DOCTYPE html><html><head><meta charset='utf-8'><title>Dynamic Island</title></head>" +
                               "<body style='font-family:Segoe UI,sans-serif;background:#1c1c1e;color:#fff;text-align:center;padding-top:90px'>" +
                               (isCallback
                                   ? (ok ? "<h2>Spotify ist verbunden ✓</h2><p>Du kannst dieses Fenster schließen.</p>"
                                         : "<h2>Anmeldung abgebrochen</h2><p>Versuch es in den Einstellungen noch einmal.</p>")
                                   : "<p>…</p>") +
                               "</body></html>";
                    var body = Encoding.UTF8.GetBytes(html);
                    var head = Encoding.ASCII.GetBytes((isCallback ? "HTTP/1.1 200 OK" : "HTTP/1.1 404 Not Found") +
                                                       "\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.Length +
                                                       "\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(head, 0, head.Length);
                    await stream.WriteAsync(body, 0, body.Length);
                }

                if (code == null)
                {
                    SetStatus(error != null ? "Anmeldung abgebrochen (" + error + ")" : "Keine Anmeldung erhalten – noch einmal versuchen");
                    return false;
                }

                var ok2 = await TokenAsync(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = RedirectUri,
                    ["client_id"] = clientId,
                    ["code_verifier"] = verifier
                });
                if (!ok2) return false;
                await LoadUserAsync();
                await RefreshAsync();
                return true;
            }
            catch (SocketException)
            {
                SetStatus("Port " + Port + " ist belegt – Programm schließen, das ihn nutzt, und erneut versuchen");
                return false;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                SetStatus("Anmeldung fehlgeschlagen: " + ex.Message);
                return false;
            }
            finally
            {
                try { listener?.Stop(); } catch { }
                loggingIn = false;
            }
        }

        public void Logout()
        {
            settings.SpotifyAccessToken = "";
            settings.SpotifyRefreshToken = "";
            settings.Save();
            Track = null;
            Playing = false;
            UserName = null;
            SetStatus("Nicht verbunden");
            StateChanged?.Invoke();
        }

        async Task<bool> TokenAsync(Dictionary<string, string> form)
        {
            try
            {
                using var resp = await Http.PostAsync("https://accounts.spotify.com/api/token", new FormUrlEncodedContent(form));
                var text = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    SetStatus("Spotify lehnt die Anmeldung ab: " + Short(text) + " – Client-ID und Redirect-URI prüfen");
                    return false;
                }
                using var doc = JsonDocument.Parse(text);
                var r = doc.RootElement;
                settings.SpotifyAccessToken = Str(r, "access_token") ?? "";
                var rt = Str(r, "refresh_token");
                if (rt != null) settings.SpotifyRefreshToken = rt;
                int exp = r.TryGetProperty("expires_in", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : 3600;
                settings.SpotifyTokenExpires = DateTime.UtcNow.AddSeconds(exp);
                settings.Save();
                return settings.SpotifyAccessToken.Length > 0;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                SetStatus("Keine Verbindung zu Spotify");
                return false;
            }
        }

        async Task<bool> EnsureTokenAsync(bool force = false)
        {
            if (!Connected) return false;
            if (!force && !string.IsNullOrEmpty(settings.SpotifyAccessToken) && settings.SpotifyTokenExpires > DateTime.UtcNow.AddMinutes(1)) return true;
            return await TokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = settings.SpotifyRefreshToken,
                ["client_id"] = (settings.SpotifyClientId ?? "").Trim()
            });
        }

        async Task LoadUserAsync()
        {
            var (st, me) = await ApiAsync(HttpMethod.Get, "/me");
            if (st != 200) { SetStatus("Verbunden"); return; }
            UserName = Str(me, "display_name") ?? Str(me, "id");
            var product = Str(me, "product");
            Premium = product == null || product == "premium";
            SetStatus("Verbunden als " + (UserName ?? "Spotify-Nutzer") + (Premium ? " (Premium)" : " – ohne Premium kann Spotify nur angezeigt, nicht gesteuert werden"));
        }

        // =================================================================
        //  Web API
        // =================================================================
        async Task<(int, JsonElement)> ApiAsync(HttpMethod method, string path, object body = null, bool retry = true)
        {
            if (!await EnsureTokenAsync()) return (401, default);
            try
            {
                using var req = new HttpRequestMessage(method, "https://api.spotify.com/v1" + path);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.SpotifyAccessToken);
                if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                else if (method != HttpMethod.Get) req.Content = new StringContent("", Encoding.UTF8, "application/json");
                using var resp = await Http.SendAsync(req);
                int st = (int)resp.StatusCode;
                if (st == 401 && retry && await EnsureTokenAsync(true)) return await ApiAsync(method, path, body, false);
                var text = await resp.Content.ReadAsStringAsync();
                JsonElement json = default;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    try { using var doc = JsonDocument.Parse(text); json = doc.RootElement.Clone(); }
                    catch { }
                }
                return (st, json);
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                return (0, default);
            }
        }

        /// <summary>Steuerbefehl; meldet typische Probleme (kein Premium, kein aktives Gerät).</summary>
        async Task<bool> CommandAsync(HttpMethod method, string path, object body = null)
        {
            var (st, json) = await ApiAsync(method, path, body);
            if (st >= 200 && st < 300) return true;
            var reason = Str(Obj(json, "error"), "reason") ?? "";
            if (st == 403 || reason == "PREMIUM_REQUIRED") Problem?.Invoke("Dafür braucht Spotify ein Premium-Konto");
            else if (st == 404 || reason == "NO_ACTIVE_DEVICE") Problem?.Invoke("Kein aktives Spotify-Gerät – öffne Spotify oder wähle ein Gerät");
            else if (st != 0) Problem?.Invoke("Spotify: " + (Str(Obj(json, "error"), "message") ?? "Fehler " + st));
            return false;
        }

        /// <summary>Holt, was gerade läuft (alle paar Sekunden).</summary>
        public async Task RefreshAsync()
        {
            if (!Connected || refreshing) return;
            refreshing = true;
            try
            {
                if (UserName == null && Status == "Nicht verbunden") await LoadUserAsync();
                var (st, p) = await ApiAsync(HttpMethod.Get, "/me/player?additional_types=episode");
                if (st == 204 || (st == 200 && p.ValueKind != JsonValueKind.Object))
                {
                    if (Playing || Track != null && (DateTime.Now - LastPlaying).TotalMinutes > 30) { Playing = false; }
                    Playing = false;
                    StateChanged?.Invoke();
                    return;
                }
                if (st != 200) return;

                Playing = Bool(p, "is_playing");
                Shuffle = Bool(p, "shuffle_state");
                Repeat = Str(p, "repeat_state") ?? "off";
                progressMs = Int(p, "progress_ms");
                progressAt = DateTime.Now;
                if (Playing) LastPlaying = DateTime.Now;

                var dev = Obj(p, "device");
                Device = dev.ValueKind == JsonValueKind.Object
                    ? new SpotifyDevice { Id = Str(dev, "id"), Name = Str(dev, "name") ?? "", Type = Str(dev, "type") ?? "", Active = Bool(dev, "is_active"), Volume = IntOrNull(dev, "volume_percent") }
                    : null;

                var item = Obj(p, "item");
                if (item.ValueKind == JsonValueKind.Object)
                {
                    var t = new SpotifyTrack
                    {
                        Id = Str(item, "id"),
                        Uri = Str(item, "uri"),
                        Title = Str(item, "name") ?? "",
                        DurationMs = Int(item, "duration_ms")
                    };
                    if (item.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array)
                        t.Artist = string.Join(", ", artists.EnumerateArray().Select(a => Str(a, "name")).Where(n => n != null));
                    var album = Obj(item, "album");
                    if (album.ValueKind != JsonValueKind.Object) album = Obj(item, "show"); // Podcast
                    if (t.Artist.Length == 0) t.Artist = Str(album, "name") ?? "";
                    t.Album = Str(album, "name") ?? "";
                    t.ArtUrl = BestImage(album) ?? BestImage(item);
                    Track = t;
                    if (t.Id != null && t.Id != likedChecked && (t.Uri ?? "").StartsWith("spotify:track:"))
                    {
                        likedChecked = t.Id;
                        Liked = await IsSavedAsync(t);
                    }
                }
                StateChanged?.Invoke();
            }
            finally { refreshing = false; }
        }

        static string BestImage(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("images", out var imgs) || imgs.ValueKind != JsonValueKind.Array) return null;
            string best = null;
            int bestW = 0;
            foreach (var i in imgs.EnumerateArray())
            {
                int w = Int(i, "width");
                if (best == null || (w >= 200 && (bestW < 200 || w < bestW)) || (bestW < 200 && w > bestW)) { best = Str(i, "url"); bestW = w; }
            }
            return best;
        }

        // ---- Steuerung ----
        public async void PlayPause()
        {
            bool wasPlaying = Playing;
            progressMs = (int)Position.TotalMilliseconds;
            progressAt = DateTime.Now;
            Playing = !wasPlaying;
            StateChanged?.Invoke();
            if (!await CommandAsync(HttpMethod.Put, wasPlaying ? "/me/player/pause" : "/me/player/play")) Playing = wasPlaying;
            await Task.Delay(400);
            await RefreshAsync();
        }

        public async void Next() { if (await CommandAsync(HttpMethod.Post, "/me/player/next")) { await Task.Delay(500); await RefreshAsync(); } }
        public async void Previous() { if (await CommandAsync(HttpMethod.Post, "/me/player/previous")) { await Task.Delay(500); await RefreshAsync(); } }

        public async void Seek(double fraction)
        {
            if (Track == null) return;
            int ms = (int)(Math.Max(0, Math.Min(1, fraction)) * Track.DurationMs);
            progressMs = ms;
            progressAt = DateTime.Now;
            await CommandAsync(HttpMethod.Put, "/me/player/seek?position_ms=" + ms);
        }

        public async void ToggleShuffle()
        {
            Shuffle = !Shuffle;
            StateChanged?.Invoke();
            await CommandAsync(HttpMethod.Put, "/me/player/shuffle?state=" + (Shuffle ? "true" : "false"));
        }

        public async void CycleRepeat()
        {
            Repeat = Repeat == "off" ? "context" : Repeat == "context" ? "track" : "off";
            StateChanged?.Invoke();
            await CommandAsync(HttpMethod.Put, "/me/player/repeat?state=" + Repeat);
        }

        public async void SetVolume(double fraction)
        {
            int v = (int)Math.Round(Math.Max(0, Math.Min(1, fraction)) * 100);
            if (Device != null) Device.Volume = v;
            await CommandAsync(HttpMethod.Put, "/me/player/volume?volume_percent=" + v);
        }

        // Seit Februar 2026 speichert Spotify über /me/library (mit URIs). Die alten /me/tracks-Wege
        // gibt es für neue Apps nicht mehr – deshalb ging „Gefällt mir“ nicht. Alt bleibt als Rückfall.
        static string TrackUri(SpotifyTrack t) => !string.IsNullOrEmpty(t.Uri) ? t.Uri : "spotify:track:" + t.Id;

        async Task<bool> IsSavedAsync(SpotifyTrack t)
        {
            var (st, j) = await ApiAsync(HttpMethod.Get, "/me/library/contains?uris=" + Uri.EscapeDataString(TrackUri(t)));
            if (st != 200) (st, j) = await ApiAsync(HttpMethod.Get, "/me/tracks/contains?ids=" + t.Id);
            return st == 200 && j.ValueKind == JsonValueKind.Array && j.GetArrayLength() > 0 && j[0].ValueKind == JsonValueKind.True;
        }

        public async void ToggleLike()
        {
            var t = Track;
            if (t?.Id == null) return;
            bool was = Liked;
            Liked = !was;
            StateChanged?.Invoke();
            var method = was ? HttpMethod.Delete : HttpMethod.Put;
            var (st, json) = await ApiAsync(method, "/me/library?uris=" + Uri.EscapeDataString(TrackUri(t)));
            if ((st < 200 || st >= 300) && st != 401 && st != 403)
                (st, json) = await ApiAsync(method, "/me/tracks?ids=" + t.Id);
            if (st >= 200 && st < 300) return;

            Liked = was;
            StateChanged?.Invoke();
            if (st == 401 || st == 403)
                Problem?.Invoke("Spotify erlaubt das Speichern noch nicht – Einstellungen → Spotify → „Neu verbinden“");
            else
                Problem?.Invoke("„Gefällt mir“: " + (Str(Obj(json, "error"), "message") ?? "Fehler " + st));
        }

        public async Task<List<SpotifyDevice>> DevicesAsync()
        {
            var list = new List<SpotifyDevice>();
            var (st, j) = await ApiAsync(HttpMethod.Get, "/me/player/devices");
            if (st == 200 && j.ValueKind == JsonValueKind.Object && j.TryGetProperty("devices", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var d in arr.EnumerateArray())
                    list.Add(new SpotifyDevice { Id = Str(d, "id"), Name = Str(d, "name") ?? "", Type = Str(d, "type") ?? "", Active = Bool(d, "is_active"), Volume = IntOrNull(d, "volume_percent") });
            return list;
        }

        public async Task TransferAsync(string deviceId)
        {
            if (await CommandAsync(HttpMethod.Put, "/me/player", new { device_ids = new[] { deviceId }, play = true }))
            {
                await Task.Delay(800);
                await RefreshAsync();
            }
        }

        public async Task<List<SpotifyPlaylist>> PlaylistsAsync()
        {
            var list = new List<SpotifyPlaylist>();
            var (st, j) = await ApiAsync(HttpMethod.Get, "/me/playlists?limit=30");
            if (st == 200 && j.ValueKind == JsonValueKind.Object && j.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var p in arr.EnumerateArray())
                {
                    if (p.ValueKind != JsonValueKind.Object) continue;
                    list.Add(new SpotifyPlaylist
                    {
                        Uri = Str(p, "uri"),
                        Name = Str(p, "name") ?? "",
                        ImageUrl = BestImage(p),
                        Tracks = Obj(p, "items").ValueKind == JsonValueKind.Object ? Int(Obj(p, "items"), "total") : Int(Obj(p, "tracks"), "total")
                    });
                }
            return list;
        }

        public async Task PlayContextAsync(string uri)
        {
            if (await CommandAsync(HttpMethod.Put, "/me/player/play", new { context_uri = uri }))
            {
                await Task.Delay(800);
                await RefreshAsync();
            }
        }

        // ---- Helfer ----
        static string RandomString(int len)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
            var bytes = RandomNumberGenerator.GetBytes(len);
            var sb = new StringBuilder(len);
            foreach (var b in bytes) sb.Append(chars[b % chars.Length]);
            return sb.ToString();
        }

        static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        static Dictionary<string, string> ParseQuery(string q)
        {
            var d = new Dictionary<string, string>();
            foreach (var part in q.Split('&'))
            {
                if (part.Length == 0) continue;
                int i = part.IndexOf('=');
                var k = Uri.UnescapeDataString(i < 0 ? part : part.Substring(0, i));
                var v = i < 0 ? "" : Uri.UnescapeDataString(part.Substring(i + 1).Replace('+', ' '));
                d[k] = v;
            }
            return d;
        }

        static string Short(string t) => t == null ? "" : (t.Length > 120 ? t.Substring(0, 120) + "…" : t);

        static JsonElement Obj(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : default;

        static string Str(JsonElement e, string name)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String) return null;
            var s = v.GetString();
            return string.IsNullOrEmpty(s) ? null : s;
        }

        static bool Bool(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

        static int Int(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : 0;

        static int? IntOrNull(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : (int?)null;
    }
}

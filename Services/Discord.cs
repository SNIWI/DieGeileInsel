using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicIsland
{
    public sealed class VoiceMember
    {
        public string Id = "", Name = "", AvatarHash;
        public bool Muted, Deafened, Speaking;
        public string AvatarUrl => DiscordService.AvatarUrl(Id, AvatarHash);
    }

    public sealed class VoiceChannel
    {
        public string Id, Name;
        public bool IsCall;
        public DateTime JoinedAt = DateTime.Now;
        public readonly List<VoiceMember> Members = new List<VoiceMember>();
    }

    /// <summary>
    /// Verbindung zur Discord-App auf diesem PC über deren lokale RPC-Schnittstelle (Named Pipe
    /// "discord-ipc-0"). Liefert neue Nachrichten, den aktuellen Sprachkanal bzw. Anruf und wer
    /// gerade spricht – wie das Discord-Overlay. Läuft komplett asynchron auf dem UI-Thread.
    /// Braucht eine eigene Discord-Anwendung (Client-ID + Secret, siehe Einstellungsfenster).
    /// </summary>
    public sealed class DiscordService
    {
        public const string RedirectUri = "http://localhost";
        static readonly string[] Scopes = { "rpc", "identify", "rpc.notifications.read", "rpc.voice.read", "rpc.voice.write" };
        static readonly string[] ChannelEvents = { "VOICE_STATE_CREATE", "VOICE_STATE_UPDATE", "VOICE_STATE_DELETE", "SPEAKING_START", "SPEAKING_STOP" };
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        readonly Settings settings;
        readonly Dictionary<string, TaskCompletionSource<Reply>> pending = new Dictionary<string, TaskCompletionSource<Reply>>();
        readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
        NamedPipeClientStream pipe;
        Task readerTask;
        TaskCompletionSource<bool> ready;
        bool running;
        int generation;

        public string Status { get; private set; } = "Nicht verbunden";
        public bool Connected { get; private set; }
        public string SelfId { get; private set; }
        public string SelfName { get; private set; }
        public bool SelfMute { get; private set; }
        public bool SelfDeaf { get; private set; }
        public VoiceChannel Channel { get; private set; }

        public event Action StatusChanged, ChannelChanged, MembersChanged, SpeakingChanged, SelfVoiceChanged;
        /// <summary>Neue Nachricht: Titel, Text, Bild-URL, Kanal-ID.</summary>
        public event Action<string, string, string, string> Notification;

        public DiscordService(Settings s) { settings = s; }

        struct Reply
        {
            public string Evt;
            public JsonElement Data;
            public bool Ok => Evt != "ERROR";
            public string Message => Str(Data, "message") ?? "Unbekannter Fehler";
        }

        public static string AvatarUrl(string id, string hash)
        {
            if (!string.IsNullOrEmpty(hash)) return $"https://cdn.discordapp.com/avatars/{id}/{hash}.png?size=64";
            ulong.TryParse(id, out ulong n);
            return $"https://cdn.discordapp.com/embed/avatars/{(n >> 22) % 6}.png";
        }

        void SetStatus(string s)
        {
            Status = s;
            StatusChanged?.Invoke();
        }

        // =================================================================
        //  Verbindung
        // =================================================================
        /// <summary>Verbindet sich mit Discord und versucht es alle 10 s erneut, solange Discord nicht läuft.</summary>
        public async void Start()
        {
            if (running) return;
            if (string.IsNullOrWhiteSpace(settings.DiscordClientId) || string.IsNullOrWhiteSpace(settings.DiscordClientSecret))
            {
                SetStatus("Client-ID und Secret fehlen");
                return;
            }
            running = true;
            int gen = ++generation;
            while (running && gen == generation)
            {
                bool retry = true;
                try { retry = await RunOnceAsync(); }
                catch (Exception ex)
                {
                    Program.Log(ex);
                    if (running && gen == generation) SetStatus("Verbindung zu Discord getrennt – neuer Versuch …");
                }
                if (gen != generation) break;
                Cleanup();
                if (!retry) { running = false; break; }
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        public void Stop()
        {
            running = false;
            generation++;
            Cleanup();
            SetStatus("Nicht verbunden");
        }

        public void Restart()
        {
            Stop();
            Start();
        }

        void Cleanup()
        {
            Connected = false;
            try { pipe?.Dispose(); } catch { }
            pipe = null;
            readerTask = null;
            foreach (var t in pending.Values.ToList()) t.TrySetCanceled();
            pending.Clear();
            ready?.TrySetCanceled();
            if (Channel != null)
            {
                Channel = null;
                ChannelChanged?.Invoke();
            }
        }

        /// <returns>true = später erneut versuchen, false = aufgeben (z. B. falsche Client-ID).</returns>
        async Task<bool> RunOnceAsync()
        {
            NamedPipeClientStream p = null;
            for (int i = 0; i < 10 && p == null; i++)
            {
                var c = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    await c.ConnectAsync(300);
                    p = c;
                }
                catch { c.Dispose(); }
            }
            if (p == null)
            {
                SetStatus("Discord läuft nicht – warte, bis Discord geöffnet ist …");
                return true;
            }

            pipe = p;
            ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reader = ReadLoopAsync(p);
            readerTask = reader;

            await WriteFrameAsync(0, new JsonObject { ["v"] = 1, ["client_id"] = settings.DiscordClientId.Trim() }.ToJsonString());
            var first = await Task.WhenAny(ready.Task, reader, Task.Delay(8000));
            if (first == reader)
            {
                var msg = reader.Exception?.GetBaseException().Message ?? "Verbindung abgelehnt";
                SetStatus("Discord hat abgelehnt: " + msg + " (Client-ID prüfen)");
                return false;
            }
            if (first != ready.Task)
            {
                SetStatus("Discord antwortet nicht");
                return true;
            }

            if (!await AuthAsync()) return false;
            Connected = true;
            SetStatus("Verbunden als " + (SelfName ?? "Discord-Nutzer"));

            await TrySubscribe("NOTIFICATION_CREATE", null);
            await TrySubscribe("VOICE_CHANNEL_SELECT", null);
            await TrySubscribe("VOICE_SETTINGS_UPDATE", null);
            try
            {
                var vs = await SendAsync("GET_VOICE_SETTINGS", null);
                if (vs.Ok)
                {
                    SelfMute = Bool(vs.Data, "mute");
                    SelfDeaf = Bool(vs.Data, "deaf");
                    SelfVoiceChanged?.Invoke();
                }
                var ch = await SendAsync("GET_SELECTED_VOICE_CHANNEL", null);
                if (ch.Ok) await SetChannelAsync(ch.Data);
            }
            catch (Exception ex) { Program.Log(ex); }

            await reader; // läuft, bis die Verbindung endet (wirft dann eine Ausnahme)
            return true;
        }

        // =================================================================
        //  Anmeldung (OAuth2 über die Discord-App)
        // =================================================================
        async Task<bool> AuthAsync()
        {
            var s = settings;
            if (s.DiscordTokenExpires > DateTime.UtcNow.AddMinutes(2) && await AuthenticateAsync(s.DiscordAccessToken)) return true;
            if (!string.IsNullOrEmpty(s.DiscordRefreshToken))
            {
                if (await ExchangeAsync("refresh_token", s.DiscordRefreshToken) && await AuthenticateAsync(s.DiscordAccessToken)) return true;
                s.DiscordRefreshToken = "";
                s.DiscordAccessToken = "";
                s.Save();
            }

            // Discord erlaubt nicht jeder Anwendung alle Berechtigungen → notfalls mit weniger versuchen
            var scopeSets = new[]
            {
                Scopes,
                new[] { "rpc", "identify", "rpc.voice.read", "rpc.voice.write" },
                new[] { "rpc", "identify", "rpc.voice.read" },
                new[] { "rpc", "identify" }
            };
            Reply r = default;
            foreach (var set in scopeSets)
            {
                SetStatus("Bitte jetzt in Discord auf „Autorisieren“ klicken …");
                var scopes = new JsonArray();
                foreach (var sc in set) scopes.Add(sc);
                try
                {
                    // Keine redirect_uri mitschicken – Discord nimmt die in der App eingetragene Adresse
                    r = await SendAsync("AUTHORIZE", new JsonObject
                    {
                        ["client_id"] = s.DiscordClientId.Trim(),
                        ["scopes"] = scopes
                    }, null, 180000);
                }
                catch (TimeoutException)
                {
                    SetStatus("Keine Bestätigung in Discord – in den Einstellungen erneut verbinden");
                    return false;
                }
                if (r.Ok) break;
                Program.Log(new Exception("Discord AUTHORIZE (" + string.Join(" ", set) + "): " + r.Message));
                if (r.Message.IndexOf("scope", StringComparison.OrdinalIgnoreCase) < 0) break; // anderer Fehler → nicht weiter probieren
            }
            if (!r.Ok)
            {
                if (r.Message.IndexOf("redirect_uri", StringComparison.OrdinalIgnoreCase) >= 0)
                    SetStatus("Discord: In deiner App fehlt die Redirect-Adresse. Developer Portal → deine App → OAuth2 → Redirects → " +
                              "„Add Redirect“ → " + RedirectUri + " eintragen → unten „Save Changes“ klicken. Dann erneut verbinden.");
                else SetStatus("Discord: " + r.Message);
                return false;
            }
            var code = Str(r.Data, "code");
            if (code == null || !await ExchangeAsync("authorization_code", code)) return false;
            if (await AuthenticateAsync(s.DiscordAccessToken)) return true;
            SetStatus("Anmeldung bei Discord fehlgeschlagen");
            return false;
        }

        async Task<bool> AuthenticateAsync(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            var r = await SendAsync("AUTHENTICATE", new JsonObject { ["access_token"] = token });
            if (!r.Ok) return false;
            var user = Obj(r.Data, "user");
            SelfId = Str(user, "id");
            SelfName = Str(user, "global_name") ?? Str(user, "username");
            return true;
        }

        async Task<bool> ExchangeAsync(string grant, string value)
        {
            if (grant != "authorization_code") return await ExchangeOnceAsync(grant, value, true);
            // Je nach Discord-Version wird redirect_uri verlangt oder abgelehnt → beides probieren
            if (await ExchangeOnceAsync(grant, value, true)) return true;
            return await ExchangeOnceAsync(grant, value, false);
        }

        async Task<bool> ExchangeOnceAsync(string grant, string value, bool withRedirect)
        {
            var s = settings;
            var form = new Dictionary<string, string>
            {
                ["client_id"] = s.DiscordClientId.Trim(),
                ["client_secret"] = s.DiscordClientSecret.Trim(),
                ["grant_type"] = grant
            };
            if (grant == "authorization_code")
            {
                form["code"] = value;
                if (withRedirect) form["redirect_uri"] = RedirectUri;
            }
            else form["refresh_token"] = value;

            try
            {
                using var resp = await Http.PostAsync("https://discord.com/api/oauth2/token", new FormUrlEncodedContent(form));
                var text = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    Program.Log(new Exception("Discord token (" + grant + ", redirect=" + withRedirect + "): " + text));
                    if (grant == "authorization_code" && !withRedirect)
                        SetStatus("Anmeldung fehlgeschlagen: " + Shorten(text) + " – stimmt das Secret, und ist " + RedirectUri + " unter OAuth2 → Redirects eingetragen?");
                    return false;
                }
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                s.DiscordAccessToken = Str(root, "access_token") ?? "";
                var rt = Str(root, "refresh_token");
                if (rt != null) s.DiscordRefreshToken = rt;
                int exp = root.TryGetProperty("expires_in", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : 3600;
                s.DiscordTokenExpires = DateTime.UtcNow.AddSeconds(exp);
                s.Save();
                return s.DiscordAccessToken.Length > 0;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                SetStatus("Keine Verbindung zu discord.com");
                return false;
            }
        }

        static string Shorten(string t) => t == null ? "" : (t.Length > 120 ? t.Substring(0, 120) + "…" : t);

        // =================================================================
        //  Steuerung aus der Island
        // =================================================================
        /// <summary>Mikro aus – auch bei „Ton aus“ (Deafen schaltet in Discord das Mikro mit stumm).</summary>
        public bool MicOff => SelfMute || SelfDeaf;

        public void ToggleMute()
        {
            if (!Connected) return;
            if (SelfDeaf)
            {
                // wie in Discord: Mikro einschalten hebt auch „Ton aus“ auf
                SelfDeaf = false;
                SelfMute = false;
                SelfVoiceChanged?.Invoke();
                Fire("SET_VOICE_SETTINGS", new JsonObject { ["deaf"] = false, ["mute"] = false });
                return;
            }
            SelfMute = !SelfMute;
            SelfVoiceChanged?.Invoke();
            Fire("SET_VOICE_SETTINGS", new JsonObject { ["mute"] = SelfMute });
        }

        public void ToggleDeaf()
        {
            if (!Connected) return;
            SelfDeaf = !SelfDeaf;
            SelfVoiceChanged?.Invoke();
            Fire("SET_VOICE_SETTINGS", new JsonObject { ["deaf"] = SelfDeaf });
        }

        public void Leave() => Fire("SELECT_VOICE_CHANNEL", new JsonObject { ["channel_id"] = null });

        public void Join(string channelId) => Fire("SELECT_VOICE_CHANNEL", new JsonObject { ["channel_id"] = channelId });

        async void Fire(string cmd, JsonObject args)
        {
            if (!Connected) return;
            try
            {
                var r = await SendAsync(cmd, args);
                if (!r.Ok) Program.Log(new Exception($"Discord {cmd}: {r.Message}"));
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        // =================================================================
        //  Ereignisse
        // =================================================================
        async void OnEvent(string evt, JsonElement data)
        {
            switch (evt)
            {
                case "READY":
                    ready?.TrySetResult(true);
                    break;

                case "NOTIFICATION_CREATE":
                    Notification?.Invoke(Str(data, "title"), Str(data, "body"), Str(data, "icon_url"), Str(data, "channel_id"));
                    break;

                case "VOICE_SETTINGS_UPDATE":
                    SelfMute = Bool(data, "mute");
                    SelfDeaf = Bool(data, "deaf");
                    SelfVoiceChanged?.Invoke();
                    break;

                case "VOICE_CHANNEL_SELECT":
                    try
                    {
                        var selId = Str(data, "channel_id");
                        if (selId == null) await SetChannelAsync(default);
                        else
                        {
                            var r = await SendAsync("GET_CHANNEL", new JsonObject { ["channel_id"] = selId });
                            if (r.Ok) await SetChannelAsync(r.Data);
                        }
                    }
                    catch (Exception ex) { Program.Log(ex); }
                    break;

                case "VOICE_STATE_CREATE":
                case "VOICE_STATE_UPDATE":
                    if (Channel != null)
                    {
                        var m = ParseMember(data);
                        var old = Channel.Members.Find(x => x.Id == m.Id);
                        if (old != null)
                        {
                            m.Speaking = old.Speaking;
                            Channel.Members[Channel.Members.IndexOf(old)] = m;
                        }
                        else Channel.Members.Add(m);
                        MembersChanged?.Invoke();
                    }
                    break;

                case "VOICE_STATE_DELETE":
                    if (Channel != null)
                    {
                        var goneId = Str(Obj(data, "user"), "id");
                        Channel.Members.RemoveAll(x => x.Id == goneId);
                        MembersChanged?.Invoke();
                    }
                    break;

                case "SPEAKING_START":
                case "SPEAKING_STOP":
                    if (Channel != null)
                    {
                        var uid = Str(data, "user_id");
                        var sm = Channel.Members.Find(x => x.Id == uid);
                        if (sm != null)
                        {
                            sm.Speaking = evt == "SPEAKING_START";
                            SpeakingChanged?.Invoke();
                        }
                    }
                    break;
            }
        }

        async Task SetChannelAsync(JsonElement ch)
        {
            var old = Channel;
            if (old != null)
                foreach (var e in ChannelEvents) await TrySubscribe(e, old.Id, "UNSUBSCRIBE");

            var id = Str(ch, "id");
            if (id == null)
            {
                Channel = null;
                ChannelChanged?.Invoke();
                return;
            }

            int type = ch.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : 2;
            var c = new VoiceChannel { Id = id, IsCall = type == 1 || type == 3 };
            if (old != null && old.Id == id) c.JoinedAt = old.JoinedAt;
            if (ch.TryGetProperty("voice_states", out var states) && states.ValueKind == JsonValueKind.Array)
                foreach (var st in states.EnumerateArray()) c.Members.Add(ParseMember(st));

            var name = Str(ch, "name");
            if (name == null && c.IsCall)
            {
                var others = c.Members.Where(m => m.Id != SelfId).Select(m => m.Name).Take(2).ToList();
                name = others.Count > 0 ? "Anruf mit " + string.Join(", ", others) : "Anruf";
            }
            c.Name = name ?? "Sprachkanal";
            Channel = c;

            foreach (var e in ChannelEvents) await TrySubscribe(e, id, "SUBSCRIBE");
            ChannelChanged?.Invoke();
        }

        static VoiceMember ParseMember(JsonElement vs)
        {
            var user = Obj(vs, "user");
            var m = new VoiceMember { Id = Str(user, "id") ?? "", AvatarHash = Str(user, "avatar") };
            m.Name = Str(vs, "nick") ?? Str(user, "global_name") ?? Str(user, "username") ?? "Unbekannt";
            var st = Obj(vs, "voice_state");
            m.Muted = Bool(st, "self_mute") || Bool(st, "mute") || Bool(st, "suppress");
            m.Deafened = Bool(st, "self_deaf") || Bool(st, "deaf");
            return m;
        }

        // =================================================================
        //  Protokoll: [Opcode int32][Länge int32][JSON]
        // =================================================================
        async Task TrySubscribe(string evt, string channelId, string cmd = "SUBSCRIBE")
        {
            try
            {
                var args = new JsonObject();
                if (channelId != null) args["channel_id"] = channelId;
                var r = await SendAsync(cmd, args, evt);
                if (!r.Ok) Program.Log(new Exception($"Discord {cmd} {evt}: {r.Message}"));
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        async Task<Reply> SendAsync(string cmd, JsonObject args, string evt = null, int timeoutMs = 10000)
        {
            var nonce = Guid.NewGuid().ToString("N");
            var o = new JsonObject { ["cmd"] = cmd, ["args"] = args ?? new JsonObject(), ["nonce"] = nonce };
            if (evt != null) o["evt"] = evt;
            var tcs = new TaskCompletionSource<Reply>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[nonce] = tcs;
            await WriteFrameAsync(1, o.ToJsonString());
            var rt = readerTask ?? Task.Delay(Timeout.Infinite);
            var done = await Task.WhenAny(tcs.Task, rt, Task.Delay(timeoutMs));
            pending.Remove(nonce);
            if (done == tcs.Task) return await tcs.Task;
            if (done == rt) throw new IOException("Verbindung zu Discord getrennt");
            throw new TimeoutException("Discord antwortet nicht (" + cmd + ")");
        }

        Task WriteFrameAsync(int op, string json) => WriteFrameAsync(op, Encoding.UTF8.GetBytes(json));

        async Task WriteFrameAsync(int op, byte[] body)
        {
            var p = pipe ?? throw new IOException("Nicht mit Discord verbunden");
            var buf = new byte[8 + body.Length];
            BitConverter.GetBytes(op).CopyTo(buf, 0);
            BitConverter.GetBytes(body.Length).CopyTo(buf, 4);
            body.CopyTo(buf, 8);
            await writeLock.WaitAsync();
            try
            {
                await p.WriteAsync(buf, 0, buf.Length);
                await p.FlushAsync();
            }
            finally { writeLock.Release(); }
        }

        async Task ReadLoopAsync(Stream s)
        {
            var header = new byte[8];
            while (true)
            {
                await s.ReadExactlyAsync(header, 0, 8);
                int op = BitConverter.ToInt32(header, 0);
                int len = BitConverter.ToInt32(header, 4);
                if (len < 0 || len > 8 * 1024 * 1024) throw new IOException("Ungültige Antwort von Discord");
                var body = new byte[len];
                if (len > 0) await s.ReadExactlyAsync(body, 0, len);
                switch (op)
                {
                    case 1: Handle(body); break;
                    case 2: throw new IOException(CloseReason(body));
                    case 3: await WriteFrameAsync(4, body); break; // Ping → Pong
                }
            }
        }

        static string CloseReason(byte[] body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                return Str(doc.RootElement, "message") ?? "Verbindung geschlossen";
            }
            catch { return "Verbindung geschlossen"; }
        }

        void Handle(byte[] body)
        {
            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(body);
                root = doc.RootElement.Clone();
            }
            catch (Exception ex) { Program.Log(ex); return; }

            string cmd = Str(root, "cmd"), evt = Str(root, "evt"), nonce = Str(root, "nonce");
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : default;
            if (nonce != null && pending.TryGetValue(nonce, out var tcs))
            {
                pending.Remove(nonce);
                tcs.TrySetResult(new Reply { Evt = evt, Data = data });
                return;
            }
            if (cmd != "DISPATCH") return;
            try { OnEvent(evt, data); }
            catch (Exception ex) { Program.Log(ex); }
        }

        // ---- JSON-Helfer (vertragen fehlende Felder) ----
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
    }
}

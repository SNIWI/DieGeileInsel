using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicIsland
{
    /// <summary>Zustand der laufenden Teams-Besprechung (aus der lokalen Teams-API).</summary>
    public sealed class TeamsMeeting
    {
        public bool InMeeting, Muted, VideoOn, HandRaised, Recording, Blurred, Sharing, UnreadMessages;
        public bool CanToggleMute, CanToggleVideo, CanToggleHand, CanToggleBlur, CanLeave, CanReact, CanToggleChat, CanStopSharing, CanPair;
    }

    /// <summary>
    /// Microsoft Teams über die „Drittanbieter-App-API“ (neues Teams: Einstellungen → Datenschutz → API verwalten).
    /// WebSocket auf 127.0.0.1:8124. Beim ersten Mal muss die Kopplung in Teams bestätigt werden (nur während einer Besprechung),
    /// danach schickt Teams einen Token, mit dem die Island sich künftig ohne Nachfrage verbindet.
    /// </summary>
    public sealed class TeamsService
    {
        const string Url = "ws://127.0.0.1:8124";
        readonly Settings settings;
        ClientWebSocket ws;
        CancellationTokenSource cts;
        bool running;
        int generation, requestId;

        public string Status { get; private set; } = "Nicht verbunden";
        /// <summary>WebSocket zu Teams steht.</summary>
        public bool Connected { get; private set; }
        /// <summary>Gekoppelt (Teams hat einen Token geschickt).</summary>
        public bool Paired => !string.IsNullOrEmpty(settings.TeamsToken);
        public TeamsMeeting Meeting { get; private set; } = new TeamsMeeting();
        public DateTime JoinedAt { get; private set; } = DateTime.Now;

        /// <summary>In einer Besprechung und gekoppelt – nur dann zeigt die Island etwas an.</summary>
        public bool Active => Connected && Paired && Meeting.InMeeting;

        public event Action StatusChanged, MeetingChanged, ActiveChanged;

        public TeamsService(Settings s) { settings = s; }

        void SetStatus(string s)
        {
            if (Status == s) return;
            Status = s;
            StatusChanged?.Invoke();
        }

        /// <summary>Verbindet sich mit Teams und versucht es alle 5 s erneut, solange Teams nicht läuft.</summary>
        public async void Start()
        {
            if (running) return;
            running = true;
            int gen = ++generation;
            while (running && gen == generation)
            {
                try { await RunOnceAsync(gen); }
                catch (Exception ex)
                {
                    bool normal = ex is WebSocketException || ex is OperationCanceledException || ex is ObjectDisposedException;
                    if (!normal) Program.Log(ex);
                }
                if (gen != generation) break;
                Cleanup();
                if (running) SetStatus(Paired ? "Gekoppelt – wartet auf Teams …" : "Wartet auf Teams …");
                try { await Task.Delay(TimeSpan.FromSeconds(5)); } catch { }
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

        /// <summary>Kopplung vergessen (Teams fragt beim nächsten Mal wieder).</summary>
        public void Unpair()
        {
            settings.TeamsToken = "";
            settings.Save();
            Restart();
        }

        void Cleanup()
        {
            bool wasActive = Active;
            Connected = false;
            Meeting = new TeamsMeeting();
            try { cts?.Cancel(); } catch { }
            try { ws?.Dispose(); } catch { }
            ws = null;
            cts = null;
            if (wasActive) ActiveChanged?.Invoke();
        }

        async Task RunOnceAsync(int gen)
        {
            ws = new ClientWebSocket();
            cts = new CancellationTokenSource();
            var q = "protocol-version=2.0.0&manufacturer=SNIWI&device=PC&app=Die%20Geile%20Insel&app-version=1.3";
            if (Paired) q = "token=" + Uri.EscapeDataString(settings.TeamsToken) + "&" + q;
            using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cts.Token))
            {
                connectTimeout.CancelAfter(TimeSpan.FromSeconds(4));
                await ws.ConnectAsync(new Uri(Url + "?" + q), connectTimeout.Token);
            }
            if (gen != generation) return;
            Connected = true;
            SetStatus(Paired ? "Verbunden" : "Verbunden – in einer Besprechung auf „Koppeln“ klicken");

            var buf = new byte[16 * 1024];
            var sb = new StringBuilder();
            while (ws != null && ws.State == WebSocketState.Open && gen == generation)
            {
                var r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                if (r.MessageType == WebSocketMessageType.Close) break;
                sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
                if (!r.EndOfMessage) continue;
                var text = sb.ToString();
                sb.Clear();
                try { Handle(text); } catch (Exception ex) { Program.Log(ex); }
            }
        }

        void Handle(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("tokenRefresh", out var tok) && tok.ValueKind == JsonValueKind.String)
            {
                bool first = !Paired;
                settings.TeamsToken = tok.GetString() ?? "";
                settings.Save();
                SetStatus("Verbunden");
                if (first && Meeting.InMeeting) ActiveChanged?.Invoke();
            }
            if (root.TryGetProperty("errorMsg", out var err) && err.ValueKind == JsonValueKind.String)
            {
                var e = err.GetString() ?? "";
                // abgelaufener / widerrufener Token: neu koppeln
                if (e.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 && Paired)
                {
                    settings.TeamsToken = "";
                    settings.Save();
                }
                SetStatus("Teams: " + e);
            }
            if (!root.TryGetProperty("meetingUpdate", out var upd)) return;

            bool wasActive = Active, wasIn = Meeting.InMeeting;
            var m = new TeamsMeeting();
            if (upd.TryGetProperty("meetingState", out var st))
            {
                m.InMeeting = B(st, "isInMeeting");
                m.Muted = B(st, "isMuted");
                m.VideoOn = B(st, "isVideoOn");
                m.HandRaised = B(st, "isHandRaised");
                m.Recording = B(st, "isRecordingOn");
                m.Blurred = B(st, "isBackgroundBlurred");
                m.Sharing = B(st, "isSharing");
                m.UnreadMessages = B(st, "hasUnreadMessages");
            }
            if (upd.TryGetProperty("meetingPermissions", out var p))
            {
                m.CanToggleMute = B(p, "canToggleMute");
                m.CanToggleVideo = B(p, "canToggleVideo");
                m.CanToggleHand = B(p, "canToggleHand");
                m.CanToggleBlur = B(p, "canToggleBlur");
                m.CanLeave = B(p, "canLeave");
                m.CanReact = B(p, "canReact");
                m.CanToggleChat = B(p, "canToggleChat");
                m.CanStopSharing = B(p, "canStopSharing");
                m.CanPair = B(p, "canPair");
            }
            if (m.InMeeting && !wasIn) JoinedAt = DateTime.Now;
            Meeting = m;
            if (!Paired) SetStatus(m.InMeeting ? "In einer Besprechung – „Koppeln“ klicken und in Teams erlauben" : "Verbunden – in einer Besprechung koppeln");
            else SetStatus(m.InMeeting ? "Verbunden · in einer Besprechung" : "Verbunden");
            if (Active != wasActive) ActiveChanged?.Invoke();
            else MeetingChanged?.Invoke();
        }

        static bool B(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

        async void Send(string action, string parameters = "{}")
        {
            var sock = ws;
            if (sock == null || sock.State != WebSocketState.Open) return;
            var msg = $"{{\"action\":\"{action}\",\"parameters\":{parameters},\"requestId\":{++requestId}}}";
            try { await sock.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None); }
            catch (Exception ex) { if (!(ex is WebSocketException || ex is ObjectDisposedException)) Program.Log(ex); }
        }

        /// <summary>Kopplung anstoßen: Teams zeigt dann „Neues Gerät koppeln?“ (nur während einer Besprechung).</summary>
        public void Pair()
        {
            if (Paired) return;
            // ungekoppelt führt Teams den Befehl nicht aus, sondern fragt nach der Kopplung
            Send("toggle-ui", "{\"type\":\"chat\"}");
        }

        public void ToggleMute() => Send("toggle-mute");
        public void ToggleVideo() => Send("toggle-video");
        public void ToggleHand() => Send("toggle-hand");
        public void ToggleBlur() => Send("toggle-background-blur");
        public void ToggleChat() => Send("toggle-ui", "{\"type\":\"chat\"}");
        public void StopSharing() => Send("stop-sharing");
        public void Leave() => Send("leave-call");
        /// <summary>Reaktion senden: like, love, applause, laugh, wow.</summary>
        public void React(string type) => Send("send-reaction", $"{{\"type\":\"{type}\"}}");
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicIsland
{
    public sealed class TodoTask
    {
        public string Id, ListId, Title;
        public bool Important;
        public DateTime? Due, Reminder;
        public DateTime Created;
    }

    public sealed class TodoList
    {
        public string Id, Name, WellKnown;
    }

    /// <summary>
    /// Microsoft To Do über Microsoft Graph (Anmeldung per Gerätecode, keine Weiterleitungs-Adresse nötig).
    /// Tipp: Apple-Erinnerungen auf dem iPhone mit dem Outlook-Konto landen automatisch in Microsoft To Do.
    /// </summary>
    public sealed class TodoService
    {
        const string Scopes = "Tasks.ReadWrite offline_access";
        const string Auth = "https://login.microsoftonline.com/common/oauth2/v2.0/";
        const string Graph = "https://graph.microsoft.com/v1.0/me/todo/";
        static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        readonly Settings s;
        string accessToken;
        DateTime accessUntil;
        bool busy;
        CancellationTokenSource loginCts;

        public List<TodoList> Lists { get; private set; } = new List<TodoList>();
        public List<TodoTask> Tasks { get; private set; } = new List<TodoTask>();
        public string Status { get; private set; } = "Nicht verbunden";
        public DateTime LastSync { get; private set; }
        public event Action Changed;
        public event Action<string> StatusChanged;

        public TodoService(Settings settings) { s = settings; }

        public bool Connected => !string.IsNullOrEmpty(RefreshToken);

        string RefreshToken
        {
            get => Secret.Unprotect(s.TodoRefresh);
            set => s.TodoRefresh = string.IsNullOrEmpty(value) ? null : Secret.Protect(value);
        }

        void SetStatus(string t) { Status = t; StatusChanged?.Invoke(t); }

        /// <summary>Liste, in die neue Aufgaben kommen (gewählte oder „Aufgaben“).</summary>
        public TodoList MainList =>
            Lists.FirstOrDefault(l => l.Id == s.TodoListId)
            ?? Lists.FirstOrDefault(l => l.WellKnown == "defaultList")
            ?? Lists.FirstOrDefault();

        /// <summary>Offene Aufgaben, sortiert: überfällig/heute fällig, wichtig, dann neueste.</summary>
        public List<TodoTask> Open(int max = 50) =>
            Tasks.OrderBy(t => t.Due.HasValue ? 0 : 1).ThenBy(t => t.Due ?? DateTime.MaxValue)
                 .ThenByDescending(t => t.Important).ThenByDescending(t => t.Created).Take(max).ToList();

        // ================================================================
        //  Anmelden (Gerätecode)
        // ================================================================
        public sealed class DeviceCode { public string UserCode, Url, Message; }

        /// <summary>Startet die Anmeldung: liefert den Code, wartet im Hintergrund auf die Bestätigung.</summary>
        public async Task<DeviceCode> BeginLoginAsync()
        {
            string id = (s.TodoClientId ?? "").Trim();
            if (id.Length == 0) throw new Exception("Bitte zuerst die Anwendungs-ID (Client-ID) eintragen.");
            loginCts?.Cancel();
            loginCts = new CancellationTokenSource();
            var res = await http.PostAsync(Auth + "devicecode", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = id,
                ["scope"] = Scopes
            }));
            string body = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var r = doc.RootElement;
            if (!res.IsSuccessStatusCode) throw new Exception(AuthError(r));
            string deviceCode = r.GetProperty("device_code").GetString();
            int interval = r.TryGetProperty("interval", out var iv) ? iv.GetInt32() : 5;
            int expires = r.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 900;
            var dc = new DeviceCode
            {
                UserCode = r.GetProperty("user_code").GetString(),
                Url = r.TryGetProperty("verification_uri", out var u) ? u.GetString() : "https://microsoft.com/devicelogin",
                Message = r.TryGetProperty("message", out var m) ? m.GetString() : null
            };
            SetStatus("Warte auf Anmeldung im Browser …");
            _ = PollLoginAsync(id, deviceCode, interval, DateTime.Now.AddSeconds(expires), loginCts.Token);
            return dc;
        }

        async Task PollLoginAsync(string clientId, string deviceCode, int interval, DateTime until, CancellationToken ct)
        {
            try
            {
                while (DateTime.Now < until && !ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(interval), ct);
                    var res = await http.PostAsync(Auth + "token", new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                        ["client_id"] = clientId,
                        ["device_code"] = deviceCode
                    }), ct);
                    string body = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var r = doc.RootElement;
                    if (res.IsSuccessStatusCode)
                    {
                        TakeTokens(r);
                        s.Save();
                        SetStatus("Verbunden");
                        await RefreshAsync();
                        return;
                    }
                    string err = r.TryGetProperty("error", out var e) ? e.GetString() : "";
                    if (err == "authorization_pending") continue;
                    if (err == "slow_down") { interval += 5; continue; }
                    SetStatus(err == "expired_token" ? "Code abgelaufen – nochmal anmelden" :
                              err == "authorization_declined" ? "Anmeldung abgelehnt" : AuthError(r));
                    return;
                }
                if (!ct.IsCancellationRequested) SetStatus("Code abgelaufen – nochmal anmelden");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Program.Log(ex); SetStatus("Anmeldung fehlgeschlagen: " + ex.Message); }
        }

        void TakeTokens(JsonElement r)
        {
            accessToken = r.GetProperty("access_token").GetString();
            int exp = r.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
            accessUntil = DateTime.Now.AddSeconds(exp - 120);
            if (r.TryGetProperty("refresh_token", out var rt)) RefreshToken = rt.GetString();
        }

        static string AuthError(JsonElement r)
        {
            string d = r.TryGetProperty("error_description", out var ed) ? ed.GetString() : r.TryGetProperty("error", out var e) ? e.GetString() : "Fehler";
            if (d != null && d.Contains("AADSTS7000218")) return "In der App-Registrierung „Öffentliche Clientflows zulassen“ auf Ja stellen.";
            if (d != null && d.Contains("AADSTS700016")) return "Client-ID nicht gefunden – bitte prüfen.";
            if (d != null && d.Contains("AADSTS50059")) return "Die App muss auch „persönliche Microsoft-Konten“ erlauben.";
            int nl = d?.IndexOf("\r\n", StringComparison.Ordinal) ?? -1;
            return nl > 0 ? d.Substring(0, nl) : d;
        }

        public void Logout()
        {
            loginCts?.Cancel();
            RefreshToken = null;
            accessToken = null;
            Tasks = new List<TodoTask>();
            Lists = new List<TodoList>();
            s.Save();
            SetStatus("Nicht verbunden");
            Changed?.Invoke();
        }

        async Task<string> TokenAsync()
        {
            if (accessToken != null && DateTime.Now < accessUntil) return accessToken;
            string rt = RefreshToken;
            if (string.IsNullOrEmpty(rt)) return null;
            var res = await http.PostAsync(Auth + "token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = (s.TodoClientId ?? "").Trim(),
                ["refresh_token"] = rt,
                ["scope"] = Scopes
            }));
            string body = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (!res.IsSuccessStatusCode)
            {
                string err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : "";
                if (err == "invalid_grant") { RefreshToken = null; s.Save(); SetStatus("Anmeldung abgelaufen – bitte neu verbinden"); }
                return null;
            }
            TakeTokens(doc.RootElement);
            s.Save();
            return accessToken;
        }

        async Task<HttpResponseMessage> SendAsync(HttpMethod m, string path, object body = null)
        {
            string token = await TokenAsync();
            if (token == null) return null;
            var req = new HttpRequestMessage(m, path.StartsWith("http") ? path : Graph + path);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            return await http.SendAsync(req);
        }

        // ================================================================
        //  Laden / Ändern
        // ================================================================
        public async Task RefreshAsync()
        {
            if (busy || !Connected) return;
            busy = true;
            try
            {
                var res = await SendAsync(HttpMethod.Get, "lists");
                if (res == null) return;
                if (!res.IsSuccessStatusCode) { SetStatus("To Do nicht erreichbar (" + (int)res.StatusCode + ")"); return; }
                var lists = new List<TodoList>();
                using (var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync()))
                    foreach (var l in doc.RootElement.GetProperty("value").EnumerateArray())
                    {
                        string wk = l.TryGetProperty("wellknownListName", out var w) ? w.GetString() : "none";
                        if (wk == "flaggedEmails") continue;
                        lists.Add(new TodoList { Id = l.GetProperty("id").GetString(), Name = l.GetProperty("displayName").GetString(), WellKnown = wk });
                    }
                Lists = lists;

                // gewählte Listen (leer = alle)
                var wanted = s.TodoShowAll ? lists : lists.Where(l => l.Id == MainList?.Id).ToList();
                var tasks = new List<TodoTask>();
                foreach (var l in wanted)
                {
                    string url = "lists/" + l.Id + "/tasks?$filter=" + Uri.EscapeDataString("status ne 'completed'") + "&$top=100";
                    while (url != null && tasks.Count < 400)
                    {
                        var r = await SendAsync(HttpMethod.Get, url);
                        if (r == null || !r.IsSuccessStatusCode) break;
                        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                        foreach (var t in doc.RootElement.GetProperty("value").EnumerateArray())
                        {
                            tasks.Add(new TodoTask
                            {
                                Id = t.GetProperty("id").GetString(),
                                ListId = l.Id,
                                Title = t.TryGetProperty("title", out var ti) ? ti.GetString() : "",
                                Important = t.TryGetProperty("importance", out var im) && im.GetString() == "high",
                                Due = GraphTime(t, "dueDateTime"),
                                Reminder = t.TryGetProperty("isReminderOn", out var ro) && ro.ValueKind == JsonValueKind.True ? GraphTime(t, "reminderDateTime") : null,
                                Created = GraphCreated(t)
                            });
                        }
                        url = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl) ? nl.GetString() : null;
                    }
                }
                Tasks = tasks;
                LastSync = DateTime.Now;
                SetStatus($"Verbunden · {tasks.Count} offene Aufgaben · {DateTime.Now:HH:mm}");
            }
            catch (Exception ex) { Program.Log(ex); SetStatus("Fehler beim Laden: " + ex.Message); }
            finally { busy = false; }
            Changed?.Invoke();
        }

        public async Task<bool> CompleteAsync(TodoTask t)
        {
            Tasks.Remove(t);
            Changed?.Invoke();
            var r = await SendAsync(new HttpMethod("PATCH"), "lists/" + t.ListId + "/tasks/" + t.Id, new { status = "completed" });
            bool ok = r != null && r.IsSuccessStatusCode;
            if (!ok) { Tasks.Add(t); Changed?.Invoke(); }
            return ok;
        }

        public async Task<bool> AddAsync(string title)
        {
            var list = MainList;
            if (list == null || string.IsNullOrWhiteSpace(title)) return false;
            var r = await SendAsync(HttpMethod.Post, "lists/" + list.Id + "/tasks", new { title = title.Trim() });
            bool ok = r != null && r.IsSuccessStatusCode;
            if (ok)
            {
                using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                Tasks.Add(new TodoTask { Id = doc.RootElement.GetProperty("id").GetString(), ListId = list.Id, Title = title.Trim(), Created = DateTime.Now });
                Changed?.Invoke();
            }
            return ok;
        }

        static DateTime? GraphTime(JsonElement t, string prop)
        {
            if (!t.TryGetProperty(prop, out var d) || d.ValueKind != JsonValueKind.Object) return null;
            if (!d.TryGetProperty("dateTime", out var dt) || dt.GetString() is not string v) return null;
            if (!DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var wall)) return null;
            string tz = d.TryGetProperty("timeZone", out var z) ? z.GetString() : "UTC";
            try
            {
                if (string.IsNullOrEmpty(tz) || tz.Equals("UTC", StringComparison.OrdinalIgnoreCase) || tz == "tzone://Microsoft/Utc")
                    return DateTime.SpecifyKind(wall, DateTimeKind.Utc).ToLocalTime();
                var info = TimeZoneInfo.FindSystemTimeZoneById(tz);
                return TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(wall, DateTimeKind.Unspecified), info, TimeZoneInfo.Local);
            }
            catch { return wall; }
        }

        static DateTime GraphCreated(JsonElement t) =>
            t.TryGetProperty("createdDateTime", out var c) && DateTime.TryParse(c.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var d)
                ? d.ToLocalTime() : DateTime.MinValue;
    }
}

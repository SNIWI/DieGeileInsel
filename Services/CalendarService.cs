using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace DynamicIsland
{
    /// <summary>Ein Termin (Zeiten in Ortszeit).</summary>
    public sealed class CalEvent
    {
        public string Uid, Title, Location, Link, Calendar;
        public DateTime Start, End;
        public bool AllDay;
        public string Key => Uid + "|" + Start.ToString("yyyyMMddHHmm");
    }

    /// <summary>
    /// Kalender über iCal-Adressen (Google „Geheime Adresse im iCal-Format“, Outlook „Kalender veröffentlichen“, iCloud …).
    /// Lädt regelmäßig, rechnet Serientermine aus und liefert die nächsten Termine.
    /// </summary>
    public sealed class CalendarService
    {
        static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        readonly Settings s;
        bool busy;
        /// <summary>Zuletzt protokollierter Fehler je Adresse – derselbe Fehler landet nur einmal im Protokoll (nicht alle 10 Minuten).</summary>
        readonly Dictionary<string, string> loggedErr = new Dictionary<string, string>();

        public List<CalEvent> Events { get; private set; } = new List<CalEvent>();
        public DateTime LastSync { get; private set; }
        public string Status { get; private set; } = "Noch nicht geladen";
        public event Action Changed;

        public CalendarService(Settings settings) { s = settings; }

        // ---- Adressen (verschlüsselt gespeichert: enthalten einen geheimen Schlüssel) ----
        public List<string> Urls
        {
            get => (s.CalendarUrls ?? new List<string>()).Select(Secret.Unprotect).Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
            set => s.CalendarUrls = value.Select(Secret.Protect).Where(u => u != null).ToList();
        }

        public bool HasCalendars => (s.CalendarUrls?.Count ?? 0) > 0;

        public static string Normalize(string url)
        {
            url = (url ?? "").Trim();
            if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)) url = "https://" + url.Substring(9);
            return url;
        }

        /// <summary>Nächste Termine (laufende zuerst), ganztägige ab heute.</summary>
        public List<CalEvent> Upcoming(int max = 20, double hours = 48)
        {
            var now = DateTime.Now;
            var until = now.AddHours(hours);
            return Events.Where(e => e.End > now && e.Start < until)
                         .OrderBy(e => e.AllDay ? 1 : 0).ThenBy(e => e.Start)
                         .Take(max).ToList();
        }

        public CalEvent Next => Events.Where(e => !e.AllDay && e.Start > DateTime.Now).OrderBy(e => e.Start).FirstOrDefault();

        public async Task RefreshAsync()
        {
            if (busy) return;
            var urls = Urls;
            if (urls.Count == 0) { Events = new List<CalEvent>(); Status = "Kein Kalender verknüpft"; Changed?.Invoke(); return; }
            busy = true;
            var all = new List<CalEvent>();
            int ok = 0;
            string err = null;
            try
            {
                var from = DateTime.Today.AddDays(-1);
                var to = DateTime.Today.AddDays(8);
                foreach (var u in urls)
                {
                    try
                    {
                        string ics = await http.GetStringAsync(Normalize(u));
                        if (ics.IndexOf("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase) < 0) { err = "Adresse liefert keinen Kalender"; continue; }
                        all.AddRange(Ics.Parse(ics, from, to));
                        ok++;
                        loggedErr.Remove(u);
                    }
                    catch (Exception ex)
                    {
                        int code = ex is HttpRequestException hx && hx.StatusCode.HasValue ? (int)hx.StatusCode.Value : 0;
                        err = code == 401 || code == 403 ? "Kalender-Adresse abgelaufen oder ohne Freigabe – neu kopieren (Einstellungen → Kalender)"
                            : code == 404 ? "Kalender-Adresse gibt es nicht mehr – neu kopieren (Einstellungen → Kalender)"
                            : ex is HttpRequestException || ex is TaskCanceledException ? "Kalender nicht erreichbar"
                            : ex.Message;
                        string sig = ex.GetType().Name + code;
                        if (!loggedErr.TryGetValue(u, out var last) || last != sig) { loggedErr[u] = sig; Program.Log(ex); }
                    }
                }
                if (ok > 0)
                {
                    Events = all.GroupBy(e => e.Key).Select(g => g.First()).OrderBy(e => e.Start).ToList();
                    LastSync = DateTime.Now;
                }
                Status = ok == urls.Count
                    ? $"{Events.Count(e => e.Start.Date >= DateTime.Today)} Termine in den nächsten Tagen · aktualisiert {DateTime.Now:HH:mm}"
                    : ok > 0 ? $"{ok} von {urls.Count} Kalendern geladen – {err}" : err ?? "Fehler beim Laden";
            }
            finally { busy = false; }
            Changed?.Invoke();
        }

        /// <summary>Link zu einem Online-Meeting im Termin (Teams, Zoom, Meet, Webex).</summary>
        public static string FindMeetingLink(params string[] texts)
        {
            foreach (var t in texts)
            {
                if (string.IsNullOrEmpty(t)) continue;
                var m = Regex.Match(t, @"https://[^\s""<>]*(teams\.microsoft\.com|teams\.live\.com|meet\.google\.com|zoom\.us|webex\.com|whereby\.com|discord\.gg)[^\s""<>]*", RegexOptions.IgnoreCase);
                if (m.Success) return m.Value.TrimEnd('.', ',', ')', ';');
            }
            return null;
        }
    }

    /// <summary>Kleiner iCalendar-Leser (RFC 5545) inkl. Serienterminen (RRULE), Ausnahmen (EXDATE) und Änderungen (RECURRENCE-ID).</summary>
    static class Ics
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Dictionary<string, TimeZoneInfo> tzCache = new Dictionary<string, TimeZoneInfo>();

        sealed class Prop
        {
            public string Name, Value;
            public Dictionary<string, string> Params = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        struct Dt
        {
            public DateTime Wall;        // Wanduhrzeit in der Zeitzone des Termins
            public TimeZoneInfo Tz;      // null = Ortszeit
            public bool Utc, AllDay;
            public DateTime Local =>
                AllDay ? Wall.Date
                : Utc ? DateTime.SpecifyKind(Wall, DateTimeKind.Utc).ToLocalTime()
                : Tz != null ? SafeConvert(Wall, Tz)
                : Wall;
            public Dt With(DateTime wall) => new Dt { Wall = wall, Tz = Tz, Utc = Utc, AllDay = AllDay };
        }

        static DateTime SafeConvert(DateTime wall, TimeZoneInfo tz)
        {
            try
            {
                var w = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
                if (tz.IsInvalidTime(w)) w = w.AddHours(1);
                return TimeZoneInfo.ConvertTime(w, tz, TimeZoneInfo.Local);
            }
            catch { return wall; }
        }

        public static List<CalEvent> Parse(string ics, DateTime from, DateTime to)
        {
            // Zeilen entfalten (Fortsetzungszeilen beginnen mit Leerzeichen/Tab)
            var lines = new List<string>();
            foreach (var raw in ics.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                if ((raw.StartsWith(" ") || raw.StartsWith("\t")) && lines.Count > 0) lines[lines.Count - 1] += raw.Substring(1);
                else if (raw.Length > 0) lines.Add(raw);
            }

            var events = new List<List<Prop>>();
            List<Prop> cur = null;
            int depth = 0;
            foreach (var l in lines)
            {
                if (l.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase)) { cur = new List<Prop>(); depth = 0; continue; }
                if (cur == null) continue;
                if (l.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase)) { depth++; continue; }   // VALARM usw. überspringen
                if (l.StartsWith("END:", StringComparison.OrdinalIgnoreCase))
                {
                    if (depth > 0) { depth--; continue; }
                    if (l.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase)) { events.Add(cur); cur = null; }
                    continue;
                }
                if (depth > 0) continue;
                var p = ParseLine(l);
                if (p != null) cur.Add(p);
            }

            // Geänderte Einzeltermine einer Serie (RECURRENCE-ID) ersetzen das Original
            var overridden = new HashSet<string>();
            foreach (var ev in events)
            {
                var rid = ev.FirstOrDefault(p => p.Name == "RECURRENCE-ID");
                var uid = ev.FirstOrDefault(p => p.Name == "UID")?.Value ?? "";
                if (rid != null && TryDt(rid, out var r)) overridden.Add(uid + "|" + r.Local.ToString("yyyyMMddHHmm"));
            }

            var result = new List<CalEvent>();
            foreach (var ev in events)
            {
                try { Expand(ev, from, to, overridden, result); }
                catch (Exception ex) { Program.Log(ex); }
            }
            return result;
        }

        static void Expand(List<Prop> ev, DateTime from, DateTime to, HashSet<string> overridden, List<CalEvent> result)
        {
            string Get(string n) => ev.FirstOrDefault(p => p.Name == n)?.Value;
            if (string.Equals(Get("STATUS"), "CANCELLED", StringComparison.OrdinalIgnoreCase)) return;
            var dsP = ev.FirstOrDefault(p => p.Name == "DTSTART");
            if (dsP == null || !TryDt(dsP, out var start)) return;

            TimeSpan dur;
            var deP = ev.FirstOrDefault(p => p.Name == "DTEND");
            if (deP != null && TryDt(deP, out var end)) dur = end.Local - start.Local;
            else if (Get("DURATION") is string d && TryDuration(d, out var dd)) dur = dd;
            else dur = start.AllDay ? TimeSpan.FromDays(1) : TimeSpan.Zero;
            if (dur < TimeSpan.Zero) dur = TimeSpan.Zero;

            string uid = Get("UID") ?? Guid.NewGuid().ToString();
            string title = Unescape(Get("SUMMARY")) ?? "Termin";
            string loc = Unescape(Get("LOCATION"));
            string desc = Unescape(Get("DESCRIPTION"));
            string link = CalendarService.FindMeetingLink(loc, Get("URL"), desc, Get("X-GOOGLE-CONFERENCE"));
            bool isOverride = ev.Any(p => p.Name == "RECURRENCE-ID");

            var ex = new HashSet<string>();
            foreach (var p in ev.Where(p => p.Name == "EXDATE"))
                foreach (var part in p.Value.Split(','))
                {
                    var q = new Prop { Name = "EXDATE", Value = part.Trim(), Params = p.Params };
                    if (TryDt(q, out var x)) ex.Add(x.Local.ToString("yyyyMMddHHmm"));
                }

            void Add(Dt occ)
            {
                var s = occ.Local;
                var e = s + dur;
                if (e < from || s > to) return;
                string k = s.ToString("yyyyMMddHHmm");
                if (ex.Contains(k)) return;
                if (!isOverride && overridden.Contains(uid + "|" + k)) return;
                result.Add(new CalEvent { Uid = uid, Title = title, Location = loc, Link = link, Start = s, End = e == s && !occ.AllDay ? s.AddMinutes(30) : e, AllDay = occ.AllDay });
            }

            string rrule = Get("RRULE");
            if (string.IsNullOrEmpty(rrule) || isOverride) { Add(start); return; }

            var rule = rrule.Split(';').Select(x => x.Split('=')).Where(x => x.Length == 2)
                            .ToDictionary(x => x[0].ToUpperInvariant(), x => x[1], StringComparer.OrdinalIgnoreCase);
            string freq = rule.TryGetValue("FREQ", out var f) ? f.ToUpperInvariant() : "DAILY";
            int interval = rule.TryGetValue("INTERVAL", out var iv) && int.TryParse(iv, out var ivn) && ivn > 0 ? ivn : 1;
            int count = rule.TryGetValue("COUNT", out var c) && int.TryParse(c, out var cn) ? cn : int.MaxValue;
            DateTime until = DateTime.MaxValue;
            if (rule.TryGetValue("UNTIL", out var u) && TryDt(new Prop { Name = "UNTIL", Value = u }, out var ud)) until = ud.Local.Date == ud.Local ? ud.Local.AddDays(1).AddSeconds(-1) : ud.Local;
            var byDay = rule.TryGetValue("BYDAY", out var bd) ? bd.Split(',').Select(x => x.Trim().ToUpperInvariant()).ToList() : new List<string>();

            int n = 0, guard = 0;
            var wall0 = start.Wall;
            DateTime limit = to.AddDays(1);

            bool Emit(DateTime wall)
            {
                if (wall < wall0) return true;
                var occ = start.With(wall);
                if (occ.Local > until || n >= count) return false;
                n++;
                Add(occ);
                return occ.Local <= limit;
            }

            switch (freq)
            {
                case "WEEKLY":
                {
                    var days = byDay.Select(DayOf).Where(x => x.HasValue).Select(x => x.Value).ToList();
                    if (days.Count == 0) days.Add(wall0.DayOfWeek);
                    var weekStart = wall0.Date.AddDays(-(((int)wall0.DayOfWeek + 6) % 7)); // Montag
                    for (var w = weekStart; guard++ < 3000; w = w.AddDays(7 * interval))
                    {
                        bool go = true;
                        foreach (var day in days.OrderBy(x => ((int)x + 6) % 7))
                        {
                            var date = w.AddDays(((int)day + 6) % 7) + wall0.TimeOfDay;
                            if (!Emit(date)) { go = false; break; }
                        }
                        if (!go) break;
                    }
                    break;
                }
                case "MONTHLY":
                {
                    var nth = byDay.FirstOrDefault(x => Regex.IsMatch(x, @"^-?\d"));
                    for (int i = 0; guard++ < 2000; i += interval)
                    {
                        var month = new DateTime(wall0.Year, wall0.Month, 1).AddMonths(i);
                        DateTime? date;
                        if (nth != null) date = NthWeekday(month, nth);
                        else date = wall0.Day <= DateTime.DaysInMonth(month.Year, month.Month) ? month.AddDays(wall0.Day - 1) : (DateTime?)null;
                        if (date == null) continue;
                        if (!Emit(date.Value.Date + wall0.TimeOfDay)) break;
                    }
                    break;
                }
                case "YEARLY":
                    for (int i = 0; guard++ < 200; i += interval)
                    {
                        var y = wall0.Year + i;
                        if (wall0.Month == 2 && wall0.Day == 29 && !DateTime.IsLeapYear(y)) continue;
                        if (!Emit(new DateTime(y, wall0.Month, wall0.Day) + wall0.TimeOfDay)) break;
                    }
                    break;
                default: // DAILY
                {
                    var days = byDay.Select(DayOf).Where(x => x.HasValue).Select(x => x.Value).ToHashSet();
                    // große Sprünge bis kurz vor den gesuchten Zeitraum (spart Rechenzeit bei alten Serien)
                    var w = wall0;
                    if (count == int.MaxValue && w < from.AddDays(-2))
                    {
                        long skip = (long)((from.AddDays(-2) - w).TotalDays / interval);
                        w = w.AddDays(skip * interval);
                    }
                    for (; guard++ < 5000; w = w.AddDays(interval))
                    {
                        if (days.Count > 0 && !days.Contains(w.DayOfWeek)) continue;
                        if (!Emit(w)) break;
                    }
                    break;
                }
            }
        }

        static DateTime? NthWeekday(DateTime month, string spec)
        {
            var m = Regex.Match(spec, @"^(-?\d+)(MO|TU|WE|TH|FR|SA|SU)$");
            if (!m.Success) return null;
            int k = int.Parse(m.Groups[1].Value, Inv);
            var dow = DayOf(m.Groups[2].Value).Value;
            int dim = DateTime.DaysInMonth(month.Year, month.Month);
            if (k > 0)
            {
                var first = month;
                int off = ((int)dow - (int)first.DayOfWeek + 7) % 7;
                int day = 1 + off + (k - 1) * 7;
                return day <= dim ? month.AddDays(day - 1) : (DateTime?)null;
            }
            var last = month.AddDays(dim - 1);
            int back = ((int)last.DayOfWeek - (int)dow + 7) % 7;
            int d2 = dim - back + (k + 1) * 7;
            return d2 >= 1 ? month.AddDays(d2 - 1) : (DateTime?)null;
        }

        static DayOfWeek? DayOf(string s)
        {
            s = Regex.Replace(s ?? "", @"^[-+]?\d+", "");
            switch (s)
            {
                case "MO": return DayOfWeek.Monday;
                case "TU": return DayOfWeek.Tuesday;
                case "WE": return DayOfWeek.Wednesday;
                case "TH": return DayOfWeek.Thursday;
                case "FR": return DayOfWeek.Friday;
                case "SA": return DayOfWeek.Saturday;
                case "SU": return DayOfWeek.Sunday;
            }
            return null;
        }

        static Prop ParseLine(string l)
        {
            // NAME;PARAM=..;PARAM="a:b":VALUE  – der Doppelpunkt in Anführungszeichen zählt nicht
            int colon = -1;
            bool q = false;
            for (int i = 0; i < l.Length; i++)
            {
                if (l[i] == '"') q = !q;
                else if (l[i] == ':' && !q) { colon = i; break; }
            }
            if (colon <= 0) return null;
            var head = l.Substring(0, colon).Split(';');
            var p = new Prop { Name = head[0].ToUpperInvariant(), Value = l.Substring(colon + 1) };
            for (int i = 1; i < head.Length; i++)
            {
                int eq = head[i].IndexOf('=');
                if (eq > 0) p.Params[head[i].Substring(0, eq)] = head[i].Substring(eq + 1).Trim('"');
            }
            return p;
        }

        static bool TryDt(Prop p, out Dt dt)
        {
            dt = default;
            string v = (p.Value ?? "").Trim();
            if (v.Length < 8) return false;
            bool allDay = (p.Params.TryGetValue("VALUE", out var vt) && vt.Equals("DATE", StringComparison.OrdinalIgnoreCase)) || v.Length == 8;
            if (allDay)
            {
                if (!DateTime.TryParseExact(v.Substring(0, 8), "yyyyMMdd", Inv, DateTimeStyles.None, out var d)) return false;
                dt = new Dt { Wall = d, AllDay = true };
                return true;
            }
            bool utc = v.EndsWith("Z", StringComparison.OrdinalIgnoreCase);
            v = v.TrimEnd('Z', 'z');
            if (!DateTime.TryParseExact(v, new[] { "yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm" }, Inv, DateTimeStyles.None, out var w)) return false;
            TimeZoneInfo tz = null;
            if (!utc && p.Params.TryGetValue("TZID", out var id)) tz = FindTz(id);
            dt = new Dt { Wall = w, Utc = utc, Tz = tz };
            return true;
        }

        static TimeZoneInfo FindTz(string id)
        {
            id = (id ?? "").Trim().Trim('"');
            if (id.Length == 0) return null;
            lock (tzCache)
            {
                if (tzCache.TryGetValue(id, out var c)) return c;
                TimeZoneInfo tz = null;
                try { tz = TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch
                {
                    try { if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var win)) tz = TimeZoneInfo.FindSystemTimeZoneById(win); }
                    catch { }
                }
                tzCache[id] = tz;
                return tz;
            }
        }

        static bool TryDuration(string d, out TimeSpan ts)
        {
            ts = TimeSpan.Zero;
            var m = Regex.Match(d, @"^([+-])?P(?:(\d+)W)?(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?)?$");
            if (!m.Success) return false;
            int G(int i) => m.Groups[i].Success ? int.Parse(m.Groups[i].Value, Inv) : 0;
            ts = new TimeSpan(G(2) * 7 + G(3), G(4), G(5), G(6));
            if (m.Groups[1].Value == "-") ts = -ts;
            return true;
        }

        static string Unescape(string t)
        {
            if (t == null) return null;
            t = t.Replace("\\n", "\n").Replace("\\N", "\n").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\").Trim();
            return t.Length == 0 ? null : t;
        }
    }

    /// <summary>Sprachausgabe mit der Windows-Stimme (deutsch, falls installiert).</summary>
    public static class Speaker
    {
        static MediaPlayer player;
        static SpeechSynthesizer synth;

        public static async Task SpeakAsync(string text)
        {
            try
            {
                synth ??= CreateSynth();
                player ??= new MediaPlayer { AudioCategory = MediaPlayerAudioCategory.Speech };
                var stream = await synth.SynthesizeTextToStreamAsync(text);
                player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
                player.Play();
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        static SpeechSynthesizer CreateSynth()
        {
            var sy = new SpeechSynthesizer();
            try
            {
                var lang = CultureInfo.CurrentUICulture.Name;
                var v = SpeechSynthesizer.AllVoices.FirstOrDefault(x => x.Language.Equals(lang, StringComparison.OrdinalIgnoreCase))
                        ?? SpeechSynthesizer.AllVoices.FirstOrDefault(x => x.Language.StartsWith("de", StringComparison.OrdinalIgnoreCase));
                if (v != null) sy.Voice = v;
            }
            catch { }
            return sy;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DynamicIsland
{
    /// <summary>
    /// Zeigt in der Island, was Claude Code gerade macht und wie lange schon.
    /// Claude Code schickt dafür bei jedem Schritt (Hooks) ein kleines JSON an
    ///   http://localhost:7843/claude
    /// Alles bleibt auf deinem PC.
    /// </summary>
    public sealed class ClaudeTracker
    {
        public static readonly Color Orange = Color.FromRgb(0xD9, 0x77, 0x57);
        const string HookUrl = "http://localhost:7843/claude"; // = LocalApi.Port
        /// <summary>Befehl, den die Hooks ausführen: schickt das Hook-JSON an die Island, Fehler werden ignoriert.</summary>
        public const string HookCommand = "curl -s -m 2 --data-binary @- " + HookUrl + " >/dev/null 2>&1 || true";

        readonly IslandWindow host;
        readonly Dictionary<string, ClaudeActivity> sessions = new Dictionary<string, ClaudeActivity>();

        public ClaudeTracker(IslandWindow host) => this.host = host;

        public DateTime LastEvent { get; private set; }

        // ---- Nutzungs-Limits (aus der Statuszeile von Claude Code: rate_limits) ----
        const string LimitsUrl = "http://localhost:7843/claude-limits";
        /// <summary>Statuszeilen-Befehl: schickt das JSON an die Island und zeigt selbst nichts an.</summary>
        public const string StatusCommand = "curl -s -m 1 --data-binary @- " + LimitsUrl + " >/dev/null 2>&1; true";
        public static double? FivePct, WeekPct;
        public static DateTime FiveReset, WeekReset, LimitsAt;
        int fiveWarned, weekWarned;          // schon gewarnt bei 80 / 95 %
        DateTime fiveWarnReset, weekWarnReset;

        public void HandleLimits(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return;
            JsonNode j;
            try { j = JsonNode.Parse(body); } catch { return; }
            var rl = j?["rate_limits"];
            if (rl == null) return;
            static double? Num(JsonNode n)
            {
                if (n == null) return null;
                try { return n.GetValue<double>(); } catch { }
                try { return n.GetValue<long>(); } catch { }
                return double.TryParse(n.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (double?)null;
            }
            static (double?, DateTime) Win(JsonNode w)
            {
                if (w == null) return (null, default);
                double? p = Num(w["used_percentage"]);
                DateTime r = default;
                var ra = w["resets_at"];
                double? sec = Num(ra);
                if (sec > 0) r = DateTimeOffset.FromUnixTimeMilliseconds((long)(sec.Value > 1e11 ? sec.Value : sec.Value * 1000)).LocalDateTime;   // Sekunden (oder ms)
                else if (ra != null && DateTimeOffset.TryParse(ra.ToString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var dto)) r = dto.LocalDateTime;
                if (p != null) p = Math.Max(0, Math.Min(100, p.Value));
                return (p, r);
            }
            (FivePct, FiveReset) = Win(rl["five_hour"]);
            (WeekPct, WeekReset) = Win(rl["seven_day"]);
            LimitsAt = DateTime.Now;
            if (!host.Settings.ClaudeEnabled || !host.Settings.ClaudeLimitWarn) return;
            CheckWarn("5-Stunden-Limit", FivePct, FiveReset, ref fiveWarned, ref fiveWarnReset);
            CheckWarn("Wochen-Limit", WeekPct, WeekReset, ref weekWarned, ref weekWarnReset);
        }

        void CheckWarn(string name, double? pct, DateTime reset, ref int warned, ref DateTime warnReset)
        {
            if (pct == null) return;
            if (reset != warnReset) { warnReset = reset; warned = 0; }   // neues Fenster → wieder warnen
            int level = pct >= 95 ? 95 : pct >= 80 ? 80 : 0;
            if (level <= warned) return;
            warned = level;
            double p = pct.Value;
            string when = reset == default ? "" : " · wieder frei " + ResetText(reset);
            host.Alert("claude-limit", 8, g =>
            {
                g.Banner = true;
                g.Glyph = "✻";
                g.GlyphFont = Ui.SymbolFont;
                g.Accent = level >= 95 ? Ui.Red : Ui.Orange;
                g.Title = "Claude: " + name + " bei " + Math.Round(p) + " %";
                g.Subtitle = (level >= 95 ? "Fast aufgebraucht" : "Schon viel genutzt") + when;
                g.Progress = Math.Min(1, p / 100);
            });
        }

        public static string ResetText(DateTime r)
        {
            if (r == default) return "";
            return r.Date == DateTime.Today ? "um " + r.ToString("HH:mm")
                 : r.Date == DateTime.Today.AddDays(1) ? "morgen " + r.ToString("HH:mm")
                 : r.ToString("ddd HH:mm", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        }

        /// <summary>Kurze Zeile für die Anzeige, z. B. „Limit: 5 Std 23 % · Woche 41 %“ (null, wenn nichts bekannt).</summary>
        public static string LimitsLine()
        {
            if ((FivePct == null && WeekPct == null) || (DateTime.Now - LimitsAt).TotalHours > 12) return null;
            var parts = new List<string>();
            if (FivePct != null) parts.Add("5 Std " + Math.Round(FivePct.Value) + " %" + (FiveReset != default ? " (frei " + ResetText(FiveReset) + ")" : ""));
            if (WeekPct != null) parts.Add("Woche " + Math.Round(WeekPct.Value) + " %");
            return "Limit: " + string.Join("  ·  ", parts);
        }

        public static bool LimitsInstalled()
        {
            try { return File.Exists(ClaudeSettingsPath) && File.ReadAllText(ClaudeSettingsPath).Contains(LimitsUrl); }
            catch { return false; }
        }

        /// <summary>Hat Claude Code schon eine eigene Statuszeile (dann überschreiben wir sie nicht)?</summary>
        public static bool ForeignStatusLine()
        {
            try
            {
                if (!File.Exists(ClaudeSettingsPath)) return false;
                var root = JsonNode.Parse(File.ReadAllText(ClaudeSettingsPath), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
                return root?["statusLine"] != null && !root["statusLine"].ToJsonString().Contains(LimitsUrl);
            }
            catch { return false; }
        }

        public void Handle(string body, NameValueCollection q)
        {
            if (!host.Settings.ClaudeEnabled || string.IsNullOrWhiteSpace(body)) return;
            JsonNode j;
            try { j = JsonNode.Parse(body); }
            catch { return; }
            if (j == null) return;
            LastEvent = DateTime.Now;

            string ev = Str(j, "hook_event_name");
            string sid = Str(j, "session_id");
            if (string.IsNullOrEmpty(sid)) sid = "claude";
            string cwd = Str(j, "cwd");
            sessions.TryGetValue(sid, out var act);
            if (act != null && act.Removed) { sessions.Remove(sid); act = null; }
            string transcript = Str(j, "transcript_path");
            if (act != null && transcript != "") act.Transcript = transcript;

            switch (ev)
            {
                case "UserPromptSubmit":
                    act = Ensure(sid, cwd, act);
                    if (transcript != "") act.Transcript = transcript;
                    act.StartTask(Str(j, "prompt"));
                    host.Relayout();   // nicht hervorholen – nur der Kreis erscheint
                    break;

                case "PreToolUse":
                {
                    // Die Hooks laufen im Hintergrund und können sich überholen: ein verspätetes „Werkzeug startet“
                    // direkt nach „Fertig“ ignorieren (sonst hinge der Kreis als „arbeitet“). Kommt es später, arbeitet
                    // Claude ohne neue Nachricht weiter (z. B. Fortsetzung in der Claude-App) → neue Aufgabe.
                    if (act != null && act.State == ClaudeState.Done && act.SinceDone.TotalSeconds < 4) break;
                    bool fresh = act == null || act.State == ClaudeState.Done;
                    act = Ensure(sid, cwd, act);
                    if (transcript != "") act.Transcript = transcript;
                    // Beginn der Aufgabe = Zeit deiner letzten Nachricht im Verlauf (nicht „jetzt“ – z. B. nach Neustart der Island)
                    if (fresh) act.AdoptStart(LastPromptTime(act.Transcript));
                    bool wasIdle = act.State != ClaudeState.Working;
                    act.ToolStarted(Describe(Str(j, "tool_name"), j["tool_input"]));
                    if (wasIdle) host.Relayout();
                    break;
                }

                case "PostToolUse":
                case "PostToolUseFailure":
                    if (act != null) act.ToolDone();
                    break;

                case "PreCompact":
                    // „/compact“ von Hand startet keine neue Aufgabe und endet ohne „Stop“ – sonst bliebe ein Kreis mit Zähler hängen.
                    // Nur eine laufende Aufgabe bekommt den Hinweis.
                    if (act != null && act.State == ClaudeState.Working) act.ToolStarted("Fasst das Gespräch zusammen");
                    break;

                case "Notification":
                {
                    string msg = Str(j, "message");
                    bool permission = msg.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0
                                   || msg.IndexOf("Berechtigung", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (act == null || act.State == ClaudeState.Done)
                    {
                        // Sitzung ist fertig oder unbekannt: kurzer Hinweis
                        act = Ensure(sid, cwd, act);
                        act.Waiting(permission ? "Braucht deine Erlaubnis" : "Wartet auf deine Antwort", alertSeconds: permission ? 0 : 8);
                    }
                    else act.Waiting(permission ? "Braucht deine Erlaubnis" : "Wartet auf dich");
                    host.Nudge();
                    break;
                }

                case "Stop":
                    if (act != null && act.State != ClaudeState.Done) { act.Done(); host.Relayout(); host.ClaudeFinished(act); }
                    break;

                case "SessionEnd":
                    if (act != null) { act.Finished = true; sessions.Remove(sid); host.Relayout(); }
                    break;
            }
        }

        /// <summary>Zeit der letzten echten Nachricht im Gesprächsverlauf (Zeitstempel sind UTC → Ortszeit). null = unbekannt.</summary>
        public static DateTime? LastPromptTime(string transcript)
        {
            try
            {
                if (string.IsNullOrEmpty(transcript) || !File.Exists(transcript)) return null;
                using var fs = new FileStream(transcript, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                long n = Math.Min(fs.Length, 400_000);
                fs.Seek(-n, SeekOrigin.End);
                var buf = new byte[n];
                int read = fs.Read(buf, 0, (int)n);
                var lines = System.Text.Encoding.UTF8.GetString(buf, 0, read).Split((char)10);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    var l = lines[i];
                    if (!l.Contains("\"type\":\"user\"") || l.Contains("\"tool_result\"") || l.Contains("\"isMeta\":true") || l.Contains("\"isSidechain\":true")) continue;
                    int k = l.IndexOf("\"timestamp\":\"", StringComparison.Ordinal);
                    if (k < 0) continue;
                    int e = l.IndexOf((char)34, k + 13);
                    if (e < 0) continue;
                    if (DateTimeOffset.TryParse(l.Substring(k + 13, e - k - 13), System.Globalization.CultureInfo.InvariantCulture,
                                                System.Globalization.DateTimeStyles.AssumeUniversal, out var t))
                    {
                        var local = t.LocalDateTime;
                        // nur plausible Werte (nicht in der Zukunft, nicht älter als 12 Stunden)
                        return local <= DateTime.Now.AddSeconds(5) && local > DateTime.Now.AddHours(-12) ? local : (DateTime?)null;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>Alle Claude-Kreise entfernen (z. B. wenn „Claude in der Island zeigen“ ausgeschaltet wird).</summary>
        public void Clear()
        {
            foreach (var a in sessions.Values) a.Finished = true;
            sessions.Clear();
        }

        ClaudeActivity Ensure(string sid, string cwd, ClaudeActivity act)
        {
            if (act != null) { act.SetProject(cwd); return act; }
            act = new ClaudeActivity("claude-" + (sid.Length > 12 ? sid.Substring(0, 12) : sid));
            act.SetProject(cwd);
            sessions[sid] = act;
            host.Add(act);
            return act;
        }

        static string Str(JsonNode j, string key)
        {
            try { return j?[key]?.GetValue<string>() ?? ""; }
            catch { return j?[key]?.ToString() ?? ""; }
        }

        static string Short(string s, int max)
        {
            s = (s ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s.Length <= max ? s : s.Substring(0, max - 1).TrimEnd() + "…";
        }

        static string FileOf(JsonNode input)
        {
            var p = Str(input, "file_path");
            if (p == "") p = Str(input, "notebook_path");
            if (p == "") p = Str(input, "path");
            if (p == "") return "";
            try { return Path.GetFileName(p.TrimEnd('\\', '/')); } catch { return p; }
        }

        /// <summary>Macht aus Werkzeug + Eingabe einen kurzen deutschen Satz.</summary>
        public static string Describe(string tool, JsonNode input)
        {
            string file = FileOf(input);
            switch (tool)
            {
                case "Read": return file != "" ? "Liest " + file : "Liest Dateien";
                case "Edit":
                case "MultiEdit":
                case "NotebookEdit": return file != "" ? "Bearbeitet " + file : "Bearbeitet Code";
                case "Write": return file != "" ? "Schreibt " + file : "Schreibt eine Datei";
                case "Bash":
                case "PowerShell":
                {
                    var d = Str(input, "description");
                    return d != "" ? Short(d, 46) : "Führt aus: " + Short(Str(input, "command"), 36);
                }
                case "Grep": return "Sucht „" + Short(Str(input, "pattern"), 28) + "“";
                case "Glob": return "Sucht Dateien " + Short(Str(input, "pattern"), 28);
                case "WebFetch":
                {
                    var u = Str(input, "url");
                    try { u = new Uri(u).Host; } catch { }
                    return "Liest " + Short(u, 36);
                }
                case "WebSearch": return "Sucht im Web: " + Short(Str(input, "query"), 30);
                case "Task":
                case "Agent": return "Helfer: " + Short(Str(input, "description"), 38);
                case "TodoWrite":
                case "TaskCreate":
                case "TaskUpdate": return "Plant die Schritte";
                case "Skill": return "Lädt Fähigkeit " + Short(Str(input, "skill"), 30);
                case "AskUserQuestion": return "Stellt dir eine Frage";
                case "ToolSearch": return "Sucht Werkzeuge";
                case "Artifact": return "Erstellt eine Seite";
            }
            if (tool.StartsWith("mcp__"))
            {
                var parts = tool.Split(new[] { "__" }, StringSplitOptions.None);
                string server = parts.Length > 1 ? parts[1] : tool;
                string name = parts.Length > 2 ? parts[2] : "";
                if (name == "update_plan") return "Plant die Schritte";
                if (name == "wrap_up") return "Fasst zusammen";
                if (server.IndexOf("Browser", StringComparison.OrdinalIgnoreCase) >= 0 || server.Contains("chrome")) return "Benutzt den Browser";
                if (server == "computer-use") return "Bedient den PC";
                return "Benutzt " + server.Replace('_', ' ').Replace('-', ' ');
            }
            return string.IsNullOrEmpty(tool) ? "Arbeitet" : "Benutzt " + tool;
        }

        // -------------------------------------------------------------
        //  Hooks in Claude Code eintragen (~/.claude/settings.json)
        // -------------------------------------------------------------
        static readonly string[] HookEvents = { "UserPromptSubmit", "PreToolUse", "PostToolUse", "Notification", "Stop", "SessionEnd", "PreCompact" };

        public static string ClaudeSettingsPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

        /// <summary>Ist Claude Code (oder die Claude-App) auf diesem PC installiert?</summary>
        public static bool ClaudeInstalled()
        {
            try
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Directory.Exists(Path.Combine(home, ".claude"))
                       || File.Exists(Path.Combine(home, ".local", "bin", "claude.exe"))
                       || Directory.Exists(Path.Combine(local, "AnthropicClaude"));
            }
            catch { return false; }
        }

        /// <summary>Sind unsere Hooks schon in Claude Code eingetragen?</summary>
        public static bool HooksInstalled()
        {
            try { return File.Exists(ClaudeSettingsPath) && File.ReadAllText(ClaudeSettingsPath).Contains(HookUrl); }
            catch { return false; }
        }

        /// <summary>Trägt die Hooks ein (vorhandene Einstellungen und Hooks bleiben erhalten). Gibt eine Fehlermeldung oder null zurück.</summary>
        public static string InstallHooks()
        {
            try
            {
                var path = ClaudeSettingsPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                JsonObject root = null;
                if (File.Exists(path))
                {
                    var text = File.ReadAllText(path);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
                        if (root == null) return "Die Datei settings.json von Claude Code ist kein gültiges JSON.";
                        File.Copy(path, path + ".bak-dynamicisland", true);
                    }
                }
                root ??= new JsonObject();
                var hooks = root["hooks"] as JsonObject;
                if (hooks == null) { hooks = new JsonObject(); root["hooks"] = hooks; }
                foreach (var ev in HookEvents)
                {
                    var list = hooks[ev] as JsonArray;
                    if (list == null) { list = new JsonArray(); hooks[ev] = list; }
                    if (list.ToJsonString().Contains(HookUrl)) continue;
                    var entry = new JsonObject
                    {
                        // async: Claude wartet nicht auf die Island – kostet keine Zeit
                        ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = HookCommand, ["timeout"] = 5, ["async"] = true })
                    };
                    if (ev == "PreToolUse" || ev == "PostToolUse") entry["matcher"] = "*";
                    list.Add(entry);
                }
                // Statuszeile für die Nutzungs-Limits – nur, wenn noch keine eigene da ist
                if (root["statusLine"] == null)
                    root["statusLine"] = new JsonObject { ["type"] = "command", ["command"] = StatusCommand };
                File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                return null;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                return ex.Message;
            }
        }

        /// <summary>Entfernt nur unsere Hooks wieder.</summary>
        public static string RemoveHooks()
        {
            try
            {
                var path = ClaudeSettingsPath;
                if (!File.Exists(path)) return null;
                var root = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
                if (root?["statusLine"] != null && root["statusLine"].ToJsonString().Contains(LimitsUrl)) root.Remove("statusLine");
                if (!(root?["hooks"] is JsonObject hooks)) { File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true })); return null; }
                foreach (var ev in hooks.Select(kv => kv.Key).ToList())
                {
                    if (!(hooks[ev] is JsonArray list)) continue;
                    for (int i = list.Count - 1; i >= 0; i--)
                        if (list[i] != null && list[i].ToJsonString().Contains(HookUrl)) list.RemoveAt(i);
                    if (list.Count == 0) hooks.Remove(ev);
                }
                if (hooks.Count == 0) root.Remove("hooks");
                File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                return null;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                return ex.Message;
            }
        }
    }

    public enum ClaudeState { Working, Waiting, Done }

    /// <summary>Live-Aktivität für eine Claude-Code-Sitzung: was gerade passiert + wie lange schon.</summary>
    public sealed class ClaudeActivity : GenericActivity
    {
        public ClaudeState State { get; private set; } = ClaudeState.Working;
        public bool Removed { get; private set; }

        string project = "", action = "Denkt nach …", prompt = "";
        DateTime taskStart = DateTime.Now, stepStart = DateTime.Now, lastPre = DateTime.Now, lastPost = DateTime.MinValue, lastEvent = DateTime.Now;
        int steps;
        TimeSpan doneAfter;

        public ClaudeActivity(string id)
        {
            Id = id;
            Priority = 60;
            Glyph = "✻";               // ✻ (kein Emoji – das ✳ zeichnet Windows als buntes, abgeschnittenes Emoji)
            GlyphFont = Ui.SymbolFont;
            Accent = ClaudeTracker.Orange;
            CompactW = 310;
            Ticker = _ => Tick();
            Tick();
        }

        public override void OnRemoved() => Removed = true;
        /// <summary>Gesprächsverlauf von Claude Code (aus den Hooks) – verrät, ob mit Esc abgebrochen wurde.</summary>
        public string Transcript { get; set; } = "";
        public string Project => project;
        public TimeSpan DoneAfter => doneAfter;

        // ---------------------------------------------------------------
        //  Kleiner Kreis neben der Island: Ring (dreht sich, solange Claude arbeitet) + Dauer in der Mitte.
        //  Drüberfahren macht ihn breit: was gerade passiert + wie lange schon. Antippen = große Ansicht.
        // ---------------------------------------------------------------
        public override bool PreferBubble => true;
        public override double BubbleHoverWidth => 270;

        TextBlock bubbleTime, wideAction, wideTime;
        System.Windows.Shapes.Ellipse bubbleTrack;
        System.Windows.Shapes.Path bubbleArc;
        System.Windows.Media.RotateTransform arcSpin;
        ClaudeState bubbleState = (ClaudeState)(-1);

        /// <summary>Farbe für „fertig“ – die bei „Claude ist fertig“ eingestellte Farbe (setzt die Island).</summary>
        public static Func<Color> DoneColor = () => Ui.Green;
        Color StateColor => State == ClaudeState.Working ? ClaudeTracker.Orange : State == ClaudeState.Waiting ? Ui.Yellow : DoneColor();

        public override FrameworkElement BuildMinimal()
        {
            var g = new Grid { Width = 32, Height = 32 };
            bubbleTrack = new System.Windows.Shapes.Ellipse { Width = 28, Height = 28, StrokeThickness = 2.4 };
            // Bogen (¼ Kreis) oben rechts, dreht sich um die Mitte
            bubbleArc = new System.Windows.Shapes.Path
            {
                Data = System.Windows.Media.Geometry.Parse("M 16,2 A 14,14 0 0 1 30,16"),
                StrokeThickness = 2.6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            arcSpin = new System.Windows.Media.RotateTransform(0, 16, 16);
            bubbleArc.RenderTransform = arcSpin;
            bubbleTime = new TextBlock
            {
                FontFamily = Ui.TextFont,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = Ui.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            g.Children.Add(bubbleTrack);
            g.Children.Add(bubbleArc);
            g.Children.Add(bubbleTime);
            bubbleState = (ClaudeState)(-1);
            RefreshBubble();
            return g;
        }

        public override FrameworkElement BuildBubbleHover()
        {
            var ring = BuildMinimal();
            ring.Margin = new Thickness(2, 0, 0, 0);
            wideAction = Ui.Text("", 12.5, Ui.White, true, true);
            wideAction.MaxWidth = 170;
            wideTime = Ui.Text("", 12.5, Ui.Br(StateColor), true, true);
            wideTime.Margin = new Thickness(8, 0, 12, 0);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            wideAction.Margin = new Thickness(7, 0, 0, 0);
            wideAction.VerticalAlignment = wideTime.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(wideAction, 1);
            Grid.SetColumn(wideTime, 2);
            g.Children.Add(ring);
            g.Children.Add(wideAction);
            g.Children.Add(wideTime);
            g.Width = BubbleHoverWidth;
            RefreshBubble();
            return g;
        }

        static string ShortDur(TimeSpan t) =>
            t.TotalSeconds < 60 ? $"{(int)t.TotalSeconds}s" : t.TotalMinutes < 60 ? $"{(int)t.TotalMinutes}m" : $"{(int)t.TotalHours}h";

        void RefreshBubble()
        {
            var c = StateColor;
            if (bubbleTrack != null && bubbleState != State)
            {
                bubbleState = State;
                bubbleTrack.Stroke = Ui.Br(c, State == ClaudeState.Working ? (byte)55 : (byte)255);
                bubbleArc.Stroke = Ui.Br(c);
                bubbleArc.Visibility = State == ClaudeState.Working ? Visibility.Visible : Visibility.Collapsed;
                if (State == ClaudeState.Working && !Ui.ReduceMotion)
                    arcSpin.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty,
                        new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.1)) { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
                else arcSpin.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
            }
            var total = State == ClaudeState.Done ? doneAfter : DateTime.Now - taskStart;
            if (bubbleTime != null)
            {
                bubbleTime.Text = State == ClaudeState.Waiting ? "!" : State == ClaudeState.Done ? "✓" : ShortDur(total);
                Ui.SetFg(bubbleTime, State == ClaudeState.Working ? Ui.WhiteC : c);
                bubbleTime.FontSize = State == ClaudeState.Working ? 10 : 14;
            }
            if (wideAction != null) { wideAction.Text = LeftText ?? ""; Ui.SetFg(wideAction, State == ClaudeState.Working ? Ui.WhiteC : c); }
            if (wideTime != null) { wideTime.Text = RightText ?? ""; Ui.SetFg(wideTime, c); }
        }

        public void SetProject(string cwd)
        {
            if (string.IsNullOrWhiteSpace(cwd)) return;
            try { project = Path.GetFileName(cwd.TrimEnd('\\', '/')); } catch { project = cwd; }
        }

        void Live()
        {
            IsAlert = false;
            ExpiresAt = null;
            lastEvent = DateTime.Now;
        }

        public void StartTask(string text)
        {
            Live();
            State = ClaudeState.Working;
            taskStart = stepStart = lastPre = DateTime.Now;
            lastPost = DateTime.MinValue;
            steps = 0;
            prompt = text ?? "";
            action = "Denkt nach …";
            Tick();
        }

        public TimeSpan SinceDone => State == ClaudeState.Done ? DateTime.Now - doneAt : TimeSpan.MaxValue;
        DateTime doneAt;

        /// <summary>Aufgabe hat schon früher begonnen (Zeit aus dem Verlauf) – nur, wenn sie nach dem letzten „Fertig“ liegt.</summary>
        public void AdoptStart(DateTime? t)
        {
            if (t == null) return;
            if (State == ClaudeState.Done && t.Value < doneAt) return;   // alte Nachricht, gehört zur vorigen Aufgabe
            taskStart = t.Value;
            if (State == ClaudeState.Done) { State = ClaudeState.Working; steps = 0; }
        }

        public void ToolStarted(string what)
        {
            Live();
            if (State == ClaudeState.Done) { taskStart = DateTime.Now; steps = 0; }
            State = ClaudeState.Working;
            action = what;
            steps++;
            stepStart = lastPre = DateTime.Now;
            Tick();
        }

        public void ToolDone()
        {
            lastEvent = lastPost = DateTime.Now;
            if (State == ClaudeState.Waiting) State = ClaudeState.Working;
        }

        public void Waiting(string why, double alertSeconds = 0)
        {
            Live();
            State = ClaudeState.Waiting;
            action = why;
            stepStart = DateTime.Now;
            if (alertSeconds > 0) ExpiresAt = DateTime.Now.AddSeconds(alertSeconds);
            Tick();
        }

        public void Done()
        {
            lastEvent = DateTime.Now;
            doneAfter = DateTime.Now - taskStart;
            doneAt = DateTime.Now;
            State = ClaudeState.Done;
            action = "Fertig";
            // Kreis mit ✓ nur kurz zeigen: die Meldung dauert 4,5 s, danach steht er noch ~1,5 s und verschwindet
            ExpiresAt = DateTime.Now.AddSeconds(6);
            Tick();
        }

        // ---------------------------------------------------------------
        //  Läuft Claude überhaupt noch?  Abbrechen mit Esc, ein geschlossenes Fenster oder ein Absturz
        //  schicken kein „Stop“ – ohne diese Prüfung bliebe der orange Kreis mit Zähler stehen.
        // ---------------------------------------------------------------
        DateTime nextCheck = DateTime.Now.AddSeconds(2), transcriptWrite = DateTime.MinValue;   // nicht schon im Konstruktor prüfen
        bool sawClaudeProcess;
        long transcriptLen = -1;
        static DateTime nextProcCheck = DateTime.MinValue;
        static bool claudeRunning = true;

        bool StoppedWithoutHook(DateTime now)
        {
            if (now < nextCheck) return false;
            nextCheck = now.AddSeconds(2);

            // 1. Kein Claude-Prozess mehr (Fenster zu, Absturz) → sofort weg. Nur wenn während dieser Aufgabe schon einmal
            //    ein „claude“-Prozess lief – per npm installiert läuft Claude Code als node.exe, dann zählen nur Verlauf und Stille.
            if (now >= nextProcCheck)
            {
                nextProcCheck = now.AddSeconds(5);
                try
                {
                    var procs = System.Diagnostics.Process.GetProcessesByName("claude");
                    claudeRunning = procs.Length > 0;
                    foreach (var p in procs) p.Dispose();
                }
                catch { claudeRunning = true; }
            }
            if (claudeRunning) sawClaudeProcess = true;
            else if (sawClaudeProcess) return true;

            // 2. Im Verlauf steht „abgebrochen“ (Esc) → weg
            DateTime lastWrite = DateTime.MinValue;
            try
            {
                if (Transcript != "" && File.Exists(Transcript))
                {
                    var fi = new FileInfo(Transcript);
                    lastWrite = fi.LastWriteTime;
                    if (fi.Length != transcriptLen)
                    {
                        transcriptLen = fi.Length;
                        transcriptWrite = lastWrite;
                        if (lastWrite > taskStart && TailSaysInterrupted(fi)) return true;
                    }
                }
            }
            catch { }

            // 3. Lange nichts mehr gehört → aufräumen. Läuft gerade ein Werkzeug (z. B. ein langer Befehl), großzügig warten.
            var quiet = now - (transcriptWrite > lastEvent ? transcriptWrite : lastEvent);
            bool inTool = State == ClaudeState.Working && lastPre > lastPost && action != "Denkt nach …";
            if (State == ClaudeState.Waiting) return quiet.TotalMinutes > 30;
            return quiet.TotalMinutes > (inTool ? 12 : 6);
        }

        static bool TailSaysInterrupted(FileInfo fi)
        {
            using var fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long n = Math.Min(fs.Length, 6000);
            fs.Seek(-n, SeekOrigin.End);
            var buf = new byte[n];
            int read = fs.Read(buf, 0, (int)n);
            var text = System.Text.Encoding.UTF8.GetString(buf, 0, read);
            // Die NEUESTE echte Nutzer-Zeile entscheidet (Werkzeug-Ergebnisse übersprungen): ist sie der Abbruch-Vermerk → abgebrochen.
            // Ein älterer Vermerk vor einer neuen Nachricht zählt nicht mehr.
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - 1; i >= 0 && i >= lines.Length - 8; i--)
            {
                var l = lines[i];
                if (l.Contains("\"type\":\"assistant\"")) return false;
                if (!l.Contains("\"type\":\"user\"")) continue;
                if (l.Contains("\"tool_result\"") || l.Contains("\"tool_use_id\"")) continue;
                // genau der Vermerk als eigener Text (unmaskiert) – nicht, wenn er nur zitiert in einer Werkzeug-Ausgabe steht
                return l.Contains("\"text\":\"[Request interrupted by user");
            }
            return false;
        }

        public static string Dur(TimeSpan t) => t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{(int)t.TotalMinutes}:{t.Seconds:00}";

        void Tick()
        {
            var now = DateTime.Now;
            // ein Werkzeug ist fertig und noch kein neues gestartet → Claude denkt nach
            if (State == ClaudeState.Working && lastPost > lastPre && (now - lastPost).TotalSeconds > 1.5)
            {
                action = "Denkt nach …";
                stepStart = lastPost;
                lastPre = now;
            }
            if (State != ClaudeState.Done && StoppedWithoutHook(now)) { Finished = true; return; }

            var total = State == ClaudeState.Done ? doneAfter : now - taskStart;
            switch (State)
            {
                case ClaudeState.Working:
                    Accent = ClaudeTracker.Orange;
                    Glyph = "✻";
                    break;
                case ClaudeState.Waiting:
                    Accent = Ui.Yellow;
                    Glyph = "!";
                    break;
                default:
                    Accent = DoneColor();
                    Glyph = "✓";
                    break;
            }

            // fertig / wartet in der eingestellten Farbe statt weiß (Pille, große Ansicht, breiter Kreis)
            Color? stateCol = State == ClaudeState.Done ? DoneColor() : State == ClaudeState.Waiting ? Ui.Yellow : (Color?)null;
            LeftColor = stateCol;
            TitleColor = stateCol;
            LeftText = State == ClaudeState.Done ? "Claude ist fertig" : action;
            RightText = Dur(total);
            Title = "Claude" + (project != "" ? " · " + project : "");
            Subtitle = State == ClaudeState.Done ? $"Fertig nach {Dur(doneAfter)}" : action;
            var lines = new List<string>();
            if (State == ClaudeState.Working)
                lines.Add($"Dieser Schritt: {Dur(now - stepStart)}  ·  {steps} {(steps == 1 ? "Aktion" : "Aktionen")}");
            else if (State == ClaudeState.Done)
                lines.Add($"{steps} {(steps == 1 ? "Aktion" : "Aktionen")}");
            if (prompt != "")
            {
                var p = prompt.Replace('\r', ' ').Replace('\n', ' ').Trim();
                lines.Add("„" + (p.Length > 70 ? p.Substring(0, 69).TrimEnd() + "…" : p) + "“");
            }
            var lim = ClaudeTracker.LimitsLine();
            if (lim != null) lines.Add(lim);
            Detail = string.Join("\n", lines);
            RefreshBubble();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace DynamicIsland
{
    /// <summary>Eine eigene Seite mit bis zu 4 Widgets.</summary>
    public sealed class WidgetPageConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "Widgets";
        public List<string> Widgets { get; set; } = new List<string>();
    }

    /// <summary>Eigene Darstellung einer Seite (0 / null = wie bei allen Seiten).</summary>
    public sealed class PageLook
    {
        public double Font { get; set; }
        public string Style { get; set; }
    }

    public sealed class Settings
    {
        public bool IdlePill { get; set; } = true;
        public bool HideInFullscreen { get; set; } = true;
        public bool ShowVolume { get; set; } = true;
        public bool ShowClipboard { get; set; } = true;
        public bool ShowCapsLock { get; set; } = true;
        public bool ShowBluetooth { get; set; } = true;
        public bool ShowPrivacy { get; set; } = true;
        /// <summary>Im Leerlauf nach ein paar Sekunden nach oben wegfahren.</summary>
        public bool AutoHide { get; set; } = true;
        /// <summary>Auch ausblenden, während Musik, Timer oder Downloads laufen.</summary>
        public bool HideWithActivities { get; set; } = true;
        /// <summary>Sekunden bis zum Ausblenden.</summary>
        public double HideDelay { get; set; } = 3;
        /// <summary>Größe der Island (1 = normal).</summary>
        public double Scale { get; set; } = 1;
        /// <summary>Größe der großen (aufgeklappten) Island: 0,75 klein · 0,85 mittel · 1 groß.</summary>
        public double PageScale { get; set; } = 0.8;
        /// <summary>Breite der großen Island im Verhältnis zur Höhe: 1 normal · 1,15 breit · 1,3 extra breit.</summary>
        public double PageWide { get; set; } = 1.3;
        /// <summary>Seitensymbole oben in der großen Island farbig (sonst alle weiß).</summary>
        public bool NavIconsColor { get; set; } = true;
        /// <summary>Schrift in den Seiten der großen Island – unabhängig von deren Größe (1 = wie entworfen).</summary>
        public double PageFont { get; set; } = 1.2;
        /// <summary>Stil der Seiten: „clean“ = aufgeräumt (weniger, größer) · „full“ = ausführlich.</summary>
        public string PageStyle { get; set; } = "clean";
        /// <summary>Pro Seite eigene Schrift / eigener Stil (Schlüssel wie in Pages).</summary>
        public Dictionary<string, PageLook> PageLooks { get; set; } = new Dictionary<string, PageLook>();
        /// <summary>Animation, wenn etwas fertig ist (Claude, Timer, Download): off, glow, bounce, both.</summary>
        public string DoneAnimation { get; set; } = "both";
        /// <summary>Bei welchen Ereignissen die Animation läuft (claude, timer, download, copy, message, device).</summary>
        public List<string> AnimEvents { get; set; } = new List<string> { "claude", "timer", "download", "copy" };
        /// <summary>Animation pro Ereignis: off, app (Island wird länger: App-Symbol + Inhalt), glow, bounce, both.</summary>
        public Dictionary<string, string> AnimModes { get; set; }
        /// <summary>Licht-Effekt bei „Licht“: ring (Rahmen pulsiert), orbit (Licht läuft einmal um die Island), halo (weicher Schein), shine (Glanz zieht durch).</summary>
        public string GlowStyle { get; set; } = "orbit";
        /// <summary>Licht-Effekt pro Ereignis (leer/fehlt = der allgemeine GlowStyle).</summary>
        public Dictionary<string, string> GlowStyles { get; set; } = new Dictionary<string, string>();
        /// <summary>Hinweis-Design (alter Name „AlertSize“ bleibt für die Kompatibilität): small, normal, ios, minimal, glow.</summary>
        /// <summary>Design der Hinweise: small (klein), normal (groß), ios (Mitteilung), minimal, glow (leuchtend).</summary>
        public string AlertSize { get; set; } = "small";
        /// <summary>Pille bleibt genau in der Mitte, auch wenn rechts der kleine Kreis erscheint.</summary>
        public bool IslandFixedCenter { get; set; } = true;
        /// <summary>Wie Hinweise erscheinen: slide (sanft hineinschieben), morph (Apple-Tropfen mit Federn), none (ohne).</summary>
        public string AlertMotion { get; set; } = "slide";
        /// <summary>Reihenfolge, wenn mehrere Hinweise gleichzeitig kommen (oben = zuerst; höhere Priorität unterbricht).</summary>
        public List<string> AlertPriority { get; set; } = new List<string> { "call", "message", "claude", "reminder", "timer", "download", "device", "copy", "other" };
        /// <summary>Zuletzt gewählte Timer-Dauer in Sekunden.</summary>
        public int LastTimerSeconds { get; set; } = 300;
        /// <summary>Eigene Farbe pro Ereignis (Hex, leer = Standardfarbe).</summary>
        public Dictionary<string, string> AnimColors { get; set; } = new Dictionary<string, string>();
        public int LayoutVersion { get; set; }
        /// <summary>Bedienelemente: "glass" = Apple Liquid Glass, "flat" = flach.</summary>
        public string Material { get; set; } = "glass";
        /// <summary>Glasfarbe der Knöpfe: "clear" (Kristall), "sapphire" oder "smoke".</summary>
        public string GlassTint { get; set; } = "clear";
        /// <summary>Akzentfarbe der Island (Regler, Häkchen, Symbole).</summary>
        public string AccentColor { get; set; } = "#0A84FF";
        /// <summary>Farbe des Spotify-Players.</summary>
        public string SpotifyColor { get; set; } = "#1ED760";
        /// <summary>Kacheln in der Übersicht, in dieser Reihenfolge (null = Standard).</summary>
        public List<string> DashboardTiles { get; set; }
        public int TilesVersion { get; set; }
        /// <summary>Seiten zum Wischen in der großen Island, in dieser Reihenfolge (bearbeitbar).</summary>
        public List<string> Pages { get; set; }
        /// <summary>Eigene Seiten mit Widgets.</summary>
        public List<WidgetPageConfig> WidgetPages { get; set; }
        public int PagesVersion { get; set; }

        // Uhr (Startseite, Uhr beim Drüberfahren, Uhr-Widget)
        // Kalender (iCal-Adressen, verschlüsselt)
        public List<string> CalendarUrls { get; set; } = new List<string>();
        public bool CalendarEnabled { get; set; } = true;
        public int CalendarRemind { get; set; } = 10;      // Minuten vorher, 0 = aus
        public bool CalendarSpeak { get; set; } = true;
        public bool CalendarAtStart { get; set; } = true;
        /// <summary>Wetter zusätzlich links auf der Kalender-Seite (früher Seite „Heute“).</summary>
        public bool CalendarWithWeather { get; set; }

        // Microsoft To Do
        public string TodoClientId { get; set; }
        public string TodoRefresh { get; set; }        // verschlüsselt (DPAPI)
        public string TodoListId { get; set; }
        public bool TodoShowAll { get; set; }
        public bool TodoReminders { get; set; } = true;
        public bool TodoSpeak { get; set; } = true;

        // KI (Gemini)
        public bool AiEnabled { get; set; } = true;
        public string GeminiKeyProtected { get; set; }   // verschlüsselt (DPAPI)
        public string GeminiModel { get; set; } = "gemini-3.5-flash-lite";
        /// <summary>Welche KI antwortet: gemini, openai (ChatGPT), grok, claude, mistral, deepseek, openrouter.</summary>
        public string AiProvider { get; set; } = "gemini";
        /// <summary>API-Schlüssel der anderen Anbieter (verschlüsselt, DPAPI).</summary>
        public Dictionary<string, string> AiKeysProtected { get; set; } = new Dictionary<string, string>();
        /// <summary>Gewähltes Modell je Anbieter (leer = Standard).</summary>
        public Dictionary<string, string> AiModelFor { get; set; } = new Dictionary<string, string>();
        public string AiTranslateTo { get; set; } = "Deutsch";
        public bool AiMiddleClickOcr { get; set; } = true;

        public bool Clock24 { get; set; } = true;
        public bool ClockSeconds { get; set; }
        /// <summary>short, long, week, numeric, off</summary>
        public string ClockDate { get; set; } = "short";
        /// <summary>bold, regular, light</summary>
        public string ClockStyle { get; set; } = "bold";
        /// <summary>medium, large, xl</summary>
        public string ClockSize { get; set; } = "large";
        public bool ClockBattery { get; set; } = true;
        public bool DashPlayer { get; set; } = true;
        /// <summary>Player auch pausiert dauerhaft erreichbar lassen.</summary>
        public bool KeepPlayer { get; set; } = true;
        /// <summary>Zwischenablage-Verlauf merken (nur im Arbeitsspeicher).</summary>
        public bool ClipHistory { get; set; } = true;
        /// <summary>Position: Abstand von der Bildschirmmitte (X) und vom oberen Rand (Y).</summary>
        public double PosX { get; set; } = 0;
        public double PosY { get; set; } = 0;
        /// <summary>Bildschirm, auf dem die Island sitzt (z. B. „\\.\DISPLAY2“; leer = Hauptbildschirm).</summary>
        public string MonitorName { get; set; } = "";

        // Katzen
        public bool ShowCats { get; set; } = true;
        /// <summary>"expanded" = nur wenn die Island groß ist, "always" = immer, "off" = aus.</summary>
        public string CatsMode { get; set; }
        public bool CatsStayVisible { get; set; } = true;
        /// <summary>Katzen rollen sich im Schlaf zusammen.</summary>
        public bool CatsCurl { get; set; } = true;
        /// <summary>Katzen neben der Island laufen ab und zu herum.</summary>
        public bool CatsWalk { get; set; } = true;
        /// <summary>Warnen, wenn das Claude-Limit (5 Std / Woche) bei 80 % bzw. 95 % ist.</summary>
        public bool ClaudeLimitWarn { get; set; } = true;

        // ---- Obsidian: auf die Island gezogene Texte / Dateien landen in einer Notiz ----
        public bool ObsidianEnabled { get; set; }
        /// <summary>Vault-Ordner ("" = der zuletzt in Obsidian geöffnete).</summary>
        public string ObsidianVault { get; set; } = "";
        /// <summary>"folder" = je Eintrag eine Datei im Inbox-Ordner (ObsidianFolder), "daily" = Tagesnotiz, "note" = feste Notiz (ObsidianNote).</summary>
        public string ObsidianTarget { get; set; } = "folder";
        /// <summary>Inbox-Ordner im Vault ("" = automatisch, z. B. „00 Inbox“).</summary>
        public string ObsidianFolder { get; set; } = "";
        public string ObsidianNote { get; set; } = "Eingang";
        /// <summary>Uhrzeit vor jeden Eintrag schreiben.</summary>
        public bool ObsidianTime { get; set; } = true;

        /// <summary>Akku von Logitech-Maus/-Tastatur lesen (HID++) und bei wenig Akku warnen.</summary>
        public bool LogitechEnabled { get; set; } = true;
        /// <summary>Unter wie viel Prozent Akku gewarnt wird (danach nochmal bei 5 %).</summary>
        public int LogitechWarnAt { get; set; } = 15;

        /// <summary>Laune der Katzen hängt von Tageszeit und Wetter ab.</summary>
        public bool CatsMoodAuto { get; set; } = true;
        public List<CatConfig> Cats { get; set; } = new List<CatConfig>();

        // Claude Code: zeigt in der Island, was Claude gerade macht (über Hooks)
        public bool ClaudeEnabled { get; set; } = true;

        // Wetter (Open-Meteo). Leerer Ort = Windows-Standort
        public bool WeatherEnabled { get; set; } = true;
        public string WeatherCity { get; set; } = "";
        /// <summary>Ohne Stadt den Windows-Standort benutzen (nur auf Wunsch).</summary>
        public bool WeatherUseLocation { get; set; }

        // Kurzbefehle (null = Standard-Liste beim ersten Start)
        public List<ShortcutConfig> Shortcuts { get; set; }

        // Downloads + Windows-Mitteilungen
        public bool ShowDownloads { get; set; } = true;
        public bool MirrorNotifications { get; set; } = true;
        /// <summary>Antworten aus der Island bei WhatsApp/Discord-DMs direkt absenden (sonst nur in den Chat einfügen).</summary>
        public bool ReplyAutoSend { get; set; } = true;

        // Discord (eigene Anwendung aus dem Developer Portal; wird nur lokal gespeichert)
        public bool DiscordEnabled { get; set; }
        public string DiscordClientId { get; set; } = "";
        public string DiscordClientSecret { get; set; } = "";
        public string DiscordAccessToken { get; set; } = "";
        public string DiscordRefreshToken { get; set; } = "";
        public DateTime DiscordTokenExpires { get; set; }

        // Microsoft Teams (lokale Drittanbieter-API, Token kommt von Teams nach der Kopplung)
        public bool TeamsEnabled { get; set; }
        public string TeamsToken { get; set; } = "";

        // AirPods (früher eigene App „AirPods Companion“, jetzt fest eingebaut)
        public bool AirPodsEnabled { get; set; } = true;
        public bool AirPodsAutoPause { get; set; } = true;
        public bool AirPodsAutoResume { get; set; } = true;
        public bool AirPodsAutoSwitch { get; set; } = true;
        public bool AirPodsSwitchCommunications { get; set; }
        public bool AirPodsPopup { get; set; } = true;
        public bool AirPodsLowBattery { get; set; } = true;
        /// <summary>Signalstärke-Grenze in dBm: -55 nah, -65 normal, -75 weit.</summary>
        public int AirPodsRssi { get; set; } = -65;
        public string AirPodsNameFilter { get; set; } = "AirPods";
        public bool AirPodsImported { get; set; }
        /// <summary>Über WPods direkt mit den AirPods sprechen (ANC, Konversationserkennung, genaue Prozente).</summary>
        public bool AirPodsAap { get; set; } = true;

        // Spotify (eigene App aus dem Spotify Developer Dashboard; nur lokal gespeichert)
        public string SpotifyClientId { get; set; } = "";
        public string SpotifyAccessToken { get; set; } = "";
        public string SpotifyRefreshToken { get; set; } = "";
        public DateTime SpotifyTokenExpires { get; set; }

        static string FilePath => Path.Combine(Program.DataDir, "einstellungen.json");

        public static Settings Load()
        {
            Settings s = null;
            try
            {
                if (File.Exists(FilePath))
                    s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath));
            }
            catch (Exception ex)
            {
                // kaputte Datei: Sicherung (.bak vom letzten Speichern) nehmen und die kaputte als .bad aufheben,
                // statt alles still auf Standard zurückzusetzen und gleich zu überschreiben
                Program.Log(ex);
                try { File.Copy(FilePath, FilePath + ".bad", true); } catch { }
                try { if (File.Exists(FilePath + ".bak")) s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath + ".bak")); }
                catch (Exception ex2) { Program.Log(ex2); }
            }
            s ??= new Settings();
            s.Cats ??= new List<CatConfig>();
            s.CatsMode ??= s.ShowCats ? "expanded" : "off";
            s.DashboardTiles ??= DashTiles.DefaultKeys();
            if (s.TilesVersion < 2)
            {
                // neue Kacheln einmalig in die Übersicht aufnehmen (vor „Einstellungen“)
                foreach (var key in new[] { "player", "apps", "clipboard", "brightness" })
                {
                    if (s.DashboardTiles.Contains(key) || s.DashboardTiles.Count >= DashTiles.Max) continue;
                    int at = s.DashboardTiles.IndexOf("settings");
                    if (at < 0) s.DashboardTiles.Add(key);
                    else s.DashboardTiles.Insert(at, key);
                }
                s.TilesVersion = 2;
            }
            if (s.TilesVersion < 3)
            {
                if (!s.DashboardTiles.Contains("airpods") && s.DashboardTiles.Count < DashTiles.Max)
                {
                    int at = s.DashboardTiles.IndexOf("settings");
                    if (at < 0) s.DashboardTiles.Add("airpods");
                    else s.DashboardTiles.Insert(at, "airpods");
                }
                s.TilesVersion = 3;
            }
            if (s.PagesVersion < 1 || s.Pages == null)
            {
                s.WidgetPages ??= new List<WidgetPageConfig>();
                if (s.WidgetPages.Count == 0)
                    s.WidgetPages.Add(new WidgetPageConfig { Id = "1", Name = "Widgets", Widgets = new List<string> { "clock", "battery", "volume", "cpu" } });
                s.Pages = new List<string> { "dashboard", "player", "w:1", "sound", "bluetooth", "downloads" };
                s.PagesVersion = 2;
            }
            if (s.PagesVersion < 2)
            {
                // AirPods-Seite → Bluetooth-Seite (zeigt die AirPods, wenn sie verbunden sind) + Downloads dazu
                int i = s.Pages.IndexOf("airpods");
                if (i >= 0) s.Pages[i] = "bluetooth";
                else if (!s.Pages.Contains("bluetooth")) s.Pages.Add("bluetooth");
                if (!s.Pages.Contains("downloads")) s.Pages.Add("downloads");
                s.PagesVersion = 2;
            }
            if (s.PagesVersion < 3)
            {
                // neue KI-Seite links neben der Übersicht
                if (!s.Pages.Contains("ai"))
                {
                    int i = s.Pages.IndexOf("dashboard");
                    s.Pages.Insert(i >= 0 ? i + 1 : 0, "ai");
                }
                s.PagesVersion = 3;
            }
            if (s.PagesVersion < 4)
            {
                // Kalender-Seite neben die Übersicht
                if (!s.Pages.Contains("calendar"))
                {
                    int i = s.Pages.IndexOf("dashboard");
                    s.Pages.Insert(i >= 0 ? i + 1 : 0, "calendar");
                }
                s.PagesVersion = 4;
            }
            if (s.PagesVersion < 5)
            {
                if (!s.Pages.Contains("todo"))
                {
                    int i = s.Pages.IndexOf("calendar");
                    s.Pages.Insert(i >= 0 ? i + 1 : Math.Min(1, s.Pages.Count), "todo");
                }
                s.PagesVersion = 5;
            }
            if (s.PagesVersion < 6)
            {
                // neue Wetter-Seite direkt hinter der Übersicht
                if (!s.Pages.Contains("weather"))
                {
                    int i = s.Pages.IndexOf("dashboard");
                    s.Pages.Insert(i >= 0 ? i + 1 : 0, "weather");
                }
                s.PagesVersion = 6;
            }
            if (s.PagesVersion < 7)
            {
                // Wetter + Kalender sind jetzt eine Seite („Heute“, Schlüssel „calendar“)
                int wi = s.Pages.IndexOf("weather");
                if (wi >= 0)
                {
                    s.Pages.RemoveAt(wi);
                    if (!s.Pages.Contains("calendar")) s.Pages.Insert(Math.Min(wi, s.Pages.Count), "calendar");
                }
                s.PagesVersion = 7;
            }
            if (s.PagesVersion < 8)
            {
                // Wetter und Kalender sind wieder getrennte Seiten (einzeln an-/abschaltbar)
                int ci = s.Pages.IndexOf("calendar");
                if (ci >= 0 && !s.Pages.Contains("weather")) s.Pages.Insert(ci, "weather");
                s.PagesVersion = 8;
            }
            s.PageLooks ??= new Dictionary<string, PageLook>();
            if (s.PageFont <= 0) s.PageFont = 1.2;
            if (string.IsNullOrEmpty(s.PageStyle)) s.PageStyle = "clean";
            s.WidgetPages ??= new List<WidgetPageConfig>();
            if (!s.Pages.Contains("dashboard")) s.Pages.Insert(0, "dashboard");
            if (string.IsNullOrWhiteSpace(s.AirPodsNameFilter)) s.AirPodsNameFilter = "AirPods";
            if (s.AirPodsRssi == 0) s.AirPodsRssi = -65;
            if (s.HideDelay <= 0) s.HideDelay = 3;
            if (s.Scale <= 0) s.Scale = 1;
            if (s.PageScale <= 0 || s.LayoutVersion < 3) { s.PageScale = 1; s.LayoutVersion = 3; }
            if (s.LayoutVersion < 4)
            {
                // große Island: etwas kleiner und flacher, dafür breiter
                s.PageScale = Math.Min(s.PageScale, 0.9);
                s.PageWide = 1.15;
                s.LayoutVersion = 4;
            }
            if (s.LayoutVersion < 5)
            {
                // noch flacher: kleiner skaliert, dafür deutlich breiter
                s.PageScale = Math.Min(s.PageScale, 0.8);
                s.PageWide = Math.Max(s.PageWide, 1.3);
                s.LayoutVersion = 5;
            }
            if (s.PageWide < 1 || s.PageWide > 1.5) s.PageWide = 1.3;
            if (s.AnimModes == null)
            {
                // aus den alten Schaltern übernehmen; neue Standard-Art: „App im Vordergrund“
                s.AnimModes = new Dictionary<string, string>();
                foreach (var k in new[] { "claude", "timer", "download", "copy", "message", "device" })
                {
                    bool on = s.AnimEvents != null && s.AnimEvents.Contains(k) && s.DoneAnimation != "off";
                    s.AnimModes[k] = on ? "app" : "off";
                }
            }
            s.Shortcuts ??= DynamicIsland.Shortcuts.Defaults();
            s.DiscordClientId ??= "";
            s.DiscordClientSecret ??= "";
            s.DiscordAccessToken ??= "";
            s.DiscordRefreshToken ??= "";
            s.TeamsToken ??= "";
            s.SpotifyClientId ??= "";
            s.SpotifyAccessToken ??= "";
            s.SpotifyRefreshToken ??= "";
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Program.DataDir);
                // sicher speichern: erst in eine Hilfsdatei, dann austauschen (alte Fassung bleibt als .bak) –
                // ein Absturz/Herunterfahren mitten im Schreiben zerstört so nie die Einstellungen
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                var tmp = FilePath + ".tmp";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var w = new StreamWriter(fs, new System.Text.UTF8Encoding(false)))
                {
                    w.Write(json);
                    w.Flush();
                    fs.Flush(true);
                }
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, FilePath + ".bak", true);
                else File.Move(tmp, FilePath);
            }
            catch (Exception ex) { Program.Log(ex); }
        }
    }

    public static class Autostart
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool Enabled
        {
            get
            {
                try { using var k = Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue("DynamicIsland") != null; }
                catch { return false; }
            }
        }

        public static void Set(bool on)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(Key);
                if (on) k.SetValue("DynamicIsland", "\"" + Environment.ProcessPath + "\"");
                else k.DeleteValue("DynamicIsland", false);
            }
            catch (Exception ex) { Program.Log(ex); }
        }
    }
}

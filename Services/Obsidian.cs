using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DynamicIsland
{
    /// <summary>
    /// Obsidian: Text, Links und Dateien, die auf die Island gezogen werden, landen in einer Notiz im Vault
    /// (Tagesnotiz oder eine feste Eingangs-Notiz). Dateien werden in den Anhang-Ordner kopiert und verlinkt.
    /// Der Vault wird aus Obsidians eigener Liste (%APPDATA%\obsidian\obsidian.json) erkannt oder selbst gewählt.
    /// </summary>
    public static class Obsidian
    {
        public sealed class Vault { public string Path, Name; public bool Open; public long Ts; }

        /// <summary>Alle Vaults, die Obsidian kennt (zuletzt benutzter zuerst).</summary>
        public static List<Vault> FindVaults()
        {
            var list = new List<Vault>();
            try
            {
                string f = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obsidian", "obsidian.json");
                if (!File.Exists(f)) return list;
                using var doc = JsonDocument.Parse(File.ReadAllText(f));
                if (!doc.RootElement.TryGetProperty("vaults", out var vs) || vs.ValueKind != JsonValueKind.Object) return list;
                foreach (var v in vs.EnumerateObject())
                {
                    if (!v.Value.TryGetProperty("path", out var p) || p.ValueKind != JsonValueKind.String) continue;
                    string path = p.GetString();
                    if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) continue;
                    list.Add(new Vault
                    {
                        Path = path,
                        Name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/')),
                        Open = v.Value.TryGetProperty("open", out var o) && o.ValueKind == JsonValueKind.True,
                        Ts = v.Value.TryGetProperty("ts", out var ts) && ts.TryGetInt64(out long t) ? t : 0
                    });
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            return list.OrderByDescending(v => v.Open).ThenByDescending(v => v.Ts).ToList();
        }

        /// <summary>Gewählter Vault, sonst der zuletzt in Obsidian geöffnete.</summary>
        public static string VaultPath(Settings s)
        {
            if (!string.IsNullOrWhiteSpace(s.ObsidianVault) && Directory.Exists(s.ObsidianVault)) return s.ObsidianVault;
            return FindVaults().FirstOrDefault()?.Path;
        }

        public static bool Ready(Settings s) => s.ObsidianEnabled && VaultPath(s) != null;

        static JsonElement? ReadJson(string vault, string file)
        {
            try
            {
                string f = System.IO.Path.Combine(vault, ".obsidian", file);
                if (!File.Exists(f)) return null;
                using var doc = JsonDocument.Parse(File.ReadAllText(f));
                return doc.RootElement.Clone();
            }
            catch { return null; }
        }

        static string Str(JsonElement? e, string name)
        {
            if (e is JsonElement el && el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
            return null;
        }

        /// <summary>Inbox-Ordner: eingestellt, sonst der erste Ordner mit „Inbox“/„Eingang“ im Namen (z. B. „00 Inbox“).</summary>
        public static string InboxFolder(Settings s, string vault)
        {
            if (!string.IsNullOrWhiteSpace(s.ObsidianFolder)) return s.ObsidianFolder.Trim().Replace('\\', '/').Trim('/');
            try
            {
                var d = Directory.GetDirectories(vault).Select(System.IO.Path.GetFileName)
                    .Where(n => !n.StartsWith("."))
                    .FirstOrDefault(n => n.IndexOf("inbox", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("eingang", StringComparison.OrdinalIgnoreCase) >= 0);
                if (d != null) return d;
            }
            catch { }
            return "Inbox";
        }

        static bool FolderMode(Settings s) => s.ObsidianTarget == "folder";

        /// <summary>Letzte angelegte Inbox-Notiz (zum Öffnen beim Antippen).</summary>
        static string lastCreated;

        /// <summary>Datei der Ziel-Notiz relativ zum Vault (mit .md) – im Inbox-Modus der Ordner.</summary>
        public static string NoteRelPath(Settings s, string vault)
        {
            if (FolderMode(s)) return InboxFolder(s, vault) + "/";
            if (s.ObsidianTarget == "note")
            {
                string n = string.IsNullOrWhiteSpace(s.ObsidianNote) ? "Eingang" : s.ObsidianNote.Trim().Replace('\\', '/').TrimStart('/');
                return n.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? n : n + ".md";
            }
            // Tagesnotiz: Ordner + Format aus den Obsidian-Einstellungen (Kern-Plugin „Tägliche Notizen“)
            var dn = ReadJson(vault, "daily-notes.json");
            string folder = (Str(dn, "folder") ?? "").Trim().Trim('/', '\\');
            string name = MomentFormat(DateTime.Now, Str(dn, "format"));
            return (folder.Length > 0 ? folder + "/" : "") + name + ".md";
        }

        /// <summary>Wandelt die gängigen moment.js-Datumsformate (YYYY-MM-DD, DD.MM.YYYY …) um.</summary>
        static string MomentFormat(DateTime d, string fmt)
        {
            if (string.IsNullOrWhiteSpace(fmt)) fmt = "YYYY-MM-DD";
            var sb = new StringBuilder();
            int i = 0;
            while (i < fmt.Length)
            {
                if (fmt[i] == '[') { int j = fmt.IndexOf(']', i); if (j < 0) j = fmt.Length; sb.Append(fmt, i + 1, Math.Max(0, j - i - 1)); i = j + 1; continue; }
                string[] tokens = { "YYYY", "YY", "MMMM", "MMM", "MM", "M", "DD", "D", "dddd", "ddd" };
                string tok = tokens.FirstOrDefault(t => string.CompareOrdinal(fmt, i, t, 0, t.Length) == 0);
                if (tok == null) { sb.Append(fmt[i]); i++; continue; }
                var de = CultureInfo.GetCultureInfo("de-DE");
                sb.Append(tok switch
                {
                    "YYYY" => d.ToString("yyyy"), "YY" => d.ToString("yy"),
                    "MMMM" => d.ToString("MMMM", de), "MMM" => d.ToString("MMM", de), "MM" => d.ToString("MM"), "M" => d.Month.ToString(),
                    "DD" => d.ToString("dd"), "D" => d.Day.ToString(),
                    "dddd" => d.ToString("dddd", de), _ => d.ToString("ddd", de)
                });
                i += tok.Length;
            }
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) if (c != '/') sb.Replace(c, '-');
            return sb.ToString();
        }

        /// <summary>Anhang-Ordner des Vaults (Einstellung „Neue Anhänge“), sonst „Anhänge“.</summary>
        static string AttachmentDir(string vault, string noteRel)
        {
            string a = Str(ReadJson(vault, "app.json"), "attachmentFolderPath");
            if (string.IsNullOrWhiteSpace(a) || a == "/") return vault;   // wie in Obsidian: ohne Einstellung im Vault-Ordner
            if (a.StartsWith("./"))
            {
                string noteDir = System.IO.Path.GetDirectoryName(System.IO.Path.Combine(vault, noteRel.Replace('/', '\\'))) ?? vault;
                return System.IO.Path.Combine(noteDir, a.Substring(2));
            }
            return System.IO.Path.Combine(vault, a.Replace('/', '\\'));
        }

        static void Append(string vault, string noteRel, string block)
        {
            string file = System.IO.Path.Combine(vault, noteRel.Replace('/', '\\'));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
            string sep = "";
            if (File.Exists(file))
            {
                string old = File.ReadAllText(file);
                if (old.Length > 0) sep = old.EndsWith("\n\n") ? "" : old.EndsWith("\n") ? "\n" : "\n\n";
            }
            File.AppendAllText(file, sep + block.TrimEnd() + "\n", new UTF8Encoding(false));
        }

        static string Stamp(Settings s) => s.ObsidianTime ? "**" + DateTime.Now.ToString("HH:mm") + "** " : "";

        /// <summary>Text oder Link anhängen. Gibt den Notiz-Namen zurück (für die Anzeige), sonst null.</summary>
        public static string AddText(Settings s, string text)
        {
            string vault = VaultPath(s);
            if (vault == null || string.IsNullOrWhiteSpace(text)) return null;
            string note = NoteRelPath(s, vault);
            text = text.Replace("\r\n", "\n").Trim();
            if (FolderMode(s)) return NewInboxNote(s, vault, text);
            string block;
            if (Uri.TryCreate(text, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https") && !text.Contains(' ') && !text.Contains('\n'))
                block = "- " + Stamp(s) + "<" + text + ">";                         // Link
            else if (text.Contains('\n'))
                block = (s.ObsidianTime ? "- " + Stamp(s).Trim() + "\n" : "") + string.Join("\n", text.Split('\n').Select(l => (s.ObsidianTime ? "\t" : "") + l));
            else
                block = "- " + Stamp(s) + text;
            Append(vault, note, block);
            return System.IO.Path.GetFileNameWithoutExtension(note);
        }

        static string SafeName(string t)
        {
            var sb = new StringBuilder();
            foreach (char c in t) sb.Append(System.IO.Path.GetInvalidFileNameChars().Contains(c) || c == '#' || c == '^' || c == '[' || c == ']' || c == '|' ? ' ' : c);
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim().TrimEnd('.');
        }

        static string FreePath(string dir, string baseName, string ext)
        {
            string p = System.IO.Path.Combine(dir, baseName + ext);
            for (int i = 2; File.Exists(p); i++) p = System.IO.Path.Combine(dir, baseName + " " + i + ext);
            return p;
        }

        /// <summary>Inbox-Modus: Text / Link wird eine eigene neue Notiz im Inbox-Ordner (Name aus den ersten Worten).</summary>
        static string NewInboxNote(Settings s, string vault, string text)
        {
            string folder = InboxFolder(s, vault);
            string dir = System.IO.Path.Combine(vault, folder.Replace('/', '\\'));
            Directory.CreateDirectory(dir);
            bool link = Uri.TryCreate(text, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https") && !text.Contains(' ') && !text.Contains('\n');
            string first = link ? "Link " + u.Host.Replace("www.", "") : text.Split('\n')[0];
            string title = SafeName(first);
            if (title.Length > 50) title = title.Substring(0, 50).TrimEnd() + "…";
            if (title.Length == 0) title = "Notiz";
            string file = FreePath(dir, title, ".md");
            var body = new StringBuilder();
            body.Append("---\nerstellt: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("\nquelle: Dynamic Island\n---\n");
            body.Append(link ? "<" + text + ">" : text).Append('\n');
            File.WriteAllText(file, body.ToString(), new UTF8Encoding(false));
            lastCreated = file;
            return folder;
        }

        /// <summary>Dateien in den Anhang-Ordner kopieren und in der Notiz verlinken (Bilder eingebettet).
        /// Im Inbox-Modus landen sie direkt im Inbox-Ordner.</summary>
        public static string AddFiles(Settings s, IEnumerable<string> paths)
        {
            string vault = VaultPath(s);
            if (vault == null) return null;
            string note = NoteRelPath(s, vault);
            if (FolderMode(s))
            {
                string folder = InboxFolder(s, vault);
                string dir = System.IO.Path.Combine(vault, folder.Replace('/', '\\'));
                Directory.CreateDirectory(dir);
                int n = 0;
                foreach (var p in paths)
                {
                    try
                    {
                        if (!File.Exists(p)) continue;
                        string dest = FreePath(dir, System.IO.Path.GetFileNameWithoutExtension(p), System.IO.Path.GetExtension(p));
                        File.Copy(p, dest);
                        lastCreated = dest;
                        n++;
                    }
                    catch (Exception ex) { Program.Log(ex); }
                }
                return n > 0 ? folder : null;
            }
            var links = new List<string>();
            foreach (var p in paths)
            {
                try
                {
                    if (Directory.Exists(p)) { links.Add("- " + Stamp(s) + "Ordner: `" + p + "`"); continue; }
                    if (!File.Exists(p)) continue;
                    string full = System.IO.Path.GetFullPath(p);
                    string name;
                    // liegt schon im Vault → nur verlinken
                    if (full.StartsWith(System.IO.Path.GetFullPath(vault).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                        name = System.IO.Path.GetFileName(full);
                    else
                    {
                        string dir = AttachmentDir(vault, note);
                        Directory.CreateDirectory(dir);
                        name = System.IO.Path.GetFileName(full);
                        string dest = System.IO.Path.Combine(dir, name);
                        for (int i = 1; File.Exists(dest); i++)
                        {
                            name = System.IO.Path.GetFileNameWithoutExtension(full) + " " + i + System.IO.Path.GetExtension(full);
                            dest = System.IO.Path.Combine(dir, name);
                        }
                        File.Copy(full, dest);
                    }
                    string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
                    bool embed = ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".svg" or ".pdf" or ".mp3" or ".wav" or ".m4a" or ".mp4";
                    if (ext == ".md") links.Add("- " + Stamp(s) + "[[" + System.IO.Path.GetFileNameWithoutExtension(name) + "]]");
                    else links.Add((embed ? "!" : "- " + Stamp(s)) + "[[" + name + "]]");
                }
                catch (Exception ex) { Program.Log(ex); }
            }
            if (links.Count == 0) return null;
            Append(vault, note, string.Join("\n", links));
            return System.IO.Path.GetFileNameWithoutExtension(note);
        }

        /// <summary>Notiz in Obsidian öffnen.</summary>
        public static void OpenNote(Settings s)
        {
            try
            {
                string vault = VaultPath(s);
                if (vault == null) return;
                string target;
                if (FolderMode(s))
                {
                    // die eben angelegte Notiz, sonst die Übersichts-Notiz des Ordners („00 Inbox/00 Inbox.md“)
                    string folder = InboxFolder(s, vault);
                    string overview = System.IO.Path.Combine(vault, folder.Replace('/', '\\'), System.IO.Path.GetFileName(folder) + ".md");
                    target = lastCreated != null && File.Exists(lastCreated) && lastCreated.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? lastCreated
                           : File.Exists(overview) ? overview : null;
                    if (target == null) { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("obsidian://open?vault=" + Uri.EscapeDataString(System.IO.Path.GetFileName(vault.TrimEnd('\\')))) { UseShellExecute = true }); return; }
                }
                else target = System.IO.Path.Combine(vault, NoteRelPath(s, vault).Replace('/', '\\'));
                string url = "obsidian://open?path=" + Uri.EscapeDataString(target);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        /// <summary>Obsidian-Farbe (Lila).</summary>
        public static readonly System.Windows.Media.Color Purple = System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DynamicIsland
{
    public sealed class GameDownload
    {
        public string Key, Store, Name;
        public long Total, Done;
        public double Speed;                  // Bytes pro Sekunde (geglättet)
        public DateTime LastChange = DateTime.Now, LastSample = DateTime.Now;
        public double? Progress => Total > 0 ? Math.Min(1, Done / (double)Total) : (double?)null;
    }

    /// <summary>
    /// Erkennt laufende Spiele-Downloads/Updates:
    ///  · Steam: liest die appmanifest_*.acf aller Bibliotheken (BytesDownloaded / BytesToDownload)
    ///  · Epic: Manifeste mit „bIsIncompleteInstall“ (ohne genauen Fortschritt)
    /// Nur lesen, nichts wird verändert.
    /// </summary>
    public sealed class GameDownloadWatcher
    {
        readonly Dictionary<string, GameDownload> active = new Dictionary<string, GameDownload>();
        List<string> steamLibs;
        DateTime libsRead;
        bool busy;

        public event Action<GameDownload> Started, Progress, Completed;
        public event Action<GameDownload> Stopped;   // abgebrochen / pausiert (lange keine Bewegung)

        public async Task PollAsync()
        {
            if (busy) return;
            busy = true;
            try
            {
                var found = await Task.Run(Scan);
                var now = DateTime.Now;
                foreach (var f in found)
                {
                    if (!active.TryGetValue(f.Key, out var d))
                    {
                        // Nur zeigen, was wirklich lädt (nicht schon fertig, nicht „Update ausstehend“ ohne Bytes)
                        if (f.Total > 0 && f.Done >= f.Total) continue;
                        active[f.Key] = f;
                        Started?.Invoke(f);
                        continue;
                    }
                    double dt = (now - d.LastSample).TotalSeconds;
                    if (f.Done != d.Done || f.Total != d.Total)
                    {
                        if (dt > 0.5 && f.Done > d.Done)
                        {
                            double inst = (f.Done - d.Done) / dt;
                            d.Speed = d.Speed <= 0 ? inst : d.Speed * 0.6 + inst * 0.4;
                        }
                        d.Done = f.Done;
                        d.Total = f.Total;
                        d.Name = f.Name;
                        d.LastChange = now;
                        d.LastSample = now;
                        Progress?.Invoke(d);
                    }
                    else if ((now - d.LastChange).TotalSeconds > 30) d.Speed = 0;
                }
                // nicht mehr im Download-Zustand → fertig (oder abgebrochen)
                foreach (var d in active.Values.ToList())
                {
                    if (found.Any(f => f.Key == d.Key)) continue;
                    active.Remove(d.Key);
                    if (d.Total <= 0 || d.Done >= d.Total * 0.9 || d.Store == "Epic" || SteamInstalled(d.Key)) Completed?.Invoke(d);
                    else Stopped?.Invoke(d);
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            finally { busy = false; }
        }

        // ------------------------------------------------------------------
        List<GameDownload> Scan()
        {
            var list = new List<GameDownload>();
            try { ScanSteam(list); } catch (Exception ex) { Program.Log(ex); }
            try { ScanEpic(list); } catch (Exception ex) { Program.Log(ex); }
            return list;
        }

        static readonly Regex KV = new Regex("^\\s*\"(?<k>[^\"]+)\"\\s+\"(?<v>[^\"]*)\"", RegexOptions.Multiline | RegexOptions.Compiled);

        static Dictionary<string, string> ReadAcf(string path)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string text;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var r = new StreamReader(fs)) text = r.ReadToEnd();
            foreach (Match m in KV.Matches(text))
                if (!d.ContainsKey(m.Groups["k"].Value)) d[m.Groups["k"].Value] = m.Groups["v"].Value;
            return d;
        }

        List<string> SteamLibraries()
        {
            if (steamLibs != null && (DateTime.Now - libsRead).TotalMinutes < 10) return steamLibs;
            var libs = new List<string>();
            string steam = null;
            try { steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string; } catch { }
            steam ??= @"C:\Program Files (x86)\Steam";
            steam = steam.Replace('/', '\\');
            var main = Path.Combine(steam, "steamapps");
            if (Directory.Exists(main)) libs.Add(main);
            var vdf = Path.Combine(main, "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"(?<p>[^\"]+)\""))
                {
                    var p = Path.Combine(m.Groups["p"].Value.Replace("\\\\", "\\"), "steamapps");
                    if (Directory.Exists(p) && !libs.Any(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase))) libs.Add(p);
                }
            }
            steamLibs = libs;
            libsRead = DateTime.Now;
            return libs;
        }

        // StateFlags: 4 installiert, 256 Update läuft, 1024 Update gestartet, 1048576 lädt herunter
        const int UpdateRunning = 256, UpdateStarted = 1024, Downloading = 1048576;

        void ScanSteam(List<GameDownload> list)
        {
            foreach (var lib in SteamLibraries())
            {
                foreach (var f in Directory.EnumerateFiles(lib, "appmanifest_*.acf"))
                {
                    Dictionary<string, string> kv;
                    try { kv = ReadAcf(f); } catch { continue; }
                    int.TryParse(kv.GetValueOrDefault("StateFlags"), out int flags);
                    long.TryParse(kv.GetValueOrDefault("BytesToDownload"), out long total);
                    long.TryParse(kv.GetValueOrDefault("BytesDownloaded"), out long done);
                    bool running = (flags & (UpdateRunning | UpdateStarted | Downloading)) != 0;
                    if (!running || total <= 0) continue;
                    list.Add(new GameDownload
                    {
                        Key = "steam:" + kv.GetValueOrDefault("appid"),
                        Store = "Steam",
                        Name = kv.GetValueOrDefault("name") ?? "Steam-Spiel",
                        Total = total,
                        Done = done
                    });
                }
            }
        }

        bool SteamInstalled(string key)
        {
            if (!key.StartsWith("steam:")) return false;
            var id = key.Substring(6);
            foreach (var lib in SteamLibraries())
            {
                var f = Path.Combine(lib, "appmanifest_" + id + ".acf");
                if (!File.Exists(f)) continue;
                try
                {
                    var kv = ReadAcf(f);
                    int.TryParse(kv.GetValueOrDefault("StateFlags"), out int flags);
                    return flags == 4;
                }
                catch { }
            }
            return false;
        }

        static void ScanEpic(List<GameDownload> list)
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.EnumerateFiles(dir, "*.item"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(f));
                    var r = doc.RootElement;
                    if (!r.TryGetProperty("bIsIncompleteInstall", out var inc) || inc.ValueKind != JsonValueKind.True) continue;
                    // nur wenn der Launcher gerade schreibt (sonst ist es eine liegengebliebene Installation)
                    if ((DateTime.Now - File.GetLastWriteTime(f)).TotalHours > 6) continue;
                    list.Add(new GameDownload
                    {
                        Key = "epic:" + (r.TryGetProperty("AppName", out var an) ? an.GetString() : Path.GetFileNameWithoutExtension(f)),
                        Store = "Epic",
                        Name = r.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : "Epic-Spiel",
                        Total = 0
                    });
                }
                catch { }
            }
        }
    }
}

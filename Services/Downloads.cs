using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;

namespace DynamicIsland
{
    public sealed class DownloadItem
    {
        public readonly string Key = Guid.NewGuid().ToString("N");
        public string TempPath, FinalPath, Name;
        public long Size, LastSize;
        public double Speed;
        public bool Done;
        public DateTime Started = DateTime.Now, LastSample = DateTime.Now, LastChange = DateTime.Now;
        public DateTime? MissingSince;
    }

    /// <summary>
    /// Beobachtet den Downloads-Ordner. Browser schreiben zuerst in eine Temp-Datei
    /// (Chrome/Edge/Opera: .crdownload, Firefox: .part) und benennen sie am Ende um.
    /// Daran erkennen wir Start, Fortschritt (Größe + Geschwindigkeit) und Ende.
    /// </summary>
    public sealed class DownloadWatcher : IDisposable
    {
        static readonly string[] TempExt = { ".crdownload", ".part", ".partial", ".download", ".opdownload" };
        readonly Dispatcher ui;
        FileSystemWatcher fsw;

        public string Folder { get; private set; }
        public readonly Dictionary<string, DownloadItem> Active = new Dictionary<string, DownloadItem>(StringComparer.OrdinalIgnoreCase);
        public event Action<DownloadItem> Started, Completed, Cancelled;

        public DownloadWatcher(Dispatcher d) { ui = d; }

        public static bool IsTemp(string path)
        {
            var ext = Path.GetExtension(path);
            return TempExt.Any(t => string.Equals(t, ext, StringComparison.OrdinalIgnoreCase));
        }

        public static string CleanName(string tempPath)
        {
            var n = Path.GetFileName(tempPath) ?? "";
            if (IsTemp(n)) n = Path.GetFileNameWithoutExtension(n);
            if (n.StartsWith("Unconfirmed ", StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("Nicht bestätigt", StringComparison.OrdinalIgnoreCase))
                n = "Neuer Download";
            return n;
        }

        public void Start()
        {
            Stop();
            Folder = Native.DownloadsFolder();
            if (string.IsNullOrEmpty(Folder) || !Directory.Exists(Folder)) return;
            try
            {
                fsw = new FileSystemWatcher(Folder)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                    InternalBufferSize = 32768
                };
                fsw.Created += (s, e) => ui.BeginInvoke(new Action(() => OnCreated(e.FullPath)));
                fsw.Renamed += (s, e) => ui.BeginInvoke(new Action(() => OnRenamed(e.OldFullPath, e.FullPath)));
                fsw.Deleted += (s, e) => ui.BeginInvoke(new Action(() => OnDeleted(e.FullPath)));
                fsw.EnableRaisingEvents = true;

                // Downloads, die gerade schon laufen (z. B. nach einem Neustart der Island)
                foreach (var f in Directory.EnumerateFiles(Folder))
                    if (IsTemp(f) && (DateTime.Now - File.GetLastWriteTime(f)).TotalMinutes < 2) Track(f);
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        public void Stop()
        {
            try { fsw?.Dispose(); } catch { }
            fsw = null;
            foreach (var it in Active.Values.ToList()) Cancelled?.Invoke(it);
            Active.Clear();
        }

        public void Dispose() => Stop();

        void Track(string path)
        {
            if (Active.ContainsKey(path)) return;
            var it = new DownloadItem { TempPath = path, Name = CleanName(path) };
            try { it.Size = it.LastSize = new FileInfo(path).Length; } catch { }
            Active[path] = it;
            Started?.Invoke(it);
        }

        void OnCreated(string path)
        {
            if (IsTemp(path)) Track(path);
        }

        void OnRenamed(string oldPath, string newPath)
        {
            Active.TryGetValue(oldPath, out var it);
            if (IsTemp(newPath))
            {
                // z. B. Chrome: "Unconfirmed 123.crdownload" → "datei.zip.crdownload"
                if (it != null)
                {
                    Active.Remove(oldPath);
                    it.TempPath = newPath;
                    it.Name = CleanName(newPath);
                    Active[newPath] = it;
                }
                else Track(newPath);
                return;
            }
            if (it == null && !IsTemp(oldPath)) return;
            if (it == null) it = new DownloadItem { TempPath = oldPath };
            Active.Remove(oldPath);
            Finish(it, newPath);
        }

        void OnDeleted(string path)
        {
            if (!Active.TryGetValue(path, out var it)) return;
            Active.Remove(path);
            ResolveGone(it);
        }

        /// <summary>Temp-Datei weg: fertig (manche Browser schreiben das Ziel direkt) oder abgebrochen?</summary>
        void ResolveGone(DownloadItem it)
        {
            var final = Path.Combine(Path.GetDirectoryName(it.TempPath) ?? "", CleanName(it.TempPath));
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                try
                {
                    if (File.Exists(final) && new FileInfo(final).Length > 0 &&
                        (DateTime.Now - File.GetLastWriteTime(final)).TotalSeconds < 30)
                    {
                        Finish(it, final);
                        return;
                    }
                }
                catch { }
                if (!it.Done) { it.Done = true; Cancelled?.Invoke(it); }
            };
            t.Start();
        }

        void Finish(DownloadItem it, string finalPath)
        {
            if (it.Done) return;
            it.Done = true;
            it.FinalPath = finalPath;
            it.Name = Path.GetFileName(finalPath);
            try { it.Size = new FileInfo(finalPath).Length; } catch { }
            Completed?.Invoke(it);
        }

        /// <summary>Etwa einmal pro Sekunde: Größe und Geschwindigkeit aktualisieren.</summary>
        public void Poll()
        {
            if (Active.Count == 0) return;
            var now = DateTime.Now;
            foreach (var it in Active.Values.ToList())
            {
                long size;
                try
                {
                    var fi = new FileInfo(it.TempPath);
                    if (!fi.Exists)
                    {
                        // normalerweise meldet der Ordner-Beobachter das; falls nicht, nach 5 s aufräumen
                        it.MissingSince ??= now;
                        if ((now - it.MissingSince.Value).TotalSeconds > 5)
                        {
                            Active.Remove(it.TempPath);
                            ResolveGone(it);
                        }
                        continue;
                    }
                    size = fi.Length;
                }
                catch { continue; }

                double dt = (now - it.LastSample).TotalSeconds;
                if (dt > 0.2)
                {
                    double inst = Math.Max(0, (size - it.LastSize) / dt);
                    it.Speed = it.Speed <= 0 ? inst : it.Speed * 0.6 + inst * 0.4;
                    if (size != it.LastSize) it.LastChange = now;
                    it.LastSize = size;
                    it.LastSample = now;
                }
                it.Size = size;

                // hängt seit 90 s → nicht mehr anzeigen
                if ((now - it.LastChange).TotalSeconds > 90)
                {
                    Active.Remove(it.TempPath);
                    if (!it.Done) { it.Done = true; Cancelled?.Invoke(it); }
                }
            }
        }

        /// <summary>Die neuesten Dateien im Downloads-Ordner.</summary>
        public List<FileInfo> Recent(int count)
        {
            try
            {
                var folder = Folder ?? Native.DownloadsFolder();
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return new List<FileInfo>();
                return new DirectoryInfo(folder).EnumerateFiles()
                    .Where(f => !IsTemp(f.Name) && (f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                    .OrderByDescending(Latest)
                    .Take(count)
                    .ToList();
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                return new List<FileInfo>();
            }
        }

        public static DateTime Latest(FileInfo f) => f.CreationTime > f.LastWriteTime ? f.CreationTime : f.LastWriteTime;

        public static string FormatSize(long b)
        {
            if (b < 1024) return b + " B";
            if (b < 1024 * 1024) return (b / 1024.0).ToString("0", Ui.De) + " KB";
            if (b < 1024L * 1024 * 1024) return (b / 1048576.0).ToString("0.0", Ui.De) + " MB";
            return (b / 1073741824.0).ToString("0.00", Ui.De) + " GB";
        }

        public static string Ago(DateTime t)
        {
            var d = DateTime.Now - t;
            if (d.TotalMinutes < 1) return "gerade eben";
            if (d.TotalHours < 1) return $"vor {(int)d.TotalMinutes} Min";
            if (d.TotalDays < 1) return $"vor {(int)d.TotalHours} Std";
            return t.ToString("d. MMM", Ui.De);
        }
    }
}

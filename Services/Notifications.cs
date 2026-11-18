using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DynamicIsland
{
    /// <summary>
    /// Liest neue Windows-Mitteilungen (WhatsApp, Discord, Teams, Anrufe …) und meldet sie der Island.
    /// Windows muss den Zugriff erlauben. Auf manchen Windows-Versionen geht das für Apps ohne
    /// Store-Paket nicht – dann steht das im Status und die Island läuft normal weiter.
    /// </summary>
    public sealed class NotificationMirror
    {
        readonly HashSet<uint> seen = new HashSet<uint>();
        bool ok, primed, busy;
        int failures;                       // Lesefehler hintereinander
        DateTime retryAt = DateTime.MaxValue;
        TimeSpan backoff = TimeSpan.FromSeconds(30);
        bool unsupported;                   // „geht für diese App nicht“ – nie erneut versuchen

        /// <summary>Nach einem Fehler später neu verbinden (statt für immer aus zu bleiben).</summary>
        public bool WantsRetry => !ok && !unsupported && Status != "aus" && DateTime.Now >= retryAt;

        void ScheduleRetry()
        {
            retryAt = DateTime.Now + backoff;
            backoff = TimeSpan.FromSeconds(Math.Min(600, backoff.TotalSeconds * 2));
        }

        public string Status { get; private set; } = "aus";
        public bool Active => ok;

        /// <summary>App-Name, Titel, Text, App-ID (AppUserModelId, zum Öffnen der App).</summary>
        public event Action<string, string, string, string> Received;

        public async Task<bool> InitAsync()
        {
            try
            {
                var st = await UserNotificationListener.Current.RequestAccessAsync();
                ok = st == UserNotificationListenerAccessStatus.Allowed;
                if (ok) { failures = 0; backoff = TimeSpan.FromSeconds(30); retryAt = DateTime.MaxValue; }
                else ScheduleRetry();
                Status = ok ? "aktiv"
                    : st == UserNotificationListenerAccessStatus.Denied
                        ? "Windows erlaubt den Zugriff nicht. Gib ihn in den Windows-Einstellungen unter Mitteilungen frei."
                        : "Zugriff noch nicht erlaubt";
            }
            catch (Exception ex)
            {
                ok = false;
                Status = "Windows unterstützt das für diese App leider nicht (" + ex.Message.Trim() + ")";
                // „nicht unterstützt“ bleibt so; andere Fehler (z. B. beim Anmelden noch nicht bereit) später erneut
                unsupported = ex is NotSupportedException || ex is NotImplementedException;
                if (!unsupported) ScheduleRetry();
                if (failures++ == 0) Program.Log(ex);
            }
            primed = false;
            seen.Clear();
            return ok;
        }

        public void Disable()
        {
            ok = false;
            retryAt = DateTime.MaxValue;
            Status = "aus";
        }

        public async Task PollAsync()
        {
            if (!ok || busy) return;
            busy = true;
            try
            {
                var list = await UserNotificationListener.Current.GetNotificationsAsync(NotificationKinds.Toast);
                var now = new HashSet<uint>();
                foreach (var n in list)
                {
                    now.Add(n.Id);
                    if (!primed || seen.Contains(n.Id)) continue; // beim ersten Mal nur merken, nicht anzeigen

                    string app = "", aumid = null;
                    try { app = n.AppInfo?.DisplayInfo?.DisplayName ?? ""; } catch { }
                    try { aumid = n.AppInfo?.AppUserModelId; } catch { }
                    var texts = new List<string>();
                    try
                    {
                        var binding = n.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
                        if (binding != null)
                            foreach (var t in binding.GetTextElements())
                                if (!string.IsNullOrWhiteSpace(t.Text)) texts.Add(t.Text.Trim());
                    }
                    catch { }
                    string title = texts.Count > 0 ? texts[0] : app;
                    string body = texts.Count > 1 ? string.Join(" ", texts.Skip(1)) : "";
                    try { Received?.Invoke(app, title, body, aumid); }
                    catch (Exception ex) { Program.Log(ex); }
                }
                seen.Clear();
                seen.UnionWith(now);
                primed = true;
                failures = 0;
            }
            catch (Exception ex)
            {
                // einzelne Lesefehler überstehen; erst nach mehreren hintereinander pausieren und später neu verbinden
                if (failures++ == 0) Program.Log(ex);
                if (failures >= 3)
                {
                    ok = false;
                    Status = "Mitteilungen gerade nicht lesbar – neuer Versuch gleich";
                    ScheduleRetry();
                }
            }
            finally { busy = false; }
        }
    }

    /// <summary>
    /// Bringt die App nach vorne, von der eine Mitteilung kam: offenes Fenster aktivieren,
    /// sonst über die App-ID (wie das Startmenü) starten.
    /// </summary>
    public static class AppActivator
    {
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetApplicationUserModelId(IntPtr process, ref uint len, StringBuilder id);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref uint size);

        /// <summary>true, wenn ein Fenster aktiviert oder die App gestartet wurde.</summary>
        public static bool Activate(string appName, string aumid)
        {
            try
            {
                var h = FindWindow(appName, aumid);
                if (h != IntPtr.Zero) { Native.Focus(h); return true; }
                if (!string.IsNullOrWhiteSpace(aumid))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\" + aumid) { UseShellExecute = true });
                    return true;
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            return false;
        }

        static string Norm(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        static IntPtr FindWindow(string appName, string aumid)
        {
            string want = Norm(appName);
            // App-ID ohne Paket-Anhang (z. B. „5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App“ → Paket-Familie)
            string family = aumid != null && aumid.Contains("!") ? aumid.Substring(0, aumid.IndexOf('!')) : null;
            IntPtr byId = IntPtr.Zero, byName = IntPtr.Zero, byTitle = IntPtr.Zero;
            uint me = (uint)Environment.ProcessId;

            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h) || GetWindow(h, 4) != IntPtr.Zero) return true;        // unsichtbar / gehört zu einem anderen Fenster
                long ex = (long)GetWindowLongPtr(h, -20);
                if ((ex & 0x80) != 0) return true;                                              // Werkzeugfenster
                var sb = new StringBuilder(256);
                GetWindowText(h, sb, 256);
                string title = sb.ToString();
                if (title.Length == 0) return true;
                GetWindowThreadProcessId(h, out uint pid);
                if (pid == me) return true;

                string procId = null, exe = null;
                IntPtr ph = OpenProcess(0x1000, false, pid);                                      // PROCESS_QUERY_LIMITED_INFORMATION
                if (ph != IntPtr.Zero)
                {
                    try
                    {
                        uint len = 256;
                        var idb = new StringBuilder(256);
                        if (GetApplicationUserModelId(ph, ref len, idb) == 0) procId = idb.ToString();
                        uint size = 520;
                        var nb = new StringBuilder(520);
                        if (QueryFullProcessImageName(ph, 0, nb, ref size)) exe = System.IO.Path.GetFileNameWithoutExtension(nb.ToString());
                    }
                    finally { CloseHandle(ph); }
                }

                if (byId == IntPtr.Zero && procId != null && aumid != null &&
                    (procId.Equals(aumid, StringComparison.OrdinalIgnoreCase) || (family != null && procId.StartsWith(family, StringComparison.OrdinalIgnoreCase))))
                    byId = h;
                if (byName == IntPtr.Zero && want.Length >= 3 && exe != null)
                {
                    string e = Norm(exe);
                    if (e.Length >= 3 && (e.Contains(want) || want.Contains(e))) byName = h;
                }
                if (byTitle == IntPtr.Zero && want.Length >= 4 && Norm(title).EndsWith(want)) byTitle = h;   // z. B. „… – Google Chrome“
                return byId == IntPtr.Zero;
            }, IntPtr.Zero);

            return byId != IntPtr.Zero ? byId : byName != IntPtr.Zero ? byName : byTitle;
        }
    }
}

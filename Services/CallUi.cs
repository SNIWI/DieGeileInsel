using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace DynamicIsland
{
    /// <summary>
    /// Anrufe annehmen / ablehnen (WhatsApp, Discord, Teams …) über die Windows-Bedienungshilfen (UI Automation):
    /// sucht den Knopf „Annehmen“ bzw. „Ablehnen“ in der Windows-Mitteilung des Anrufs oder im Anruf-Fenster der App
    /// und drückt ihn. Die Apps haben dafür keine offizielle Schnittstelle.
    /// </summary>
    public static class CallUi
    {
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);

        static readonly string[] AcceptWords = { "annehmen", "accept", "answer", "abnehmen", "entgegennehmen", "beitreten", "join call", "join" };
        static readonly string[] DeclineWords = { "ablehnen", "decline", "reject", "ignorieren", "ignore", "auflegen" };

        /// <summary>Prozessnamen der App (für das Anruf-Fenster).</summary>
        static string[] ProcNames(string app)
        {
            var a = (app ?? "").ToLowerInvariant();
            if (a.Contains("whatsapp")) return new[] { "whatsapp", "whatsapp.root" };
            if (a.Contains("discord")) return new[] { "discord", "discordptb", "discordcanary" };
            if (a.Contains("teams")) return new[] { "ms-teams", "teams", "msteams" };
            if (a.Contains("skype")) return new[] { "skype" };
            if (a.Contains("telegram")) return new[] { "telegram" };
            if (a.Contains("signal")) return new[] { "signal" };
            return a.Length >= 3 ? new[] { a.Replace(" ", "") } : Array.Empty<string>();
        }

        static bool Matches(string name, bool accept)
        {
            var n = (name ?? "").Trim().ToLowerInvariant();
            if (n.Length == 0 || n.Length > 50) return false;
            if (accept) return AcceptWords.Any(n.Contains) && !DeclineWords.Any(n.Contains);
            return DeclineWords.Any(n.Contains);
        }

        /// <summary>Fenster, in denen der Knopf sein kann: zuerst Windows-Mitteilungen (Toast), dann alle Fenster der App.</summary>
        static List<IntPtr> CandidateWindows(string app)
        {
            var names = ProcNames(app);
            var pids = new HashSet<uint>();
            var shell = new HashSet<uint>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    string pn = p.ProcessName.ToLowerInvariant();
                    if (names.Any(n => pn == n || pn.StartsWith(n))) pids.Add((uint)p.Id);
                    else if (pn == "shellexperiencehost") shell.Add((uint)p.Id);
                }
                catch { }
                finally { p.Dispose(); }
            }
            var toasts = new List<IntPtr>();
            var appWins = new List<IntPtr>();
            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h)) return true;
                GetWindowThreadProcessId(h, out uint pid);
                if (pids.Contains(pid)) appWins.Add(h);
                else if (shell.Contains(pid)) toasts.Add(h);   // Mitteilungs-Popup (nur von Windows selbst, keine anderen Apps)
                return true;
            }, IntPtr.Zero);
            toasts.AddRange(appWins);
            return toasts;
        }

        /// <summary>true = Knopf gefunden und gedrückt.</summary>
        public static Task<bool> AnswerAsync(string app, bool accept) => Task.Run(async () =>
        {
            var btnCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
            // Chromium-Apps (WhatsApp, Discord, Teams) bauen den Bedienungshilfen-Baum erst bei der ersten Anfrage → 3 Versuche
            for (int attempt = 0; attempt < 3; attempt++)
            {
                foreach (var h in CandidateWindows(app))
                {
                    try
                    {
                        var root = AutomationElement.FromHandle(h);
                        AutomationElement best = null;
                        int bestScore = -1;
                        foreach (AutomationElement b in root.FindAll(TreeScope.Descendants, btnCond))
                        {
                            string name;
                            try { name = b.Current.Name; } catch { continue; }
                            if (!Matches(name, accept)) continue;
                            var n = name.ToLowerInvariant();
                            // lieber nur Ton als gleich mit Kamera
                            int score = n.Contains("video") ? 1 : n.Contains("audio") || n.Contains("sprach") ? 3 : 2;
                            if (score > bestScore) { best = b; bestScore = score; }
                        }
                        if (best != null && best.TryGetCurrentPattern(InvokePattern.Pattern, out var p))
                        {
                            ((InvokePattern)p).Invoke();
                            return true;
                        }
                    }
                    catch { }
                }
                await Task.Delay(400);
            }
            return false;
        });
    }
}

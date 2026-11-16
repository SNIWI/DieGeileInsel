using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace DynamicIsland
{
    /// <summary>
    /// Dinge, die Discords Schnittstelle (RPC) nicht kann, über die Windows-Bedienungshilfen (UI Automation):
    /// z. B. den Go-Live-Stream beenden. Sucht im Discord-Fenster den Knopf „Stream beenden“ und drückt ihn.
    /// </summary>
    public static class DiscordUi
    {
        static bool IsStopStream(string name)
        {
            var n = (name ?? "").ToLowerInvariant();
            if (n.Length == 0 || n.Length > 60) return false;
            bool stream = n.Contains("stream") || n.Contains("übertragung") || n.Contains("go live");
            bool stop = n.Contains("beenden") || n.Contains("stop") || n.Contains("end ");
            return stream && stop;
        }

        static IntPtr DiscordWindow()
        {
            foreach (var p in Process.GetProcesses().Where(p => p.ProcessName.StartsWith("Discord", StringComparison.OrdinalIgnoreCase)))
            {
                try { if (p.MainWindowHandle != IntPtr.Zero) return p.MainWindowHandle; } catch { }
            }
            return IntPtr.Zero;
        }

        /// <summary>true = Stream beendet. false = kein Discord-Fenster / kein laufender Stream gefunden.</summary>
        public static Task<bool> StopStreamAsync(Action openDiscord) => Task.Run(async () =>
        {
            try
            {
                var h = DiscordWindow();
                if (h == IntPtr.Zero)
                {
                    // Discord liegt nur im Infobereich → Fenster öffnen, dann suchen
                    openDiscord?.Invoke();
                    for (int i = 0; i < 10 && h == IntPtr.Zero; i++) { await Task.Delay(300); h = DiscordWindow(); }
                    if (h == IntPtr.Zero) return false;
                }
                var root = AutomationElement.FromHandle(h);
                var buttons = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
                // Chromium baut den Bedienungshilfen-Baum erst bei der ersten Anfrage auf → bis zu 3 Versuche
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    foreach (AutomationElement b in root.FindAll(TreeScope.Descendants, buttons))
                    {
                        string name;
                        try { name = b.Current.Name; } catch { continue; }
                        if (!IsStopStream(name)) continue;
                        if (b.TryGetCurrentPattern(InvokePattern.Pattern, out var p)) { ((InvokePattern)p).Invoke(); return true; }
                    }
                    await Task.Delay(500);
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            return false;
        });
    }
}

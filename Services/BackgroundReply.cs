using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;

namespace DynamicIsland
{
    /// <summary>
    /// Antwort unsichtbar senden – ohne das App-Fenster nach vorn zu holen (Windows-Bedienhilfen / UI Automation).
    /// Klappt nur, wenn
    ///   • das Fenster der App offen ist (auch minimiert),
    ///   • der Chat mit dem Absender gerade geöffnet ist (Name steht im Chat-Bereich, nicht nur in der Chat-Liste),
    ///   • das Eingabefeld Text annimmt und die App einen Senden-Knopf hat (WhatsApp, Teams, Signal, Telegram …; Discord hat keinen).
    /// Sonst false – dann übernimmt der normale Weg (App nach vorn, einfügen, Enter).
    /// Gesendet gilt nur, wenn das Eingabefeld danach wieder leer ist.
    /// </summary>
    public static class BackgroundReply
    {
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);

        static readonly Regex SendName = new Regex(@"^(senden|send|nachricht senden|send message|absenden|send a message)\b", RegexOptions.IgnoreCase);

        public sealed class Probe
        {
            public bool WindowFound, ChatOpen, ComposerFound, SendButtonFound;
            public string Note = "";
            public bool Ready => WindowFound && ChatOpen && ComposerFound && SendButtonFound;
            public override string ToString() =>
                $"Fenster {(WindowFound ? "ja" : "nein")} · Chat offen {(ChatOpen ? "ja" : "nein")} · Eingabefeld {(ComposerFound ? "ja" : "nein")} · Senden-Knopf {(SendButtonFound ? "ja" : "nein")}" + (Note.Length > 0 ? " · " + Note : "");
        }

        static string Norm(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        /// <summary>Nur nachsehen, ob unsichtbares Senden ginge (sendet nichts).</summary>
        public static Probe Check(string app, string aumid, string sender) => Run(app, aumid, sender, null);

        /// <summary>Unsichtbar senden. true = Nachricht ist raus (Eingabefeld danach leer).</summary>
        public static bool TrySend(string app, string aumid, string sender, string text, out Probe probe)
        {
            probe = Run(app, aumid, sender, text);
            return probe.Note == "gesendet";
        }

        static Probe Run(string app, string aumid, string sender, string text)
        {
            var p = new Probe();
            try
            {
                var hwnd = AppActivator.FindWindow(app, aumid);
                if (hwnd == IntPtr.Zero) { p.Note = "App-Fenster nicht offen"; return p; }
                p.WindowFound = true;
                var root = AutomationElement.FromHandle(hwnd);

                // Eingabefeld: bearbeitbares Feld ganz unten im Fenster
                var editCond = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
                AutomationElement composer = null; Rect cRect = Rect.Empty;
                foreach (AutomationElement e in root.FindAll(TreeScope.Descendants, editCond))
                {
                    try
                    {
                        if (!e.Current.IsEnabled || !e.TryGetCurrentPattern(ValuePattern.Pattern, out object vo)) continue;
                        if (((ValuePattern)vo).Current.IsReadOnly) continue;
                        var r = e.Current.BoundingRectangle;
                        if (r.IsEmpty || r.Width < 120) continue;
                        if (composer == null || r.Bottom > cRect.Bottom) { composer = e; cRect = r; }
                    }
                    catch { }
                }
                if (composer == null) { p.Note = "kein Eingabefeld gefunden"; return p; }
                p.ComposerFound = true;

                // Chat offen? Name des Absenders muss im Chat-Bereich (über dem Eingabefeld, nicht links in der Liste) stehen
                string want = Norm(StripChannel(sender));
                if (want.Length < 2) { p.Note = "Absender unbekannt"; return p; }
                foreach (AutomationElement e in root.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition))
                {
                    try
                    {
                        string n = Norm(e.Current.Name);
                        if (n.Length == 0 || !(n == want || (n.StartsWith(want) && n.Length <= want.Length + 12))) continue;
                        var r = e.Current.BoundingRectangle;
                        if (r.IsEmpty) continue;
                        if (r.Left >= cRect.Left - 8 && r.Bottom <= cRect.Top && r.Top < cRect.Top) { p.ChatOpen = true; break; }
                    }
                    catch { }
                }
                if (!p.ChatOpen) { p.Note = "anderer Chat offen"; return p; }

                AutomationElement sendBtn = FindSend(root, cRect);
                if (text == null)
                {
                    // Prüfmodus: Senden-Knopf erscheint bei vielen Apps erst mit Text → nur vorhandene zählen, sonst „vermutlich“
                    p.SendButtonFound = sendBtn != null;
                    if (sendBtn == null) p.Note = "Senden-Knopf erst mit Text sichtbar oder nicht vorhanden";
                    return p;
                }

                var vp = (ValuePattern)composer.GetCurrentPattern(ValuePattern.Pattern);
                string before = vp.Current.Value ?? "";
                if (before.Trim().Length > 0) { p.Note = "Eingabefeld enthält schon Text"; return p; }   // Entwurf des Nutzers nicht anfassen
                vp.SetValue(text);
                Thread.Sleep(250);
                if ((vp.Current.Value ?? "").IndexOf(text.Trim(), StringComparison.Ordinal) < 0) { SafeClear(vp); p.Note = "Text wurde nicht übernommen"; return p; }

                sendBtn ??= FindSend(root, cRect);
                if (sendBtn == null || !sendBtn.TryGetCurrentPattern(InvokePattern.Pattern, out object io)) { SafeClear(vp); p.Note = "kein Senden-Knopf"; return p; }
                p.SendButtonFound = true;
                ((InvokePattern)io).Invoke();

                // gesendet = Feld ist wieder leer
                for (int i = 0; i < 10; i++)
                {
                    Thread.Sleep(150);
                    string now = "";
                    try { now = vp.Current.Value ?? ""; } catch { }
                    if (now.Trim().Length == 0) { p.Note = "gesendet"; return p; }
                }
                SafeClear(vp);
                p.Note = "App hat nicht gesendet";
            }
            catch (Exception ex) { Program.Log(ex); p.Note = "Fehler: " + ex.GetType().Name; }
            return p;
        }

        static AutomationElement FindSend(AutomationElement root, Rect composer)
        {
            AutomationElement best = null;
            foreach (AutomationElement b in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)))
            {
                try
                {
                    string n = (b.Current.Name ?? "").Trim();
                    if (!SendName.IsMatch(n) || !b.Current.IsEnabled) continue;
                    var r = b.Current.BoundingRectangle;
                    // in der Zeile des Eingabefelds (oder knapp darunter/daneben)
                    if (r.IsEmpty || r.Bottom < composer.Top - 40 || r.Top > composer.Bottom + 40) continue;
                    best = b;
                }
                catch { }
            }
            return best;
        }

        static void SafeClear(ValuePattern vp) { try { vp.SetValue(""); } catch { } }

        /// <summary>„Luca (#allgemein, Server)“ → „Luca“.</summary>
        static string StripChannel(string s)
        {
            s ??= "";
            int a = s.IndexOf(" (");
            return a > 0 ? s.Substring(0, a) : s;
        }

        public static bool Minimized(IntPtr h) => h != IntPtr.Zero && IsIconic(h);
    }
}


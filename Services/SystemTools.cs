using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace DynamicIsland
{
    // =====================================================================
    //  Offene Fenster (App-Switcher)
    // =====================================================================
    public sealed class WindowInfo
    {
        public IntPtr Handle;
        public string Title = "", ExePath;
    }

    public static class WindowList
    {
        delegate bool EnumWindowsProc(IntPtr h, IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc proc, IntPtr data);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder sb, ref int size);

        static readonly string[] SkipClasses = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow" };

        /// <summary>Sichtbare App-Fenster, das zuletzt benutzte zuerst.</summary>
        public static List<WindowInfo> Open(IntPtr self, int max = 12)
        {
            var list = new List<WindowInfo>();
            try
            {
                EnumWindows((h, d) =>
                {
                    try
                    {
                        if (list.Count >= max) return false;
                        if (h == self || !IsWindowVisible(h)) return true;
                        if (GetWindow(h, 4) != IntPtr.Zero) return true; // hat einen Besitzer (Dialog)
                        long ex = (long)GetWindowLongPtr(h, -20);
                        bool tool = (ex & 0x80) != 0, app = (ex & 0x40000) != 0;
                        if (tool && !app) return true;
                        if (DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return true; // unsichtbar (andere Desktops)
                        int len = GetWindowTextLength(h);
                        if (len <= 0) return true;
                        var sb = new StringBuilder(len + 1);
                        GetWindowText(h, sb, sb.Capacity);
                        var cls = new StringBuilder(256);
                        GetClassName(h, cls, 256);
                        if (SkipClasses.Contains(cls.ToString())) return true;
                        list.Add(new WindowInfo { Handle = h, Title = sb.ToString(), ExePath = ExeOf(h) });
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex) { Program.Log(ex); }
            return list;
        }

        static string ExeOf(IntPtr h)
        {
            GetWindowThreadProcessId(h, out uint pid);
            var p = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
            if (p == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                return QueryFullProcessImageName(p, 0, sb, ref size) ? sb.ToString() : null;
            }
            finally { CloseHandle(p); }
        }

        /// <summary>Holt ein Fenster nach vorne (auch wenn Windows das sonst blockiert).</summary>
        public static void Activate(IntPtr h)
        {
            try
            {
                if (IsIconic(h)) ShowWindow(h, 9);
                keybd_event(0x12, 0, 0, UIntPtr.Zero); // kurz Alt drücken: erlaubt den Fokuswechsel
                keybd_event(0x12, 0, 2, UIntPtr.Zero);
                SetForegroundWindow(h);
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        /// <summary>Eine Taste (optional mit Strg) an das aktive Fenster schicken.</summary>
        public static void Key(byte vk, bool ctrl = false)
        {
            try
            {
                if (ctrl) keybd_event(0x11, 0, 0, UIntPtr.Zero);
                keybd_event(vk, 0, 0, UIntPtr.Zero);
                keybd_event(vk, 0, 2, UIntPtr.Zero);
                if (ctrl) keybd_event(0x11, 0, 2, UIntPtr.Zero);
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        /// <summary>Strg+V an das aktive Fenster schicken.</summary>
        public static void SendPaste()
        {
            try
            {
                keybd_event(0x11, 0, 0, UIntPtr.Zero);
                keybd_event(0x56, 0, 0, UIntPtr.Zero);
                keybd_event(0x56, 0, 2, UIntPtr.Zero);
                keybd_event(0x11, 0, 2, UIntPtr.Zero);
            }
            catch (Exception ex) { Program.Log(ex); }
        }
    }

    // =====================================================================
    //  Zwischenablage-Verlauf (nur im Arbeitsspeicher, nie auf der Festplatte)
    // =====================================================================
    public sealed class ClipItem
    {
        public string Text;                 // Text oder Dateiliste
        public string[] Files;
        public BitmapSource Image;
        public bool Secret;                 // sieht nach Passwort aus → verdeckt anzeigen
        public DateTime At = DateTime.Now;

        public string Preview
        {
            get
            {
                if (Image != null) return $"Bild · {Image.PixelWidth} × {Image.PixelHeight}";
                if (Files != null) return Files.Length == 1 ? System.IO.Path.GetFileName(Files[0]) : $"{Files.Length} Dateien";
                if (Secret) return new string('•', Math.Min(Text.Length, 12));
                var t = Text.Replace("\r", " ").Replace("\n", " ").Trim();
                return t.Length > 70 ? t.Substring(0, 70) + "…" : t;
            }
        }

        public string Glyph => Image != null ? "" : Files != null ? "" : "";
    }

    // =====================================================================
    //  Helligkeit: externe Monitore per DDC/CI, Laptop-Bildschirm per WMI
    // =====================================================================
    public sealed class DisplayInfo
    {
        public string Name = "Bildschirm";
        public int Min, Max = 100, Current;
        public IntPtr Handle;   // physischer Monitor (DDC/CI)
        public bool Wmi;        // Laptop-Bildschirm
        public double Fraction => Max > Min ? (double)(Current - Min) / (Max - Min) : 0;
    }

    public static class Brightness
    {
        delegate bool MonitorEnumProc(IntPtr mon, IntPtr hdc, IntPtr rect, IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
        [DllImport("dxva2.dll")] static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr mon, out uint count);
        [DllImport("dxva2.dll")] static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr mon, uint size, [Out] PHYSICAL_MONITOR[] arr);
        [DllImport("dxva2.dll")] static extern bool GetMonitorBrightness(IntPtr h, out uint min, out uint cur, out uint max);
        [DllImport("dxva2.dll")] static extern bool SetMonitorBrightness(IntPtr h, uint value);
        [DllImport("dxva2.dll")] static extern bool DestroyPhysicalMonitor(IntPtr h);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szPhysicalMonitorDescription;
        }

        static readonly object gate = new object();
        static List<DisplayInfo> cached = new List<DisplayInfo>();
        static readonly Dictionary<DisplayInfo, int> pending = new Dictionary<DisplayInfo, int>();
        static bool worker;

        /// <summary>Sucht alle Bildschirme mit regelbarer Helligkeit (dauert etwas, läuft im Hintergrund).</summary>
        public static Task<List<DisplayInfo>> LoadAsync() => Task.Run(() =>
        {
            var result = new List<DisplayInfo>();
            lock (gate)
            {
                foreach (var d in cached) if (d.Handle != IntPtr.Zero) { try { DestroyPhysicalMonitor(d.Handle); } catch { } }
                cached = new List<DisplayInfo>();
                try
                {
                    var mons = new List<IntPtr>();
                    EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (m, h, r, d) => { mons.Add(m); return true; }, IntPtr.Zero);
                    int n = 1;
                    foreach (var m in mons)
                    {
                        if (!GetNumberOfPhysicalMonitorsFromHMONITOR(m, out uint count) || count == 0) continue;
                        var arr = new PHYSICAL_MONITOR[count];
                        if (!GetPhysicalMonitorsFromHMONITOR(m, count, arr)) continue;
                        foreach (var pm in arr)
                        {
                            if (GetMonitorBrightness(pm.hPhysicalMonitor, out uint min, out uint cur, out uint max) && max > min)
                            {
                                var name = string.IsNullOrWhiteSpace(pm.szPhysicalMonitorDescription) || pm.szPhysicalMonitorDescription.StartsWith("Generic", StringComparison.OrdinalIgnoreCase)
                                    ? "Monitor " + n : pm.szPhysicalMonitorDescription;
                                result.Add(new DisplayInfo { Name = name, Min = (int)min, Max = (int)max, Current = (int)cur, Handle = pm.hPhysicalMonitor });
                                n++;
                            }
                            else DestroyPhysicalMonitor(pm.hPhysicalMonitor);
                        }
                    }
                }
                catch (Exception ex) { Program.Log(ex); }

                // Laptop-Bildschirm (unterstützt meist kein DDC/CI)
                var wmi = RunPowerShell("(Get-CimInstance -Namespace root/wmi -ClassName WmiMonitorBrightness -ErrorAction Stop | Select-Object -First 1).CurrentBrightness", true);
                if (int.TryParse((wmi ?? "").Trim(), out int b))
                    result.Insert(0, new DisplayInfo { Name = "Eingebauter Bildschirm", Min = 0, Max = 100, Current = b, Wmi = true });
                cached = result;
            }
            return result;
        });

        /// <summary>Setzt die Helligkeit (0–1). Schnelle Wiederholungen werden zusammengefasst.</summary>
        public static void Set(DisplayInfo d, double fraction)
        {
            int v = d.Min + (int)Math.Round(Math.Max(0, Math.Min(1, fraction)) * (d.Max - d.Min));
            d.Current = v;
            lock (pending)
            {
                pending[d] = v;
                if (worker) return;
                worker = true;
            }
            Task.Run(() =>
            {
                while (true)
                {
                    KeyValuePair<DisplayInfo, int> job;
                    lock (pending)
                    {
                        if (pending.Count == 0) { worker = false; return; }
                        job = pending.First();
                        pending.Remove(job.Key);
                    }
                    try
                    {
                        if (job.Key.Wmi)
                            RunPowerShell($"(Get-WmiObject -Namespace root/WMI -Class WmiMonitorBrightnessMethods).WmiSetBrightness(1,{job.Value})", false);
                        else
                            lock (gate) SetMonitorBrightness(job.Key.Handle, (uint)job.Value);
                    }
                    catch (Exception ex) { Program.Log(ex); }
                }
            });
        }

        static string RunPowerShell(string command, bool output)
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"" + command.Replace("\"", "\\\"") + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = output,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                if (p == null) return null;
                string text = output ? p.StandardOutput.ReadToEnd() : null;
                p.WaitForExit(8000);
                return text;
            }
            catch { return null; }
        }
    }

    // =====================================================================
    //  Xbox-Controller (XInput): verbunden / getrennt + Akkustand
    // =====================================================================
    public static class XInput
    {
        [StructLayout(LayoutKind.Sequential)]
        struct XINPUT_STATE { public uint PacketNumber; public ushort Buttons; public byte LT, RT; public short LX, LY, RX, RY; }

        [StructLayout(LayoutKind.Sequential)]
        struct XINPUT_BATTERY_INFORMATION { public byte BatteryType; public byte BatteryLevel; }

        [DllImport("xinput1_4.dll")] static extern int XInputGetState(int index, out XINPUT_STATE state);
        [DllImport("xinput1_4.dll")] static extern int XInputGetBatteryInformation(int index, byte devType, out XINPUT_BATTERY_INFORMATION info);

        static bool broken;

        public static bool Connected(int i)
        {
            if (broken) return false;
            try { return XInputGetState(i, out _) == 0; }
            catch { broken = true; return false; }
        }

        /// <summary>Akku in Prozent (XInput kennt nur 4 Stufen), null = Kabel / unbekannt.</summary>
        public static int? Battery(int i)
        {
            if (broken) return null;
            try
            {
                if (XInputGetBatteryInformation(i, 0, out var b) != 0) return null;
                if (b.BatteryType == 0 || b.BatteryType == 1 || b.BatteryType == 0xFF) return null; // getrennt, Kabel, unbekannt
                return b.BatteryLevel switch { 0 => 5, 1 => 30, 2 => 65, _ => 100 };
            }
            catch { return null; }
        }
    }
}

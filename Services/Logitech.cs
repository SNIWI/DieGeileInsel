using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace DynamicIsland
{
    /// <summary>Eine Logitech-Maus / -Tastatur mit Akkustand.</summary>
    public sealed class LogiDevice
    {
        public string Key, Name;
        /// <summary>0 Tastatur, 3 Maus, 4 Touchpad, 5 Trackball, 8 Headset … (HID++ Gerätetyp)</summary>
        public int Kind = -1;
        public int Percent;
        public bool Charging, Full;
        /// <summary>Gerät schläft gerade (antwortet nicht) – Wert ist der zuletzt gelesene.</summary>
        public bool Asleep;
        public DateTime SeenAt = DateTime.Now;
        public bool IsMouse => Kind == 3 || Kind == 4 || Kind == 5;
        public bool IsKeyboard => Kind == 0 || Kind == 2;
        public string Glyph => IsKeyboard ? "" : Kind == 8 ? "" : "";
    }

    /// <summary>
    /// Akkustand kabelloser Logitech-Geräte über das Logitech-Protokoll HID++ 2.0 – direkt über Windows,
    /// ohne G HUB / Options+: Empfänger (Lightspeed, Bolt, Unifying, Geräte 1–6) und per Bluetooth verbundene Geräte.
    /// Liest die Funktionen UNIFIED_BATTERY (0x1004), BATTERY_STATUS (0x1000) oder BATTERY_VOLTAGE (0x1001)
    /// sowie Name und Typ (0x0005).
    /// </summary>
    public static class Logitech
    {
        // ---------------- Windows-HID ----------------
        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

        [StructLayout(LayoutKind.Sequential)]
        struct HIDP_CAPS
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
                          NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
                          NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid g);
        [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
        [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data, ref HIDP_CAPS caps);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr en, IntPtr hwnd, int flags);
        [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid g, int idx, ref SP_DEVICE_INTERFACE_DATA d);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA d, IntPtr detail, int size, out int req, IntPtr info);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);

        /// <summary>Pfade der HID++-„langen“ Schnittstellen (Logitech-Empfänger: FF00/0002, Bluetooth: FF43/0202).</summary>
        static List<(string Path, bool Receiver)> HidppPaths()
        {
            var res = new List<(string, bool)>();
            HidD_GetHidGuid(out var g);
            IntPtr set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12);   // PRESENT | DEVICEINTERFACE
            if (set == IntPtr.Zero || set == new IntPtr(-1)) return res;
            try
            {
                for (int i = 0; ; i++)
                {
                    var d = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref d)) break;
                    SetupDiGetDeviceInterfaceDetail(set, ref d, IntPtr.Zero, 0, out int need, IntPtr.Zero);
                    if (need <= 0) continue;
                    IntPtr buf = Marshal.AllocHGlobal(need);
                    try
                    {
                        Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref d, buf, need, out _, IntPtr.Zero)) continue;
                        string path = Marshal.PtrToStringUni(buf + 4);
                        if (path == null || path.IndexOf("vid_046d", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        using var h = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);   // nur Eigenschaften lesen
                        if (h.IsInvalid) continue;
                        if (!HidD_GetPreparsedData(h, out var pd)) continue;
                        var caps = new HIDP_CAPS();
                        try { HidP_GetCaps(pd, ref caps); } finally { HidD_FreePreparsedData(pd); }
                        if (caps.UsagePage == 0xFF00 && caps.Usage == 0x0002 && caps.OutputReportByteLength == 20) res.Add((path, true));
                        else if (caps.UsagePage == 0xFF43 && caps.Usage == 0x0202 && caps.OutputReportByteLength == 20) res.Add((path, false));
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return res;
        }

        // ---------------- HID++ ----------------
        sealed class Conn : IDisposable
        {
            readonly FileStream fs;
            byte sw = 0x9;
            public Conn(SafeFileHandle h) { fs = new FileStream(h, FileAccess.ReadWrite, 0, true); }
            public void Dispose() { try { fs.Dispose(); } catch { } }

            /// <summary>Anfrage senden, passende Antwort (Parameter ab Byte 4) abwarten. null = Fehler / keine Antwort.</summary>
            public async Task<byte[]> Request(byte dev, byte feat, byte func, int timeoutMs, params byte[] args)
            {
                sw = (byte)(sw >= 0xE ? 0x9 : sw + 1);
                var msg = new byte[20];
                msg[0] = 0x11; msg[1] = dev; msg[2] = feat; msg[3] = (byte)((func << 4) | sw);
                Array.Copy(args, 0, msg, 4, Math.Min(args.Length, 16));
                using var cts = new CancellationTokenSource(timeoutMs);
                try
                {
                    await fs.WriteAsync(msg, 0, 20, cts.Token);
                    var buf = new byte[64];
                    while (true)
                    {
                        int n = await fs.ReadAsync(buf, 0, buf.Length, cts.Token);
                        if (n < 7 || buf[1] != dev) continue;
                        if (buf[2] == feat && (buf[3] & 0x0F) == sw && buf[0] == 0x11)
                            return buf.Skip(4).Take(16).ToArray();
                        if (buf[2] == 0xFF && buf[3] == feat && (buf[4] & 0x0F) == sw) return null;   // HID++ 2.0 Fehler
                        if (buf[2] == 0x8F && buf[3] == feat) return null;                           // Gerät nicht erreichbar
                    }
                }
                catch { return null; }
            }
        }

        sealed class Cached { public string Name; public int Kind = -1; public byte NameFeat, BatFeat; public ushort BatId; }
        static readonly Dictionary<string, Cached> cache = new Dictionary<string, Cached>();

        static async Task<byte> FeatureIndex(Conn c, byte dev, ushort id, int timeout = 500)
        {
            var r = await c.Request(dev, 0x00, 0x0, timeout, (byte)(id >> 8), (byte)id);
            return r == null ? (byte)0 : r[0];
        }

        static int VoltToPercent(int mv)
        {
            int[,] t = { { 4186, 100 }, { 4067, 90 }, { 3989, 80 }, { 3922, 70 }, { 3859, 60 }, { 3811, 50 }, { 3778, 40 }, { 3751, 30 }, { 3717, 20 }, { 3671, 10 }, { 3646, 5 }, { 3579, 2 }, { 3500, 0 } };
            if (mv >= t[0, 0]) return 100;
            for (int i = 1; i < t.GetLength(0); i++)
                if (mv >= t[i, 0])
                {
                    double f = (mv - t[i, 0]) / (double)(t[i - 1, 0] - t[i, 0]);
                    return (int)Math.Round(t[i, 1] + f * (t[i - 1, 1] - t[i, 1]));
                }
            return 0;
        }

        static async Task<LogiDevice> QueryDevice(Conn c, string path, byte dev)
        {
            string key = path + "#" + dev;
            if (!cache.TryGetValue(key, out var info))
            {
                // Erreichbar? (Root-Funktion „Feature-Index von DEVICE_NAME“)
                byte nameFeat = await FeatureIndex(c, dev, 0x0005, 400);
                if (nameFeat == 0) return null;
                info = new Cached { NameFeat = nameFeat };
                foreach (ushort id in new ushort[] { 0x1004, 0x1000, 0x1001 })
                {
                    byte fi = await FeatureIndex(c, dev, id);
                    if (fi != 0) { info.BatFeat = fi; info.BatId = id; break; }
                }
                var cnt = await c.Request(dev, nameFeat, 0x0, 500);
                if (cnt != null)
                {
                    int len = cnt[0];
                    var sb = new StringBuilder();
                    while (sb.Length < len && sb.Length < 64)
                    {
                        var part = await c.Request(dev, nameFeat, 0x1, 500, (byte)sb.Length);
                        if (part == null) break;
                        int before = sb.Length;
                        foreach (var b in part) { if (b == 0 || sb.Length >= len) break; sb.Append((char)b); }
                        if (sb.Length == before) break;
                    }
                    info.Name = sb.ToString().Trim();
                }
                var ty = await c.Request(dev, nameFeat, 0x2, 500);
                if (ty != null) info.Kind = ty[0];
                if (string.IsNullOrEmpty(info.Name)) info.Name = info.Kind == 0 ? "Logitech-Tastatur" : "Logitech-Maus";
                cache[key] = info;
            }
            if (info.BatFeat == 0) return null;
            var d = new LogiDevice { Key = key, Name = info.Name, Kind = info.Kind };
            if (info.BatId == 0x1004)
            {
                var r = await c.Request(dev, info.BatFeat, 0x1, 600);
                if (r == null) { cache.Remove(key); return null; }
                int pct = r[0];
                if (pct == 0) pct = (r[1] & 8) != 0 ? 100 : (r[1] & 4) != 0 ? 60 : (r[1] & 2) != 0 ? 20 : (r[1] & 1) != 0 ? 5 : 0;
                d.Percent = pct; d.Charging = r[2] == 1 || r[2] == 2; d.Full = r[2] == 3;
            }
            else if (info.BatId == 0x1000)
            {
                var r = await c.Request(dev, info.BatFeat, 0x0, 600);
                if (r == null) { cache.Remove(key); return null; }
                d.Percent = r[0]; d.Charging = r[2] == 1 || r[2] == 2 || r[2] == 4; d.Full = r[2] == 3;
            }
            else
            {
                var r = await c.Request(dev, info.BatFeat, 0x0, 600);
                if (r == null) { cache.Remove(key); return null; }
                d.Percent = VoltToPercent((r[0] << 8) | r[1]); d.Charging = (r[2] & 0x80) != 0;
            }
            d.Percent = Math.Max(0, Math.Min(100, d.Percent));
            return d;
        }

        static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        /// <summary>Alle erreichbaren Logitech-Geräte mit Akku (läuft im Hintergrund, ein paar hundert ms).</summary>
        public static async Task<List<LogiDevice>> ScanAsync()
        {
            var list = new List<LogiDevice>();
            if (!await gate.WaitAsync(0)) return null;
            try
            {
                await Task.Run(async () =>
                {
                    foreach (var (path, receiver) in HidppPaths())
                    {
                        try
                        {
                            var h = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);   // lesen+schreiben, geteilt, asynchron
                            if (h.IsInvalid) { h.Dispose(); continue; }
                            using var c = new Conn(h);
                            if (receiver) { for (byte dev = 1; dev <= 6; dev++) { var d = await QueryDevice(c, path, dev); if (d != null) list.Add(d); } }
                            else { var d = await QueryDevice(c, path, 0xFF); if (d != null) list.Add(d); }
                        }
                        catch (Exception ex) { Program.Log(ex); }
                    }
                });
            }
            finally { gate.Release(); }
            // gleiche Geräte (über mehrere Wege erreichbar) nur einmal
            return list.GroupBy(d => d.Name).Select(g => g.First()).OrderBy(d => d.IsKeyboard ? 1 : 0).ToList();
        }
    }
}

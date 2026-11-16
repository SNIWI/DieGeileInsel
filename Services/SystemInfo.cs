using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Enumeration.Pnp;

namespace DynamicIsland
{
    /// <summary>CPU- und RAM-Auslastung.</summary>
    public sealed class SysStats
    {
        long lastIdle, lastTotal;
        public double Cpu, Ram, RamUsedGb, RamTotalGb;

        public void Sample()
        {
            if (Native.GetSystemTimes(out var idle, out var kernel, out var user))
            {
                long total = kernel + user; // kernel enthält idle
                if (lastTotal != 0)
                {
                    long dt = total - lastTotal, di = idle - lastIdle;
                    if (dt > 0) Cpu = Math.Max(0, Math.Min(1, 1.0 - (double)di / dt));
                }
                lastIdle = idle;
                lastTotal = total;
            }
            var m = new Native.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
            if (Native.GlobalMemoryStatusEx(ref m))
            {
                Ram = m.dwMemoryLoad / 100.0;
                RamTotalGb = m.ullTotalPhys / 1073741824.0;
                RamUsedGb = (m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0;
            }
        }
    }

    /// <summary>
    /// Welche Apps nutzen gerade Kamera oder Mikrofon? Windows protokolliert das in der Registry
    /// (dasselbe, was das Kamera-Symbol in der Taskleiste anzeigt).
    /// </summary>
    public static class Privacy
    {
        const string Root = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\";

        public static List<string> AppsUsing(string capability)
        {
            var result = new List<string>();
            try
            {
                using var root = Registry.CurrentUser.OpenSubKey(Root + capability);
                if (root == null) return result;
                foreach (var name in root.GetSubKeyNames())
                {
                    if (name == "NonPackaged")
                    {
                        using var np = root.OpenSubKey(name);
                        if (np == null) continue;
                        foreach (var exe in np.GetSubKeyNames())
                        {
                            using var k = np.OpenSubKey(exe);
                            if (InUse(k)) result.Add(Path.GetFileNameWithoutExtension(exe.Replace('#', '\\')));
                        }
                    }
                    else
                    {
                        using var k = root.OpenSubKey(name);
                        if (InUse(k)) result.Add(Pretty(name));
                    }
                }
            }
            catch { }
            result.RemoveAll(n => n.Equals("DynamicIsland", StringComparison.OrdinalIgnoreCase));
            return result.Distinct().ToList();
        }

        static bool InUse(RegistryKey k)
        {
            if (k == null) return false;
            try
            {
                var stop = k.GetValue("LastUsedTimeStop");
                var start = k.GetValue("LastUsedTimeStart");
                return stop is long s && s == 0 && start is long st && st > 0;
            }
            catch { return false; }
        }

        static string Pretty(string pkg)
        {
            var n = pkg.Split('_')[0];
            int i = n.LastIndexOf('.');
            return i >= 0 ? n.Substring(i + 1) : n;
        }
    }

    /// <summary>Ein verbundenes Bluetooth-Gerät (klassisch, kein BLE).</summary>
    public sealed class BtDev
    {
        public string Name;
        public Guid? Container;
        public int? Battery;
        public bool IsAudio;
        public DateTime Since;
        internal int Seen, Missed;
        internal bool Announced;
    }

    /// <summary>
    /// Erkennt, wenn Bluetooth-Geräte (Kopfhörer, Controller, Maus …) verbunden/getrennt werden.
    /// Gemeldet wird erst, wenn ein Gerät stabil da ist bzw. wirklich weg ist – Bluetooth flackert
    /// sonst ständig (Geräte im Standby, BLE-Verbindungen, die nur kurz aufgehen).
    /// </summary>
    public sealed class BtScanner
    {
        const int SeenToConnect = 2;   // 2 Abfragen hintereinander da (≈ 8 s)
        const int MissedToDrop = 4;    // 4 Abfragen hintereinander weg (≈ 16 s)
        readonly Dictionary<string, BtDev> devs = new Dictionary<string, BtDev>(StringComparer.OrdinalIgnoreCase);
        bool busy, first = true, noCod;
        int polls;

        /// <summary>Etwas hat sich geändert (Gerät da/weg, Akku).</summary>
        public event Action Changed;

        public List<BtDev> Connected => devs.Values.Where(d => d.Announced).OrderByDescending(d => d.Since).ToList();

        /// <summary>Zuletzt verbundene Kopfhörer (skip = z. B. AirPods, die eine eigene Seite haben).</summary>
        public BtDev Headphones(Func<string, bool> skip = null) =>
            devs.Values.Where(d => d.Announced && d.IsAudio && (skip == null || !skip(d.Name)))
                       .OrderByDescending(d => d.Since).FirstOrDefault();

        static readonly string[] AudioHints = { "pod", "bud", "head", "ear", "wh-", "wf-", "jbl", "bose", "beats", "sony", "kopfh", "headset", "speaker", "lautspr", "soundcore", "jabra", "sennheiser", "marshall" };
        static bool LooksAudio(string n) { n = (n ?? "").ToLowerInvariant(); return AudioHints.Any(h => n.Contains(h)); }

        public async Task PollAsync(Action<string, int?> connected, Action<string> disconnected)
        {
            if (busy) return;
            busy = true;
            bool changed = false;
            try
            {
                const string cKey = "System.Devices.Aep.ContainerId", codKey = "System.Devices.Aep.Bluetooth.Cod.Major";
                var sel = BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
                DeviceInformationCollection list;
                try { list = await DeviceInformation.FindAllAsync(sel, noCod ? new[] { cKey } : new[] { cKey, codKey }); }
                catch (ArgumentException) { noCod = true; list = await DeviceInformation.FindAllAsync(sel, new[] { cKey }); }

                var now = new Dictionary<string, (Guid? c, int major)>(StringComparer.OrdinalIgnoreCase);
                foreach (var d in list)
                {
                    if (string.IsNullOrWhiteSpace(d.Name)) continue;
                    Guid? c = null; int major = -1;
                    if (d.Properties.TryGetValue(cKey, out var v) && v is Guid g) c = g;
                    if (d.Properties.TryGetValue(codKey, out var m) && m != null) { try { major = Convert.ToInt32(m); } catch { } }
                    now[d.Name] = (c, major);
                }

                foreach (var kv in now)
                {
                    if (!devs.TryGetValue(kv.Key, out var dev)) devs[kv.Key] = dev = new BtDev { Name = kv.Key };
                    dev.Seen++;
                    dev.Missed = 0;
                    dev.Container = kv.Value.c ?? dev.Container;
                    dev.IsAudio = kv.Value.major == 4 || (kv.Value.major < 0 && LooksAudio(kv.Key)) || (kv.Value.major == 0 && LooksAudio(kv.Key));
                    if (!dev.Announced && (first || dev.Seen >= SeenToConnect))
                    {
                        dev.Announced = true;
                        dev.Since = DateTime.Now;
                        if (dev.Container.HasValue) dev.Battery = await BatteryAsync(dev.Container.Value);
                        if (!first) connected?.Invoke(dev.Name, dev.Battery);
                        changed = true;
                    }
                }
                foreach (var dev in devs.Values.ToList())
                {
                    if (now.ContainsKey(dev.Name)) continue;
                    dev.Seen = 0;
                    dev.Missed++;
                    if (!dev.Announced) { devs.Remove(dev.Name); continue; }
                    if (dev.Missed < MissedToDrop) continue;
                    devs.Remove(dev.Name);
                    // nur melden, wenn es vorher eine Weile verbunden war
                    if ((DateTime.Now - dev.Since).TotalSeconds >= 60) disconnected?.Invoke(dev.Name);
                    changed = true;
                }

                // Akku der Kopfhörer etwa jede Minute auffrischen
                if (++polls % 15 == 0)
                    foreach (var dev in devs.Values.Where(d => d.Announced && d.Container.HasValue).ToList())
                    {
                        var b = await BatteryAsync(dev.Container.Value);
                        if (b != dev.Battery) { dev.Battery = b; changed = true; }
                    }
                first = false;
            }
            catch (Exception ex) { Program.Log(ex); }
            finally { busy = false; }
            if (changed) Changed?.Invoke();
        }

        /// <summary>Akkustand, falls das Gerät ihn an Windows meldet (klappt bei vielen Kopfhörern).</summary>
        static async Task<int?> BatteryAsync(Guid container)
        {
            try
            {
                const string key = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
                var aqs = "System.Devices.ContainerId:=\"{" + container + "}\"";
                var objs = await PnpObject.FindAllAsync(PnpObjectType.Device, new[] { key }, aqs);
                foreach (var o in objs)
                    if (o.Properties.TryGetValue(key, out var v) && v != null)
                        return Convert.ToInt32(v);
            }
            catch { }
            return null;
        }
    }
}

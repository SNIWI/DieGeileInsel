using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Media.Control;
using Windows.Media.Devices;
using Windows.Storage.Streams;

namespace DynamicIsland
{
    // =====================================================================
    //  AirPods Companion – fest in die Island eingebaut (früher eigenes Tray-Programm).
    //  Akku links/rechts/Case, Ohrerkennung mit Auto-Pause, automatisches Umschalten
    //  der Audioausgabe, Akku-Karte beim Öffnen des Case, Warnung bei schwachem Akku.
    // =====================================================================

    /// <summary>Zustand eines einzelnen AirPods. Battery in Prozent (10er-Schritte) oder null = unbekannt.</summary>
    public sealed record PodInfo(int? Battery, bool Charging, bool InEar);

    public sealed record AirPodsStatus(
        ushort ModelId,
        string Model,
        PodInfo Left,
        PodInfo Right,
        int? CaseBattery,
        bool CaseCharging,
        bool LidOpen,
        short Rssi,
        string RawHex,
        bool Flipped = false)
    {
        public int InEarCount => (Left.InEar ? 1 : 0) + (Right.InEar ? 1 : 0);
        public bool AnyInEar => InEarCount > 0;

        /// <summary>Ein AirPod, der lädt, steckt im Case.</summary>
        public bool AnyPodInCase => Left.Charging || Right.Charging;

        /// <summary>Over-Ear (AirPods Max, Beats Solo/Studio) haben kein Case und keine zwei Hörer.</summary>
        public bool IsHeadphones => Model.Contains("Max") || Model.Contains("Solo") || Model.Contains("Studio 3");

        /// <summary>Niedrigster Akkustand der AirPods, die gerade benutzt werden (sonst beider).</summary>
        public int? LowestBattery
        {
            get
            {
                var vals = new List<int>();
                foreach (var p in new[] { Left, Right })
                    if (p.Battery is int b && (!AnyInEar || p.InEar))
                        vals.Add(b);
                return vals.Count == 0 ? null : vals.Min();
            }
        }
    }

    /// <summary>
    /// Dekodiert Apples "Proximity Pairing"-BLE-Werbepakete (Hersteller-ID 0x004C, Typ 0x07).
    /// Diese senden AirPods/Beats unverschlüsselt, sobald das Case offen ist oder sie
    /// außerhalb des Case sind – daraus kommen Akku, Ladezustand, Ohrerkennung und Deckelstatus.
    /// </summary>
    public static class AirPodsParser
    {
        public const ushort AppleCompanyId = 0x004C;

        static readonly Dictionary<ushort, string> Models = new Dictionary<ushort, string>
        {
            [0x0220] = "AirPods (1. Gen)",
            [0x0F20] = "AirPods (2. Gen)",
            [0x1320] = "AirPods (3. Gen)",
            [0x1920] = "AirPods 4",
            [0x1B20] = "AirPods 4 (ANC)",
            [0x0E20] = "AirPods Pro",
            [0x1420] = "AirPods Pro 2",
            [0x2420] = "AirPods Pro 2 (USB-C)",
            [0x2720] = "AirPods Pro 3",
            [0x0A20] = "AirPods Max",
            [0x1F20] = "AirPods Max (USB-C)",
            [0x0320] = "Powerbeats 3",
            [0x0B20] = "Powerbeats Pro",
            [0x0C20] = "Beats Solo Pro",
            [0x1120] = "Beats Studio Buds",
            [0x1020] = "Beats Flex",
            [0x0520] = "BeatsX",
            [0x0620] = "Beats Solo 3",
            [0x0920] = "Beats Studio 3",
            [0x1720] = "Beats Studio Buds+",
            [0x1220] = "Beats Fit Pro",
        };

        /// <param name="data">ManufacturerData-Payload ohne die Company-ID.</param>
        public static AirPodsStatus TryParse(byte[] data, short rssi)
        {
            // Apple packt mehrere TLV-Blöcke hintereinander: [Typ][Länge][Daten…]
            int i = 0;
            while (i + 1 < data.Length)
            {
                byte type = data[i];
                int len = data[i + 1];
                int start = i + 2;
                if (start + len > data.Length) break;
                if (type == 0x07 && len >= 7)
                    return ParseProximityPairing(data, start, len, rssi);
                i = start + len;
            }
            return null;
        }

        static AirPodsStatus ParseProximityPairing(byte[] d, int o, int len, short rssi)
        {
            // o+0 Präfix · o+1 Modell (2 Byte) · o+3 Status (Bit5 = linker sendet, Bit1 = sendender im Ohr,
            // Bit3 = anderer im Ohr) · o+4 Akku (je ein Nibble, 0–10 → 0–100 %, 15 = unbekannt)
            // o+5 Laden (Bit4/Bit5 AirPods, Bit6 Case, unteres Nibble Case-Akku) · o+6 Deckel (Bit3 == 0 → offen)
            ushort model = (ushort)((d[o + 1] << 8) | d[o + 2]);
            byte status = d[o + 3];
            byte battery = d[o + 4];
            byte charge = d[o + 5];
            byte lid = d[o + 6];

            // Ausrichtung wie bei OpenPods/LibrePods: Bit5 = 0 → „gespiegelt“.
            // Vorher waren hier die Akku-Nibbles vertauscht – daher kam z. B. „linker AirPod leer“.
            bool flip = (status & 0x20) == 0;
            int hiLevel = battery >> 4, loLevel = battery & 0x0F;
            bool leftCharging = (charge & (flip ? 0x20 : 0x10)) != 0;
            bool rightCharging = (charge & (flip ? 0x10 : 0x20)) != 0;
            bool leftEar = (status & (flip ? 0x08 : 0x02)) != 0;
            bool rightEar = (status & (flip ? 0x02 : 0x08)) != 0;
            // Ein ladender AirPod liegt im Case → nie "im Ohr"
            var left = new PodInfo(Level(flip ? hiLevel : loLevel), leftCharging, leftEar && !leftCharging);
            var right = new PodInfo(Level(flip ? loLevel : hiLevel), rightCharging, rightEar && !rightCharging);

            string name = Models.TryGetValue(model, out var n) ? n : $"Apple-Kopfhörer (0x{model:X4})";
            string hex = BitConverter.ToString(d, o - 2, len + 2).Replace("-", " ");

            return new AirPodsStatus(model, name, left, right, Level(charge & 0x0F), (charge & 0x40) != 0,
                ((lid >> 3) & 1) == 0, rssi, hex, flip);
        }

        static int? Level(int nibble) => nibble <= 10 ? nibble * 10 : null;
    }

    /// <summary>Lauscht per Bluetooth LE auf die Werbepakete der AirPods.</summary>
    public sealed class AirPodsWatcher : IDisposable
    {
        BluetoothLEAdvertisementWatcher watcher;
        readonly Timer restartTimer;
        volatile bool disposed;
        string lastLoggedHex = "";
        DateTime lastLogTime = DateTime.MinValue;

        public event Action<AirPodsStatus> StatusReceived;
        public event Action<string> Log;
        public int RssiThreshold { get; set; } = -65;

        public AirPodsWatcher()
        {
            restartTimer = new Timer(_ => Restart(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            if (disposed) return;
            watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
            watcher.Received += OnReceived;
            watcher.Stopped += OnStopped;
            try
            {
                watcher.Start();
                Log?.Invoke("Bluetooth-Scan gestartet.");
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Bluetooth-Scan konnte nicht starten ({ex.Message}). Ist Bluetooth an? Neuer Versuch in 5 s.");
                restartTimer.Change(5000, Timeout.Infinite);
            }
        }

        public void Stop()
        {
            restartTimer.Change(Timeout.Infinite, Timeout.Infinite);
            Detach();
        }

        void Restart()
        {
            if (disposed) return;
            Detach();
            Start();
        }

        void Detach()
        {
            if (watcher == null) return;
            try
            {
                watcher.Received -= OnReceived;
                watcher.Stopped -= OnStopped;
                if (watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started) watcher.Stop();
            }
            catch { }
            watcher = null;
        }

        void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            if (disposed || watcher == null) return;
            Log?.Invoke($"Bluetooth-Scan gestoppt ({args.Error}). Neuer Versuch in 5 s.");
            restartTimer.Change(5000, Timeout.Infinite);
        }

        void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            foreach (var md in args.Advertisement.ManufacturerData)
            {
                if (md.CompanyId != AirPodsParser.AppleCompanyId || md.Data.Length < 9) continue;
                var bytes = new byte[md.Data.Length];
                using (var reader = DataReader.FromBuffer(md.Data))
                    reader.ReadBytes(bytes);
                var status = AirPodsParser.TryParse(bytes, args.RawSignalStrengthInDBm);
                if (status == null) continue;
                bool accepted = status.Rssi >= RssiThreshold;
                LogThrottled(status, accepted);
                if (accepted) StatusReceived?.Invoke(status);
            }
        }

        void LogThrottled(AirPodsStatus s, bool accepted)
        {
            var now = DateTime.Now;
            if (s.RawHex == lastLoggedHex && (now - lastLogTime).TotalSeconds < 5) return;
            lastLoggedHex = s.RawHex;
            lastLogTime = now;
            Log?.Invoke(
                $"{(accepted ? "✓" : "✗ zu weit")} {s.Model}  RSSI {s.Rssi}  " +
                $"L {Fmt(s.Left)}  R {Fmt(s.Right)}  Case {(s.CaseBattery?.ToString() ?? "–")}{(s.CaseCharging ? "⚡" : "")}  " +
                $"Deckel {(s.LidOpen ? "offen" : "zu")}  [{s.RawHex}]");
        }

        static string Fmt(PodInfo p) =>
            $"{(p.Battery?.ToString() ?? "–")}{(p.Charging ? "⚡" : "")}{(p.InEar ? " (Ohr)" : "")}";

        public void Dispose()
        {
            disposed = true;
            restartTimer.Dispose();
            Detach();
        }
    }

    /// <summary>
    /// Pausiert/startet Medien gezielt über die Windows-Mediensteuerung. Merkt sich, was WIR
    /// pausiert haben, damit nur das weiterläuft.
    /// </summary>
    public sealed class AirPodsMedia
    {
        GlobalSystemMediaTransportControlsSessionManager manager;
        readonly HashSet<string> pausedByUs = new HashSet<string>();
        public DateTime PausedAt { get; private set; }
        public bool HasPaused => pausedByUs.Count > 0;

        async Task<GlobalSystemMediaTransportControlsSessionManager> ManagerAsync() =>
            manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

        public async Task<int> PauseAllPlayingAsync()
        {
            pausedByUs.Clear();
            var mgr = await ManagerAsync();
            foreach (var session in mgr.GetSessions())
            {
                try
                {
                    var info = session.GetPlaybackInfo();
                    if (info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                        && await session.TryPauseAsync())
                        pausedByUs.Add(session.SourceAppUserModelId);
                }
                catch { }
            }
            if (pausedByUs.Count > 0) PausedAt = DateTime.Now;
            return pausedByUs.Count;
        }

        public async Task<int> ResumeAsync()
        {
            int resumed = 0;
            var mgr = await ManagerAsync();
            foreach (var session in mgr.GetSessions())
            {
                try
                {
                    if (pausedByUs.Contains(session.SourceAppUserModelId) && await session.TryPlayAsync())
                        resumed++;
                }
                catch { }
            }
            pausedByUs.Clear();
            return resumed;
        }

        public void Forget() => pausedByUs.Clear();
    }

    public sealed record AirPodsEndpoint(string Id, string Name);

    /// <summary>Findet die AirPods als Windows-Audiogerät und macht sie zum Standardgerät.</summary>
    public static class AirPodsAudio
    {
        static readonly string[] HandsFreeHints = { "Hands-Free", "Freisprech", "Headset" };

        /// <summary>Verbundenes Wiedergabegerät mit dem Filter im Namen (Stereo statt Freisprechen bevorzugt).</summary>
        public static async Task<AirPodsEndpoint> FindAsync(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return null;
            var devices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());
            AirPodsEndpoint fallback = null;
            foreach (var d in devices)
            {
                if (!d.IsEnabled || d.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool handsFree = HandsFreeHints.Any(h => d.Name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!handsFree) return new AirPodsEndpoint(d.Id, d.Name);
                fallback ??= new AirPodsEndpoint(d.Id, d.Name);
            }
            return fallback;
        }

        public static bool IsDefault(string id) =>
            string.Equals(MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default), id, StringComparison.OrdinalIgnoreCase);

        /// <summary>Ist gerade ein Gerät mit diesem Namen das Standard-Ausgabegerät?</summary>
        public static async Task<bool> DefaultMatchesAsync(string filter)
        {
            string id = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
            if (string.IsNullOrEmpty(id)) return false;
            try
            {
                var info = await DeviceInformation.CreateFromIdAsync(id);
                return info.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        public static bool SetDefault(string deviceInterfaceId, bool includeCommunications)
        {
            string mmId = ToMmDeviceId(deviceInterfaceId);
            try
            {
                var policy = (IPolicyConfig)new CPolicyConfigClient();
                int hr = policy.SetDefaultEndpoint(mmId, ERole.eConsole);
                if (hr == 0) hr = policy.SetDefaultEndpoint(mmId, ERole.eMultimedia);
                if (hr == 0 && includeCommunications) hr = policy.SetDefaultEndpoint(mmId, ERole.eCommunications);
                Marshal.ReleaseComObject(policy);
                return hr == 0;
            }
            catch { return false; }
        }

        // WinRT: "\\?\SWD#MMDEVAPI#{0.0.0.00000000}.{guid}#{…}" → Core Audio: "{0.0.0.00000000}.{guid}"
        static string ToMmDeviceId(string id)
        {
            const string marker = "MMDEVAPI#";
            int start = id.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return id;
            start += marker.Length;
            int end = id.IndexOf('#', start);
            return end > start ? id.Substring(start, end - start) : id.Substring(start);
        }

        enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

        [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
        class CPolicyConfigClient { }

        [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPolicyConfig
        {
            [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr format);
            [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int isDefault, IntPtr format);
            [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
            [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr endpointFormat, IntPtr mixFormat);
            [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int isDefault, IntPtr defaultPeriod, IntPtr minPeriod);
            [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr period);
            [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
            [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
            [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, int fxStore, IntPtr key, IntPtr value);
            [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, int fxStore, IntPtr key, IntPtr value);
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, ERole role);
            [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
        }
    }

    /// <summary>Die Logik: Ohrerkennung, Auto-Pause, Audio-Umschaltung, Akku-Warnungen. Läuft auf dem UI-Thread.</summary>
    public sealed class AirPodsEngine : IDisposable
    {
        readonly Settings s;
        readonly Dispatcher ui;
        readonly AirPodsWatcher watcher = new AirPodsWatcher();
        readonly AirPodsMedia media = new AirPodsMedia();
        readonly DispatcherTimer timer;
        readonly List<string> log = new List<string>();

        DateTime lastSeen = DateTime.MinValue, lastPopup = DateTime.MinValue, lidClosedAt = DateTime.MinValue, endpointGoneAt = DateTime.MinValue;
        bool wasLidOpen, endpointPresent, tickBusy, running;
        int stableInEar = -1, pendingInEar = -1, pendingHits, countBeforePause;
        DateTime pendingSince;
        bool lastFlip;
        readonly Steady leftBat = new Steady(), rightBat = new Steady(), caseBat = new Steady();

        /// <summary>
        /// Akkuwerte flackern in den Paketen (z. B. kurz 0 oder ein Nachbarwert). Ein neuer Wert gilt
        /// erst, wenn er 3× hintereinander kommt – so gibt es keine falschen „leer“-Meldungen mehr.
        /// </summary>
        sealed class Steady
        {
            int? shown, candidate;
            int hits;
            bool init;
            public int? Feed(int? v)
            {
                if (v == null) return shown;           // unbekannt → letzten guten Wert behalten
                if (!init) { init = true; shown = v; return shown; }
                if (v == shown) { candidate = null; hits = 0; return shown; }
                if (v == candidate) hits++; else { candidate = v; hits = 1; }
                if (hits >= 3) { shown = v; candidate = null; hits = 0; }
                return shown;
            }
            public void Reset() { init = false; shown = candidate = null; hits = 0; }
        }

        DateTime caseSeenAt = DateTime.MinValue;

        // Case-Akku: Die AirPods senden den Wert auch weiter, wenn das Case längst zu oder weit weg ist –
        // dann ist er veraltet. Gezeigt wird er nur, solange ein AirPod im Case liegt oder der Deckel offen ist
        // (plus 90 s Nachlauf).
        AirPodsStatus Smooth(AirPodsStatus st)
        {
            bool caseLive = (st.AnyPodInCase || st.LidOpen) && st.CaseBattery != null;
            if (caseLive) caseSeenAt = DateTime.Now;
            bool caseFresh = (DateTime.Now - caseSeenAt).TotalSeconds < 90;
            int? cb = caseLive ? caseBat.Feed(st.CaseBattery) : caseFresh ? caseBat.Feed(null) : null;
            return st with
            {
                Left = st.Left with { Battery = leftBat.Feed(st.Left.Battery) },
                Right = st.Right with { Battery = rightBat.Feed(st.Right.Battery) },
                CaseBattery = cb
            };
        }
        readonly HashSet<int> lowNotified = new HashSet<int>();

        AirPodsStatus ble;   // was die Bluetooth-Pakete sagen
        /// <summary>Aktueller Stand – mit genauen Akkuwerten vom WPods-Kanal, wenn der verbunden ist.</summary>
        public AirPodsStatus Current => Merge(ble);
        /// <summary>Direkter Kanal über WPods (Geräuschkontrolle, Konversationserkennung, genaue Prozente).</summary>
        public readonly AapLink Aap;
        public string EndpointName { get; private set; }
        public bool Running => running;
        public IReadOnlyList<string> History => log;

        public event Action<AirPodsStatus> StatusChanged;
        public event Action<AirPodsStatus> CaseOpened;
        public event Action<string> EndpointChanged;
        public event Action<string, string> Notify;     // Titel, Text
        public event Action<bool> EarPause;             // true = pausiert, false = weiter
        public event Action<string> Log;

        public AirPodsEngine(Settings settings, Dispatcher dispatcher)
        {
            s = settings;
            ui = dispatcher;
            watcher.StatusReceived += st => ui.InvokeAsync(() => OnStatus(st));
            watcher.Log += msg => ui.InvokeAsync(() => AddLog(msg));
            Aap = new AapLink(settings, dispatcher);
            Aap.Log += AddLog;
            Aap.Changed += () => StatusChanged?.Invoke(Current);
            Aap.EarChanged += OnAapEar;
            timer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, async (o, e) => await TickAsync(), ui);
        }

        void AddLog(string msg)
        {
            var line = $"{DateTime.Now:HH:mm:ss}  {msg}";
            log.Add(line);
            if (log.Count > 500) log.RemoveRange(0, 100);
            Log?.Invoke(line);
        }

        public void Start()
        {
            if (running) return;
            running = true;
            watcher.RssiThreshold = s.AirPodsRssi;
            watcher.Start();
            timer.Start();
            if (s.AirPodsAap) Aap.Start();
            _ = TickAsync();
        }

        public void Stop()
        {
            if (!running) return;
            running = false;
            timer.Stop();
            watcher.Stop();
            ble = null;
            Aap.Stop();
            StatusChanged?.Invoke(null);
            AddLog("AirPods-Funktionen ausgeschaltet.");
        }

        public void SetRssiThreshold(int dbm)
        {
            s.AirPodsRssi = dbm;
            watcher.RssiThreshold = dbm;
        }

        void OnStatus(AirPodsStatus st)
        {
            if (!running) return;
            var now = DateTime.Now;

            // Andere AirPods in der Nähe (auch gleiches Modell, z. B. von Mitbewohnern) ignorieren, solange
            // „deine“ frisch empfangen werden – erkennbar an Modell und Akkuständen. Fremde Pakete waren ein
            // Grund für Pausen, ohne dass du etwas herausgenommen hast.
            if (ble != null && (now - lastSeen).TotalSeconds < 8 && !SameDevice(st, ble))
                return;

            bool wasAway = (now - lastSeen).TotalSeconds > 20;
            if (wasAway) { leftBat.Reset(); rightBat.Reset(); caseBat.Reset(); }
            bool flipChanged = ble != null && st.Flipped != lastFlip;
            lastFlip = st.Flipped;
            st = Smooth(st);
            ble = st;
            lastSeen = now;
            StatusChanged?.Invoke(Current);

            // Akku-Karte: Case geöffnet (Deckel zu → auf) oder Case taucht nach Pause wieder auf
            bool lidOpenNow = st.LidOpen && st.AnyPodInCase;
            // flackernde Deckel-/Lade-Bits lösten früher alle 30 s die Karte aus (und hielten die Island offen):
            // „gerade geöffnet“ zählt nur, wenn der Deckel vorher mindestens 10 s zu war
            if (!lidOpenNow && wasLidOpen) lidClosedAt = now;
            bool lidJustOpened = lidOpenNow && !wasLidOpen && (now - lidClosedAt).TotalSeconds >= 60;
            wasLidOpen = lidOpenNow;
            // höchstens alle 10 Minuten, und nur wenn das Case wirklich frisch geöffnet wurde
            if (s.AirPodsPopup && lidJustOpened && (now - lastPopup).TotalMinutes > 10)
            {
                lastPopup = now;
                CaseOpened?.Invoke(st);
            }

            // Kommt die Ohrerkennung schon direkt über den WPods-Kanal, ist die zuverlässiger → die nehmen
            if (Aap.Connected && Aap.PrimaryEar >= 0) { CheckLowBattery(st); return; }

            // Ohrerkennung mit Entprellung: Die Bluetooth-Pakete flackern, wenn die AirPods intern
            // wechseln, wer sendet. Ein solcher Wechsel setzt die Zählung zurück, und Herausnehmen zählt
            // erst, wenn der neue Zustand mindestens 5 Pakete UND 2 Sekunden lang stabil ist
            // (Einsetzen: 2 Pakete / 0,5 s).
            int count = st.InEarCount;
            if (stableInEar < 0 || wasAway)
            {
                stableInEar = count;
                pendingInEar = -1;
            }
            else if (flipChanged && count < stableInEar)
            {
                pendingInEar = -1;   // Senderwechsel: diesem Paket beim Herausnehmen nicht trauen
            }
            else if (count != stableInEar)
            {
                if (count == pendingInEar) pendingHits++;
                else { pendingInEar = count; pendingHits = 1; pendingSince = now; }
                bool removing = count < stableInEar;
                int needHits = removing ? 5 : 2;
                double needSec = removing ? 2.0 : 0.5;
                if (pendingHits >= needHits && (now - pendingSince).TotalSeconds >= needSec)
                {
                    int old = stableInEar;
                    stableInEar = count;
                    pendingInEar = -1;
                    _ = OnInEarChangedAsync(old, count);
                }
            }
            else pendingInEar = -1;

            CheckLowBattery(st);
        }

        int aapStable = -1;

        /// <summary>Ohrerkennung direkt von den AirPods (WPods-Kanal): schnell und ohne Flackern.</summary>
        async void OnAapEar()
        {
            int count = Aap.InEarCount;
            if (aapStable < 0) { aapStable = count; return; }
            if (count == aapStable) return;
            int before = aapStable;
            if (count < before)
            {
                await Task.Delay(700);                  // kurz bestätigen
                if (Aap.InEarCount != count) return;
            }
            aapStable = count;
            stableInEar = count;
            pendingInEar = -1;
            await OnInEarChangedAsync(before, count);
        }

        /// <summary>BLE-Stand + genaue Akkuwerte vom WPods-Kanal.</summary>
        AirPodsStatus Merge(AirPodsStatus b)
        {
            if (Aap == null || !Aap.Connected || !Aap.HasBattery) return b;
            var empty = new PodInfo(null, false, false);
            var st = b ?? new AirPodsStatus(0, EndpointName ?? "AirPods", empty, empty, null, false, false, 0, "");
            var left = st.Left with { Battery = Aap.Left ?? Aap.Single ?? st.Left.Battery, Charging = Aap.LeftCharging };
            var right = st.Right with { Battery = Aap.Right ?? st.Right.Battery, Charging = Aap.RightCharging };
            return st with { Left = left, Right = right, CaseBattery = Aap.Case ?? st.CaseBattery, CaseCharging = Aap.CaseCharging };
        }

        /// <summary>Gleiche AirPods? Gleiches Modell und Akkustände höchstens 10 % auseinander.</summary>
        static bool SameDevice(AirPodsStatus a, AirPodsStatus b)
        {
            if (a.ModelId != b.ModelId) return false;
            static bool Close(int? x, int? y) => x == null || y == null || Math.Abs(x.Value - y.Value) <= 10;
            return Close(a.Left.Battery, b.Left.Battery) && Close(a.Right.Battery, b.Right.Battery) && Close(a.CaseBattery, b.CaseBattery);
        }

        async Task OnInEarChangedAsync(int before, int after)
        {
            AddLog($"Ohrerkennung: {before} → {after} AirPod(s) im Ohr");
            try
            {
                if (after < before)
                {
                    // AirPod rausgenommen → pausieren (nur wenn der Ton auch über die AirPods läuft)
                    if (s.AirPodsAutoPause && !media.HasPaused && await AirPodsAudio.DefaultMatchesAsync(s.AirPodsNameFilter))
                    {
                        int n = await media.PauseAllPlayingAsync();
                        if (n > 0)
                        {
                            countBeforePause = before;
                            AddLog($"Pausiert ({n} App(s)).");
                            EarPause?.Invoke(true);
                        }
                    }
                }
                else if (after > before)
                {
                    if (s.AirPodsAutoSwitch) await EnsureOutputAsync();
                    if (media.HasPaused)
                    {
                        bool recent = (DateTime.Now - media.PausedAt).TotalMinutes < 5;
                        if (!recent) media.Forget();
                        else if (s.AirPodsAutoResume && after >= countBeforePause)
                        {
                            int n = await media.ResumeAsync();
                            AddLog($"Weitergespielt ({n} App(s)).");
                            if (n > 0) EarPause?.Invoke(false);
                        }
                    }
                }
            }
            catch (Exception ex) { AddLog("Fehler bei Ohrerkennung: " + ex.Message); }
        }

        /// <summary>Windows-Ton auf die AirPods legen.</summary>
        public async Task<bool> EnsureOutputAsync()
        {
            var ep = await AirPodsAudio.FindAsync(s.AirPodsNameFilter);
            if (ep == null)
            {
                AddLog($"Kein verbundenes Audiogerät mit \"{s.AirPodsNameFilter}\" im Namen gefunden.");
                return false;
            }
            if (AirPodsAudio.IsDefault(ep.Id)) return true;
            bool ok = AirPodsAudio.SetDefault(ep.Id, s.AirPodsSwitchCommunications);
            AddLog(ok ? $"Audioausgabe → {ep.Name}" : $"Umschalten auf {ep.Name} fehlgeschlagen.");
            return ok;
        }

        async Task TickAsync()
        {
            if (tickBusy || !running) return;
            tickBusy = true;
            try
            {
                // länger nicht gesehen → außer Reichweite / Case zu
                if (ble != null && (DateTime.Now - lastSeen).TotalSeconds > 20)
                {
                    ble = null;
                    stableInEar = -1;
                    wasLidOpen = false;
                    StatusChanged?.Invoke(null);
                }

                // neu verbunden? → wie am Mac sofort als Ausgabe nehmen
                var ep = await AirPodsAudio.FindAsync(s.AirPodsNameFilter);
                bool present = ep != null;
                if (present != endpointPresent)
                {
                    endpointPresent = present;
                    EndpointName = ep?.Name;
                    EndpointChanged?.Invoke(EndpointName);
                    AddLog(present ? $"Audiogerät verbunden: {ep.Name}" : "AirPods-Audiogerät getrennt.");
                    // nur umschalten, wenn sie wirklich neu da sind (nicht bei kurzem Flackern in der Ruhephase)
                    if (present && s.AirPodsAutoSwitch && (DateTime.Now - endpointGoneAt).TotalSeconds > 60) await EnsureOutputAsync();
                    if (!present) endpointGoneAt = DateTime.Now;
                }
            }
            catch (Exception ex) { AddLog("Fehler: " + ex.Message); }
            finally { tickBusy = false; }
        }

        void CheckLowBattery(AirPodsStatus st)
        {
            if (!s.AirPodsLowBattery || !st.AnyInEar || !(st.LowestBattery is int level)) return;
            if (level > 30) { lowNotified.Clear(); return; }
            bool fresh = false;
            foreach (int threshold in new[] { 20, 10 })
                if (level <= threshold && lowNotified.Add(threshold)) fresh = true;
            if (fresh) Notify?.Invoke("AirPods-Akku schwach", $"{st.Model}: noch {level} %");
        }

        // ------------------------------------------------------------------
        //  Umzug vom alten Tray-Programm
        // ------------------------------------------------------------------

        /// <summary>Übernimmt einmalig die Einstellungen der alten „AirPods Companion“-App.</summary>
        public static void ImportOldSettings(Settings s)
        {
            if (s.AirPodsImported) return;
            s.AirPodsImported = true;
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AirPodsCompanion", "settings.json");
                if (!File.Exists(path)) return;
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var r = doc.RootElement;
                bool B(string k, bool d) => r.TryGetProperty(k, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : d;
                s.AirPodsAutoPause = B("AutoPause", s.AirPodsAutoPause);
                s.AirPodsAutoResume = B("AutoResume", s.AirPodsAutoResume);
                s.AirPodsAutoSwitch = B("AutoSwitchOutput", s.AirPodsAutoSwitch);
                s.AirPodsSwitchCommunications = B("AlsoSwitchCommunications", s.AirPodsSwitchCommunications);
                s.AirPodsPopup = B("ShowPopupOnCaseOpen", s.AirPodsPopup);
                s.AirPodsLowBattery = B("LowBatteryWarning", s.AirPodsLowBattery);
                if (r.TryGetProperty("RssiThreshold", out var rssi) && rssi.TryGetInt32(out int db)) s.AirPodsRssi = db;
                if (r.TryGetProperty("DeviceNameFilter", out var f) && f.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(f.GetString()))
                    s.AirPodsNameFilter = f.GetString();
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        /// <summary>
        /// Die alte, separate App würde sonst doppelt pausieren und umschalten: laufende Instanz beenden
        /// und ihren Autostart entfernen – die Island startet die AirPods-Funktionen jetzt selbst mit.
        /// </summary>
        public static string RetireOldApp()
        {
            var done = new List<string>();
            try
            {
                foreach (var p in Process.GetProcessesByName("AirPodsCompanion"))
                {
                    try { p.Kill(); done.Add("alte App beendet"); } catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key?.GetValue("AirPodsCompanion") != null)
                {
                    key.DeleteValue("AirPodsCompanion", false);
                    done.Add("Autostart der alten App entfernt");
                }
            }
            catch { }
            return string.Join(", ", done);
        }

        public void Dispose()
        {
            timer.Stop();
            watcher.Dispose();
            Aap.Dispose();
        }
    }
}

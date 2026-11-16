using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32.SafeHandles;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace DynamicIsland
{
    /// <summary>
    /// Direkter Draht zu den AirPods (Apples AAP-Protokoll auf L2CAP PSM 0x1001) – über den Dienst von WPods
    /// (github.com/FPNDev/wpods, GPL-3.0). Windows lässt normale Apps diesen Kanal nicht öffnen; der WPods-Dienst
    /// mit seinen Treibern kann es und reicht ihn über die Named Pipe \\.\pipe\WPods.Aap weiter.
    /// Damit kann die Island Geräuschkontrolle (Aus/ANC/Transparenz/Adaptiv) und Konversationserkennung umschalten
    /// und bekommt genaue Akkuwerte (1 %) und eine schnelle Ohrerkennung.
    ///
    /// Pipe-Vertrag (aus WPods.Service/Protocol.cs): App → Dienst zuerst "AP" + u64 Bluetooth-Adresse;
    /// Dienst → App 0x02 + u32 Fehler (0 offen, 1167 keine Verbindung, 170 schon offen, 1225 abgelehnt, 121 Zeitüberschreitung).
    /// Danach: App → Dienst ein AAP-Paket pro Nachricht; Dienst → App 0x00 + Paket, 0x01 + u32 = Kanal zu.
    /// </summary>
    public sealed class AapLink : IDisposable
    {
        public enum LinkState { Off, NoService, NoAirPods, Busy, Connecting, Connected, Failed }

        // ---------------- AAP-Pakete (LibrePods-Dokumentation / WPods) ----------------
        static readonly byte[] Handshake = { 0x00, 0x00, 0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        static readonly byte[] FeatureFlags = { 0x04, 0x00, 0x04, 0x00, 0x4D, 0x00, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        static readonly byte[] RequestNotifications = { 0x04, 0x00, 0x04, 0x00, 0x0F, 0x00, 0xFF, 0xFF, 0xFF, 0xFF };
        static byte[] Setting(byte id, byte value) => new byte[] { 0x04, 0x00, 0x04, 0x00, 0x09, 0x00, id, value, 0x00, 0x00, 0x00 };

        public const byte ModeOff = 1, ModeAnc = 2, ModeTransparency = 3, ModeAdaptive = 4;
        const byte ListeningModeId = 0x0D, ConversationId = 0x28, AllowOffId = 0x34;

        const string PipePath = @"\\.\pipe\WPods.Aap";
        const uint PipeAccess = 0x00100183;      // Lesen/Schreiben + Attribute + SYNCHRONIZE (genau das, was die Pipe-ACL erlaubt)
        const uint OpenExisting = 3;
        const uint PipeFlags = 0x40000000 | 0x00100000 | 0x00010000; // OVERLAPPED | SECURITY_SQOS_PRESENT | SECURITY_IDENTIFICATION

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafePipeHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", EntryPoint = "WaitNamedPipeW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool WaitNamedPipe(string name, uint timeoutMs);

        readonly Settings s;
        readonly Dispatcher ui;
        readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
        CancellationTokenSource life;
        NamedPipeClientStream pipe;
        TaskCompletionSource<bool> firstPacket;
        DateTime readyAt = DateTime.MaxValue;

        public LinkState State { get; private set; } = LinkState.Off;
        public string StateText { get; private set; } = "Aus";
        public bool Connected => State == LinkState.Connected;

        // Zustand der AirPods (vom Kanal)
        public int? Left, Right, Case, Single;
        public bool LeftCharging, RightCharging, CaseCharging;
        public DateTime BatteryAt = DateTime.MinValue;
        public byte? ListeningMode;
        public bool? Conversation;
        public int PrimaryEar = -1, SecondaryEar = -1;   // 0 im Ohr, 1 draußen, 2 im Case, 3 getrennt
        public DateTime EarAt = DateTime.MinValue;

        public event Action Changed;      // irgendetwas Sichtbares
        public event Action EarChanged;   // Ohrstatus (für Auto-Pause)
        public event Action<string> Log;

        public AapLink(Settings settings, Dispatcher dispatcher)
        {
            s = settings;
            ui = dispatcher;
        }

        public int InEarCount => (PrimaryEar == 0 ? 1 : 0) + (SecondaryEar == 0 ? 1 : 0);
        public bool HasBattery => (DateTime.Now - BatteryAt).TotalMinutes < 5 && (Left.HasValue || Right.HasValue || Single.HasValue);

        public void Start()
        {
            if (life != null) return;
            life = new CancellationTokenSource();
            var token = life.Token;
            Task.Run(() => LoopAsync(token));
        }

        public void Stop()
        {
            life?.Cancel();
            life = null;
            ClosePipe();
            SetState(LinkState.Off, "Aus");
        }

        // ------------------------------------------------------------------
        //  Verbindung
        // ------------------------------------------------------------------
        async Task LoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { await RunOnceAsync(ct); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Program.Log(ex); SetState(LinkState.Failed, "Fehler: " + ex.Message); }
                ClosePipe();
                if (State == LinkState.Connected) SetState(LinkState.Failed, "Verbindung getrennt");
                try { await Task.Delay(TimeSpan.FromSeconds(State == LinkState.Busy ? 15 : 8), ct); }
                catch (OperationCanceledException) { return; }
            }
        }

        async Task RunOnceAsync(CancellationToken ct)
        {
            ulong address = await FindAddressAsync(s.AirPodsNameFilter);
            if (address == 0) { SetState(LinkState.NoAirPods, "Keine gekoppelten AirPods gefunden"); return; }

            var h = CreateFile(PipePath, PipeAccess, 0, IntPtr.Zero, OpenExisting, PipeFlags, IntPtr.Zero);
            int err = h.IsInvalid ? Marshal.GetLastWin32Error() : 0;
            if (err == 231 && WaitNamedPipe(PipePath, 2000))
            {
                h.Dispose();
                h = CreateFile(PipePath, PipeAccess, 0, IntPtr.Zero, OpenExisting, PipeFlags, IntPtr.Zero);
                err = h.IsInvalid ? Marshal.GetLastWin32Error() : 0;
            }
            if (err != 0)
            {
                h.Dispose();
                SetState(LinkState.NoService, err == 2 ? "WPods ist nicht installiert oder der Dienst läuft nicht" : "WPods-Dienst nicht erreichbar (Fehler " + err + ")");
                return;
            }

            pipe = new NamedPipeClientStream(PipeDirection.InOut, true, true, h);
            pipe.ReadMode = PipeTransmissionMode.Message;
            SetState(LinkState.Connecting, "Verbinde mit den AirPods …");

            var hello = new byte[10];
            hello[0] = (byte)'A';
            hello[1] = (byte)'P';
            BitConverter.GetBytes(address).CopyTo(hello, 2);
            await pipe.WriteAsync(hello, 0, hello.Length, ct);

            var buf = new byte[2048];
            int n;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                n = await ReadMessageAsync(buf, timeout.Token);
            }
            int result = n == 5 && buf[0] == 0x02 ? BitConverter.ToInt32(buf, 1) : -1;
            if (result != 0)
            {
                switch (result)
                {
                    case 170: SetState(LinkState.Busy, "Die WPods-App nutzt die AirPods gerade"); break;
                    case 1167: SetState(LinkState.NoAirPods, "AirPods nicht mit dem PC verbunden"); break;
                    case 1225: SetState(LinkState.Failed, "Die AirPods haben abgelehnt"); break;
                    case 121: SetState(LinkState.Failed, "Keine Antwort von den AirPods"); break;
                    default: SetState(LinkState.Failed, "Öffnen fehlgeschlagen (" + result + ")"); break;
                }
                return;
            }

            // Kanal offen → Pakete lesen (eigener Task) und Handshake schicken
            firstPacket = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reader = ReadLoopAsync(ct);
            await WriteAsync(Handshake, ct);
            var first = await Task.WhenAny(firstPacket.Task, Task.Delay(10000, ct));
            if (first != firstPacket.Task) { SetState(LinkState.Failed, "AirPods antworten nicht"); return; }
            await Task.Delay(100, ct);
            await WriteAsync(FeatureFlags, ct);
            await WriteAsync(RequestNotifications, ct);
            readyAt = DateTime.Now.AddSeconds(2);   // Einstellungen wirken erst nach ~2 s
            SetState(LinkState.Connected, "Verbunden – Steuerung aktiv");
            await Task.Delay(1000, ct);
            await WriteAsync(Setting(AllowOffId, 0x01), ct);   // „Aus“ als Modus erlauben
            await reader;
        }

        async Task ReadLoopAsync(CancellationToken ct)
        {
            var buf = new byte[4096];
            while (!ct.IsCancellationRequested && pipe != null && pipe.IsConnected)
            {
                int n = await ReadMessageAsync(buf, ct);
                if (n <= 0) break;
                byte kind = buf[0];
                if (kind == 0x00 && n > 1)
                {
                    var p = new byte[n - 1];
                    Buffer.BlockCopy(buf, 1, p, 0, p.Length);
                    firstPacket?.TrySetResult(true);
                    Handle(p);
                }
                else if (kind == 0x01) break;   // Kanal zu (AirPods weg oder ins Case)
            }
        }

        async Task<int> ReadMessageAsync(byte[] buf, CancellationToken ct)
        {
            int total = 0;
            do
            {
                int n = await pipe.ReadAsync(buf, total, buf.Length - total, ct);
                if (n == 0) return total;
                total += n;
            }
            while (!pipe.IsMessageComplete && total < buf.Length);
            return total;
        }

        async Task WriteAsync(byte[] packet, CancellationToken ct)
        {
            await writeLock.WaitAsync(ct);
            try { await pipe.WriteAsync(packet, 0, packet.Length, ct); }
            finally { writeLock.Release(); }
        }

        void ClosePipe()
        {
            try { pipe?.Dispose(); } catch { }
            pipe = null;
            readyAt = DateTime.MaxValue;
        }

        static async Task<ulong> FindAddressAsync(string filter)
        {
            try
            {
                var devs = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
                ulong best = 0;
                foreach (var d in devs)
                {
                    if (string.IsNullOrEmpty(d.Name) || d.Name.IndexOf(filter ?? "AirPods", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    using var bd = await BluetoothDevice.FromIdAsync(d.Id);
                    if (bd == null) continue;
                    if (bd.ConnectionStatus == BluetoothConnectionStatus.Connected) return bd.BluetoothAddress;
                    if (best == 0) best = bd.BluetoothAddress;
                }
                return best;
            }
            catch (Exception ex) { Program.Log(ex); return 0; }
        }

        // ------------------------------------------------------------------
        //  Eingehende Pakete
        // ------------------------------------------------------------------
        void Handle(byte[] p)
        {
            if (p.Length < 6 || p[0] != 0x04 || p[1] != 0x00 || p[2] != 0x04 || p[3] != 0x00) return;
            int op = p[4] | (p[5] << 8);
            bool changed = false, ear = false;
            switch (op)
            {
                case 0x04: // Akku: Anzahl, dann je 5 Bytes [Typ][-][Stand][Status][-]
                    if (p.Length < 7) break;
                    int count = p[6];
                    for (int i = 0; i < count && 7 + i * 5 + 3 < p.Length; i++)
                    {
                        int o = 7 + i * 5;
                        byte type = p[o], level = p[o + 2], status = p[o + 3];
                        if (status == 0x03 || status == 0x04 || level > 100) continue;  // keine Messung / getrennt
                        bool charging = status == 0x01;
                        switch (type)
                        {
                            case 0x01: Single = level; LeftCharging = charging; break;
                            case 0x02: Right = level; RightCharging = charging; break;
                            case 0x04: Left = level; LeftCharging = charging; break;
                            case 0x08: if (level > 0 || charging) { Case = level; CaseCharging = charging; } break;
                        }
                    }
                    BatteryAt = DateTime.Now;
                    changed = true;
                    break;
                case 0x06: // Ohr: [primär][sekundär]  00 Ohr · 01 draußen · 02 Case · 03 getrennt · 04 in Bewegung
                    if (p.Length < 8) break;
                    int a = p[6] == 0x04 ? PrimaryEar : p[6];
                    int b = p[7] == 0x04 ? SecondaryEar : p[7];
                    if (a != PrimaryEar || b != SecondaryEar)
                    {
                        PrimaryEar = a;
                        SecondaryEar = b;
                        EarAt = DateTime.Now;
                        ear = changed = true;
                    }
                    break;
                case 0x09: // Einstellung: [Id][Wert]
                    if (p.Length < 8) break;
                    if (p[6] == ListeningModeId) { ListeningMode = p[7]; changed = true; }
                    else if (p[6] == ConversationId) { Conversation = p[7] == 0x01; changed = true; }
                    break;
            }
            if (changed || ear)
                ui.InvokeAsync(() =>
                {
                    if (ear) EarChanged?.Invoke();
                    if (changed) Changed?.Invoke();
                });
        }

        // ------------------------------------------------------------------
        //  Steuern
        // ------------------------------------------------------------------

        /// <summary>Aus / ANC / Transparenz / Adaptiv.</summary>
        public Task SetListeningModeAsync(byte mode)
        {
            ListeningMode = mode;
            Changed?.Invoke();
            return SendSettingAsync(Setting(ListeningModeId, mode));
        }

        public Task SetConversationAsync(bool on)
        {
            Conversation = on;
            Changed?.Invoke();
            return SendSettingAsync(Setting(ConversationId, on ? (byte)0x01 : (byte)0x02));
        }

        // Die AirPods übernehmen eine Einstellung nur ~2 s nach dem Handshake und wenn sie zweimal kommt.
        async Task SendSettingAsync(byte[] packet)
        {
            if (!Connected || pipe == null) return;
            try
            {
                var wait = readyAt - DateTime.Now;
                if (wait > TimeSpan.Zero && wait < TimeSpan.FromSeconds(3)) await Task.Delay(wait);
                var ct = life?.Token ?? CancellationToken.None;
                await WriteAsync(packet, ct);
                await Task.Delay(80);
                await WriteAsync(packet, ct);
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        // ------------------------------------------------------------------
        //  WPods-App
        // ------------------------------------------------------------------

        /// <summary>Die WPods-App beenden, damit die Island die AirPods steuern kann (der Dienst läuft weiter).</summary>
        public static bool CloseWPodsApp()
        {
            bool any = false;
            foreach (var p in Process.GetProcessesByName("WPods"))
            {
                try { p.Kill(); any = true; } catch { }
                finally { p.Dispose(); }
            }
            return any;
        }

        public static bool ServiceInstalled()
        {
            try { return Process.GetProcessesByName("WPods.Service").Length > 0; }
            catch { return false; }
        }

        void SetState(LinkState st, string text)
        {
            bool diff = st != State || text != StateText;
            State = st;
            StateText = text;
            if (diff) ui.InvokeAsync(() => { Log?.Invoke("AAP: " + text); Changed?.Invoke(); });
        }

        public void Dispose() => Stop();
    }
}

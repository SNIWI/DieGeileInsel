using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DynamicIsland
{
    // ---- Windows Core Audio (COM) ----
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr ptr;
    }

    // Standard-Ausgabegerät wechseln – dieselbe (nicht offiziell dokumentierte) Schnittstelle,
    // die auch Tools wie EarTrumpet oder SoundSwitch benutzen.
    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int def, IntPtr format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr a, IntPtr b);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int def, IntPtr a, IntPtr b);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
    }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    class PolicyConfigClient { }

    public sealed class AudioDevice
    {
        public string Id, Name;
        public bool IsDefault;
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint ch, float levelDb, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint ch, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint ch, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
    }

    /// <summary>Lautstärke, Stumm und Live-Pegel von Lautsprecher und Mikrofon.</summary>
    public sealed class AudioService
    {
        readonly IMMDeviceEnumerator en;
        IAudioEndpointVolume outVol, inVol;
        IAudioMeterInformation outMeter, inMeter;
        DateTime lastAcquire = DateTime.MinValue;

        public float? Volume, MicVolume;
        public bool? Muted;
        public bool? MicMuted;
        public string OutputName = "", InputName = "";

        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant pv);
        public float Peak, MicPeak;

        public AudioService()
        {
            try { en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom(); }
            catch (Exception ex) { Program.Log(ex); }
        }

        void Acquire()
        {
            lastAcquire = DateTime.Now;
            OutputName = Get(0, out outVol, out outMeter) ?? ""; // 0 = Wiedergabe
            InputName = Get(1, out inVol, out inMeter) ?? "";    // 1 = Aufnahme
        }

        /// <returns>Name des Standardgeräts</returns>
        string Get(int flow, out IAudioEndpointVolume vol, out IAudioMeterInformation meter)
        {
            vol = null;
            meter = null;
            if (en == null) return null;
            string name = null;
            try
            {
                if (en.GetDefaultAudioEndpoint(flow, 1, out var dev) != 0 || dev == null) return null;
                name = FriendlyName(dev);
                var iidVol = typeof(IAudioEndpointVolume).GUID;
                var iidMeter = typeof(IAudioMeterInformation).GUID;
                if (dev.Activate(ref iidVol, 23, IntPtr.Zero, out var o1) == 0) vol = o1 as IAudioEndpointVolume;
                if (dev.Activate(ref iidMeter, 23, IntPtr.Zero, out var o2) == 0) meter = o2 as IAudioMeterInformation;
            }
            catch { }
            return name;
        }

        static string FriendlyName(IMMDevice dev)
        {
            try
            {
                if (dev.OpenPropertyStore(0, out var store) != 0 || store == null) return null;
                var key = new PropertyKey { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
                if (store.GetValue(ref key, out var pv) != 0) return null;
                try { return pv.vt == 31 && pv.ptr != IntPtr.Zero ? Marshal.PtrToStringUni(pv.ptr) : null; }
                finally { PropVariantClear(ref pv); }
            }
            catch { return null; }
        }

        public List<AudioDevice> OutputDevices() => Devices(0);
        public List<AudioDevice> InputDevices() => Devices(1);

        List<AudioDevice> Devices(int flow)
        {
            var list = new List<AudioDevice>();
            if (en == null) return list;
            try
            {
                string def = null;
                if (en.GetDefaultAudioEndpoint(flow, 1, out var d) == 0 && d != null) d.GetId(out def);
                if (en.EnumAudioEndpoints(flow, 1, out var col) != 0 || col == null) return list; // 1 = nur aktive Geräte
                col.GetCount(out uint n);
                for (uint i = 0; i < n; i++)
                {
                    if (col.Item(i, out var dev) != 0 || dev == null) continue;
                    if (dev.GetId(out string id) != 0 || id == null) continue;
                    list.Add(new AudioDevice { Id = id, Name = FriendlyName(dev) ?? "Audiogerät", IsDefault = id == def });
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            return list;
        }

        /// <summary>Macht ein Gerät zum Standard (für Musik, System und Anrufe).</summary>
        public bool SetDefault(string id)
        {
            try
            {
                var pc = (IPolicyConfig)new PolicyConfigClient();
                int hr = 0;
                for (int role = 0; role < 3; role++) hr |= pc.SetDefaultEndpoint(id, role);
                lastAcquire = DateTime.MinValue; // beim nächsten Poll neu holen
                return hr == 0;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                return false;
            }
        }

        public void Poll()
        {
            // Gerät regelmäßig neu holen (z. B. wenn Kopfhörer eingesteckt werden)
            if ((DateTime.Now - lastAcquire).TotalSeconds > 3) Acquire();
            try
            {
                Volume = outVol != null && outVol.GetMasterVolumeLevelScalar(out var v) == 0 ? v : (float?)null;
                Muted = outVol != null && outVol.GetMute(out var m) == 0 ? m : (bool?)null;
            }
            catch { Volume = null; Muted = null; }
            try { MicMuted = inVol != null && inVol.GetMute(out var mm) == 0 ? mm : (bool?)null; }
            catch { MicMuted = null; }
            try { MicVolume = inVol != null && inVol.GetMasterVolumeLevelScalar(out var mv) == 0 ? mv : (float?)null; }
            catch { MicVolume = null; }
            try { Peak = outMeter != null && outMeter.GetPeakValue(out var p) == 0 ? p : 0f; }
            catch { Peak = 0; }
            try { MicPeak = inMeter != null && inMeter.GetPeakValue(out var q) == 0 ? q : 0f; }
            catch { MicPeak = 0; }
        }

        public void SetVolume(float v)
        {
            try
            {
                var g = Guid.Empty;
                if (outVol == null) return;
                outVol.SetMasterVolumeLevelScalar(Math.Clamp(v, 0f, 1f), ref g);
                if (v > 0 && Muted == true) outVol.SetMute(false, ref g);
            }
            catch { }
        }

        public void SetMicVolume(float v)
        {
            try
            {
                var g = Guid.Empty;
                inVol?.SetMasterVolumeLevelScalar(Math.Clamp(v, 0f, 1f), ref g);
            }
            catch { }
        }

        public void ToggleMicMute()
        {
            try
            {
                var g = Guid.Empty;
                if (inVol != null && inVol.GetMute(out var m) == 0) inVol.SetMute(!m, ref g);
            }
            catch { }
        }

        public void ToggleMute()
        {
            try
            {
                var g = Guid.Empty;
                if (outVol != null && outVol.GetMute(out var m) == 0) outVol.SetMute(!m, ref g);
            }
            catch { }
        }
    }
}

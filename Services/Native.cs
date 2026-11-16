using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicIsland
{
    /// <summary>Direkte Windows-Funktionen (user32 / kernel32 / winmm).</summary>
    public static class Native
    {
        const int GWL_EXSTYLE = -20, GWL_STYLE = -16;
        const long WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_CAPTION = 0x00C00000;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
        [DllImport("user32.dll")] static extern short GetKeyState(int vk);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO info, uint size, uint flags);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out string path);
        [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)] static extern bool LockWorkStation();
        [DllImport("user32.dll")] static extern bool MessageBeep(uint type);
        [DllImport("kernel32.dll")] public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);
        [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
        [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] static extern int mciSendString(string cmd, StringBuilder ret, int len, IntPtr cb);

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
            public int BatteryLifeTime, BatteryFullLifeTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        /// <summary>Kein Taskleisten-/Alt-Tab-Eintrag und klaut nie den Fokus.</summary>
        public static void MakeToolWindow(IntPtr h)
        {
            long ex = (long)GetWindowLongPtr(h, GWL_EXSTYLE);
            ex |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetWindowLongPtr(h, GWL_EXSTYLE, new IntPtr(ex));
        }

        public static void KeepTopmost(IntPtr h)
        {
            if (h != IntPtr.Zero) SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }

        public static void Focus(IntPtr h)
        {
            if (IsIconic(h)) ShowWindow(h, 9);
            SetForegroundWindow(h);
        }

        /// <summary>Dunkle Titelleiste (Windows 10 20H1+ / 11).</summary>
        public static void UseDarkTitleBar(IntPtr h)
        {
            try { int on = 1; DwmSetWindowAttribute(h, 20, ref on, sizeof(int)); } catch { }
        }

        /// <summary>Der echte Downloads-Ordner (auch wenn er verschoben wurde).</summary>
        public static string DownloadsFolder()
        {
            try
            {
                if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out var p) == 0 && !string.IsNullOrEmpty(p))
                    return p;
            }
            catch { }
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        static readonly System.Collections.Generic.Dictionary<string, System.Windows.Media.ImageSource> iconCache =
            new System.Collections.Generic.Dictionary<string, System.Windows.Media.ImageSource>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Das Windows-Symbol einer Datei, eines Ordners oder Programms (32 px).</summary>
        public static System.Windows.Media.ImageSource FileIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            bool unique = ext == ".exe" || ext == ".lnk" || ext == ".ico" || ext == ".url" || ext == "" || System.IO.Directory.Exists(path);
            string key = unique ? path : ext;
            if (iconCache.TryGetValue(key, out var cached)) return cached;
            System.Windows.Media.ImageSource result = null;
            try
            {
                var info = new SHFILEINFO();
                var r = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100 /* SHGFI_ICON | LARGEICON */);
                if (r != IntPtr.Zero && info.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(info.hIcon, System.Windows.Int32Rect.Empty,
                            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                        src.Freeze();
                        result = src;
                    }
                    finally { DestroyIcon(info.hIcon); }
                }
            }
            catch { }
            iconCache[key] = result;
            return result;
        }

        public static bool CapsLock => (GetKeyState(0x14) & 1) != 0;
        /// <summary>PC sperren. Fällt auf rundll32 zurück, falls der direkte Aufruf scheitert.</summary>
        public static bool Lock()
        {
            try
            {
                if (LockWorkStation()) return true;
                Program.Log(new Exception("LockWorkStation fehlgeschlagen, Fehler " + Marshal.GetLastWin32Error()));
            }
            catch (Exception ex) { Program.Log(ex); }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("rundll32.exe", "user32.dll,LockWorkStation")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                return true;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                return false;
            }
        }

        /// <summary>Hat Windows das Sperren per Richtlinie abgeschaltet?</summary>
        public static bool LockDisabledByPolicy
        {
            get
            {
                try
                {
                    foreach (var root in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
                    {
                        using var k = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System");
                        if (k?.GetValue("DisableLockWorkstation") is int i && i == 1) return true;
                    }
                }
                catch { }
                return false;
            }
        }
        public static void Beep() => MessageBeep(0x40);

        public static int Mci(string cmd)
        {
            try { return mciSendString(cmd, null, 0, IntPtr.Zero); }
            catch { return -1; }
        }

        // ---- Bildschirme (für die Island auf dem zweiten Monitor) ----
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MONITORINFOEX
        {
            public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }
        delegate bool MonitorEnumProc(IntPtr mon, IntPtr hdc, IntPtr rect, IntPtr data);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")] static extern bool GetMonitorInfoEx(IntPtr mon, ref MONITORINFOEX mi);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);

        public sealed class Monitor { public string Name; public RECT Bounds; public bool Primary; }

        static Monitor Info(IntPtr h)
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (h == IntPtr.Zero || !GetMonitorInfoEx(h, ref mi)) return null;
            return new Monitor { Name = mi.szDevice, Bounds = mi.rcMonitor, Primary = (mi.dwFlags & 1) != 0 };
        }

        public static System.Collections.Generic.List<Monitor> Monitors()
        {
            var list = new System.Collections.Generic.List<Monitor>();
            try { EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (m, dc, r, d) => { var i = Info(m); if (i != null) list.Add(i); return true; }, IntPtr.Zero); }
            catch { }
            return list;
        }

        /// <summary>Bildschirm an einem Punkt (Bildschirm-Pixel), sonst der nächste.</summary>
        public static Monitor MonitorAt(int x, int y) => Info(MonitorFromPoint(new POINT { X = x, Y = y }, 2));

        public static Monitor PrimaryMonitor() => MonitorAt(0, 0) ?? new Monitor { Name = "", Primary = true };

        /// <summary>Läuft gerade ein Spiel/Video im Vollbild auf dem Bildschirm der Island?</summary>
        public static bool IsForegroundFullscreen(IntPtr self)
        {
            var h = GetForegroundWindow();
            if (h == IntPtr.Zero || h == self) return false;
            var sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            var cls = sb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return false;
            long style = (long)GetWindowLongPtr(h, GWL_STYLE);
            if ((style & WS_CAPTION) == WS_CAPTION) return false;
            if (!GetWindowRect(h, out var r)) return false;
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            var fgMon = MonitorFromWindow(h, 2);
            if (fgMon != MonitorFromWindow(self, 2)) return false; // nur der Bildschirm, auf dem die Island sitzt
            if (!GetMonitorInfo(fgMon, ref mi)) return false;
            return r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top &&
                   r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
        }
    }
}

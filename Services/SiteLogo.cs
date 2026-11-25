using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIsland
{
    /// <summary>
    /// Spielt eine Webseite Medien ab, zeigt der Player das Logo der Seite statt des Browser-Logos:
    /// Adresse aus der Adressleiste des Browser-Fensters (in dem der Titel steht), Logo direkt von der Seite.
    /// </summary>
    public static class SiteLogo
    {
        static readonly HttpClient http = CreateHttp();
        static readonly ConcurrentDictionary<string, BitmapSource> cache = new ConcurrentDictionary<string, BitmapSource>();

        static HttpClient CreateHttp()
        {
            var h = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            h.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36");
            return h;
        }

        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        delegate bool EnumProc(IntPtr h, IntPtr l);

        /// <summary>Domain der Seite, deren Fenstertitel den Medientitel enthält (z. B. „twitch.tv“); null = nicht gefunden.</summary>
        public static string FindDomain(string mediaTitle)
        {
            if (string.IsNullOrWhiteSpace(mediaTitle)) return null;
            string part = mediaTitle.Length > 40 ? mediaTitle.Substring(0, 40) : mediaTitle;
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h)) return true;
                var cls = new StringBuilder(64); GetClassName(h, cls, 64);
                string c = cls.ToString();
                if (c != "Chrome_WidgetWin_1" && c != "MozillaWindowClass") return true;
                var t = new StringBuilder(512); GetWindowText(h, t, 512);
                if (t.ToString().IndexOf(part, StringComparison.OrdinalIgnoreCase) < 0) return true;
                found = h;
                return false;
            }, IntPtr.Zero);
            if (found == IntPtr.Zero) return null;
            try
            {
                var win = AutomationElement.FromHandle(found);
                var edits = win.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                foreach (AutomationElement e in edits)
                {
                    if (!e.TryGetCurrentPattern(ValuePattern.Pattern, out object vp)) continue;
                    string v = ((ValuePattern)vp).Current.Value ?? "";
                    var d = DomainOf(v);
                    if (d != null) return d;
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            return null;
        }

        static string DomainOf(string address)
        {
            address = address.Trim();
            if (address.Length < 4 || address.Contains(' ')) return null;
            if (!address.Contains("://")) address = "https://" + address;
            if (!Uri.TryCreate(address, UriKind.Absolute, out var u)) return null;
            if (u.Scheme != "http" && u.Scheme != "https") return null;
            string host = u.Host.ToLowerInvariant();
            if (!host.Contains('.')) return null;
            if (host.StartsWith("www.")) host = host.Substring(4);
            return host;
        }

        /// <summary>Schöner Name aus der Domain: „music.amazon.de“ → „Amazon“, „twitch.tv“ → „Twitch“.</summary>
        public static string NameOf(string domain)
        {
            if (string.IsNullOrEmpty(domain)) return null;
            var parts = domain.Split('.');
            string core = parts.Length >= 2 ? parts[parts.Length - 2] : parts[0];
            if (parts.Length >= 3 && (core == "co" || core == "com" || core == "org") ) core = parts[parts.Length - 3];
            return core.Length == 0 ? domain : char.ToUpperInvariant(core[0]) + core.Substring(1);
        }

        /// <summary>Logo der Seite als quadratisches Cover (zentriert auf ruhigem Hintergrund); null = keins gefunden.</summary>
        public static async Task<BitmapSource> GetAsync(string domain)
        {
            if (string.IsNullOrEmpty(domain)) return null;
            if (cache.TryGetValue(domain, out var hit)) return hit;
            BitmapSource icon = null;
            try
            {
                string root = "https://" + domain;
                // 1. Icons aus dem HTML (größtes zuerst), 2. apple-touch-icon, 3. favicon.ico
                var urls = new System.Collections.Generic.List<string>();
                try
                {
                    string html = await http.GetStringAsync(root + "/");
                    foreach (Match m in Regex.Matches(html, "<link[^>]+>", RegexOptions.IgnoreCase))
                    {
                        string tag = m.Value;
                        var rel = Regex.Match(tag, "rel\\s*=\\s*[\"']([^\"']+)", RegexOptions.IgnoreCase).Groups[1].Value.ToLowerInvariant();
                        if (!rel.Contains("icon")) continue;
                        var href = Regex.Match(tag, "href\\s*=\\s*[\"']([^\"']+)", RegexOptions.IgnoreCase).Groups[1].Value;
                        if (string.IsNullOrEmpty(href) || href.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) continue;
                        var sz = Regex.Match(tag, "sizes\\s*=\\s*[\"'](\\d+)", RegexOptions.IgnoreCase).Groups[1].Value;
                        int size = int.TryParse(sz, out var n) ? n : rel.Contains("apple") ? 180 : 32;
                        if (Uri.TryCreate(new Uri(root + "/"), System.Net.WebUtility.HtmlDecode(href), out var abs))
                            urls.Add(size.ToString("D5") + "|" + abs);
                    }
                }
                catch { }
                var ordered = urls.OrderByDescending(x => x).Select(x => x.Substring(6)).ToList();
                ordered.Add(root + "/apple-touch-icon.png");
                ordered.Add(root + "/favicon.ico");
                foreach (var u in ordered.Distinct())
                {
                    icon = await LoadAsync(u);
                    if (icon != null && icon.PixelWidth >= 16) break;
                    icon = null;
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            // nichts gefunden (z. B. „stream.radio.de“) → Hauptseite („radio.de“) versuchen
            var labels = domain.Split('.');
            if (icon == null && labels.Length > 2)
            {
                var parent = await GetAsync(string.Join(".", labels.Skip(labels.Length - 2)));
                cache[domain] = parent;
                return parent;
            }
            var cover = icon != null ? Compose(icon) : null;
            cache[domain] = cover;
            return cover;
        }

        static async Task<BitmapSource> LoadAsync(string url)
        {
            try
            {
                var bytes = await http.GetByteArrayAsync(url);
                if (bytes.Length < 60) return null;
                var ms = new System.IO.MemoryStream(bytes);
                var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                // .ico enthält mehrere Größen → größte nehmen
                var frame = dec.Frames.OrderByDescending(f => f.PixelWidth).First();
                frame.Freeze();
                return frame;
            }
            catch { return null; }
        }

        /// <summary>Logo mittig auf einer dunklen Fläche in seiner Farbe – wirkt wie ein Cover, auch bei kleinen Favicons.</summary>
        static BitmapSource Compose(BitmapSource icon)
        {
            const int S = 200;
            var accent = MediaService.AccentOf(icon);
            var bg = Color.FromRgb((byte)(accent.R * 0.22 + 18), (byte)(accent.G * 0.22 + 18), (byte)(accent.B * 0.22 + 18));
            double logo = icon.PixelWidth >= 96 ? 120 : 96;
            var dv = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(dv, icon.PixelWidth >= 64 ? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, S, S));
                var r = new Rect((S - logo) / 2, (S - logo) / 2, logo, logo);
                dc.PushClip(new RectangleGeometry(r, logo * 0.22, logo * 0.22));
                dc.DrawImage(icon, r);
                dc.Pop();
            }
            var rtb = new RenderTargetBitmap(S, S, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        /// <summary>Hat das Bild durchsichtige Stellen? Dann ist es ein App-Symbol (z. B. Chrome-Logo), kein echtes Cover.</summary>
        public static bool LooksLikeIcon(BitmapSource bmp)
        {
            try
            {
                if (bmp == null) return true;
                var src = bmp.Format == PixelFormats.Bgra32 ? bmp : new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
                int w = src.PixelWidth, h = src.PixelHeight;
                var px = new byte[w * h * 4];
                src.CopyPixels(px, w * 4, 0);
                int clear = 0, total = 0;
                for (int i = 3; i < px.Length; i += 16) { total++; if (px[i] < 200) clear++; }
                return total > 0 && clear > total * 0.08;
            }
            catch { return false; }
        }
    }
}

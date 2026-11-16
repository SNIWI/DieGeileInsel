using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace DynamicIsland
{
    /// <summary>
    /// Apple Liquid Glass für die Bedienelemente in der Island (Knöpfe, Kacheln, Mini-Player):
    /// formstabiles Glas mit ebenem Zentrum und gewölbter Randfase. Im Pixel-Shader: Zwei-Flächen-
    /// Brechung mit Dispersion, subtraktive Farbglas-Transmission (Beer-Lambert), Schlick-Fresnel mit
    /// Studio-Umgebung, GGX-Glanzlichter, polierte Schnittkante, ACES-Tone-Mapping und Dithering.
    /// Kanten mit 4× RGSS geglättet. Die Island selbst bleibt schwarz.
    /// </summary>
    public static class LiquidGlass
    {
        // ------------------------------------------------------------------
        //  Shader (HLSL, ps_3_0) – wird beim Start mit d3dcompiler_47.dll von Windows übersetzt.
        //  Nur ASCII hier drin, der Compiler bekommt Bytes.
        // ------------------------------------------------------------------
        const string Hlsl = @"
sampler2D input    : register(s0);
sampler2D backdrop : register(s1);
float4 rectPx  : register(c0);   // Glas auf dem Bildschirm: x, y, Breite, Hoehe (Pixel)
float4 capRect : register(c1);   // abgefilmter Bereich: x, y, Breite, Hoehe (Pixel)
float4 shape   : register(c2);   // x Eckradius, y Fasenbreite, z Glasdicke, w Luftspalt (Pixel)
float4 optics  : register(c3);   // x Brechungsindex, y Dispersion, z Spiegelung, w Helligkeit
float4 tint    : register(c4);   // rgb Glasfarbe (linear), a Farbintensitaet
float4 extra   : register(c5);   // x Dicke-Parameter, y Kantenlicht

float sdRoundBox(float2 p, float2 b, float r)
{
    r = min(r, min(b.x, b.y));
    float2 q = abs(p) - b + r;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

float sdGlass(float2 p) { return sdRoundBox(p, rectPx.zw * 0.5, shape.x); }

float3 backdropAt(float2 screenPx)
{
    float2 uv = (screenPx - capRect.xy) / capRect.zw;
    uv = clamp(uv, 0.003, 0.997);
    float3 c = tex2Dlod(backdrop, float4(uv, 0, 0)).rgb;
    return pow(max(c, 0.0001), 2.2);
}

float2 traceGlass(float2 screenPx, float3 n, float2 g, float ior)
{
    float3 v = float3(0, 0, -1);
    float3 rEnter = refract(v, n, 1.0 / ior);
    if (dot(rEnter, rEnter) < 0.000001) rEnter = reflect(v, n);
    float dzEnter = max(abs(rEnter.z), 0.25);
    float2 pos = screenPx + rEnter.xy * (shape.z / dzEnter);

    float3 rExit = refract(rEnter, float3(0, 0, 1), ior);
    if (dot(rExit, rExit) < 0.000001)
    {
        float3 rTir = reflect(rEnter, normalize(float3(-g * 0.65, 0.76)));
        rExit = refract(rTir, float3(0, 0, 1), ior);
        if (dot(rExit, rExit) < 0.000001) rExit = float3(rTir.xy, -0.5);
    }
    float dzExit = max(abs(rExit.z), 0.25);
    return pos + rExit.xy * (shape.w / dzExit);
}

float3 aces(float3 x)
{
    return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
}

float ign(float2 p)
{
    return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float inA = tex2Dlod(input, float4(uv, 0, 0)).a;
    float2 size = rectPx.zw;
    float2 p = uv * size - size * 0.5;
    float2 screenPx = rectPx.xy + uv * size;

    // 4x Rotated Grid Super-Sampling der Kontur
    float m = 0;
    m += 1.0 - smoothstep(-0.55, 0.55, sdGlass(p + float2(-0.375, -0.125)));
    m += 1.0 - smoothstep(-0.55, 0.55, sdGlass(p + float2( 0.125, -0.375)));
    m += 1.0 - smoothstep(-0.55, 0.55, sdGlass(p + float2( 0.375,  0.125)));
    m += 1.0 - smoothstep(-0.55, 0.55, sdGlass(p + float2(-0.125,  0.375)));
    m *= 0.25 * inA;
    if (m <= 0.001) return float4(0, 0, 0, 0);

    // Fasen-Normale aus dem Abstandsfeld
    float d = sdGlass(p);
    float2 g = float2(sdGlass(p + float2(0.5, 0)) - sdGlass(p - float2(0.5, 0)),
                      sdGlass(p + float2(0, 0.5)) - sdGlass(p - float2(0, 0.5)));
    g = g / max(length(g), 0.00001);
    float uEdge = saturate(max(0.0, -d) / max(shape.y, 1.0));
    float invU = 1.0 - uEdge;
    float sinTilt = pow(invU, 1.55) * 0.90;
    float cosTilt = sqrt(max(0.05, 1.0 - sinTilt * sinTilt));
    float3 n = normalize(float3(g * sinTilt, cosTilt));

    // Zwei-Flaechen-Brechung pro Wellenlaenge (Dispersion)
    float ior = optics.x;
    float spread = optics.y * 0.035;
    float cr = backdropAt(traceGlass(screenPx, n, g, ior - spread)).r;
    float cg = backdropAt(traceGlass(screenPx, n, g, ior)).g;
    float cb = backdropAt(traceGlass(screenPx, n, g, ior + spread)).b;
    float3 refracted = float3(cr, cg, cb) * optics.w;

    // Subtraktive Farbglas-Transmission (Beer-Lambert)
    float3 filt = lerp(float3(1, 1, 1), clamp(tint.rgb, 0.04, 1.0), tint.a);
    float3 absorption = -log(filt);
    float thick = (0.72 + 0.38 * extra.x) * (1.0 + invU * invU * 0.42);
    refracted *= exp(-absorption * thick);

    // Schlick-Fresnel mit Studio-Umgebung (Softbox oben, dunkler Boden)
    float f0 = (ior - 1.0) / (ior + 1.0);
    f0 *= f0;
    float fres = f0 + (1.0 - f0) * pow(1.0 - saturate(n.z), 4.6);
    float3 rv = reflect(float3(0, 0, -1), n);
    float sky = saturate(-rv.y * 0.9 + 0.25);
    float3 env = lerp(float3(0.05, 0.055, 0.065), float3(1.25, 1.27, 1.32), sky * sky);
    float3 col = lerp(refracted, env, saturate(fres * optics.z));

    // GGX-Glanzlichter: Key-Light oben links, Rim-Light unten rechts
    float a2 = 0.0064;
    float3 V = float3(0, 0, 1);
    float3 H1 = normalize(normalize(float3(-0.42, -0.62, 0.66)) + V);
    float3 H2 = normalize(normalize(float3(0.55, 0.50, 0.67)) + V);
    float nh1 = saturate(dot(n, H1));
    float nh2 = saturate(dot(n, H2));
    float dd1 = nh1 * nh1 * (a2 - 1.0) + 1.0;
    float dd2 = nh2 * nh2 * (a2 - 1.0) + 1.0;
    float spec = 0.018 * a2 / (3.14159 * dd1 * dd1) + 0.008 * a2 / (3.14159 * dd2 * dd2);
    col += spec * lerp(float3(1, 1, 1), tint.rgb * 0.5 + 0.5, 0.3);

    // Polierte Schnittkante, hell wo das Licht hinfaellt
    float t = (d + 0.65) / 0.9;
    float rim = exp(-t * t);
    float lit = 0.35 + 0.65 * abs(dot(g, float2(-0.55, -0.835)));
    col += rim * lit * extra.y * (float3(0.75, 0.75, 0.75) + tint.rgb * 0.25);

    // ACES, sRGB, Dithering gegen Farbstufen
    col = aces(col);
    col = pow(max(col, 0.0), 1.0 / 2.2);
    col += (ign(screenPx) - 0.5) / 255.0;
    return float4(saturate(col) * m, m);
}
";

        // ------------------------------------------------------------------
        //  Laufzeit-Compiler (d3dcompiler_47.dll ist seit Windows 8.1 dabei)
        // ------------------------------------------------------------------
        [ComImport, Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ID3DBlob
        {
            [PreserveSig] IntPtr GetBufferPointer();
            [PreserveSig] UIntPtr GetBufferSize();
        }

        [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi)]
        static extern int D3DCompile(byte[] src, UIntPtr size, string name, IntPtr defines, IntPtr include,
                                     string entry, string target, uint flags1, uint flags2,
                                     out ID3DBlob code, out ID3DBlob errors);

        static PixelShader shader;
        static bool compileTried;
        public static string Problem { get; private set; } = "";

        public static PixelShader Shader()
        {
            if (compileTried) return shader;
            compileTried = true;
            try
            {
                if (!RenderCapability.IsPixelShaderVersionSupported(3, 0))
                {
                    Problem = "Grafikkarte unterstützt Pixel-Shader 3.0 nicht";
                    return null;
                }
                var src = System.Text.Encoding.ASCII.GetBytes(Hlsl);
                const uint OptimizationLevel3 = 1u << 15;
                int hr = D3DCompile(src, (UIntPtr)src.Length, "LiquidGlass", IntPtr.Zero, IntPtr.Zero,
                                    "main", "ps_3_0", OptimizationLevel3, 0, out var code, out var errors);
                if (hr < 0 || code == null)
                {
                    string msg = "";
                    if (errors != null) msg = Marshal.PtrToStringAnsi(errors.GetBufferPointer(), (int)errors.GetBufferSize());
                    Problem = "Shader-Fehler: " + msg;
                    Program.Log(new Exception(Problem));
                    return null;
                }
                var bytes = new byte[(int)code.GetBufferSize()];
                Marshal.Copy(code.GetBufferPointer(), bytes, 0, bytes.Length);
                var ps = new PixelShader();
                ps.SetStreamSource(new MemoryStream(bytes));
                shader = ps;
            }
            catch (Exception ex)
            {
                Problem = "Liquid Glass nicht verfügbar: " + ex.Message;
                Program.Log(ex);
            }
            return shader;
        }

        // ------------------------------------------------------------------
        //  Effekt-Klasse (verbindet Eigenschaften mit den Shader-Registern)
        // ------------------------------------------------------------------
        public sealed class GlassEffect : ShaderEffect
        {
            public static readonly DependencyProperty InputProperty =
                RegisterPixelShaderSamplerProperty("Input", typeof(GlassEffect), 0);
            public static readonly DependencyProperty BackdropProperty =
                RegisterPixelShaderSamplerProperty("Backdrop", typeof(GlassEffect), 1, SamplingMode.Bilinear);
            public static readonly DependencyProperty RectProperty = Reg("Rect", 0);
            public static readonly DependencyProperty CaptureProperty = Reg("Capture", 1);
            public static readonly DependencyProperty ShapeProperty = Reg("Shape", 2);
            public static readonly DependencyProperty OpticsProperty = Reg("Optics", 3);
            public static readonly DependencyProperty TintProperty = Reg("Tint", 4);
            public static readonly DependencyProperty ExtraProperty = Reg("Extra", 5);

            static DependencyProperty Reg(string name, int register) =>
                DependencyProperty.Register(name, typeof(Point4D), typeof(GlassEffect),
                    new UIPropertyMetadata(new Point4D(0, 0, 1, 1), PixelShaderConstantCallback(register)));

            public GlassEffect(PixelShader ps)
            {
                PixelShader = ps;
                UpdateShaderValue(InputProperty);
                UpdateShaderValue(BackdropProperty);
                UpdateShaderValue(RectProperty);
                UpdateShaderValue(CaptureProperty);
                UpdateShaderValue(ShapeProperty);
                UpdateShaderValue(OpticsProperty);
                UpdateShaderValue(TintProperty);
                UpdateShaderValue(ExtraProperty);
            }

            public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }
            public Brush Backdrop { get => (Brush)GetValue(BackdropProperty); set => SetValue(BackdropProperty, value); }
            public Point4D Rect { get => (Point4D)GetValue(RectProperty); set => SetValue(RectProperty, value); }
            public Point4D Capture { get => (Point4D)GetValue(CaptureProperty); set => SetValue(CaptureProperty, value); }
            public Point4D Shape { get => (Point4D)GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }
            public Point4D Optics { get => (Point4D)GetValue(OpticsProperty); set => SetValue(OpticsProperty, value); }
            public Point4D Tint { get => (Point4D)GetValue(TintProperty); set => SetValue(TintProperty, value); }
            public Point4D Extra { get => (Point4D)GetValue(ExtraProperty); set => SetValue(ExtraProperty, value); }
        }

        // ------------------------------------------------------------------
        //  Einstellungen
        // ------------------------------------------------------------------
        public static bool ControlsEnabled { get; private set; } = true;
        static string tintKey = "clear";

        // Material-Werte (Standard laut Apple-Liquid-Glass-Vorgabe)
        public const double Ior = 1.52, GlassThickness = 1.15, Dispersion = 0.60, Reflection = 1.05;

        public static void Configure(bool enabled, string tint)
        {
            ControlsEnabled = enabled;
            tintKey = tint ?? "clear";
        }

        /// <summary>Glasfarbe für neutrale Knöpfe: Kristall, Saphir oder Rauch.</summary>
        public static (Color c, double strength) NeutralTint => tintKey switch
        {
            "sapphire" => (Color.FromRgb(0x0E, 0xA5, 0xE9), 0.55),
            "smoke" => (Color.FromRgb(0x64, 0x74, 0x8B), 0.62),
            _ => (Color.FromRgb(0xE2, 0xE8, 0xF0), 0.35),
        };

        public static double Lin(double c) => Math.Pow(c, 2.2);

        // Hinter dem Glas liegt die schwarze Island – der Shader bekommt ein weißes Pixel,
        // die Helligkeit regelt er selbst (Optics.w).
        static ImageBrush white;
        public static ImageBrush WhiteBackdrop()
        {
            if (white != null) return white;
            var bmp = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgr32, null);
            bmp.WritePixels(new Int32Rect(0, 0, 1, 1), new[] { unchecked((int)0xFFFFFFFF) }, 4, 0);
            bmp.Freeze();
            white = new ImageBrush(bmp);
            white.Freeze();
            return white;
        }
    }

    /// <summary>
    /// Ein Border mit Liquid-Glass-Fläche darunter. Verhält sich sonst wie ein normaler Border
    /// (Child, Background, CornerRadius). Die Hintergrundfarbe bestimmt die Glasfarbe:
    /// grau/weiß = neutrales Glas, Rot/Grün/Blau = farbiges Glas, durchsichtig = kein Glas.
    /// </summary>
    public sealed class GlassBorder : Border
    {
        readonly Rectangle glass;
        readonly LiquidGlass.GlassEffect fx;
        bool lifted;

        public bool HasGlass => fx != null;

        public GlassBorder()
        {
            if (!LiquidGlass.ControlsEnabled) return;
            var ps = LiquidGlass.Shader();
            if (ps == null) return;
            fx = new LiquidGlass.GlassEffect(ps) { Backdrop = LiquidGlass.WhiteBackdrop() };
            glass = new Rectangle { Fill = Brushes.White, IsHitTestVisible = false, Effect = fx };
            AddVisualChild(glass);
            SizeChanged += (s, e) => UpdateShape();
            UpdateTint();
        }

        protected override int VisualChildrenCount => base.VisualChildrenCount + (glass != null ? 1 : 0);

        protected override Visual GetVisualChild(int index)
        {
            if (glass == null) return base.GetVisualChild(index);
            return index == 0 ? glass : base.GetVisualChild(index - 1);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var size = base.MeasureOverride(constraint);
            glass?.Measure(constraint);
            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = base.ArrangeOverride(finalSize);
            glass?.Arrange(new Rect(size));
            return size;
        }

        // Mit Glas malt der Border selbst nur eine unsichtbare Fläche (damit Klicks weiter ankommen).
        protected override void OnRender(DrawingContext dc)
        {
            if (glass == null || glass.Visibility != Visibility.Visible) { base.OnRender(dc); return; }
            double r = CornerRadius.TopLeft;
            dc.DrawRoundedRectangle(Brushes.Transparent, null, new Rect(RenderSize), r, r);
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (fx == null) return;
            if (e.Property == BackgroundProperty) UpdateTint();
            else if (e.Property == CornerRadiusProperty) UpdateShape();
        }

        void UpdateShape()
        {
            if (fx == null) return;
            double s = 1;
            try { s = VisualTreeHelper.GetDpi(this).DpiScaleX; } catch { }
            double w = Math.Max(1, ActualWidth * s), h = Math.Max(1, ActualHeight * s);
            double half = Math.Min(w, h) / 2;
            double r = Math.Min(CornerRadius.TopLeft * s, half);
            fx.Rect = new Point4D(0, 0, w, h);
            fx.Capture = new Point4D(-w, -h, w * 3, h * 3);
            fx.Shape = new Point4D(r, Math.Min(10 * s, half * 0.7), 14 * s * LiquidGlass.GlassThickness, 4 * s);
        }

        void UpdateTint()
        {
            var brush = Background as SolidColorBrush;
            var c = brush?.Color ?? Colors.Transparent;
            double a = c.A / 255.0 * (brush?.Opacity ?? 0);
            glass.Visibility = a < 0.02 ? Visibility.Collapsed : Visibility.Visible;
            double max = Math.Max(c.R, Math.Max(c.G, c.B)) / 255.0, min = Math.Min(c.R, Math.Min(c.G, c.B)) / 255.0;
            double sat = max > 0 ? (max - min) / max : 0;
            Color tc;
            double strength, brightness;
            if (sat < 0.2)
            {
                (tc, strength) = LiquidGlass.NeutralTint;
                brightness = 0.012 + a * 0.32;           // etwas heller als die schwarze Island
            }
            else
            {
                tc = Color.FromRgb(c.R, c.G, c.B);      // farbiges Glas (z. B. roter Auflegen-Knopf)
                strength = 0.97;
                brightness = 0.3 + 0.7 * a;
            }
            fx.Tint = new Point4D(LiquidGlass.Lin(tc.R / 255.0), LiquidGlass.Lin(tc.G / 255.0), LiquidGlass.Lin(tc.B / 255.0), strength);
            fx.Optics = new Point4D(LiquidGlass.Ior, LiquidGlass.Dispersion, LiquidGlass.Reflection * 0.55, brightness);
            fx.Extra = new Point4D(LiquidGlass.GlassThickness, lifted ? 1.1 : 0.55, 0, 0);
            InvalidateVisual();
        }

        /// <summary>Beim Drücken hebt sich das Glas an und fängt mehr Licht an der Kante.</summary>
        public void Lift(bool on)
        {
            if (fx == null || lifted == on) return;
            lifted = on;
            fx.Extra = new Point4D(LiquidGlass.GlassThickness, on ? 1.1 : 0.55, 0, 0);
        }
    }
}

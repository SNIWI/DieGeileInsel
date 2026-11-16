using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicIsland
{
    /// <summary>
    /// Kurze Fragen an Google Gemini (generateContent-API).
    /// Der API-Schlüssel liegt verschlüsselt (Windows-DPAPI, nur dein Benutzerkonto) in den Einstellungen.
    /// </summary>
    public sealed class GeminiService
    {
        public const string FastModel = "gemini-3.5-flash-lite";
        public const string SmartModel = "gemini-3.6-flash";
        const string Fallback = "gemini-flash-latest";
        const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/";
        const int MaxTurns = 10;

        static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        readonly Settings s;

        /// <summary>Verlauf des Mini-Chats (Rolle "user"/"model", Text).</summary>
        public readonly List<(string Role, string Text)> History = new List<(string, string)>();

        public GeminiService(Settings settings) { s = settings; }

        public bool HasKey => !string.IsNullOrWhiteSpace(Key);

        public string Key
        {
            get => Secret.Unprotect(s.GeminiKeyProtected);
            set => s.GeminiKeyProtected = string.IsNullOrWhiteSpace(value) ? null : Secret.Protect(value.Trim());
        }

        public string Model => string.IsNullOrWhiteSpace(s.GeminiModel) ? FastModel : s.GeminiModel;

        const string ChatSystem =
            "Du bist ein Mini-Assistent in einer Dynamic Island unter Windows. " +
            "Antworte auf Deutsch (außer der Nutzer schreibt in einer anderen Sprache), sehr knapp: höchstens 3 kurze Sätze. " +
            "Bei Rechenaufgaben nur das Ergebnis mit kurzem Rechenweg. Kein Markdown, keine Aufzählungszeichen, keine Überschriften.";

        /// <summary>Frage im Mini-Chat (mit Verlauf).</summary>
        public async Task<string> ChatAsync(string question, CancellationToken ct = default)
        {
            var turns = History.Select(h => (h.Role, h.Text)).ToList();
            turns.Add(("user", question));
            string answer = await GenerateAsync(turns, ChatSystem, ct);
            History.Add(("user", question));
            History.Add(("model", answer));
            while (History.Count > MaxTurns * 2) History.RemoveAt(0);
            return answer;
        }

        /// <summary>Einmalige Aufgabe auf einem Text (Zusammenfassen, Übersetzen, Korrigieren …).</summary>
        public Task<string> TaskAsync(string instruction, string text, CancellationToken ct = default)
        {
            if (text.Length > 30000) text = text.Substring(0, 30000);
            string system = "Du hilfst in einer Dynamic Island unter Windows. Gib nur das Ergebnis aus – ohne Einleitung, ohne Markdown.";
            return GenerateAsync(new List<(string, string)> { ("user", instruction + "\n\n\"\"\"\n" + text + "\n\"\"\"") }, system, ct);
        }

        /// <summary>Schlüssel prüfen (kurze Testanfrage).</summary>
        public async Task<string> TestAsync()
        {
            try
            {
                string r = await GenerateAsync(new List<(string, string)> { ("user", "Antworte nur mit: OK") }, null, default);
                return "Verbunden ✓  (" + Model + ")";
            }
            catch (Exception ex) { return ex.Message; }
        }

        async Task<string> GenerateAsync(List<(string Role, string Text)> turns, string system, CancellationToken ct)
        {
            string key = Key;
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("Kein Gemini-Schlüssel – in den Einstellungen unter „KI“ eintragen.");

            var body = new Dictionary<string, object>
            {
                ["contents"] = turns.Select(t => new { role = t.Role, parts = new[] { new { text = t.Text } } }).ToArray()
            };
            if (!string.IsNullOrEmpty(system))
                body["systemInstruction"] = new { parts = new[] { new { text = system } } };
            string json = JsonSerializer.Serialize(body);

            string model = Model;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint + Uri.EscapeDataString(model) + ":generateContent")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("x-goog-api-key", key);
                HttpResponseMessage res;
                try { res = await http.SendAsync(req, ct); }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new Exception("Gemini antwortet nicht (Zeitüberschreitung)."); }
                catch (HttpRequestException) { throw new Exception("Keine Verbindung zu Gemini – Internet prüfen."); }

                string text = await res.Content.ReadAsStringAsync();
                if (res.StatusCode == HttpStatusCode.NotFound && attempt == 0 && model != Fallback)
                {
                    model = Fallback;   // Modell gibt es (noch) nicht → Standard-Alias
                    continue;
                }
                if (!res.IsSuccessStatusCode) throw new Exception(ErrorText(res.StatusCode, text));
                return Clean(ParseAnswer(text));
            }
            throw new Exception("Modell nicht gefunden.");
        }

        static string ParseAnswer(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("candidates", out var cands) && cands.ValueKind == JsonValueKind.Array && cands.GetArrayLength() > 0)
            {
                var c = cands[0];
                var sb = new StringBuilder();
                if (c.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
                    foreach (var p in parts.EnumerateArray())
                    {
                        if (p.TryGetProperty("thought", out var th) && th.ValueKind == JsonValueKind.True) continue;
                        if (p.TryGetProperty("text", out var t)) sb.Append(t.GetString());
                    }
                if (sb.Length > 0) return sb.ToString();
                if (c.TryGetProperty("finishReason", out var fr) && fr.GetString() == "SAFETY")
                    throw new Exception("Gemini hat die Antwort blockiert.");
            }
            if (root.TryGetProperty("promptFeedback", out var pf) && pf.TryGetProperty("blockReason", out _))
                throw new Exception("Gemini hat die Anfrage blockiert.");
            throw new Exception("Gemini hat nichts geantwortet.");
        }

        static string ErrorText(HttpStatusCode code, string body)
        {
            string msg = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m)) msg = m.GetString();
            }
            catch { }
            if ((int)code == 400 && msg != null && msg.IndexOf("API key", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Gemini-Schlüssel ungültig – in den Einstellungen prüfen.";
            if ((int)code == 403) return "Kein Zugriff – Schlüssel oder API-Freigabe prüfen.";
            if ((int)code == 429) return "Gemini-Limit erreicht – kurz warten.";
            if ((int)code >= 500) return "Gemini ist gerade überlastet – gleich nochmal versuchen.";
            return "Gemini-Fehler " + (int)code + (msg != null ? ": " + msg : "");
        }

        /// <summary>Markdown-Reste entfernen (die Island zeigt reinen Text).</summary>
        static string Clean(string t)
        {
            t = t.Replace("**", "").Replace("__", "");
            t = Regex.Replace(t, @"^\s{0,3}#{1,6}\s*", "", RegexOptions.Multiline);
            t = Regex.Replace(t, @"^\s*[\*\-]\s+", "• ", RegexOptions.Multiline);
            t = Regex.Replace(t, @"`([^`]*)`", "$1");
            return t.Trim();
        }
    }

    /// <summary>Verschlüsselt Text mit Windows-DPAPI (nur für das aktuelle Benutzerkonto lesbar).</summary>
    static class Secret
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Blob { public int cbData; public IntPtr pbData; }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptProtectData(ref Blob dataIn, string descr, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref Blob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptUnprotectData(ref Blob dataIn, IntPtr descr, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref Blob dataOut);

        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr h);

        const int UiForbidden = 1;

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return null;
            var r = Run(Encoding.UTF8.GetBytes(plain), true);
            return r == null ? null : Convert.ToBase64String(r);
        }

        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return null;
            try
            {
                var r = Run(Convert.FromBase64String(stored), false);
                return r == null ? null : Encoding.UTF8.GetString(r);
            }
            catch { return null; }
        }

        static byte[] Run(byte[] data, bool protect)
        {
            var input = new Blob { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
            var output = new Blob();
            try
            {
                Marshal.Copy(data, 0, input.pbData, data.Length);
                bool ok = protect
                    ? CryptProtectData(ref input, "DynamicIsland", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output);
                if (!ok) return null;
                var result = new byte[output.cbData];
                Marshal.Copy(output.pbData, result, 0, output.cbData);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(input.pbData);
                if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
            }
        }
    }
}

using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DynamicIsland
{
    /// <summary>
    /// Mini-Webserver nur für deinen eigenen PC (localhost). Damit können eigene Skripte
    /// Live-Aktivitäten anzeigen, z. B.:
    ///   http://localhost:7843/show?id=fussball&amp;title=FCB%202:1%20BVB&amp;right=67'&amp;emoji=⚽
    ///   http://localhost:7843/hide?id=fussball
    /// </summary>
    public sealed class LocalApi
    {
        public const int Port = 7843;
        HttpListener listener;

        public bool Running => listener != null && listener.IsListening;

        public void Start(Action<string, NameValueCollection, string> handler)
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{Port}/");
                listener.Start();
                _ = Loop(handler);
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                listener = null;
            }
        }

        async Task Loop(Action<string, NameValueCollection, string> handler)
        {
            while (listener != null && listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); }
                catch { return; }

                string reply = "ok";
                try
                {
                    // POST-Inhalt (z. B. JSON von den Claude-Code-Hooks) mitgeben
                    string body = "";
                    if (ctx.Request.HasEntityBody)
                        using (var r = new System.IO.StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                            body = await r.ReadToEndAsync();
                    handler(ctx.Request.Url.AbsolutePath.ToLowerInvariant(), ctx.Request.QueryString, body);
                }
                catch (Exception ex) { reply = "Fehler: " + ex.Message; }

                try
                {
                    var bytes = Encoding.UTF8.GetBytes(reply);
                    ctx.Response.ContentType = "text/plain; charset=utf-8";
                    ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    ctx.Response.Close();
                }
                catch { }
            }
        }

        public void Stop()
        {
            try { listener?.Stop(); listener?.Close(); } catch { }
            listener = null;
        }
    }
}

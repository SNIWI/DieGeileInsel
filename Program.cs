using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DynamicIsland
{
    public static class Program
    {
        static string lastLog;

        [STAThread]
        public static void Main()
        {
            using var mutex = new Mutex(true, "DynamicIsland-Einzelinstanz", out bool created);
            if (!created)
            {
                MessageBox.Show("Dynamic Island läuft schon (oben in der Bildschirmmitte).", "Dynamic Island");
                return;
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (s, e) => { Log(e.Exception); e.Handled = true; };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log(e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, e) => { Log(e.Exception); e.SetObserved(); };
            app.Run(new IslandWindow());
        }

        public static string DataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIsland");

        public static void Log(Exception ex)
        {
            if (ex == null) return;
            try
            {
                var msg = ex.ToString();
                if (msg == lastLog) return;
                lastLog = msg;
                Directory.CreateDirectory(DataDir);
                File.AppendAllText(Path.Combine(DataDir, "fehler.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { }
        }
    }
}

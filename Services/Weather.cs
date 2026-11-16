using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DynamicIsland
{
    public sealed class WeatherHour { public DateTime Time; public double Temp; public int Code; public bool Day; public int Rain; }
    public sealed class WeatherDay { public DateTime Date; public double Max, Min; public int Code; public int Rain; }

    /// <summary>
    /// Wetter von Open-Meteo (kostenlos, ohne Konto/Schlüssel).
    /// Ort: der in den Einstellungen eingetragene, sonst der Windows-Standort.
    /// </summary>
    public sealed class WeatherService
    {
        static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        readonly Settings settings;

        public WeatherService(Settings s) => settings = s;

        public bool HasData { get; private set; }
        public string Place { get; private set; } = "";
        public double Temp, Feels, Wind;
        public int Code, Humidity;
        public bool IsDay = true;
        public readonly List<WeatherHour> Hours = new List<WeatherHour>();
        public readonly List<WeatherDay> Days = new List<WeatherDay>();
        public DateTime LastUpdate { get; private set; }
        public string Error { get; private set; } = "";
        public event Action Changed;

        bool busy;
        string lastCity;
        double lat, lon;
        bool havePos;

        public async Task RefreshAsync(bool force = false)
        {
            if (busy || !settings.WeatherEnabled) return;
            if (!force && HasData && (DateTime.Now - LastUpdate).TotalMinutes < 15) return;
            busy = true;
            try
            {
                if (!await EnsureLocationAsync()) { Changed?.Invoke(); return; }
                string url = "https://api.open-meteo.com/v1/forecast?latitude=" + lat.ToString(Inv) + "&longitude=" + lon.ToString(Inv) +
                             "&current=temperature_2m,apparent_temperature,weather_code,is_day,wind_speed_10m,relative_humidity_2m" +
                             "&hourly=temperature_2m,weather_code,is_day,precipitation_probability" +
                             "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
                             "&timezone=auto&forecast_days=4";
                var j = JsonNode.Parse(await http.GetStringAsync(url));
                var cur = j["current"];
                Temp = D(cur["temperature_2m"]);
                Feels = D(cur["apparent_temperature"]);
                Wind = D(cur["wind_speed_10m"]);
                Humidity = (int)D(cur["relative_humidity_2m"]);
                Code = (int)D(cur["weather_code"]);
                IsDay = D(cur["is_day"]) > 0.5;

                Hours.Clear();
                var h = j["hourly"];
                var ht = h["time"].AsArray();
                var now = DateTime.Now;
                for (int i = 0; i < ht.Count && Hours.Count < 12; i++)
                {
                    var t = DateTime.Parse(ht[i].GetValue<string>(), Inv);
                    if (t < now.AddMinutes(-30)) continue;
                    Hours.Add(new WeatherHour
                    {
                        Time = t,
                        Temp = D(h["temperature_2m"][i]),
                        Code = (int)D(h["weather_code"][i]),
                        Day = D(h["is_day"][i]) > 0.5,
                        Rain = (int)D(h["precipitation_probability"][i])
                    });
                }

                Days.Clear();
                var d = j["daily"];
                var dt = d["time"].AsArray();
                for (int i = 0; i < dt.Count; i++)
                    Days.Add(new WeatherDay
                    {
                        Date = DateTime.Parse(dt[i].GetValue<string>(), Inv),
                        Max = D(d["temperature_2m_max"][i]),
                        Min = D(d["temperature_2m_min"][i]),
                        Code = (int)D(d["weather_code"][i]),
                        Rain = (int)D(d["precipitation_probability_max"][i])
                    });

                HasData = true;
                Error = "";
                LastUpdate = DateTime.Now;
            }
            catch (Exception ex)
            {
                Error = "Wetter konnte nicht geladen werden.";
                Program.Log(ex);
            }
            finally
            {
                busy = false;
                Changed?.Invoke();
            }
        }

        static double D(JsonNode n)
        {
            if (n == null) return 0;
            try { return n.GetValue<double>(); } catch { }
            try { return n.GetValue<int>(); } catch { }
            return 0;
        }

        async Task<bool> EnsureLocationAsync()
        {
            string city = (settings.WeatherCity ?? "").Trim();
            if (city.Length > 0)
            {
                if (havePos && city == lastCity) return true;
                try
                {
                    var g = JsonNode.Parse(await http.GetStringAsync(
                        "https://geocoding-api.open-meteo.com/v1/search?count=1&language=de&name=" + Uri.EscapeDataString(city)));
                    var r = g["results"]?.AsArray();
                    if (r == null || r.Count == 0) { Error = "Ort „" + city + "“ nicht gefunden."; havePos = false; return false; }
                    lat = D(r[0]["latitude"]);
                    lon = D(r[0]["longitude"]);
                    Place = r[0]["name"]?.GetValue<string>() ?? city;
                    lastCity = city;
                    havePos = true;
                    return true;
                }
                catch (Exception ex) { Program.Log(ex); Error = "Ort konnte nicht gesucht werden."; return false; }
            }

            // kein Ort eingetragen → Windows-Standort, aber nur wenn ausdrücklich erlaubt
            // (die Standort-Abfrage beim Start konnte die Island kurz blockieren / ein Windows-Fenster öffnen)
            lastCity = null;
            if (!settings.WeatherUseLocation)
            {
                Error = "Trage unter Einstellungen → Wetter deine Stadt ein.";
                return false;
            }
            if (havePos && Place == "Dein Standort") return true;
            try
            {
                var job = Task.Run(async () =>
                {
                    var access = await Windows.Devices.Geolocation.Geolocator.RequestAccessAsync();
                    if (access != Windows.Devices.Geolocation.GeolocationAccessStatus.Allowed) return ((double, double)?)null;
                    var pos = await new Windows.Devices.Geolocation.Geolocator().GetGeopositionAsync(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(10));
                    return (pos.Coordinate.Point.Position.Latitude, pos.Coordinate.Point.Position.Longitude);
                });
                if (await Task.WhenAny(job, Task.Delay(15000)) != job || job.Result == null)
                {
                    Error = "Standort nicht verfügbar – trage unter Einstellungen → Wetter deine Stadt ein.";
                    return false;
                }
                (lat, lon) = job.Result.Value;
                Place = "Dein Standort";
                havePos = true;
                return true;
            }
            catch (Exception ex)
            {
                Program.Log(ex);
                Error = "Trage unter Einstellungen → Wetter deine Stadt ein.";
                return false;
            }
        }

        // ---- Wettercodes (WMO) → Text + Symbol ----
        public static string Text(int code)
        {
            switch (code)
            {
                case 0: return "Klar";
                case 1: return "Überwiegend klar";
                case 2: return "Teilweise bewölkt";
                case 3: return "Bedeckt";
                case 45: case 48: return "Nebel";
                case 51: case 53: case 55: case 56: case 57: return "Nieselregen";
                case 61: case 63: case 65: return "Regen";
                case 66: case 67: return "Eisregen";
                case 71: case 73: case 75: case 77: return "Schnee";
                case 80: case 81: case 82: return "Regenschauer";
                case 85: case 86: return "Schneeschauer";
                case 95: case 96: case 99: return "Gewitter";
            }
            return "–";
        }

        public static string Emoji(int code, bool day = true)
        {
            switch (code)
            {
                case 0: return day ? "☀️" : "🌙";
                case 1: return day ? "🌤️" : "🌙";
                case 2: return "⛅";
                case 3: return "☁️";
                case 45: case 48: return "🌫️";
                case 51: case 53: case 55: case 56: case 57: case 80: case 81: case 82: return "🌦️";
                case 61: case 63: case 65: case 66: case 67: return "🌧️";
                case 71: case 73: case 75: case 77: case 85: case 86: return "🌨️";
                case 95: case 96: case 99: return "⛈️";
            }
            return "🌡️";
        }

        public static string Deg(double t) => Math.Round(t).ToString("0", CultureInfo.InvariantCulture) + "°";
    }
}

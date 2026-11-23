using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

namespace DynamicIsland
{
    /// <summary>
    /// Bluetooth in der Island: an/aus, gekoppelte Geräte mit Status, Sprung in die Windows-Einstellungen.
    /// Sind deine AirPods verbunden, zeigt die Island stattdessen das AirPods-Menü.
    /// </summary>
    public sealed class BluetoothActivity : Activity
    {
        const string BtGlyph = "";
        const int MaxRows = 3;
        readonly IslandWindow host;
        List<(string name, bool connected, uint cls)> devices = new List<(string, bool, uint)>();
        Radio radio;
        bool loaded;

        public BluetoothActivity(IslandWindow h)
        {
            host = h;
            Id = "bluetooth";
            Priority = 1;
        }

        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(420, 240);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph(BtGlyph, 14, Ui.AccentBrush), Ui.Text("Bluetooth", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph(BtGlyph, 14, Ui.AccentBrush);

        /// <summary>Geräte und Funk-Status neu laden (im Hintergrund), danach neu zeichnen.</summary>
        public async void Reload()
        {
            try
            {
                var list = new List<(string, bool, uint)>();
                var infos = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
                foreach (var d in infos)
                {
                    try
                    {
                        using var bd = await BluetoothDevice.FromIdAsync(d.Id);
                        if (bd == null) continue;
                        list.Add((string.IsNullOrWhiteSpace(bd.Name) ? d.Name : bd.Name,
                                  bd.ConnectionStatus == BluetoothConnectionStatus.Connected,
                                  bd.ClassOfDevice?.RawValue ?? 0));
                    }
                    catch { }
                }
                devices = list.OrderByDescending(x => x.Item2).ThenBy(x => x.Item1).ToList();
                var radios = await Radio.GetRadiosAsync();
                radio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
            }
            catch (Exception ex) { Program.Log(ex); }
            loaded = true;
            host.Rebuild(this);
        }

        public override FrameworkElement BuildExpanded()
        {
            if (!loaded) Reload();
            bool clean = host.CleanPage(this);
            bool on = radio == null || radio.State == RadioState.On;
            var blue = Ui.Br(Color.FromRgb(0x0A, 0x84, 0xFF));
            var root = new StackPanel { Margin = Ui.PageMargin };

            // Kopf wie im Kontrollzentrum: runder Knopf (blau = an) ist der Schalter
            var toggle = new Border
            {
                Width = 44, Height = 44, CornerRadius = new CornerRadius(22),
                Background = on ? blue : Ui.Br(Colors.White, 40), Cursor = Cursors.Hand,
                Child = Ui.Glyph(BtGlyph, 18, Ui.White), ToolTip = on ? "Bluetooth ausschalten" : "Bluetooth einschalten"
            };
            Ui.Pressable(toggle, 0.9);
            toggle.MouseLeftButtonDown += (s, e) => e.Handled = true;
            toggle.MouseLeftButtonUp += async (s, e) =>
            {
                e.Handled = true;
                if (radio == null) { IslandWindow.Open("ms-settings:bluetooth"); return; }
                try
                {
                    await Radio.RequestAccessAsync();
                    await radio.SetStateAsync(on ? RadioState.Off : RadioState.On);
                }
                catch (Exception ex) { Program.Log(ex); IslandWindow.Open("ms-settings:bluetooth"); }
                await Task.Delay(600);
                Reload();
            };
            int connected = devices.Count(d => d.connected);
            var title = Ui.Text("Bluetooth", 17, Ui.White);
            var sub = Ui.Text(radio == null && loaded ? "Kein Adapter gefunden"
                              : !on ? "Aus"
                              : connected == 0 ? "An · nichts verbunden"
                              : connected == 1 ? "An · 1 Gerät verbunden" : $"An · {connected} Geräte verbunden", 12.5, Ui.Dim, false);
            var texts = Ui.VStack(1, title, sub);
            texts.Margin = new Thickness(12, 0, 0, 0);
            texts.VerticalAlignment = VerticalAlignment.Center;
            var settings = Ui.RoundButton("", 13, () => IslandWindow.Open("ms-settings:bluetooth"), 32, Ui.Br(Colors.White, 24), Ui.White);
            settings.ToolTip = "Bluetooth-Einstellungen";
            settings.VerticalAlignment = VerticalAlignment.Center;
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(texts, 1); Grid.SetColumn(settings, 2);
            head.Children.Add(toggle); head.Children.Add(texts); head.Children.Add(settings);
            root.Children.Add(head);

            // Geräte als Kacheln (2 Spalten): verbunden = blauer Kreis, sonst grau
            var label = Ui.Text("GERÄTE", 10.5, Ui.Dim, true);
            label.Margin = new Thickness(2, 14, 0, 6);
            if (clean) { label.Visibility = Visibility.Collapsed; root.Children.Add(new Border { Height = 12 }); }
            root.Children.Add(label);
            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
            if (!loaded) root.Children.Add(Ui.Text("Suche Geräte …", 12.5, Ui.Dim, false));
            else if (devices.Count == 0) root.Children.Add(Ui.Text("Noch keine Geräte gekoppelt", 12.5, Ui.Dim, false));
            // Logitech-Mäuse/-Tastaturen (Empfänger oder Bluetooth) mit Akku zuerst
            var logi = host.LogiDevices.ToList();
            var items = logi.Select(l => (name: l.Name, connected: true, glyph: l.Glyph,
                                          state: (l.Full ? "Voll geladen" : l.Charging ? $"Lädt · {l.Percent} %" : $"Akku {l.Percent} %") + (l.Asleep ? " · ruht" : ""),
                                          low: !l.Charging && l.Percent <= 15))
                .Concat(devices.Where(d => !logi.Any(l => string.Equals(l.Name, d.name, StringComparison.OrdinalIgnoreCase)))
                    .Select(d =>
                    {
                        int? b = d.connected ? host.BtBatteryOf(d.name) : null;
                        return (name: d.name, connected: d.connected, glyph: DeviceGlyph(d.cls, d.name),
                                state: d.connected ? (b.HasValue ? $"Verbunden · {b} %" : "Verbunden") : "Nicht verbunden", low: false);
                    }))
                .Take(4).ToList();
            if (loaded && devices.Count == 0 && items.Count > 0) root.Children.RemoveAt(root.Children.Count - 1);   // „Noch keine Geräte“ weg
            foreach (var d in items)
            {
                var circle = new Border
                {
                    Width = 30, Height = 30, CornerRadius = new CornerRadius(15),
                    Background = d.connected ? blue : Ui.Br(Colors.White, 30),
                    Child = Ui.Glyph(d.glyph, 13, Ui.White),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var name = Ui.Text(d.name, clean ? 14 : 13, Ui.White, d.connected);
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                var state = Ui.Text(d.state, clean ? 12 : 11, d.low ? Ui.Br(Ui.Red) : d.connected ? Ui.Br(Color.FromRgb(0x64, 0xB5, 0xFF)) : Ui.Dim, false);
                var nt = Ui.VStack(0, name, state);
                nt.Margin = new Thickness(9, 0, 4, 0);
                nt.VerticalAlignment = VerticalAlignment.Center;
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetColumn(nt, 1);
                g.Children.Add(circle); g.Children.Add(nt);
                var tile = new Border
                {
                    Height = (clean ? 52 : 48) * Math.Max(1, Ui.TextBoost * 0.92), CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 0, 6, 6),
                    Padding = new Thickness(9, 0, 9, 0), Background = d.connected ? Ui.Br(Color.FromRgb(0x0A, 0x84, 0xFF), 38) : Ui.Card,
                    Cursor = Cursors.Hand, Child = g, ToolTip = d.name
                };
                var baseBg = tile.Background;
                tile.MouseEnter += (s, e) => tile.Background = Ui.CardHover;
                tile.MouseLeave += (s, e) => tile.Background = baseBg;
                Ui.Pressable(tile, 0.97);
                tile.MouseLeftButtonDown += (s, e) => e.Handled = true;
                tile.MouseLeftButtonUp += (s, e) => { e.Handled = true; IslandWindow.Open("ms-settings:bluetooth"); };
                grid.Children.Add(tile);
            }
            root.Children.Add(grid);

            // unten: schlichte Links wie im Apple-Menü
            var links = Ui.HStack(16,
                Ui.Link("Gerät hinzufügen …", () => IslandWindow.Open("ms-settings-connectabledevices:devicediscovery"), 12),
                Ui.Link(devices.Count > 4 ? $"Alle {devices.Count} Geräte …" : "Aktualisieren", devices.Count > 4 ? (Action)(() => IslandWindow.Open("ms-settings:bluetooth")) : Reload, 12));
            links.Margin = new Thickness(2, 2, 0, 0);
            if (!clean) root.Children.Add(links);
            return root;
        }

        // Geräteklasse (Bluetooth Class of Device): Hauptklasse in Bit 8–12
        static string DeviceGlyph(uint cls, string name)
        {
            uint major = (cls >> 8) & 0x1F, minor = (cls >> 2) & 0x3F;
            string n = name?.ToLowerInvariant() ?? "";
            if (major == 0x04 || n.Contains("airpods") || n.Contains("buds") || n.Contains("head")) return "";   // Audio
            if (major == 0x05) return minor == 0x10 || (minor & 0x10) != 0 ? "" : (minor & 0x20) != 0 ? "" : ""; // Tastatur/Maus/Gamepad
            if (major == 0x02) return "";   // Telefon
            if (major == 0x01) return "";   // Computer
            if (n.Contains("controller") || n.Contains("xbox")) return "";
            return BtGlyph;
        }

        /// <summary>iOS-Schalter.</summary>
        static FrameworkElement Switch(bool on, Action<bool> changed)
        {
            var knob = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = Brushes.White,
                HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = new Thickness(2)
            };
            var track = new Border
            {
                Width = 51,
                Height = 31,
                CornerRadius = new CornerRadius(15.5),
                Background = on ? Ui.Br(Ui.Green) : Ui.Br(Color.FromRgb(0x39, 0x39, 0x3D)),
                Child = knob,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            track.MouseLeftButtonDown += (s, e) => e.Handled = true;
            track.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                bool v = knob.HorizontalAlignment == HorizontalAlignment.Left;
                knob.HorizontalAlignment = v ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                track.Background = v ? Ui.Br(Ui.Green) : Ui.Br(Color.FromRgb(0x39, 0x39, 0x3D));
                changed(v);
            };
            return track;
        }
    }
}

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
            bool on = radio == null || radio.State == RadioState.On;
            var root = new StackPanel { Margin = new Thickness(22, 16, 22, 12) };

            // Kopf: Titel + Schalter (wie im Kontrollzentrum)
            var title = Ui.Text("Bluetooth", 17, Ui.White);
            var sub = Ui.Text(radio == null && loaded ? "Kein Bluetooth-Adapter gefunden" : on ? "An" : "Aus", 12.5, Ui.Dim, false);
            var texts = Ui.VStack(2, title, sub);
            var sw = Switch(on, async v =>
            {
                if (radio == null) { IslandWindow.Open("ms-settings:bluetooth"); return; }
                try
                {
                    await Radio.RequestAccessAsync();
                    await radio.SetStateAsync(v ? RadioState.On : RadioState.Off);
                }
                catch (Exception ex) { Program.Log(ex); IslandWindow.Open("ms-settings:bluetooth"); }
                await Task.Delay(600);
                Reload();
            });
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(sw, 1);
            head.Children.Add(texts);
            head.Children.Add(sw);
            root.Children.Add(head);

            // Gekoppelte Geräte
            var list = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            if (!loaded)
                list.Children.Add(Ui.Text("Suche Geräte …", 12.5, Ui.Dim, false));
            else if (devices.Count == 0)
                list.Children.Add(Ui.Text("Noch keine Geräte gekoppelt", 12.5, Ui.Dim, false));
            foreach (var d in devices.Take(MaxRows))
            {
                var row = new Border
                {
                    Height = 38,
                    CornerRadius = new CornerRadius(12),
                    Background = Ui.Card,
                    Margin = new Thickness(0, 0, 0, 6),
                    Padding = new Thickness(12, 0, 12, 0),
                    Cursor = Cursors.Hand
                };
                var name = Ui.Text(d.name, 13, Ui.White, false);
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                name.Margin = new Thickness(10, 0, 8, 0);
                var state = Ui.Text(d.connected ? "Verbunden" : "Nicht verbunden", 12, d.connected ? Ui.Br(Ui.Green) : Ui.Dim, false);
                var g = new Grid { VerticalAlignment = VerticalAlignment.Center };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(name, 1);
                Grid.SetColumn(state, 2);
                g.Children.Add(Ui.Glyph(DeviceGlyph(d.cls, d.name), 14, d.connected ? Ui.White : Ui.Dim));
                g.Children.Add(name);
                g.Children.Add(state);
                row.Child = g;
                row.MouseEnter += (s, e) => row.Background = Ui.CardHover;
                row.MouseLeave += (s, e) => row.Background = Ui.Card;
                Ui.Pressable(row, 0.98);
                row.MouseLeftButtonDown += (s, e) => e.Handled = true;
                row.MouseLeftButtonUp += (s, e) => { e.Handled = true; IslandWindow.Open("ms-settings:bluetooth"); };
                list.Children.Add(row);
            }
            root.Children.Add(list);

            // Unten: Einstellungen / Gerät hinzufügen
            var links = Ui.HStack(18,
                Ui.Link("Einstellungen", () => IslandWindow.Open("ms-settings:bluetooth"), 12.5),
                Ui.Link("Gerät hinzufügen", () => IslandWindow.Open("ms-settings-connectabledevices:devicediscovery"), 12.5),
                Ui.Link("Aktualisieren", Reload, 12.5));
            links.Margin = new Thickness(2, devices.Count > MaxRows ? 2 : 6, 0, 0);
            if (devices.Count > MaxRows)
                root.Children.Add(Ui.Text($"+ {devices.Count - MaxRows} weitere", 11.5, Ui.Dim, false));
            root.Children.Add(links);
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

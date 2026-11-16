using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicIsland
{
    /// <summary>
    /// Seite für normale Bluetooth-Kopfhörer (wenn keine AirPods verbunden sind):
    /// Akku, Ton hierher holen, Lautstärke und Wiedergabe.
    /// </summary>
    public sealed class HeadphonesActivity : Activity
    {
        const string Glyph = "";
        readonly IslandWindow host;
        DragBar vol;
        TextBlock volT, volIcon, playIcon, outText, batText;
        Border outCard;
        string shownKey = "";

        public HeadphonesActivity(IslandWindow h)
        {
            host = h;
            Id = "headphones";
            Priority = 1;
        }

        BtDev Dev => host.StandardHeadphones;
        public override double CompactWidth => 220;
        public override Size ExpandedSize => new Size(420, 240);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph(Glyph, 14, Ui.AccentBrush), Ui.Text(Dev?.Name ?? "Kopfhörer", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph(Glyph, 14, Ui.AccentBrush);

        bool IsOutput(BtDev d) => d != null && !string.IsNullOrEmpty(host.Audio.OutputName)
                                   && host.Audio.OutputName.IndexOf(d.Name, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Was die Seite zeigt – ändert es sich, wird sie neu aufgebaut.</summary>
        public string Key { get { var d = Dev; return d == null ? "" : d.Name + "|" + d.Battery + "|" + IsOutput(d); } }

        public override FrameworkElement BuildExpanded()
        {
            var d = Dev;
            shownKey = Key;
            var root = new StackPanel { Margin = new Thickness(22, 16, 22, 12) };

            // ---------- Apple-Stil: großer Akku-Ring links, Name + Status + Ton-Knopf rechts ----------
            int? bat = d?.Battery;
            var ringColor = bat.HasValue && bat.Value <= 20 ? Ui.Red : Ui.Green;
            var ring = new RingGauge((bat ?? 0) / 100.0, ringColor, 96, 6, bat.HasValue ? null : "–");
            var ringBox = new Grid { VerticalAlignment = VerticalAlignment.Center };
            ringBox.Children.Add(ring);
            var hpIcon = Ui.Glyph(Glyph, 13, Ui.Dim);
            hpIcon.HorizontalAlignment = HorizontalAlignment.Center;
            hpIcon.VerticalAlignment = VerticalAlignment.Top;
            hpIcon.Margin = new Thickness(0, 22, 0, 0);
            ringBox.Children.Add(hpIcon);
            batText = Ui.Text("", 1, Ui.White);   // (Akku steht im Ring)

            var title = Ui.Text(d?.Name ?? "Kopfhörer", 18, Ui.White);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            bool here = IsOutput(d);
            var sub = Ui.Text(d == null ? "Nicht verbunden" : bat.HasValue ? $"Verbunden · Akku {bat} %" : "Verbunden", 12.5, Ui.Dim, false);
            sub.TextTrimming = TextTrimming.CharacterEllipsis;

            // Ton-Ausgabe als Kapsel (wie „Ton auf AirPods“ beim iPhone)
            outText = Ui.Text(here ? "Ton läuft hier" : "Ton hierher holen", 12.5, here ? Ui.Br(Ui.Green) : Ui.White, true);
            outCard = new Border
            {
                CornerRadius = new CornerRadius(15), Padding = new Thickness(12, 6, 14, 7), HorizontalAlignment = HorizontalAlignment.Left,
                Background = here ? Ui.Br(Ui.Green, 40) : Ui.Br(Colors.White, 26), Margin = new Thickness(0, 10, 0, 0),
                Child = Ui.HStack(6, Ui.Glyph("", 12, here ? Ui.Br(Ui.Green) : Ui.White), outText)
            };
            if (!here && d != null)
            {
                outCard.Cursor = Cursors.Hand;
                Ui.Pressable(outCard, 0.95);
                outCard.MouseLeftButtonDown += (s, e) => e.Handled = true;
                outCard.MouseLeftButtonUp += async (s, e) => { e.Handled = true; await MakeOutputAsync(d.Name); };
            }

            var bt = Ui.RoundButton("", 13, () => host.ShowBluetooth(true), 32, Ui.Br(Colors.White, 24), Ui.White);
            bt.ToolTip = "Andere Bluetooth-Geräte";
            var set = Ui.RoundButton("", 13, () => IslandWindow.Open("ms-settings:bluetooth"), 32, Ui.Br(Colors.White, 24), Ui.White);
            set.ToolTip = "Bluetooth-Einstellungen";
            var btns = Ui.HStack(6, bt, set);
            btns.VerticalAlignment = VerticalAlignment.Top;

            var info = Ui.VStack(1, title, sub, outCard);
            info.VerticalAlignment = VerticalAlignment.Center;
            info.Margin = new Thickness(18, 0, 8, 0);
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(info, 1); Grid.SetColumn(btns, 2);
            head.Children.Add(ringBox); head.Children.Add(info); head.Children.Add(btns);
            root.Children.Add(head);

            // ---------- Lautstärke ----------
            var vg = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            vg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            vg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            vg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            var mute = Ui.RoundButton("", 14, host.Audio.ToggleMute, 30, Brushes.Transparent);
            volIcon = (TextBlock)mute.Child;
            vol = new DragBar(Ui.AccentBrush, 5) { Margin = new Thickness(8, 0, 10, 0), WheelStep = 0.04 };
            vol.Current = () => host.Audio.Volume ?? 0;
            vol.Changed = f => { host.Audio.SetVolume((float)f); volT.Text = Math.Round(f * 100).ToString(); };
            volT = Ui.Text("", 12.5, Ui.White, true, true);
            volT.HorizontalAlignment = HorizontalAlignment.Right;
            volT.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(vol, 1); Grid.SetColumn(volT, 2);
            vg.Children.Add(mute); vg.Children.Add(vol); vg.Children.Add(volT);
            root.Children.Add(vg);


            Update();
            return root;
        }

        static Border Card(UIElement child) => new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = Ui.Card,
            Padding = new Thickness(12, 5, 12, 6),
            Child = child
        };

        async Task MakeOutputAsync(string name)
        {
            bool ok = false;
            try
            {
                var ep = await AirPodsAudio.FindAsync(name);
                ok = ep != null && AirPodsAudio.SetDefault(ep.Id, false);
            }
            catch (Exception ex) { Program.Log(ex); }
            if (!ok) IslandWindow.Open("ms-settings:sound");
            await Task.Delay(500);
            host.Rebuild(this);
        }

        public override void Update()
        {
            if (Key != shownKey) { shownKey = Key; host.Rebuild(this); return; }
            if (vol == null) return;
            var a = host.Audio;
            double v = a.Volume ?? 0;
            bool muted = a.Muted == true;
            if (!vol.Dragging)
            {
                vol.Bar.Value = muted ? 0 : v;
                volT.Text = muted ? "aus" : Math.Round(v * 100).ToString();
            }
            volIcon.Text = muted || v < 0.005 ? "" : v < 0.34 ? "" : v < 0.67 ? "" : "";
            volIcon.Foreground = muted ? Ui.Br(Ui.Red) : Ui.White;
            if (playIcon != null) playIcon.Text = host.Media.Playing ? "" : "";
        }
    }
}

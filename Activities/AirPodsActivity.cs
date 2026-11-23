using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DynamicIsland
{
    /// <summary>
    /// AirPods in der Island: Akku links / rechts / Case als Ringe (wie die Karte am iPhone),
    /// Ohrstatus und Audioausgabe. Erscheint von selbst, wenn du das Case neben dem PC öffnest.
    /// </summary>
    public sealed class AirPodsActivity : Activity
    {
        const string Glyph = ""; // Kopfhörer
        readonly IslandWindow host;
        string shownKey = "";

        public AirPodsActivity(IslandWindow h)
        {
            host = h;
            Id = "airpods";
            Priority = 2;
        }

        AirPodsStatus St => host.AirPods?.Current;

        /// <summary>Hat sich etwas Sichtbares geändert (Akku, Laden, Ohr)? Nur dann neu aufbauen.</summary>
        public bool Changed()
        {
            var k = Key();
            if (k == shownKey) return false;
            return true;
        }

        string Key()
        {
            var st = St;
            var aap = host.AirPods?.Aap;
            string a = aap == null ? "" : $"|{aap.State}|{aap.ListeningMode}|{aap.Conversation}";
            if (st == null) return "none|" + host.AirPods?.EndpointName + a;
            return $"{st.Model}|{P(st.Left)}|{P(st.Right)}|{st.CaseBattery}{st.CaseCharging}|{host.AirPods?.EndpointName}{a}";
        }

        static string P(PodInfo p) => $"{p.Battery}{(p.Charging ? "c" : "")}{(p.InEar ? "e" : "")}";

        public override double CompactWidth => 250;
        public override Size ExpandedSize => new Size(420, 240);

        public override FrameworkElement BuildCompact()
        {
            shownKey = Key();
            var st = St;
            string right = st == null ? "–"
                : st.IsHeadphones ? Pct(st.Left.Battery ?? st.Right.Battery)
                : $"L {Pct(st.Left.Battery)} · R {Pct(st.Right.Battery)}";
            var name = Ui.Text(st?.Model ?? "AirPods", 13, Ui.White);
            name.MaxWidth = 110;
            return Ui.Row3(Ui.HStack(7, Ui.Glyph(Glyph, 14, Ui.White), name), null,
                Ui.Text(right, 12.5, Ui.Dim, true, true), new Thickness(13, 0, 15, 0));
        }

        public override FrameworkElement BuildMinimal() => Ui.Glyph(Glyph, 14, Ui.White);

        public override FrameworkElement BuildExpanded()
        {
            shownKey = Key();
            var st = St;
            var eng = host.AirPods;
            var aap = eng?.Aap;
            var s = host.Settings;
            var root = new StackPanel { Margin = Ui.PageMargin };

            // ---------- Kopfzeile: Name + Status, rechts zwei runde Knöpfe ----------
            var title = Ui.Text(st?.Model ?? "AirPods", 17, Ui.White);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            var sub = Ui.Text(StateText(st, eng), 12.5, Ui.Dim, false);
            sub.TextTrimming = TextTrimming.CharacterEllipsis;
            var texts = Ui.VStack(2, title, sub);
            texts.VerticalAlignment = VerticalAlignment.Center;
            var toHere = Ui.RoundButton("\uE767", 14, async () =>
            {
                bool ok = eng != null && await eng.EnsureOutputAsync();
                host.Alert("airpods-out", 2.5, g =>
                {
                    g.Glyph = Glyph;
                    g.Accent = ok ? Ui.WhiteC : Ui.Orange;
                    g.LeftText = ok ? "Ton auf den AirPods" : "AirPods nicht verbunden";
                    g.RightText = null;
                    g.CompactW = 280;
                });
            }, 34, Ui.Faint);
            toHere.ToolTip = "Ton auf die AirPods";
            var more = Ui.RoundButton("\uE712", 14, () => host.OpenSetup("airpods"), 34, Ui.Faint);
            more.ToolTip = "Alle AirPods-Einstellungen";
            var bt = Ui.RoundButton("\uE702", 14, () => host.ShowBluetooth(true), 34, Ui.Faint);
            bt.ToolTip = "Andere Bluetooth-Geräte";
            var btns = Ui.HStack(8, bt, toHere, more);
            btns.VerticalAlignment = VerticalAlignment.Center;
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(btns, 1);
            head.Children.Add(texts);
            head.Children.Add(btns);
            root.Children.Add(head);

            // ---------- Akku als Karten (wie das AirPods-Widget am iPhone) ----------
            var cards = new Grid { Margin = new Thickness(0, 10, 0, 0), Height = 58 };
            if (st == null)
            {
                var hint = Ui.Text(eng == null || !eng.Running
                        ? "AirPods-Funktionen sind aus – unten einschalten"
                        : "Öffne das Case neben dem PC, dann erscheint hier der Akku",
                    12.5, Ui.Dim, false);
                hint.TextWrapping = TextWrapping.Wrap;
                hint.TextTrimming = TextTrimming.None;
                hint.TextAlignment = TextAlignment.Center;
                hint.HorizontalAlignment = HorizontalAlignment.Center;
                hint.Margin = new Thickness(16, 0, 16, 0);
                cards.Children.Add(new Border { CornerRadius = new CornerRadius(18), Background = Ui.Br(Colors.White, 20), Child = hint });
            }
            else
            {
                var list = new System.Collections.Generic.List<FrameworkElement>();
                if (st.IsHeadphones)
                    list.Add(BatteryCard("\uE7F6", "Akku", st.Left.Battery ?? st.Right.Battery, st.Left.Charging || st.Right.Charging, false));
                else
                {
                    list.Add(BatteryCard("\uE7F6", "Links", st.Left.Battery, st.Left.Charging, st.Left.InEar));
                    list.Add(BatteryCard("\uE7F6", "Rechts", st.Right.Battery, st.Right.Charging, st.Right.InEar));
                    if (st.CaseBattery != null)
                        list.Add(BatteryCard("\uE7B8", "Case", st.CaseBattery, st.CaseCharging, false));
                }
                for (int i = 0; i < list.Count; i++)
                {
                    cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var c = list[i];
                    c.Margin = new Thickness(i == 0 ? 0 : 4, 0, i == list.Count - 1 ? 0 : 4, 0);
                    Grid.SetColumn(c, i);
                    cards.Children.Add(c);
                }
            }
            root.Children.Add(cards);

            // ---------- Geräuschkontrolle (nur mit WPods-Kanal) ----------
            bool live = s.AirPodsEnabled && aap != null && aap.Connected;
            if (live)
            {
                var seg = new Border
                {
                    Height = 36,
                    CornerRadius = new CornerRadius(18),
                    Background = Ui.Br(Colors.White, 20),
                    Padding = new Thickness(3),
                    Margin = new Thickness(0, 8, 0, 0)
                };
                var g = new Grid();
                var modes = new (string label, byte mode)[] { ("Aus", AapLink.ModeOff), ("ANC", AapLink.ModeAnc), ("Transparenz", AapLink.ModeTransparency), ("Adaptiv", AapLink.ModeAdaptive) };
                for (int i = 0; i < modes.Length; i++)
                {
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var m = modes[i];
                    var cell = Segment(m.label, aap.ListeningMode == m.mode, () => _ = aap.SetListeningModeAsync(m.mode));
                    Grid.SetColumn(cell, i);
                    g.Children.Add(cell);
                }
                seg.Child = g;
                root.Children.Add(seg);
            }

            // ---------- Schalter ----------
            var chips = new UniformGrid { Columns = 3, Margin = new Thickness(-4, live ? 8 : 10, -4, 0) };
            if (!s.AirPodsEnabled)
                chips.Children.Add(Chip("\uE7E8", "Einschalten", false, () => s.AirPodsEnabled = true));
            else if (live)
            {
                chips.Children.Add(Chip("\uE8F2", "Konversation", aap.Conversation == true, () => _ = aap.SetConversationAsync(aap.Conversation != true)));
                chips.Children.Add(Chip("", "Akku-Karte", s.AirPodsPopup, () => s.AirPodsPopup = !s.AirPodsPopup));
                chips.Children.Add(Chip("\uE8AB", "Auto-Ton", s.AirPodsAutoSwitch, () => s.AirPodsAutoSwitch = !s.AirPodsAutoSwitch));
            }
            else
            {
                if (aap != null && aap.State == AapLink.LinkState.Busy)
                    chips.Children.Add(Chip("\uE8AB", "Übernehmen", false, () => { AapLink.CloseWPodsApp(); aap.Stop(); aap.Start(); }));
                chips.Children.Add(Chip("\uE8AB", "Auto-Ton", s.AirPodsAutoSwitch, () => s.AirPodsAutoSwitch = !s.AirPodsAutoSwitch));
                if (chips.Children.Count < 6)
                    chips.Children.Add(Chip("\uE83F", "Akku-Karte", s.AirPodsPopup, () => s.AirPodsPopup = !s.AirPodsPopup));
                if (chips.Children.Count < 6)
                    chips.Children.Add(Chip("\uE7BA", "Warnung", s.AirPodsLowBattery, () => s.AirPodsLowBattery = !s.AirPodsLowBattery));
            }
            root.Children.Add(chips);
            return root;
        }

        /// <summary>Akku-Karte: Symbol + Name, große Prozentzahl, dünner Balken.</summary>
        static FrameworkElement BatteryCard(string glyph, string label, int? percent, bool charging, bool inEar)
        {
            var color = charging ? Ui.Green : percent is int p && p <= 20 ? Ui.Red : Ui.WhiteC;
            var top = Ui.HStack(5, Ui.Glyph(glyph, 11, Ui.Dim), Ui.Text(label + (inEar ? " · Ohr" : ""), 11.5, Ui.Dim, false));
            var val = Ui.Text(percent.HasValue ? percent.Value + " %" : "–", 18, Ui.White, true, true);
            var valRow = new StackPanel { Orientation = Orientation.Horizontal };
            valRow.Children.Add(val);
            if (charging)
            {
                var bolt = Ui.Glyph("\uE945", 12, Ui.Br(Ui.Green));
                bolt.Margin = new Thickness(5, 2, 0, 0);
                valRow.Children.Add(bolt);
            }
            var bar = new Bar(3, Ui.Br(color)) { Margin = new Thickness(0, 5, 0, 0) };
            bar.Value = (percent ?? 0) / 100.0;
            var stack = Ui.VStack(1, top, valRow, bar);
            return new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = Ui.Card,
                Padding = new Thickness(12, 5, 12, 6),
                Child = stack
            };
        }

        /// <summary>Ein Feld der Geräuschkontrolle – wie der Segment-Schalter in iOS.</summary>
        FrameworkElement Segment(string label, bool on, Action pick)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(15),
                Background = on ? Ui.Br(Colors.White, 64) : Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock
                {
                    Text = label,
                    FontFamily = Ui.TextFont,
                    FontSize = 12.5,
                    FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = on ? Ui.White : Ui.Dim,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            b.MouseEnter += (o, e) => { if (!on) b.Background = Ui.Br(Colors.White, 20); };
            b.MouseLeave += (o, e) => { if (!on) b.Background = Brushes.Transparent; };
            Ui.Pressable(b, 0.95);
            b.MouseLeftButtonDown += (o, e) => e.Handled = true;
            b.MouseLeftButtonUp += (o, e) => { e.Handled = true; pick(); host.Rebuild(this); };
            return b;
        }

        /// <summary>Schalter-Kapsel (an = Akzentfarbe, wie im Kontrollzentrum). Speichert sofort.</summary>
        FrameworkElement Chip(string glyph, string label, bool on, Action change)
        {
            var scale = new ScaleTransform(1, 1);
            var b = new Border
            {
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(4, 0, 4, 0),
                Padding = new Thickness(11, 0, 9, 0),
                Background = on ? (Brush)Ui.AccentBrush : Ui.GlassFill,
                BorderBrush = on ? null : Ui.GlassRim,
                BorderThickness = new Thickness(on ? 0 : 0.8),
                Cursor = System.Windows.Input.Cursors.Hand,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = scale
            };
            var text = Ui.Text(label, 12.5, on ? Ui.OnAccent : Ui.White, true);
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Margin = new Thickness(7, 0, 0, 0);
            var row = new Grid { VerticalAlignment = VerticalAlignment.Center };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(text, 1);
            row.Children.Add(Ui.Glyph(glyph, 12.5, on ? Ui.OnAccent : Ui.White));
            row.Children.Add(text);
            b.Child = row;
            b.MouseEnter += (o, e) => b.Opacity = 0.85;
            b.MouseLeave += (o, e) => { b.Opacity = 1; Ui.Press(scale, 1, 250, new SpringEase()); };
            b.MouseLeftButtonDown += (o, e) => { e.Handled = true; Ui.Press(scale, 0.94, 90, new System.Windows.Media.Animation.CubicEase()); };
            b.MouseLeftButtonUp += (o, e) =>
            {
                e.Handled = true;
                Ui.Press(scale, 1, 300, new SpringEase());
                change();
                host.Settings.Save();
                host.ApplySettings();
            };
            return b;
        }

        static string Pct(int? v) => v.HasValue ? v.Value + " %" : "–";

        static string StateText(AirPodsStatus st, AirPodsEngine eng)
        {
            string output = eng?.EndpointName != null ? "Ton: verbunden" : "Ton: nicht verbunden";
            if (st == null) return eng != null && eng.Running ? "Nicht in Reichweite · " + output : "Aus";
            string ear = st.InEarCount == 2 ? "Beide im Ohr"
                : st.InEarCount == 1 ? "Einer im Ohr"
                : st.AnyPodInCase ? (st.LidOpen ? "Im Case · Deckel offen" : "Im Case")
                : "Nicht im Ohr";
            return ear + " · " + output;
        }

    }

    /// <summary>Zeigt die empfangenen AirPods-Pakete – hilfreich, falls etwas nicht erkannt wird.</summary>
    public sealed class AirPodsDebugWindow : Window
    {
        readonly TextBox box;
        readonly AirPodsEngine engine;

        public AirPodsDebugWindow(AirPodsEngine e)
        {
            engine = e;
            Title = "AirPods – Debug-Log";
            Width = 900;
            Height = 480;
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            box = new TextBox
            {
                IsReadOnly = true,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8)
            };
            Content = box;
            if (e != null)
            {
                foreach (var line in e.History) box.AppendText(line + "\n");
                e.Log += Append;
                Closed += (s, a) => e.Log -= Append;
            }
            else box.Text = "AirPods-Funktionen sind ausgeschaltet.";
            box.ScrollToEnd();
        }

        void Append(string line)
        {
            box.AppendText(line + "\n");
            if (box.LineCount > 1000) box.Text = box.Text.Substring(box.Text.Length / 2);
            box.ScrollToEnd();
        }
    }
}

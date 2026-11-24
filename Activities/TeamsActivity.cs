using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DynamicIsland
{
    /// <summary>
    /// Laufende Teams-Besprechung im Teams-Look: lila Akzent, dunkle Knöpfe mit Beschriftung wie die Teams-Leiste,
    /// rote „Verlassen“-Kapsel. Erscheint nur, wenn die Island mit Teams gekoppelt ist und man in einer Besprechung ist.
    /// </summary>
    public sealed class TeamsActivity : Activity
    {
        static readonly SolidColorBrush Purple = Ui.Br(Logos.TeamsPurple);
        static readonly SolidColorBrush Lavender = Ui.Br(Color.FromRgb(0x7F, 0x85, 0xF5));
        static readonly SolidColorBrush TmButton = Ui.Br(Color.FromRgb(0x29, 0x29, 0x29));
        static readonly SolidColorBrush TmButtonHover = Ui.Br(Color.FromRgb(0x3D, 0x3D, 0x3D));
        static readonly SolidColorBrush TmActive = Ui.Br(Color.FromArgb(0x55, 0x5B, 0x5F, 0xC7));
        static readonly SolidColorBrush TmRed = Ui.Br(Color.FromRgb(0xC4, 0x31, 0x4B));
        static readonly SolidColorBrush TmTile = Ui.Br(Color.FromRgb(0x1F, 0x1F, 0x1F));

        readonly TeamsService t;
        TextBlock cLeft, cTime, eSub, avatarText;
        StackPanel cIcons;
        Border selfTile, selfBadge, recChip, shareChip, chatChip, handBadge;
        readonly List<Action> refresh = new List<Action>();
        string lastSig;

        public TeamsActivity(TeamsService service)
        {
            t = service;
            Id = "teams-meeting";
            Priority = 60;
        }

        TeamsMeeting M => t.Meeting;
        public override double CompactWidth => 250;
        public override Size ExpandedSize => new Size(420, 236);

        // ---------- kleine Pille ----------
        public override FrameworkElement BuildCompact()
        {
            var logo = Logos.Teams(17);
            logo.VerticalAlignment = VerticalAlignment.Center;
            cLeft = Ui.Text("Besprechung", 13, Ui.White, true);
            var left = Ui.HStack(8, logo, cLeft);
            cIcons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            cTime = Ui.Text("", 12, Ui.Soft, false, true);
            cTime.Margin = new Thickness(7, 0, 0, 0);
            var right = Ui.HStack(0, cIcons, cTime);
            var g = Ui.Row3(left, null, right, new Thickness(13, 0, 13, 0));
            Update();
            return g;
        }

        public override FrameworkElement BuildMinimal() => Logos.Teams(15);

        // ---------- große Ansicht ----------
        public override FrameworkElement BuildExpanded()
        {
            refresh.Clear();
            var root = new Grid { Margin = Ui.PageMargin };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Kopf: Teams-Logo · Besprechung · Dauer · REC, rechts die rote Verlassen-Kapsel (wie in Teams)
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var logo = Logos.Teams(18);
            logo.VerticalAlignment = VerticalAlignment.Center;
            var status = Ui.Text("Besprechung", 15, Ui.White);
            eSub = Ui.Text("", 12.5, Ui.Soft, false);
            recChip = Pill("REC", TmRed);
            shareChip = Pill("Du teilst", Purple);
            var line = Ui.HStack(7, logo, status, eSub, recChip, shareChip);
            line.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(line);

            var leave = new Border
            {
                Background = TmRed,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 0, 12, 0),
                Height = 28,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Child = Ui.HStack(6, Ui.Glyph("", 12, Ui.White), Ui.Text("Verlassen", 12, Ui.White)),
                ToolTip = "Besprechung verlassen"
            };
            Click(leave, () => t.Leave());
            Grid.SetColumn(leave, 1);
            head.Children.Add(leave);
            root.Children.Add(head);

            // darunter: links die eigene Kachel (wie in der Teams-Galerie), rechts Leiste + Reaktionen
            var body = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.Children.Add(BuildSelfTile());

            var bar = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Margin = new Thickness(5, 0, -3, 0) };
            bar.Children.Add(BarButton(() => "", () => !M.VideoOn, () => "Kamera", () => M.VideoOn, () => t.ToggleVideo(), () => M.CanToggleVideo));
            bar.Children.Add(BarButton(() => "", () => M.Muted, () => M.Muted ? "Stumm" : "Mikro", () => false, () => t.ToggleMute(), () => M.CanToggleMute, () => M.Muted));
            bar.Children.Add(BarButton(() => "✋", () => false, () => "Hand", () => M.HandRaised, () => t.ToggleHand(), () => M.CanToggleHand, emoji: true));
            bar.Children.Add(BarButton(() => "", () => false, () => "Chat", () => M.UnreadMessages, () => t.ToggleChat(), () => M.CanToggleChat));
            bar.Children.Add(BarButton(() => "", () => false, () => "Unschärfe", () => M.Blurred, () => t.ToggleBlur(), () => M.CanToggleBlur));

            var reacts = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            reacts.Children.Add(Reaction("", Ui.Br(Color.FromRgb(0xFF, 0xC8, 0x3D)), "like", "Gefällt mir"));
            reacts.Children.Add(Reaction("", Ui.Br(Color.FromRgb(0xF0, 0x4A, 0x6B)), "love", "Herz"));
            reacts.Children.Add(Reaction("", Ui.Br(Color.FromRgb(0xFF, 0xC8, 0x3D)), "laugh", "Lachen"));
            chatChip = Pill("Neue Nachrichten", Purple);
            chatChip.Cursor = Cursors.Hand;
            chatChip.VerticalAlignment = VerticalAlignment.Center;
            chatChip.HorizontalAlignment = HorizontalAlignment.Right;
            Click(chatChip, () => t.ToggleChat());
            var lower = new Grid { Margin = new Thickness(8, 7, 0, 0) };
            lower.Children.Add(reacts);
            lower.Children.Add(chatChip);

            var right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.Children.Add(bar);
            Grid.SetRow(lower, 1);
            right.Children.Add(lower);
            Grid.SetColumn(right, 1);
            body.Children.Add(right);
            Grid.SetRow(body, 1);
            root.Children.Add(body);
            Update();
            return root;
        }

        /// <summary>Kleine farbige Kapsel (REC, Du teilst, Neue Nachrichten).</summary>
        static Border Pill(string text, Brush bg) => new Border
        {
            Background = bg,
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(6, 1, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Ui.Text(text, 10.5, Ui.White, true),
            Visibility = Visibility.Collapsed
        };

        FrameworkElement BuildSelfTile()
        {
            avatarText = Ui.Text("Du", 15, Ui.White);
            avatarText.HorizontalAlignment = HorizontalAlignment.Center;
            avatarText.VerticalAlignment = VerticalAlignment.Center;
            var avatar = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = Purple,
                Child = avatarText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            selfBadge = new Border
            {
                Background = Ui.Br(Colors.Black, 150),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 1, 6, 2),
                Margin = new Thickness(6, 0, 6, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            handBadge = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Ui.Br(Color.FromRgb(0xFF, 0xB9, 0x00)),
                Margin = new Thickness(0, 6, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = "✋", FontFamily = Ui.EmojiFont, FontSize = 12, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                Visibility = Visibility.Collapsed
            };
            var inner = new Grid();
            inner.Children.Add(avatar);
            inner.Children.Add(handBadge);
            inner.Children.Add(selfBadge);
            selfTile = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = TmTile,
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                ClipToBounds = true,
                Child = inner
            };
            return selfTile;
        }

        Border Reaction(string glyph, Brush color, string type, string tip)
        {
            var b = Ui.RoundButton(glyph, 14, () => t.React(type), 32, TmButton, color);
            b.Margin = new Thickness(0, 0, 6, 0);
            b.ToolTip = tip;
            refresh.Add(() => b.Opacity = M.CanReact ? 1 : 0.4);
            return b;
        }

        /// <summary>Knopf der Teams-Leiste. glyph = null → durchgestrichenes Symbol (aus).</summary>
        FrameworkElement BarButton(Func<string> glyph, Func<bool> slashed, Func<string> label, Func<bool> active, Action act, Func<bool> enabled,
                                   Func<bool> danger = null, bool emoji = false)
        {
            var iconHost = new Grid { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Center };
            var lbl = Ui.Text("", 11, Ui.Br(Colors.White, 210), false);
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            lbl.TextTrimming = TextTrimming.CharacterEllipsis;
            lbl.Margin = new Thickness(2, 4, 2, 0);
            var box = new Border
            {
                Height = 46,
                Margin = new Thickness(3, 0, 3, 0),
                CornerRadius = new CornerRadius(8),
                Background = TmButton,
                Cursor = Cursors.Hand,
                Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { iconHost, lbl } }
            };
            bool hover = false;
            Action paint = () =>
            {
                bool on = active(), bad = danger?.Invoke() ?? false;
                box.Background = on ? TmActive : hover ? TmButtonHover : TmButton;
                box.Opacity = enabled() ? 1 : 0.4;
                var fg = bad ? TmRed : on ? Lavender : Ui.White;
                iconHost.Children.Clear();
                var g = glyph() ?? "";
                var icon = emoji
                    ? new TextBlock { Text = g, FontFamily = Ui.EmojiFont, FontSize = 15, Foreground = fg, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                    : Ui.Glyph(g, 16, fg);
                icon.HorizontalAlignment = HorizontalAlignment.Center;
                icon.VerticalAlignment = VerticalAlignment.Center;
                iconHost.Children.Add(icon);
                if (slashed() || bad)
                    iconHost.Children.Add(new Line { X1 = 3, Y1 = 3, X2 = 17, Y2 = 17, Stroke = fg, StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
                lbl.Text = label();
            };
            box.MouseEnter += (s, e) => { hover = true; paint(); };
            box.MouseLeave += (s, e) => { hover = false; paint(); };
            Click(box, () => { if (enabled()) act(); });
            refresh.Add(paint);
            return box;
        }

        static void Click(Border b, Action act)
        {
            Ui.Pressable(b, 0.94);
            b.MouseLeftButtonDown += (s, e) => e.Handled = true;
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; try { act(); } catch (Exception ex) { Program.Log(ex); } };
        }

        public override void Update()
        {
            var m = M;
            string dur = Ui.Fmt(DateTime.Now - t.JoinedAt);
            if (cTime != null) cTime.Text = dur;
            if (eSub != null) eSub.Text = "· " + dur;
            // der Rest nur, wenn sich am Zustand etwas geändert hat (Update läuft im Takt der Island)
            string sig = $"{m.Muted}{m.VideoOn}{m.HandRaised}{m.Recording}{m.Sharing}{m.UnreadMessages}{m.Blurred}{m.CanReact}{m.CanToggleMute}{m.CanToggleVideo}{m.CanToggleHand}{m.CanToggleBlur}{m.CanToggleChat}|{cIcons?.GetHashCode()}|{selfTile?.GetHashCode()}";
            if (sig == lastSig) return;
            lastSig = sig;
            if (cLeft != null) cLeft.Text = m.HandRaised ? "Hand gehoben" : m.Sharing ? "Du teilst" : "Besprechung";
            if (cIcons != null)
            {
                cIcons.Children.Clear();
                if (m.Recording) cIcons.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = TmRed, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });
                if (m.Muted) cIcons.Children.Add(SlashedMic(TmRed));
                else cIcons.Children.Add(Ui.Glyph("", 12, Lavender));
            }
            if (selfBadge != null)
            {
                var name = Ui.Text("Du", 11, Ui.White, true);
                selfBadge.Child = m.Muted ? Ui.HStack(5, SlashedMic(Ui.White, 10), name) : (UIElement)name;
            }
            if (selfTile != null) selfTile.BorderBrush = m.Muted ? Brushes.Transparent : Purple;   // Mikro offen: lila Rahmen wie in Teams
            if (handBadge != null) handBadge.Visibility = m.HandRaised ? Visibility.Visible : Visibility.Collapsed;
            if (recChip != null) recChip.Visibility = m.Recording ? Visibility.Visible : Visibility.Collapsed;
            if (shareChip != null) shareChip.Visibility = m.Sharing ? Visibility.Visible : Visibility.Collapsed;
            if (chatChip != null) chatChip.Visibility = m.UnreadMessages ? Visibility.Visible : Visibility.Collapsed;
            foreach (var r in refresh) r();
        }

        static FrameworkElement SlashedMic(Brush b, double size = 12)
        {
            var g = new Grid { Width = size + 2, Height = size + 2, VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(Ui.Glyph("", size, b));
            g.Children.Add(new Line { X1 = 2, Y1 = 2, X2 = size, Y2 = size, Stroke = b, StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            return g;
        }
    }
}

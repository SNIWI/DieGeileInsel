using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicIsland
{
    /// <summary>Auf eine Nachricht direkt aus der Island antworten.</summary>
    public sealed class ReplyActivity : Activity
    {
        readonly IslandWindow host;
        public readonly string App, Sender, Message, IconUrl, ChannelId, Aumid;
        public readonly Color Accent;
        TextBox input;
        string draft = "";

        public ReplyActivity(IslandWindow h, string app, string sender, string message, string iconUrl, string channelId, string aumid, Color accent)
        {
            host = h;
            Id = "reply";
            Priority = 90;
            App = app; Sender = sender; Message = message; IconUrl = iconUrl; ChannelId = channelId; Aumid = aumid; Accent = accent;
        }

        public override double CompactWidth => 300;
        public override Size ExpandedSize => new Size(420, 170);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph("", 14, Ui.Br(Accent)), Ui.Text("Antwort an " + Sender, 13, Ui.White, true, true)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph("", 14, Ui.Br(Accent));

        public override FrameworkElement BuildExpanded()
        {
            var root = new StackPanel { Margin = new Thickness(22, 16, 22, 12) };

            FrameworkElement avatar = !string.IsNullOrEmpty(IconUrl)
                ? (FrameworkElement)WebAvatar.Circle(IconUrl, 38)
                : new Border
                {
                    Width = 38, Height = 38, CornerRadius = new CornerRadius(19), Background = Ui.Br(Accent, 55),
                    Child = Ui.Glyph("", 17, Ui.Br(Accent))
                };
            avatar.VerticalAlignment = VerticalAlignment.Center;
            var name = Ui.Text(Sender, 14.5, Ui.White, true, true);
            var msg = Ui.Text(string.IsNullOrWhiteSpace(Message) ? App : Message, 12.5, Ui.Dim, false);
            msg.TextTrimming = TextTrimming.CharacterEllipsis;
            msg.MaxHeight = 36;
            msg.TextWrapping = TextWrapping.Wrap;
            var texts = Ui.VStack(1, name, msg);
            texts.Margin = new Thickness(12, 0, 0, 0);
            texts.VerticalAlignment = VerticalAlignment.Center;
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(texts, 1);
            head.Children.Add(avatar);
            head.Children.Add(texts);
            root.Children.Add(head);

            var box = InputBox();
            box.Margin = new Thickness(0, 14, 0, 0);
            root.Children.Add(box);

            var hint = Ui.Text(IslandWindow.ReplyHint(this, host.Settings.ReplyAutoSend), 11, Ui.Dim, false);
            hint.Margin = new Thickness(4, 7, 0, 0);
            root.Children.Add(hint);
            return root;
        }

        FrameworkElement InputBox()
        {
            input = new TextBox
            {
                Text = draft,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Ui.White,
                CaretBrush = Ui.White,
                SelectionBrush = Ui.AccentBrush,
                FontFamily = Ui.FontFor(13.5),
                FontSize = 13.5,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0),
                FocusVisualStyle = null
            };
            var placeholder = Ui.Text("Antwort schreiben …", 13.5, Ui.Dim, false);
            placeholder.IsHitTestVisible = false;
            placeholder.VerticalAlignment = VerticalAlignment.Center;
            placeholder.Visibility = string.IsNullOrEmpty(draft) ? Visibility.Visible : Visibility.Collapsed;
            input.TextChanged += (s, e) =>
            {
                draft = input.Text;
                placeholder.Visibility = string.IsNullOrEmpty(draft) ? Visibility.Visible : Visibility.Collapsed;
            };
            input.PreviewMouseLeftButtonDown += (s, e) => host.BeginTyping(input);
            input.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; Send(); }
                else if (e.Key == Key.Escape) { e.Handled = true; Cancel(); }
            };
            input.Loaded += (s, e) => host.BeginTyping(input);

            var send = Ui.RoundButton("", 13, Send, 30, Ui.Br(Accent), Ui.White);
            send.ToolTip = "Senden (Enter)";
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(placeholder);
            g.Children.Add(input);
            Grid.SetColumn(send, 1);
            g.Children.Add(send);
            var box = new Border
            {
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = Ui.Card,
                Padding = new Thickness(16, 0, 5, 0),
                Child = g,
                Cursor = Cursors.IBeam
            };
            box.MouseLeftButtonDown += (s, e) => { e.Handled = true; host.BeginTyping(input); };
            return box;
        }

        void Send()
        {
            var text = (draft ?? "").Trim();
            if (text.Length == 0) return;
            draft = "";
            host.SendReply(this, text);
        }

        void Cancel()
        {
            draft = "";
            Finished = true;
            host.EndTyping(true);
            host.Collapse();
        }

        public override void OnRemoved()
        {
            if (host.IsTyping) host.EndTyping(true);
        }
    }
}

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DynamicIsland
{
    /// <summary>
    /// Seite „Notiz“: ein Zettel für schnelle Gedanken. Antippen und lostippen, wird beim Tippen automatisch gespeichert
    /// (notiz.txt im Datenordner). Esc oder Wegklicken beendet das Tippen.
    /// </summary>
    public sealed class NoteActivity : Activity
    {
        public const string Glyph = "";   // Notizblock
        readonly IslandWindow host;
        TextBox box;
        TextBlock info;
        readonly DispatcherTimer saver;

        static string FilePath => Path.Combine(Program.DataDir, "notiz.txt");

        static string text;
        /// <summary>Aktueller Inhalt der Notiz.</summary>
        public static string Text
        {
            get
            {
                if (text == null)
                {
                    try { text = File.Exists(FilePath) ? File.ReadAllText(FilePath) : ""; }
                    catch (Exception ex) { Program.Log(ex); text = ""; }
                }
                return text;
            }
        }

        public NoteActivity(IslandWindow h)
        {
            host = h;
            Id = "note";
            Priority = 1;
            saver = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            saver.Tick += (s, e) => { saver.Stop(); Save(); };
        }

        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(420, 240);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph(Glyph, 14, Ui.Br(Ui.Yellow)), Ui.Text("Notiz", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph(Glyph, 14, Ui.Br(Ui.Yellow));

        public bool Typing => host.IsTyping && box != null && box.IsKeyboardFocused;

        public override FrameworkElement BuildExpanded()
        {
            var root = new Grid { Margin = Ui.PageMargin };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Kopf: Titel, Stand, Kopieren, Leeren
            var title = Ui.Text("Notiz", 19, Ui.White);
            info = Ui.Text("", 11.5, Ui.Dim, false);
            info.Margin = new Thickness(10, 4, 0, 0);
            info.VerticalAlignment = VerticalAlignment.Center;
            var copy = Ui.CircleButton("", () => { try { if (Text.Length > 0) Clipboard.SetText(Text); info.Text = "Kopiert"; } catch { } }, 30);
            copy.ToolTip = "Kopieren";
            var clear = Ui.CircleButton("", () => { text = ""; Save(); if (box != null) box.Text = ""; }, 30);
            clear.ToolTip = "Leeren";
            var head = new Grid { Height = 32 };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var btns = Ui.HStack(8, copy, clear);
            Grid.SetColumn(btns, 1);
            head.Children.Add(Ui.HStack(0, title, info));
            head.Children.Add(btns);
            root.Children.Add(head);

            // Zettel
            box = new TextBox
            {
                Text = Text,
                AcceptsReturn = true,
                AcceptsTab = false,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Ui.White,
                CaretBrush = Ui.Br(Ui.Yellow),
                SelectionBrush = Ui.Br(Ui.Yellow),
                FontFamily = Ui.FontFor(14),
                FontSize = 14,
                Padding = new Thickness(0),
                FocusVisualStyle = null
            };
            var ph = Ui.Text("Antippen und lostippen …", 14, Ui.Dim, false);
            ph.IsHitTestVisible = false;
            ph.VerticalAlignment = VerticalAlignment.Top;
            ph.Visibility = Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            box.TextChanged += (s, e) =>
            {
                text = box.Text;
                ph.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                info.Text = "";
                saver.Stop(); saver.Start();
            };
            box.PreviewMouseLeftButtonDown += (s, e) => host.BeginTyping(box);
            box.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; Save(); host.EndTyping(true); }
            };
            box.LostKeyboardFocus += (s, e) => Save();
            box.Loaded += (s, e) =>
            {
                if (!host.IsTyping) return;
                box.Focus();
                Keyboard.Focus(box);
                box.CaretIndex = box.Text.Length;
            };
            var inner = new Grid();
            inner.Children.Add(ph);
            inner.Children.Add(box);
            var paper = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = Ui.Card,
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 8, 0, 0),
                Child = inner,
                Cursor = Cursors.IBeam
            };
            paper.MouseLeftButtonDown += (s, e) => { e.Handled = true; host.BeginTyping(box); box.CaretIndex = box.Text.Length; };
            Grid.SetRow(paper, 1);
            root.Children.Add(paper);
            UpdateInfo();
            return root;
        }

        void UpdateInfo()
        {
            if (info == null) return;
            try
            {
                if (!File.Exists(FilePath) || Text.Length == 0) { info.Text = ""; return; }
                var t = File.GetLastWriteTime(FilePath);
                info.Text = t.Date == DateTime.Today ? "Heute " + t.ToString("HH:mm") : t.ToString("d. MMM, HH:mm", Ui.De);
            }
            catch { }
        }

        static void Save()
        {
            if (text == null) return;
            try
            {
                Directory.CreateDirectory(Program.DataDir);
                File.WriteAllText(FilePath, text);
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        public void FocusInput() { if (box != null) { host.BeginTyping(box); box.CaretIndex = box.Text.Length; } }
    }
}

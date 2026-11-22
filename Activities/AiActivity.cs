using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DynamicIsland
{
    /// <summary>
    /// KI-Seite der Island: Mini-Chat mit Gemini, Text-Drop-Zone (Zusammenfassen/Übersetzen/Korrigieren)
    /// und Texterkennung vom Bildschirm.
    /// </summary>
    public sealed class AiActivity : Activity
    {
        public const string Spark = "✦";
        enum Mode { Home, Drop, Busy, Result }

        readonly IslandWindow host;
        Mode mode = Mode.Home;
        string resultTitle = "", resultText = "", sourceText, busyLabel = "";
        bool resultIsText;   // Ergebnis ist ein Text, mit dem man weiterarbeiten kann (Chips)
        bool contextSent;
        string draft = "";
        TextBox input;
        CancellationTokenSource cts;

        public AiActivity(IslandWindow h)
        {
            host = h;
            Id = "ai";
            Priority = 1;
        }

        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(420, 240);

        /// <summary>Island bleibt offen (nicht beim Wegfahren mit der Maus zuklappen).</summary>
        public bool Sticky => mode != Mode.Home || host.IsTyping || !string.IsNullOrWhiteSpace(draft);
        public bool InDrop => mode == Mode.Drop;
        Mode beforeDrop = Mode.Home;

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, SparkGlyph(14), Ui.Text(mode == Mode.Busy ? busyLabel : host.Gemini.Name, 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => SparkGlyph(14);

        static TextBlock SparkGlyph(double size)
        {
            var t = new TextBlock
            {
                Text = Spark,
                FontSize = size,
                FontFamily = Ui.SymbolFont,
                Foreground = new LinearGradientBrush(Color.FromRgb(0x4F, 0x9C, 0xFF), Color.FromRgb(0xC0, 0x7B, 0xFF), 45),
                VerticalAlignment = VerticalAlignment.Center
            };
            return t;
        }

        // =================================================================
        //  Aufbau
        // =================================================================
        public override FrameworkElement BuildExpanded()
        {
            var root = new Grid { Margin = new Thickness(22, 12, 22, 12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // Kopf
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });  // Inhalt
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // Eingabe

            FrameworkElement head, body;
            switch (mode)
            {
                case Mode.Drop: head = Head("Loslassen zum …", null); body = DropBody(); break;
                case Mode.Busy: head = Head(busyLabel, Ui.Link("Abbrechen", Cancel, 12.5)); body = BusyBody(); break;
                case Mode.Result: head = Head(resultTitle, ResultButtons()); body = ResultBody(); break;
                default: head = Head(host.Gemini.Name, HomeButtons()); body = HomeBody(); break;
            }
            Grid.SetRow(body, 1);
            root.Children.Add(head);
            root.Children.Add(body);
            if (mode != Mode.Drop)
            {
                var box = InputBox();
                Grid.SetRow(box, 2);
                root.Children.Add(box);
            }
            return root;
        }

        FrameworkElement Head(string title, FrameworkElement right)
        {
            var t = Ui.Text(title, 14.5, Ui.White);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            t.Margin = new Thickness(8, 0, 10, 0);
            var g = new Grid { Height = 28 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var spark = SparkGlyph(15);
            Grid.SetColumn(t, 1);
            g.Children.Add(spark);
            g.Children.Add(t);
            if (right != null)
            {
                right.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(right, 2);
                g.Children.Add(right);
            }
            return g;
        }

        static Border Icon(string glyph, string tip, Action click)
        {
            var b = Ui.RoundButton(glyph, 12.5, click, 28, Brushes.Transparent, Ui.Dim);
            b.ToolTip = tip;
            return b;
        }

        FrameworkElement HomeButtons()
        {
            var sp = Ui.HStack(2,
                host.Gemini.History.Count > 0 ? Icon("", "Neuer Chat", () => { host.Gemini.History.Clear(); host.Rebuild(this); }) : null,
                Icon("", "KI-Einstellungen", () => host.OpenSetup("ai")));
            return sp;
        }

        FrameworkElement ResultButtons()
        {
            Border copy = null;
            copy = Icon("", "Kopieren", () =>
            {
                try { Clipboard.SetText(resultText ?? ""); } catch { }
                if (copy?.Child is TextBlock tb)
                {
                    tb.Text = "";
                    tb.Foreground = Ui.Br(Ui.Green);
                    var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
                    t.Tick += (s, e) => { t.Stop(); tb.Text = ""; tb.Foreground = Ui.Dim; };
                    t.Start();
                }
            });
            var close = Icon("", "Schließen", () => { mode = Mode.Home; sourceText = null; host.Rebuild(this); });
            return Ui.HStack(2, copy, close);
        }

        // ---------- Start ----------
        FrameworkElement HomeBody()
        {
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            bool noKey = !host.Gemini.HasKey;
            if (noKey)
            {
                var row = Ui.HStack(6, Ui.Text(host.Gemini.Name + "-Schlüssel fehlt.", 12.5, Ui.Dim, false),
                                       Ui.Link("Jetzt einrichten", () => host.OpenSetup("ai"), 12.5));
                row.Margin = new Thickness(2, 0, 0, 8);
                sp.Children.Add(row);
            }

            var cards = new UniformGrid { Columns = 3, Margin = new Thickness(-3, 0, -3, 0) };
            cards.Children.Add(ActionCard("", "Bildschirm\nlesen", () => host.StartOcr()));
            cards.Children.Add(ActionCard("", "Kopierten Text\nzusammenfassen", () => FromClipboard("sum")));
            cards.Children.Add(ActionCard("", "Kopierten Text\nübersetzen", () => FromClipboard("tr")));
            sp.Children.Add(cards);
            if (noKey) return sp;
            var hint = Ui.Text("Tipp: Markierten Text auf die Island ziehen", 11.5, Ui.Dim, false);
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.Margin = new Thickness(0, 9, 0, 0);
            sp.Children.Add(hint);
            return sp;
        }

        static Border ActionCard(string glyph, string label, Action click)
        {
            var lbl = Ui.Text(label, 11.5, Ui.White, false);
            lbl.TextAlignment = TextAlignment.Left;
            lbl.TextTrimming = TextTrimming.None;
            lbl.LineHeight = 14;
            var sp = new StackPanel();
            var g = Ui.Glyph(glyph, 14, Ui.AccentBrush);
            g.HorizontalAlignment = HorizontalAlignment.Left;
            g.Margin = new Thickness(0, 0, 0, 6);
            sp.Children.Add(g);
            sp.Children.Add(lbl);
            var b = new Border
            {
                Height = 72,
                Margin = new Thickness(3, 0, 3, 0),
                CornerRadius = new CornerRadius(14),
                Background = Ui.Card,
                Padding = new Thickness(11, 10, 8, 8),
                Cursor = Cursors.Hand,
                Child = sp
            };
            b.MouseEnter += (s, e) => b.Background = Ui.CardHover;
            b.MouseLeave += (s, e) => b.Background = Ui.Card;
            Ui.Pressable(b, 0.95);
            b.MouseLeftButtonDown += (s, e) => e.Handled = true;
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; try { click(); } catch (Exception ex) { Program.Log(ex); } };
            return b;
        }

        void FromClipboard(string kind)
        {
            string t = null;
            try { if (Clipboard.ContainsText()) t = Clipboard.GetText(); } catch { }
            if (string.IsNullOrWhiteSpace(t)) { ShowResult("Zwischenablage", "In der Zwischenablage ist kein Text.", false); return; }
            RunTask(kind, t);
        }

        // ---------- Ergebnis ----------
        FrameworkElement ResultBody()
        {
            var g = new Grid { Margin = new Thickness(0, 6, 0, 8) };
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.Children.Add(ReadOnlyText(resultText));
            if (resultIsText && !string.IsNullOrWhiteSpace(resultText))
            {
                var chips = Ui.HStack(6,
                    Chip("Zusammenfassen", () => RunTask("sum", resultText)),
                    Chip("Übersetzen", () => RunTask("tr", resultText)),
                    Chip("Korrigieren", () => RunTask("fix", resultText)),
                    Chip("Erklären", () => RunTask("explain", resultText)));
                chips.Margin = new Thickness(0, 7, 0, 0);
                Grid.SetRow(chips, 1);
                g.Children.Add(chips);
            }
            return g;
        }

        static FrameworkElement ReadOnlyText(string text)
        {
            var tb = new TextBox
            {
                Text = text ?? "",
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Ui.White,
                FontFamily = Ui.FontFor(13.5),
                FontSize = 13.5,
                Padding = new Thickness(0),
                SelectionBrush = Ui.AccentBrush,
                CaretBrush = Brushes.Transparent,
                Cursor = Cursors.IBeam,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                FocusVisualStyle = null
            };
            tb.PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true;
                tb.ScrollToVerticalOffset(tb.VerticalOffset - e.Delta / 3.0);
            };
            return tb;
        }

        static Border Chip(string text, Action click)
        {
            var b = new Border
            {
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = Ui.Card,
                Padding = new Thickness(11, 0, 11, 0),
                Cursor = Cursors.Hand,
                Child = Ui.Text(text, 12, Ui.White, false)
            };
            ((TextBlock)b.Child).VerticalAlignment = VerticalAlignment.Center;
            b.MouseEnter += (s, e) => b.Background = Ui.CardHover;
            b.MouseLeave += (s, e) => b.Background = Ui.Card;
            Ui.Pressable(b, 0.94);
            b.MouseLeftButtonDown += (s, e) => e.Handled = true;
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; click(); };
            return b;
        }

        // ---------- Warten ----------
        FrameworkElement BusyBody()
        {
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var track = new Grid { Height = 4, Margin = new Thickness(2, 0, 2, 12), ClipToBounds = true };
            track.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = Ui.Br(Colors.White, 26) });
            var glow = new Border
            {
                Width = 110,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(0, 0x4F, 0x9C, 0xFF), 0),
                        new GradientStop(Color.FromRgb(0x4F, 0x9C, 0xFF), 0.4),
                        new GradientStop(Color.FromRgb(0xC0, 0x7B, 0xFF), 0.7),
                        new GradientStop(Color.FromArgb(0, 0xC0, 0x7B, 0xFF), 1)
                    }, 0),
                RenderTransform = new TranslateTransform(-110, 0)
            };
            track.Children.Add(glow);
            glow.Loaded += (s, e) =>
            {
                if (Ui.ReduceMotion) { glow.RenderTransform = new TranslateTransform(track.ActualWidth / 2 - 55, 0); return; }
                var anim = new DoubleAnimation(-110, track.ActualWidth, TimeSpan.FromSeconds(1.3)) { RepeatBehavior = RepeatBehavior.Forever };
                ((TranslateTransform)glow.RenderTransform).BeginAnimation(TranslateTransform.XProperty, anim);
            };
            sp.Children.Add(track);
            if (!string.IsNullOrWhiteSpace(sourceText))
            {
                var prev = Ui.Text(sourceText.Replace("\r", " ").Replace("\n", " "), 12, Ui.Dim, false);
                prev.TextWrapping = TextWrapping.Wrap;
                prev.TextTrimming = TextTrimming.CharacterEllipsis;
                prev.MaxHeight = 50;
                prev.Margin = new Thickness(2, 0, 2, 0);
                sp.Children.Add(prev);
            }
            return sp;
        }

        // ---------- Drop-Zone ----------
        FrameworkElement DropBody()
        {
            bool obs = host.Settings.ObsidianEnabled;
            var grid = new UniformGrid { Columns = 2, Rows = obs ? 3 : 2, Margin = new Thickness(-3, 8, -3, 0) };
            grid.Children.Add(DropCard("", "Zusammenfassen", "sum"));
            grid.Children.Add(DropCard("", "Übersetzen", "tr"));
            grid.Children.Add(DropCard("", "Korrigieren", "fix"));
            grid.Children.Add(DropCard("", "Erklären", "explain"));
            if (obs) grid.Children.Add(DropCard("", "In Obsidian", "obsidian"));   // Text direkt in die Obsidian-Notiz
            return grid;
        }

        Border DropCard(string glyph, string label, string kind)
        {
            var b = new Border
            {
                Margin = new Thickness(3),
                CornerRadius = new CornerRadius(16),
                Background = Ui.Card,
                BorderThickness = new Thickness(1.5),
                BorderBrush = Brushes.Transparent,
                AllowDrop = true,
                Child = Ui.HStack(9, Ui.Glyph(glyph, 16, Ui.AccentBrush), Ui.Text(label, 13.5, Ui.White))
            };
            ((StackPanel)b.Child).HorizontalAlignment = HorizontalAlignment.Center;
            b.DragEnter += (s, e) => { b.Background = Ui.CardHover; b.BorderBrush = Ui.AccentBrush; e.Effects = DragDropEffects.Copy; e.Handled = true; };
            b.DragOver += (s, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            b.DragLeave += (s, e) => { b.Background = Ui.Card; b.BorderBrush = Brushes.Transparent; };
            b.Drop += (s, e) =>
            {
                e.Handled = true;
                var t = DroppedText(e.Data);
                if (string.IsNullOrWhiteSpace(t)) { EndDrop(); return; }
                if (kind == "obsidian") { EndDrop(); host.Collapse(); host.SaveToObsidian(e.Data); return; }
                RunTask(kind, t);
            };
            return b;
        }

        public static string DroppedText(IDataObject d)
        {
            try
            {
                if (d.GetDataPresent(DataFormats.UnicodeText)) return d.GetData(DataFormats.UnicodeText) as string;
                if (d.GetDataPresent(DataFormats.Text)) return d.GetData(DataFormats.Text) as string;
            }
            catch { }
            return null;
        }

        public static bool HasText(IDataObject d)
        {
            try { return d.GetDataPresent(DataFormats.UnicodeText) || d.GetDataPresent(DataFormats.Text); }
            catch { return false; }
        }

        public void BeginDrop()
        {
            if (mode == Mode.Drop) return;
            beforeDrop = mode == Mode.Busy ? Mode.Home : mode;
            mode = Mode.Drop;
            host.Rebuild(this);
        }

        public void EndDrop()
        {
            if (mode != Mode.Drop) return;
            mode = beforeDrop;
            host.Rebuild(this);
        }

        // ---------- Eingabe ----------
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
            string ph = mode == Mode.Result && resultIsText ? "Frage zu diesem Text …" : "Frag " + host.Gemini.Name + " oder rechne, z. B. 23 * 19";
            var placeholder = Ui.Text(ph, 13.5, Ui.Dim, false);
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
                else if (e.Key == Key.Escape) { e.Handled = true; draft = ""; host.CloseAi(); }
            };
            input.Loaded += (s, e) =>
            {
                if (!host.IsTyping) return;
                input.Focus();
                Keyboard.Focus(input);
                input.CaretIndex = input.Text.Length;
            };

            var send = Ui.RoundButton("", 13, Send, 30, Ui.AccentBrush, Ui.OnAccent);
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

        /// <summary>Tastenkürzel: Chat öffnen und direkt tippen.</summary>
        public void FocusInput()
        {
            if (input == null) return;
            host.BeginTyping(input);
        }

        // =================================================================
        //  Aktionen
        // =================================================================
        static readonly Regex MathOnly = new Regex(@"^[\d\s\.,+\-*/()%]+$");

        async void Send()
        {
            string q = (draft ?? "").Trim();
            if (q.Length == 0) return;
            draft = "";

            // einfache Rechnung sofort lokal
            if (MathOnly.IsMatch(q) && q.IndexOfAny(new[] { '+', '-', '*', '/', '%' }) > 0)
            {
                try
                {
                    string expr = q.Replace(",", ".").Replace("%", "/100");
                    var v = Convert.ToDouble(new DataTable().Compute(expr, null), CultureInfo.InvariantCulture);
                    if (!double.IsNaN(v) && !double.IsInfinity(v))
                    {
                        ShowResult(q, "= " + Math.Round(v, 10).ToString("#,0.##########", Ui.De), false);
                        return;
                    }
                }
                catch { }
            }

            string prompt = q;
            if (mode == Mode.Result && resultIsText && !contextSent && !string.IsNullOrWhiteSpace(resultText))
            {
                prompt = q + "\n\nBezogen auf diesen Text:\n\"\"\"\n" + Trim(resultText, 12000) + "\n\"\"\"";
                contextSent = true;
            }
            await Run(Trim(q, 60), async ct =>
            {
                string a = await host.Gemini.ChatAsync(prompt, ct);
                return (Trim(q, 60), a, false);
            });
        }

        /// <summary>Aufgabe auf einem Text: sum, tr, fix, explain.</summary>
        public async void RunTask(string kind, string text)
        {
            string target = string.IsNullOrWhiteSpace(host.Settings.AiTranslateTo) ? "Deutsch" : host.Settings.AiTranslateTo;
            (string title, string instr) = kind switch
            {
                "tr" => ("Übersetzung", $"Übersetze den Text ins {target}. Ist er bereits auf {target}, übersetze ihn ins Englische. Gib nur die Übersetzung aus."),
                "fix" => ("Korrigiert", "Korrigiere Rechtschreibung, Grammatik und Zeichensetzung. Behalte Sprache, Bedeutung und Stil bei. Gib nur den korrigierten Text aus."),
                "explain" => ("Erklärung", "Erkläre kurz und verständlich auf Deutsch (höchstens 4 Sätze), worum es in dem Text geht."),
                _ => ("Zusammenfassung", "Fasse den Text in 2 bis 4 kurzen Sätzen auf Deutsch zusammen."),
            };
            sourceText = text;
            contextSent = false;
            await Run(title + " …", async ct =>
            {
                string a = await host.Gemini.TaskAsync(instr, text, ct);
                return (title, a, true);
            });
        }

        /// <summary>Erkannter Text (OCR) oder eigener Inhalt anzeigen.</summary>
        public void ShowResult(string title, string text, bool isText)
        {
            cts?.Cancel();
            resultTitle = title;
            resultText = text ?? "";
            resultIsText = isText;
            contextSent = false;
            mode = Mode.Result;
            host.ShowAi();
            host.Rebuild(this);
        }

        public void ShowBusy(string label)
        {
            busyLabel = label;
            sourceText = null;
            mode = Mode.Busy;
            host.ShowAi();
            host.Rebuild(this);
        }

        async Task Run(string label, Func<CancellationToken, Task<(string title, string text, bool isText)>> work)
        {
            cts?.Cancel();
            var my = cts = new CancellationTokenSource();
            busyLabel = label;
            mode = Mode.Busy;
            host.ShowAi();
            host.Rebuild(this);
            (string title, string text, bool isText) r;
            try { r = await work(my.Token); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { r = ("Hat nicht geklappt", ex.Message, false); }
            if (my.IsCancellationRequested || my != cts) return;
            resultTitle = r.title;
            resultText = r.text;
            resultIsText = r.isText;
            mode = Mode.Result;
            host.ShowAi();
            host.Rebuild(this);
        }

        void Cancel()
        {
            cts?.Cancel();
            mode = Mode.Home;
            host.Rebuild(this);
        }

        static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";

        /// <summary>Beim Zuklappen: Ergebnis vergessen, Chat bleibt.</summary>
        public void Reset()
        {
            if (mode == Mode.Busy) return;
            mode = Mode.Home;
            sourceText = null;
        }
    }
}

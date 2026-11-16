using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DynamicIsland
{
    /// <summary>Seite „To Do“: offene Aufgaben abhaken und neue hinzufügen (Microsoft To Do).</summary>
    public sealed class TodoActivity : Activity
    {
        public const string Glyph = "";   // Kontrollkästchen
        const int MaxRows = 3;
        readonly IslandWindow host;
        string shownKey = "", draft = "";
        TextBox input;

        public TodoActivity(IslandWindow h)
        {
            host = h;
            Id = "todo";
            Priority = 1;
        }

        public override double CompactWidth => 200;
        public override Size ExpandedSize => new Size(420, 240);

        public override FrameworkElement BuildCompact() =>
            Ui.Row3(Ui.HStack(7, Ui.Glyph(Glyph, 14, Ui.AccentBrush), Ui.Text("To Do", 13, Ui.White)), null, null, new Thickness(13, 0, 15, 0));

        public override FrameworkElement BuildMinimal() => Ui.Glyph(Glyph, 14, Ui.AccentBrush);

        public bool Typing => host.IsTyping && input != null && input.IsKeyboardFocused;

        string StateKey()
        {
            var t = host.Todo;
            return t.Connected + "|" + string.Join(";", t.Open(MaxRows).Select(x => x.Id + x.Title)) + "|" + t.Tasks.Count + "|" + DateTime.Today.Day;
        }

        public override FrameworkElement BuildExpanded()
        {
            shownKey = StateKey();
            var todo = host.Todo;
            var root = new Grid { Margin = new Thickness(22, 14, 22, 12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Kopf
            var title = Ui.Text(todo.MainList?.Name ?? "Aufgaben", 17, Ui.White);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            int n = todo.Tasks.Count;
            var count = Ui.Text(n == 0 ? "" : n.ToString(), 17, Ui.Dim, false);
            count.Margin = new Thickness(8, 0, 0, 0);
            var refresh = Ui.RoundButton("", 12.5, () => { _ = todo.RefreshAsync(); }, 28, Brushes.Transparent, Ui.Dim);
            refresh.ToolTip = "Aktualisieren";
            var open = Ui.RoundButton("", 12.5, () => IslandWindow.Open("https://to-do.live.com/tasks/"), 28, Brushes.Transparent, Ui.Dim);
            open.ToolTip = "In Microsoft To Do öffnen";
            var head = new Grid { Height = 28 };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = Ui.HStack(0, title, count);
            var btns = Ui.HStack(2, refresh, open);
            Grid.SetColumn(btns, 1);
            head.Children.Add(left);
            head.Children.Add(btns);
            root.Children.Add(head);

            // Liste
            var list = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
            Grid.SetRow(list, 1);
            if (!todo.Connected)
            {
                var t = Ui.Text("Microsoft To Do ist noch nicht verbunden.", 13, Ui.Dim, false);
                t.Margin = new Thickness(2, 8, 0, 4);
                list.Children.Add(t);
                var l = Ui.Link("Jetzt verbinden", () => host.OpenSetup("todo"), 13);
                l.Margin = new Thickness(2, 0, 0, 0);
                list.Children.Add(l);
                root.Children.Add(list);
                return root;
            }
            var items = todo.Open(MaxRows);
            if (items.Count == 0)
            {
                var t = Ui.Text(todo.LastSync == default ? "Wird geladen …" : "Alles erledigt ✓", 13, Ui.Dim, false);
                t.Margin = new Thickness(2, 8, 0, 0);
                list.Children.Add(t);
            }
            foreach (var task in items) list.Children.Add(Row(task));
            root.Children.Add(list);

            var box = InputBox();
            Grid.SetRow(box, 2);
            root.Children.Add(box);
            return root;
        }

        public static string DueText(TodoTask t, out bool late)
        {
            late = false;
            if (!t.Due.HasValue) return null;
            var d = t.Due.Value.Date;
            if (d < DateTime.Today) { late = true; return "Überfällig · " + d.ToString("d. MMM", Ui.De); }
            if (d == DateTime.Today) return "Heute";
            if (d == DateTime.Today.AddDays(1)) return "Morgen";
            return d.ToString("ddd, d. MMM", Ui.De);
        }

        FrameworkElement Row(TodoTask task)
        {
            // Kreis zum Abhaken (wie in Erinnerungen / To Do)
            var ring = new Ellipse { Width = 18, Height = 18, Stroke = Ui.Dim, StrokeThickness = 1.6, Fill = Brushes.Transparent };
            var check = Ui.Glyph("", 10, Ui.OnAccent);
            check.Opacity = 0;
            var circle = new Grid { Width = 22, Height = 22, Cursor = Cursors.Hand, Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
            circle.Children.Add(ring);
            circle.Children.Add(check);
            circle.ToolTip = "Erledigt";
            circle.MouseEnter += (s, e) => ring.Stroke = Ui.AccentBrush;
            circle.MouseLeave += (s, e) => ring.Stroke = Ui.Dim;
            Ui.Pressable(circle, 0.85);
            circle.MouseLeftButtonDown += (s, e) => e.Handled = true;

            var t = Ui.Text(task.Title, 13.5, Ui.White, false);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            string due = DueText(task, out bool late);
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0) };
            texts.Children.Add(t);
            if (due != null)
            {
                var d = Ui.Text(due + (task.Reminder.HasValue ? "  ·  " + task.Reminder.Value.ToString("HH:mm") : ""), 11, late ? Ui.Br(Ui.Red) : Ui.Dim, false);
                texts.Children.Add(d);
            }
            var g = new Grid { Height = 36 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(texts, 1);
            g.Children.Add(circle);
            g.Children.Add(texts);
            if (task.Important)
            {
                var star = Ui.Glyph("", 12, Ui.Br(Ui.Orange));
                Grid.SetColumn(star, 2);
                g.Children.Add(star);
            }

            circle.MouseLeftButtonUp += async (s, e) =>
            {
                e.Handled = true;
                // sofort Rückmeldung: Kreis füllt sich, Zeile blendet aus
                ring.Fill = Ui.AccentBrush;
                ring.Stroke = Ui.AccentBrush;
                check.Opacity = 1;
                t.Foreground = Ui.Dim;
                t.TextDecorations = TextDecorations.Strikethrough;
                if (!Ui.ReduceMotion) g.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.35, TimeSpan.FromMilliseconds(600)) { BeginTime = TimeSpan.FromMilliseconds(250) });
                await Task.Delay(700);
                bool ok = await host.Todo.CompleteAsync(task);
                if (!ok) host.Rebuild(this);
            };
            return g;
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
            var ph = Ui.Text("Neue Aufgabe …", 13.5, Ui.Dim, false);
            ph.IsHitTestVisible = false;
            ph.VerticalAlignment = VerticalAlignment.Center;
            ph.Visibility = string.IsNullOrEmpty(draft) ? Visibility.Visible : Visibility.Collapsed;
            input.TextChanged += (s, e) => { draft = input.Text; ph.Visibility = string.IsNullOrEmpty(draft) ? Visibility.Visible : Visibility.Collapsed; };
            input.PreviewMouseLeftButtonDown += (s, e) => host.BeginTyping(input);
            input.PreviewKeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; await AddAsync(); }
                else if (e.Key == Key.Escape) { e.Handled = true; draft = ""; host.EndTyping(true); host.Rebuild(this); }
            };
            input.Loaded += (s, e) =>
            {
                if (!host.IsTyping) return;
                input.Focus();
                Keyboard.Focus(input);
                input.CaretIndex = input.Text.Length;
            };
            var plus = Ui.Glyph("", 12, Ui.AccentBrush);
            plus.Margin = new Thickness(0, 0, 10, 0);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(ph, 1);
            Grid.SetColumn(input, 1);
            g.Children.Add(plus);
            g.Children.Add(ph);
            g.Children.Add(input);
            var box = new Border
            {
                Height = 38,
                CornerRadius = new CornerRadius(19),
                Background = Ui.Card,
                Padding = new Thickness(14, 0, 14, 0),
                Child = g,
                Cursor = Cursors.IBeam
            };
            box.MouseLeftButtonDown += (s, e) => { e.Handled = true; host.BeginTyping(input); };
            return box;
        }

        async Task AddAsync()
        {
            string t = (draft ?? "").Trim();
            if (t.Length == 0) return;
            draft = "";
            input.Text = "";
            bool ok = await host.Todo.AddAsync(t);
            if (!ok) { draft = t; host.Rebuild(this); }
        }

        public void FocusInput() { if (input != null) host.BeginTyping(input); }

        public override void Update()
        {
            if (StateKey() != shownKey && !Typing) { shownKey = StateKey(); host.Rebuild(this); }
        }
    }

    /// <summary>Widget: offene Aufgaben.</summary>
    sealed class TodoWidget : Widget
    {
        TextBlock big, sub;
        string key = "";
        public override Action Tap => () => H.ShowTodo();
        public override FrameworkElement Build()
        {
            big = Big("");
            sub = Ui.Text("", 11.5, Ui.Dim, false);
            sub.TextTrimming = TextTrimming.CharacterEllipsis;
            var head = Ui.HStack(5, Ui.Glyph(TodoActivity.Glyph, 11, Ui.AccentBrush), Ui.Text("To Do", 11.5, Ui.Dim, false));
            return Ui.VStack(2, head, big, sub);
        }
        public override void Update()
        {
            var t = H.Todo;
            var first = t.Open(1).FirstOrDefault();
            string k = t.Connected + "|" + t.Tasks.Count + "|" + first?.Id;
            if (k == key) return;
            key = k;
            if (!t.Connected) { big.Text = "–"; sub.Text = "Antippen zum Verbinden"; return; }
            big.Text = t.Tasks.Count == 0 ? "✓" : t.Tasks.Count + " offen";
            sub.Text = first?.Title ?? "Alles erledigt";
        }
    }
}

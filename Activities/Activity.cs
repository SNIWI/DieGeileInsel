using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DynamicIsland
{
    /// <summary>
    /// Eine "Live-Aktivität". Jede hat drei Ansichten wie beim iPhone:
    /// kompakt (Pille), minimal (kleiner Kreis daneben) und erweitert (großes Widget).
    /// </summary>
    public abstract class Activity
    {
        public string Id = Guid.NewGuid().ToString("N");
        public int Priority = 10;          // höher = bekommt die Hauptpille
        public bool IsAlert;               // kurzer Hinweis statt dauerhafter Aktivität
        public DateTime CreatedAt = DateTime.Now;
        public DateTime? ExpiresAt;
        public bool Finished;
        public IslandWindow Host;

        public virtual bool CanExpand => true;
        public virtual double CompactWidth => 250;
        public virtual double CompactHeight => 36;
        public virtual Size ExpandedSize => new Size(380, 170);

        public abstract FrameworkElement BuildCompact();
        public virtual FrameworkElement BuildExpanded() => BuildCompact();
        public virtual FrameworkElement BuildMinimal() => Ui.Glyph("", 14);

        /// <summary>Lieber als kleiner Kreis neben der Island anzeigen statt in der Hauptpille (z. B. Claude).</summary>
        public virtual bool PreferBubble => false;
        /// <summary>Beim Drüberfahren wird der Kreis so breit und zeigt <see cref="BuildBubbleHover"/> (0 = nie).</summary>
        public virtual double BubbleHoverWidth => 0;
        public virtual FrameworkElement BuildBubbleHover() => null;

        /// <summary>Wird ca. 20× pro Sekunde aufgerufen.</summary>
        public virtual void Update() { }

        /// <summary>Antippen. true = erledigt (z. B. App geöffnet), false = Island erweitern.</summary>
        public virtual bool OnTap() => false;

        public virtual void OnRemoved() { }
    }

    public sealed class ActionButton
    {
        public string Glyph;
        public Brush Background;
        public Action Click;
        public FontFamily Font;

        public ActionButton(string glyph, Brush background, Action click, FontFamily font = null)
        {
            Glyph = glyph;
            Background = background;
            Click = click;
            Font = font;
        }
    }

    /// <summary>
    /// Universelle Aktivität: Symbol, Texte, Fortschritt, Buttons. Wird für Hinweise,
    /// Demos (Navigation, Flug, Uber …) und die lokale API benutzt.
    /// </summary>
    public class GenericActivity : Activity
    {
        public string Glyph = "";
        public FontFamily GlyphFont = Ui.IconFont;
        public Color Accent = Ui.Blue;
        public string LeftText, RightText, Title, Subtitle, Detail;
        public Color? RightColor;
        public double? Progress;
        public bool BarInCompact;          // Fortschrittsbalken in der kleinen Pille (z. B. Lautstärke)
        public bool Banner;                // größere Pille mit Titel + Untertitel
        public double CompactW = 250;
        public Func<FrameworkElement> RightFactory;
        public readonly List<ActionButton> Buttons = new List<ActionButton>();
        public Action<GenericActivity> Ticker;
        public Func<bool> TapAction;
        public bool Expandable = true;
        public DateTime StartedAt = DateTime.Now;
        public double Seconds => (DateTime.Now - StartedAt).TotalSeconds;

        TextBlock cIcon, cLeft, cRight, bTitle, bSub, eIcon, eTitle, eSub, eRight, eDetail, mIcon;
        Border bCircle, eCircle;
        Bar cBar, eBar;
        Color brushColor;
        SolidColorBrush brushCache;

        public override bool CanExpand => Expandable;
        public override double CompactWidth => Banner ? Math.Max(CompactW, 350) : CompactW;
        public override double CompactHeight => Banner ? 70 : 36;

        public override Size ExpandedSize
        {
            get
            {
                double h = 96;
                if (!string.IsNullOrEmpty(Detail)) h += 10 + 18 * Detail.Split('\n').Length;
                if (Progress.HasValue) h += 18;
                if (Buttons.Count > 0) h += 60;
                return new Size(390, h);
            }
        }

        SolidColorBrush Acc()
        {
            if (brushCache == null || brushColor != Accent) { brushColor = Accent; brushCache = Ui.Br(Accent); }
            return brushCache;
        }

        void ClearRefs()
        {
            cIcon = cLeft = cRight = bTitle = bSub = eIcon = eTitle = eSub = eRight = eDetail = null;
            bCircle = eCircle = null;
            cBar = eBar = null;
        }

        public override FrameworkElement BuildCompact()
        {
            ClearRefs();
            return Banner ? BuildBanner() : BuildPill();
        }

        FrameworkElement BuildPill()
        {
            cIcon = Ui.Glyph(Glyph, 15, Acc(), GlyphFont);
            cLeft = Ui.Text(LeftText, 13, Ui.White, true, true);
            var left = Ui.HStack(7, cIcon, cLeft);
            UIElement middle = null;
            if (BarInCompact)
            {
                cBar = new Bar(5, Acc()) { Margin = new Thickness(12, 0, 12, 0) };
                middle = cBar;
            }
            UIElement right;
            if (RightFactory != null) right = RightFactory();
            else
            {
                cRight = Ui.Text(RightText, 13.5, Ui.Br(RightColor ?? Accent), true, true);
                cRight.MaxWidth = Math.Max(60, CompactWidth - 130);
                right = cRight;
            }
            var g = Ui.Row3(left, middle, right, new Thickness(13, 0, 15, 0));
            Refresh();
            return g;
        }

        FrameworkElement BuildBanner()
        {
            bCircle = new Border
            {
                Width = 42, Height = 42,
                CornerRadius = new CornerRadius(21),
                Background = Ui.Br(Accent, 55),
                VerticalAlignment = VerticalAlignment.Center
            };
            cIcon = Ui.Glyph(Glyph, 19, Acc(), GlyphFont);
            bCircle.Child = cIcon;
            bTitle = Ui.Text(Title, 14.5, Ui.White);
            bSub = Ui.Text(Subtitle, 12, Ui.Dim, false);
            var texts = Ui.VStack(1, bTitle, bSub);
            if (Progress.HasValue)
            {
                cBar = new Bar(4, Acc()) { Margin = new Thickness(0, 6, 0, 0) };
                texts.Children.Add(cBar);
            }
            texts.Margin = new Thickness(11, 0, 8, 0);
            UIElement right;
            if (Buttons.Count > 0) right = ButtonsPanel(38);
            else if (RightFactory != null) right = RightFactory();
            else { cRight = Ui.Text(RightText, 15, Ui.Br(RightColor ?? Accent), true, true); right = cRight; }
            var g = Ui.Row3(bCircle, texts, right, new Thickness(14, 0, 16, 0));
            Refresh();
            return g;
        }

        public override FrameworkElement BuildExpanded()
        {
            ClearRefs();
            var root = new StackPanel { Margin = new Thickness(20, 18, 20, 14) };
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            eCircle = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(23), Background = Ui.Br(Accent, 55) };
            eIcon = Ui.Glyph(Glyph, 21, Acc(), GlyphFont);
            eCircle.Child = eIcon;
            eTitle = Ui.Text(Title ?? LeftText, 15.5, Ui.White);
            eSub = Ui.Text(Subtitle, 12.5, Ui.Dim, false);
            var texts = Ui.VStack(2, eTitle, eSub);
            texts.Margin = new Thickness(12, 0, 10, 0);
            UIElement right;
            if (RightFactory != null) right = RightFactory();
            else { eRight = Ui.Text(RightText, 24, Ui.Br(RightColor ?? Accent), true, true); right = eRight; }

            Grid.SetColumn(eCircle, 0); Grid.SetColumn(texts, 1); Grid.SetColumn(right, 2);
            top.Children.Add(eCircle); top.Children.Add(texts); top.Children.Add(right);
            root.Children.Add(top);

            if (!string.IsNullOrEmpty(Detail))
            {
                eDetail = Ui.Text(Detail, 12.5, Ui.Dim, false);
                eDetail.Margin = new Thickness(0, 10, 0, 0);
                root.Children.Add(eDetail);
            }
            if (Progress.HasValue)
            {
                eBar = new Bar(5, Acc()) { Margin = new Thickness(0, 12, 0, 0) };
                root.Children.Add(eBar);
            }
            if (Buttons.Count > 0)
            {
                var bp = ButtonsPanel(46);
                bp.HorizontalAlignment = HorizontalAlignment.Center;
                bp.Margin = new Thickness(0, 12, 0, 0);
                root.Children.Add(bp);
            }
            Refresh();
            return root;
        }

        public override FrameworkElement BuildMinimal()
        {
            mIcon = Ui.Glyph(Glyph, 15, Acc(), GlyphFont);
            return mIcon;
        }

        StackPanel ButtonsPanel(double size)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < Buttons.Count; i++)
            {
                var b = Buttons[i];
                var btn = Ui.RoundButton(b.Glyph, size * 0.42, b.Click, size, b.Background, Ui.White, b.Font);
                if (i > 0) btn.Margin = new Thickness(size * 0.3, 0, 0, 0);
                sp.Children.Add(btn);
            }
            return sp;
        }

        public override void Update()
        {
            try { Ticker?.Invoke(this); }
            catch (Exception ex) { Program.Log(ex); }
            Refresh();
        }

        /// <summary>Überträgt die aktuellen Werte in die angezeigten Elemente.</summary>
        public void Refresh()
        {
            var acc = Acc();
            SetGlyph(cIcon, acc); SetGlyph(eIcon, acc); SetGlyph(mIcon, acc);
            if (bCircle != null) bCircle.Background = Ui.Br(Accent, 55);
            if (eCircle != null) eCircle.Background = Ui.Br(Accent, 55);
            if (cLeft != null)
            {
                cLeft.Text = LeftText ?? "";
                cLeft.Visibility = string.IsNullOrEmpty(LeftText) ? Visibility.Collapsed : Visibility.Visible;
            }
            var rightBrush = RightColor.HasValue ? Ui.Br(RightColor.Value) : acc;
            if (cRight != null) { cRight.Text = RightText ?? ""; cRight.Foreground = rightBrush; }
            if (eRight != null) { eRight.Text = RightText ?? ""; eRight.Foreground = rightBrush; }
            if (bTitle != null) bTitle.Text = Title ?? "";
            if (bSub != null) bSub.Text = Subtitle ?? "";
            if (eTitle != null) eTitle.Text = Title ?? LeftText ?? "";
            if (eSub != null) eSub.Text = Subtitle ?? "";
            if (eDetail != null) eDetail.Text = Detail ?? "";
            if (Progress.HasValue)
            {
                if (cBar != null) cBar.Value = Progress.Value;
                if (eBar != null) eBar.Value = Progress.Value;
            }
        }

        void SetGlyph(TextBlock t, Brush b)
        {
            if (t == null) return;
            t.Text = Glyph;
            t.FontFamily = GlyphFont;
            t.Foreground = b;
        }

        public override bool OnTap() => TapAction != null && TapAction();
    }
}

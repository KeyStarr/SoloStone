using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SoloStone
{
    // Read-only text that wraps at any character and grows to fit, so a long path or message is never cut off.
    internal sealed class WrapText : TextBox
    {
        public static Color VerdictColor(VerdictKind? kind) => Texts.VerdictColor(kind) == VerdictTone.Block ? Color.Firebrick : Texts.VerdictColor(kind) == VerdictTone.Warning ? Color.FromArgb(160, 80, 0) : SystemColors.ControlText;
        public int MaxHeight = 220;
        public WrapText(string name) {
            Name = name; Multiline = true; ReadOnly = true; BorderStyle = BorderStyle.None; TabStop = false; WordWrap = true;
            BackColor = SystemColors.Control; ScrollBars = ScrollBars.None; Height = 20;
            Fit();
        }
        public void SetText(string value) {
            value = value ?? "";
            if (Text == value) return;
            Text = value; Fit();
        }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Fit(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Fit(); }
        bool fitting;
        internal int WantedHeight() {
            int width = Math.Max(40, ClientSize.Width);
            var size = TextRenderer.MeasureText(string.IsNullOrEmpty(Text) ? " " : Text + " ", Font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            return size.Height + 4;
        }
        void Fit() {
            if (fitting || Width < 40) return;
            fitting = true;
            try {
                int wanted = WantedHeight();
                int height = Math.Max(Font.Height + 4, Math.Min(wanted, MaxHeight));
                // Changing ScrollBars recreates the window, which fails inside its own size change ("Error creating window handle"). Do it afterwards.
                var bars = wanted > MaxHeight ? ScrollBars.Vertical : ScrollBars.None;
                if (ScrollBars != bars) {
                    if (IsHandleCreated) BeginInvoke((Action)(() => { if (!IsDisposed) ScrollBars = WantedHeight() > MaxHeight ? ScrollBars.Vertical : ScrollBars.None; }));
                    else ScrollBars = bars;
                }
                if (Height != height) Height = height;
            } finally { fitting = false; }
        }
    }

    // Stacks its visible children top to bottom at the full width of the panel. Long content scrolls instead of clipping.
    internal sealed class Stack : Panel
    {
        public int Gap = 10;
        public Stack(string name) { Name = name; AutoScroll = true; Padding = new Padding(16, 12, 16, 12); }
        bool laying;
        protected override void OnLayout(LayoutEventArgs e) {
            base.OnLayout(e);
            if (laying) return;
            laying = true;
            try {
                int width = Math.Max(120, ClientSize.Width - Padding.Horizontal);
                int y = Padding.Top + AutoScrollPosition.Y, x = Padding.Left + AutoScrollPosition.X;
                foreach (Control c in Controls) {
                    if (!c.Visible) continue;
                    if (c is WrapText || c is Row || c is ProgressBar || c is FlowLayoutPanel) c.Width = width;
                    else if (c.AutoSize) c.MaximumSize = new Size(width, 0);
                    else c.Width = Math.Min(c.Width, width);
                    c.Left = x; c.Top = y; y += c.Height + Gap;
                }
                AutoScrollMinSize = new Size(0, y - AutoScrollPosition.Y + Padding.Bottom - Gap);
            } finally { laying = false; }
        }
    }

    // A text box with one button beside it.
    internal sealed class Row : Panel
    {
        public readonly TextBox Box; public readonly Button Action;
        public Row(string boxName, string buttonName, string buttonText) {
            Box = new TextBox { Name = boxName }; Action = Ui.Button(buttonName, buttonText);
            Height = Math.Max(Box.PreferredHeight, Action.Height);
            Controls.Add(Box); Controls.Add(Action);
        }
        protected override void OnLayout(LayoutEventArgs e) {
            base.OnLayout(e);
            Action.Left = Math.Max(0, Width - Action.Width); Action.Top = 0;
            Box.Left = 0; Box.Top = 0; Box.Width = Math.Max(40, Action.Left - 8);
            Height = Math.Max(Box.PreferredHeight, Action.Height);
        }
    }

    internal static class Ui
    {
        public static Icon AppIcon() => Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        public static void Message(string text, MessageBoxIcon icon) {
            var owner = Form.ActiveForm ?? (Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);
            if (owner != null) { owner.BringToFront(); owner.Activate(); MessageBox.Show(owner, text, Texts.AppName, MessageBoxButtons.OK, icon); }
            else using (var window = new Form { Icon = AppIcon(), ShowInTaskbar = false, StartPosition = FormStartPosition.CenterScreen }) {
                window.CreateControl(); MessageBox.Show(window, text, Texts.AppName, MessageBoxButtons.OK, icon);
            }
        }
        public static Button Button(string name, string text) {
            var b = new Button { Name = name, Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, UseVisualStyleBackColor = true, Padding = new Padding(10, 2, 10, 2), MinimumSize = new Size(96, 28) };
            return b;
        }
        public static CheckBox Check(string name, string text) => new CheckBox { Name = name, Text = text, AutoSize = true };
        public static Label Line(string name, string text = "") => new Label { Name = name, Text = text, AutoSize = true, UseMnemonic = false };
        // Marshals to the window's thread. Safe to call after the window has gone: it then does nothing.
        public static void Post(Control owner, Action action) {
            if (owner.IsDisposed || !owner.IsHandleCreated) return;
            try { owner.BeginInvoke(action); } catch (InvalidOperationException) { }
        }
        public static Form Base(string name, string title, Size size) {
            var form = new Form { Icon = AppIcon(), Name = name, Text = title, StartPosition = FormStartPosition.CenterScreen, ClientSize = size, MinimumSize = new Size(480, 420),
                AutoScaleMode = AutoScaleMode.Dpi, Font = SystemFonts.MessageBoxFont, KeyPreview = true, MaximizeBox = false };
            form.AutoScaleDimensions = new SizeF(96F, 96F);
            return form;
        }
        public static FlowLayoutPanel Footer(string name) => new FlowLayoutPanel { Name = name, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12, 8, 12, 12) };
    }
}

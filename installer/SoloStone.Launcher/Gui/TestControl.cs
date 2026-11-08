#if SOLOSTONE_TEST_HOOKS
// Live-test hooks, window part. Compiled only into a build made with -p:TestHooks=true; a release build has none of this.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SoloStone
{
    // A file-based remote control for the open window. Every action is a real input event (mouse, keyboard, the real folder dialog), never a call into a handler.
    internal static class TestControl
    {
        static string dir; static volatile Form current; static Func<string> modelState; static uint uiThread;

        internal static void Start() {
            dir = Environment.GetEnvironmentVariable(TestElevation.ControlVariable);
            if (string.IsNullOrWhiteSpace(dir)) return;
            Directory.CreateDirectory(dir);
            new Thread(Loop) { IsBackground = true, Name = "solostone-test-control" }.Start();
        }
        internal static void Attach(Form form, Func<string> state) {
            current = form; modelState = state; uiThread = GetCurrentThreadId();
            form.FormClosed += (s, e) => { if (current == form) current = null; };
        }
        static void Loop() {
            for (;;) {
                Thread.Sleep(40);
                ControlCommand command; string error;
                try { command = ControlFiles.TryTake(dir, out error); } catch (Exception) { continue; }
                if (command == null && error == null) continue;
                string reply;
                if (command == null) reply = ControlFiles.Reply("0", false, null, error);
                else {
                    try { reply = ControlFiles.Reply(command.Id, true, Handle(command), null); }
                    catch (Exception e) { reply = ControlFiles.Reply(command.Id, false, null, e is InvalidOperationException ? e.Message : e.GetType().Name + ": " + e.Message); }
                }
                try { ControlFiles.Write(dir, reply); } catch (IOException) { }
            }
        }
        static string Handle(ControlCommand c) {
            switch (c.Verb) {
                case "state": return OnUi(f => StateJson(f));
                case "ui": return OnUi(f => UiJson(f));
                case "press": return Press(c.Name);
                case "type": return Type(c.Name, c.Text);
                case "tick": return Tick(c.Name, c.Text == "true");
                case "screenshot": return Screenshot(ControlCommand.InsideChannel(dir, c.Text));
                case "close": return CloseWindow();
                default: return AcceptDialog(c.Text);
            }
        }
        // Runs on the window's thread and waits, but never forever: a stuck window must come back as an error.
        static T OnUi<T>(Func<Form, T> work) {
            var form = current;
            if (form == null || form.IsDisposed || !form.IsHandleCreated) throw new InvalidOperationException("No window is open.");
            T result = default(T); Exception failure = null;
            var done = form.BeginInvoke((Action)(() => { try { result = work(form); } catch (Exception e) { failure = e; } }));
            if (!done.AsyncWaitHandle.WaitOne(15000)) throw new InvalidOperationException("The window did not answer within 15 seconds.");
            if (failure != null) throw failure;
            return result;
        }
        // The window's own X: Close() runs the same FormClosing checks, so a window that may not close now stays open.
        static string CloseWindow() {
            var form = current;
            if (form == null || form.IsDisposed || !form.IsHandleCreated) throw new InvalidOperationException("No window is open.");
            form.BeginInvoke((Action)(() => form.Close()));
            return "{\"closing\":true}";
        }
        static string Q(string s) => ControlFiles.Quote(s);
        static string B(bool b) => b ? "true" : "false";

        // ---- state and ui ----
        static string StateJson(Form form) {
            string value = Environment.GetEnvironmentVariable(TestDiscovery.Variable);
            return "{\"window\":" + Q(form.Name) + ",\"title\":" + Q(form.Text) + ",\"discovery\":{\"overridden\":" + B(value != null) + ",\"value\":" + Q(value ?? "") + "}"
                + ",\"control\":" + Q(dir) + ",\"updateBase\":" + Q(Environment.GetEnvironmentVariable("SOLOSTONE_TEST_UPDATE_BASE") ?? "") + ",\"model\":" + (modelState?.Invoke() ?? "null") + "}";
        }
        static string UiJson(Form form) {
            var items = new List<string>();
            items.Add(Describe(form, form));
            foreach (var c in All(form)) if (!string.IsNullOrEmpty(c.Name)) items.Add(Describe(c, form));
            return "{\"form\":{\"name\":" + Q(form.Name) + ",\"foreground\":" + B(GetForegroundWindow() == form.Handle) + ",\"minimized\":" + B(form.WindowState == FormWindowState.Minimized)
                + ",\"enabled\":" + B(IsWindowEnabled(form.Handle)) + ",\"dialogOpen\":" + B(!IsWindowEnabled(form.Handle)) + ",\"dpi\":" + form.DeviceDpi + "},\"controls\":[" + string.Join(",", items) + "]}";
        }
        static IEnumerable<Control> All(Control root) {
            foreach (Control child in root.Controls) { yield return child; foreach (var inner in All(child)) yield return inner; }
        }
        static Control Find(Form form, string name) {
            if (form.Name == name) return form;
            return All(form).FirstOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("No control is named " + name + ".");
        }
        static bool EffectivelyVisible(Control c) { for (var p = c; p != null; p = p.Parent) if (!p.Visible) return false; return true; }
        static bool EffectivelyEnabled(Control c) { for (var p = c; p != null; p = p.Parent) if (!p.Enabled) return false; return true; }
        // The part of the control the player could actually see: its own area cut down by every ancestor's visible area.
        static Rectangle VisibleRect(Control c) {
            var r = c.RectangleToScreen(c.ClientRectangle);
            for (var p = c.Parent; p != null && !r.IsEmpty; p = p.Parent) r.Intersect(p.RectangleToScreen(p.ClientRectangle));
            return r;
        }
        // Null means a real click at center would reach this control.
        static string WhyNotClickable(Control c, Form form, out Point center) {
            center = Point.Empty;
            if (c is Form) return "the window itself is not a button";
            if (!c.IsHandleCreated) return "no window handle yet";
            if (!EffectivelyVisible(c)) return "not visible";
            if (!EffectivelyEnabled(c)) return "disabled";
            if (form.WindowState == FormWindowState.Minimized) return "the window is minimized";
            if (!IsWindowEnabled(form.Handle)) return "a dialog is open over the window";
            var r = VisibleRect(c);
            if (r.Width <= 0 || r.Height <= 0) return "scrolled out of view";
            center = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
            if (!Screen.FromPoint(center).Bounds.Contains(center)) return "off screen";
            IntPtr hit = WindowFromPoint(center);
            if (hit == IntPtr.Zero) return "nothing at the centre of the control";
            var owner = Control.FromChildHandle(hit);
            if (owner == c || (owner != null && c.Contains(owner))) return null;
            return "covered by " + (owner != null ? (string.IsNullOrEmpty(owner.Name) ? owner.GetType().Name : owner.Name) : "another window (" + ClassOf(hit) + ")");
        }
        static string ClassOf(IntPtr h) { var b = new StringBuilder(128); GetClassName(h, b, b.Capacity); return b.ToString(); }
        // Text that does not fit and cannot scroll. Wrapping text boxes grow, so for them this is the check that matters.
        static bool Clipped(Control c, out bool scrolls) {
            scrolls = false;
            if (c is Form || c is Panel || c is FlowLayoutPanel || c is ProgressBar || string.IsNullOrEmpty(c.Text)) return false;
            var tb = c as TextBoxBase;
            if (tb != null && !tb.Multiline) { scrolls = true; return false; }
            var area = c.ClientSize;
            // An auto-sized check box fits its text by construction; only its parent can cut it off.
            if (c is CheckBox && c.AutoSize) return c.Parent != null && c.Right > c.Parent.ClientSize.Width;
            if (c is CheckBox) area = new Size(Math.Max(1, area.Width - 20), area.Height);
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | (tb != null ? TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding : 0);
            var size = TextRenderer.MeasureText(c.Text + (tb != null ? " " : ""), c.Font, new Size(Math.Max(1, area.Width), int.MaxValue), flags);
            var box = c as TextBox;
            if (box != null && box.ScrollBars != ScrollBars.None) { scrolls = true; return false; }
            return size.Height > area.Height + 3 || (tb == null && size.Width > area.Width + 3);
        }
        static string Describe(Control c, Form form) {
            Point center; string why = WhyNotClickable(c, form, out center);
            var r = c is Form ? c.Bounds : c.RectangleToScreen(c.ClientRectangle);
            bool scrolls; bool clipped = Clipped(c, out scrolls);
            var full = c is Form ? r : c.RectangleToScreen(c.ClientRectangle);
            var visible = c is Form ? r : VisibleRect(c);
            string text = c.Text;
            var box = c as CheckBox;
            return "{\"name\":" + Q(c.Name) + ",\"type\":" + Q(c.GetType().Name) + ",\"text\":" + Q(text) + ",\"x\":" + r.X + ",\"y\":" + r.Y + ",\"w\":" + r.Width + ",\"h\":" + r.Height
                + ",\"visible\":" + B(EffectivelyVisible(c)) + ",\"enabled\":" + B(EffectivelyEnabled(c)) + ",\"clickable\":" + B(why == null) + ",\"reason\":" + (why == null ? "null" : Q(why))
                + ",\"clipped\":" + B(clipped) + ",\"scrolls\":" + B(scrolls) + ",\"partlyHidden\":" + B(!(c is Form) && EffectivelyVisible(c) && visible != full)
                + ",\"checked\":" + (box == null ? "null" : B(box.Checked)) + "}";
        }

        // ---- real input ----
        static string Press(string name) { var spot = Reach(name); Click(spot); Thread.Sleep(300); return "{\"pressed\":" + Q(name) + ",\"x\":" + spot.X + ",\"y\":" + spot.Y + "}"; }
        // Finds the control, brings it into view if a scroll panel hides it, and refuses with the reason when a user could not click it.
        static Point Reach(string name) {
            Point spot = Point.Empty; string why = null;
            OnUi<int>(form => {
                var c = Find(form, name);
                why = WhyNotClickable(c, form, out spot);
                if (why == "scrolled out of view") {
                    for (var p = c.Parent; p != null; p = p.Parent) { var scroller = p as ScrollableControl; if (scroller != null && scroller.AutoScroll) { scroller.ScrollControlIntoView(c); break; } }
                    Application.DoEvents();
                    why = WhyNotClickable(c, form, out spot);
                }
                // A player clicks a window that sits behind another to raise it. Anything still in the way after that stays a real block.
                if (why != null && why.StartsWith("covered by another window", StringComparison.Ordinal)) {
                    Raise(form); Application.DoEvents(); Thread.Sleep(200);
                    why = WhyNotClickable(c, form, out spot);
                }
                if (why == null) { SetForegroundWindow(form.Handle); }
                return 0;
            });
            if (why != null) throw new InvalidOperationException("blocked: " + name + " is not clickable: " + why);
            return spot;
        }
        static string Type(string name, string text) {
            string kind = OnUi(form => { var c = Find(form, name); return c is TextBoxBase && !((TextBoxBase)c).ReadOnly ? "ok" : "not an editable text box"; });
            if (kind != "ok") throw new InvalidOperationException("blocked: " + name + " is " + kind);
            Click(Reach(name)); Thread.Sleep(150);
            Keys(new[] { Key(0x11, false), Key(0x41, false), Key(0x41, true), Key(0x11, true) });
            Thread.Sleep(60);
            var inputs = new List<INPUT>();
            foreach (char ch in text) { inputs.Add(Unicode(ch, false)); inputs.Add(Unicode(ch, true)); }
            Keys(inputs.ToArray());
            Thread.Sleep(250);
            string now = OnUi(form => Find(form, name).Text);
            return "{\"typed\":" + Q(name) + ",\"text\":" + Q(now) + "}";
        }
        static string Tick(string name, bool wanted) {
            bool already = OnUi(form => { var box = Find(form, name) as CheckBox; if (box == null) throw new InvalidOperationException("blocked: " + name + " is not a check box"); return box.Checked; });
            if (already != wanted) { Click(Reach(name)); Thread.Sleep(300); }
            bool now = OnUi(form => ((CheckBox)Find(form, name)).Checked);
            if (now != wanted) throw new InvalidOperationException("blocked: the click on " + name + " did not change it");
            return "{\"ticked\":" + Q(name) + ",\"checked\":" + B(now) + "}";
        }
        static string Screenshot(string path) {
            Rectangle area = OnUi(form => {
                if (form.WindowState == FormWindowState.Minimized) throw new InvalidOperationException("The window is minimized.");
                Raise(form); SetForegroundWindow(form.Handle); return form.Bounds;
            });
            Thread.Sleep(150);
            using (var bitmap = new Bitmap(area.Width, area.Height))
            using (var graphics = Graphics.FromImage(bitmap)) {
                graphics.CopyFromScreen(area.Location, Point.Empty, area.Size);
                bitmap.Save(path, ImageFormat.Png);
            }
            return "{\"path\":" + Q(path) + ",\"w\":" + area.Width + ",\"h\":" + area.Height + "}";
        }
        // Drives the real folder dialog: choose the folder in its tree, check the tree really shows it, then press its OK.
        static string AcceptDialog(string path) {
            string full = Path.GetFullPath(path).TrimEnd('\\');
            IntPtr dialog = FindDialog();
            if (dialog == IntPtr.Zero) throw new InvalidOperationException("No folder dialog is open on the window's thread.");
            IntPtr text = Marshal.StringToHGlobalUni(full);
            try { SendMessage(dialog, 0x467 /* BFFM_SETSELECTIONW */, (IntPtr)1, text); }
            finally { Marshal.FreeHGlobal(text); }
            Thread.Sleep(400);
            string leaf = Path.GetFileName(full);
            string shown = SelectedTreeText(dialog);
            if (leaf.Length != 0 && !string.Equals(shown, leaf, StringComparison.OrdinalIgnoreCase)) {
                PostMessage(dialog, 0x111 /* WM_COMMAND */, (IntPtr)2 /* IDCANCEL */, IntPtr.Zero);
                throw new InvalidOperationException("The dialog did not select " + full + " (it shows \"" + shown + "\"). The dialog was cancelled.");
            }
            PostMessage(dialog, 0x111, (IntPtr)1 /* IDOK */, IntPtr.Zero);
            for (int i = 0; i < 100 && IsWindow(dialog); i++) Thread.Sleep(50);
            if (IsWindow(dialog)) throw new InvalidOperationException("The dialog stayed open after OK.");
            return "{\"accepted\":" + Q(full) + ",\"selectedText\":" + Q(shown) + "}";
        }
        static IntPtr FindDialog() {
            var found = new List<IntPtr>();
            EnumWindows((h, l) => {
                uint pid; uint thread = GetWindowThreadProcessId(h, out pid);
                if (thread == uiThread && IsWindowVisible(h) && ClassOf(h) == "#32770" && FindTree(h) != IntPtr.Zero) found.Add(h);
                return true;
            }, IntPtr.Zero);
            if (found.Count > 1) throw new InvalidOperationException("More than one folder dialog is open.");
            return found.Count == 1 ? found[0] : IntPtr.Zero;
        }
        static IntPtr FindTree(IntPtr dialog) {
            IntPtr tree = IntPtr.Zero;
            EnumChildWindows(dialog, (h, l) => { if (ClassOf(h) == "SysTreeView32") { tree = h; return false; } return true; }, IntPtr.Zero);
            return tree;
        }
        static string SelectedTreeText(IntPtr dialog) {
            IntPtr tree = FindTree(dialog);
            if (tree == IntPtr.Zero) return "";
            IntPtr item = SendMessage(tree, 0x110A /* TVM_GETNEXTITEM */, (IntPtr)9 /* TVGN_CARET */, IntPtr.Zero);
            if (item == IntPtr.Zero) return "";
            IntPtr buffer = Marshal.AllocHGlobal(520);
            try {
                var tv = new TVITEM { mask = 0x11 /* TVIF_TEXT | TVIF_HANDLE */, hItem = item, pszText = buffer, cchTextMax = 260 };
                SendMessageItem(tree, 0x113E /* TVM_GETITEMW */, IntPtr.Zero, ref tv);
                return Marshal.PtrToStringUni(buffer) ?? "";
            } finally { Marshal.FreeHGlobal(buffer); }
        }

        // ---- SendInput ----
        static void Click(Point p) {
            SetCursorPos(p.X, p.Y); Thread.Sleep(60);
            Keys(new[] { Mouse(0x0002), });
            Thread.Sleep(50);
            Keys(new[] { Mouse(0x0004) });
        }
        static INPUT Mouse(uint flags) => new INPUT { type = 0, u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flags } } };
        static INPUT Key(ushort vk, bool up) => new INPUT { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? 2u : 0u } } };
        static INPUT Unicode(char ch, bool up) => new INPUT { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wScan = ch, dwFlags = 4u | (up ? 2u : 0u) } } };
        static void Keys(INPUT[] inputs) {
            if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))) != inputs.Length) throw new InvalidOperationException("Windows refused the input (error " + Marshal.GetLastWin32Error() + ").");
        }

        delegate bool EnumProc(IntPtr h, IntPtr l);
        [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)] struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION u; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct TVITEM { public uint mask; public IntPtr hItem; public uint state, stateMask; public IntPtr pszText; public int cchTextMax, iImage, iSelectedImage, cChildren; public IntPtr lParam; }
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        static void Raise(Form form) {
            const uint NoSizeMoveActivate = 0x0001 | 0x0002 | 0x0010;
            SetWindowPos(form.Handle, new IntPtr(-1), 0, 0, 0, 0, NoSizeMoveActivate);
            SetWindowPos(form.Handle, new IntPtr(-2), 0, 0, 0, 0, NoSizeMoveActivate);
        }
        [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc p, IntPtr l);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder b, int max);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern IntPtr SendMessageItem(IntPtr h, int msg, IntPtr w, ref TVITEM l);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    }
}
#endif

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SoloStone
{
    // Only one SoloStone window per root, so two launchers never fight over the same copy.
    internal sealed class SingleInstance : IDisposable
    {
        readonly Semaphore gate; bool held;
        SingleInstance(Semaphore gate) { this.gate = gate; held = true; }
        static string Name(string root,string suffix) {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return "Local\\SoloStone-" + BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(root.ToLowerInvariant()))).Replace("-", "").Substring(0, 32) + suffix;
        }
        // Held for the whole of a helper's run. It is taken before the helper waits for the old launcher, so no re-click falls in a gap.
        internal static Semaphore MarkHelper(string root) { var marker = new Semaphore(1, 1, Name(root, "-helper")); marker.WaitOne(0); return marker; }
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int processId);
        // Wakes the helper's window, which alone can say what is happening. It may take the foreground, because a player just clicked.
        internal static void RaiseHelper(string root) {
            EventWaitHandle raise;
            if (!EventWaitHandle.TryOpenExisting(Name(root, "-raise"), out raise)) return;
            using (raise) { AllowSetForegroundWindow(-1); raise.Set(); }
        }
        internal static EventWaitHandle RaiseSignal(string root) => new EventWaitHandle(false, EventResetMode.AutoReset, Name(root, "-raise"));
        internal static bool HelperRunning(string root) { Semaphore open; if (!Semaphore.TryOpenExisting(Name(root, "-helper"), out open)) return false; open.Dispose(); return true; }
        // A fresh release started by the old one waits a moment for it to leave.
        internal static SingleInstance Acquire(string root, TimeSpan wait) {
            string name;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                name = "Local\\SoloStone-" + BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(root.ToLowerInvariant()))).Replace("-", "").Substring(0, 24);
            var semaphore = new Semaphore(1, 1, name);
            if (!semaphore.WaitOne(wait)) { semaphore.Dispose(); return null; }
            return new SingleInstance(semaphore);
        }
        public void Dispose() {
            lock (gate) { if (!held) return; held = false; try { gate.Release(); } catch (SemaphoreFullException) { } }
        }
    }

    // Opens the wizard or the launcher window, and moves from one to the other.
    internal static class GuiHost
    {
        static SingleInstance instance;
        internal static void ReleaseInstance() { instance?.Dispose(); }
        // A launcher started while a helper runs must leave at once: its own image is the file being replaced. It only raises the helper's window, which says what is happening.
        internal static int RunHelper(Options options) {
            using (SingleInstance.MarkHelper(options.Root)) {
                instance = SingleInstance.Acquire(options.Root, TimeSpan.FromSeconds(150));
                try {
                    using (var raise = SingleInstance.RaiseSignal(options.Root))
                    using (var window = new ProgressWindow(options, true)) {
                        var listener = new Thread(() => { try { for (;;) { raise.WaitOne(); window.Raise(); } } catch (ObjectDisposedException) { } }) { IsBackground = true };
                        listener.Start();
                        Application.Run(window); return window.Result;
                    }
                } finally { instance?.Dispose(); }
            }
        }
        internal static int Run(Options options) {
            if (SingleInstance.HelperRunning(options.Root)) { SingleInstance.RaiseHelper(options.Root); return 0; }
            instance = SingleInstance.Acquire(options.Root, TimeSpan.FromSeconds(15));
            if (instance == null) { Ui.Message("SoloStone is already open for this installation.", MessageBoxIcon.Information); return 1; }
            try {
#if SOLOSTONE_TEST_HOOKS
                TestControl.Start();
#endif
                bool wizard = options.Command == "wizard" || !File.Exists(Path.Combine(options.Root, "install.json"));
                for (;;) {
                    if (wizard) {
                        var ops = new WindowsWizardOps(options.Root);
                        var state = ops.State();
                        // A finished copy belongs to the launcher; only an unfinished one reopens the wizard in recovery.
                        if (state == InstallState.Committed) { wizard = false; continue; }
                        using (var form = new WizardForm(ops, state == InstallState.Pending ? InstallState.Pending : InstallState.None)) {
                            Application.Run(form);
                            if (form.OpenLauncher && File.Exists(Path.Combine(options.Root, "install.json"))) { wizard = false; continue; }
                            return form.ExitCode;
                        }
                    }
                    using (var form = new LauncherForm(new WindowsLauncherOps(options.Root, options.AfterHelper), options.AcceptWarning)) {
                        Application.Run(form);
                        if (form.UninstallerPath != null) Program.StartUninstaller(form.UninstallerPath, form.DeleteSavesChosen);
                        return form.ExitCode;
                    }
                }
            } finally { instance?.Dispose(); }
        }
    }
}

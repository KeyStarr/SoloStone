using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SoloStone
{
    // The real calls behind the wizard. Reads never write; every write happens inside the step 5 runner, under the lifecycle lease.
    internal sealed class WindowsWizardOps : IWizardOps
    {
        readonly string root, bundle;
        string cachedSource; long cachedRequired; int cachedLongest;
        internal WindowsWizardOps(string root) { this.root = root; bundle = AppDomain.CurrentDomain.BaseDirectory; }
        public string[] FindClients() => WindowsHost.FindClients(true);
        public string DefaultTarget => Installer.DefaultClient(root);
        public bool UpdatesConfigured => UpdateConfig.Compiled().Configured;
        public Verdict CheckClient(string folder)
            => global::SoloStone.CheckClient.Run(root, folder, TestedClient.Compiled(), UpdateConfig.Compiled(), new NoBindingManifest(), WindowsHost.IdleFailures(root));
        public TargetResult CheckTarget(string target, string source) {
            string ownerId = File.Exists(Path.Combine(root, "install.json")) ? InstallRecord.Load(root).InstallId : null;
            target = TargetCheck.SuggestedFolder(target, ownerId);
            // The source tree is walked once per folder, not once per keystroke in the location box.
            var facts = TargetCheck.Gather(target, root, null, WindowsHost.RegisteredFolders(), WindowsHost.SaveRoot);
            facts.Source = source;
            if (!string.IsNullOrEmpty(source) && Directory.Exists(source)) {
                string canonical = Safety.Canonical(source);
                if (!Safety.Same(canonical, cachedSource ?? "")) {
                    var files = Installer.SourceFiles(canonical);
                    cachedRequired = files.Sum(p => new FileInfo(p).Length) + TargetCheck.Headroom;
                    cachedLongest = files.Select(p => p.Length - canonical.Length - 1).DefaultIfEmpty(0).Max();
                    cachedSource = canonical;
                }
                facts.RequiredBytes = cachedRequired; facts.LongestRelative = cachedLongest;
            }
            if (facts.FreeBytes == 0) {
                try { facts.FreeBytes = new DriveInfo(Path.GetPathRoot(Safety.Canonical(target))).AvailableFreeSpace; }
                catch (Exception e) when (e is InvalidOperationException || e is ArgumentException || e is IOException) { }
            }
            return new TargetResult { Target = target, Problem = TargetCheck.Problem(facts), Required = facts.RequiredBytes, Free = facts.FreeBytes };
        }
        public InstallState State() {
            if (File.Exists(Path.Combine(root, "install.json"))) return InstallState.Committed;
            if (File.Exists(Path.Combine(root, "install.pending.json"))) return InstallState.Pending;
            return InstallState.None;
        }
        public IDisposable Exclusive() => Program.OpenLease(root);
        Options Own() => new Options { Command = "setup", Root = root };
        public InstallRecord Setup(string source, string target, bool acceptedWarning, Action<SetupProgress> progress, CancellationToken cancel) {
            WindowsHost.RequireIdle(root);
            var own = Own();
            return Installer.Setup(new SetupOptions { Source = source, Root = root, ClientPath = target, Bundle = bundle, SaveRoot = WindowsHost.SaveRoot,
                Registered = WindowsHost.RegisteredFolders(), Check = facts => Program.CheckFolder(facts, own, WindowsHost.IdleFailures(root)),
                AcceptedWarning = acceptedWarning, Progress = progress, Cancel = cancel });
        }
        public InstallRecord Recover(Action<SetupProgress> progress, CancellationToken cancel) {
            WindowsHost.RequireIdle(root);
            return Installer.Recover(root, bundle, WindowsHost.RegisteredFolders(), WindowsHost.SaveRoot, progress, cancel);
        }
        public InstallRecord Load() => InstallRecord.Load(root);
        public void ApplyFirewall(InstallRecord record) => WindowsHost.Firewall(record, true);
        public void SaveSettings(bool checkForUpdates) => Settings.Save(root, new Settings { CheckForUpdates = checkForUpdates });
        public void RemovePartial() => Installer.RemovePartial(root, WindowsHost.RegisteredFolders());
        public string LogPath => Texts.LogFile(root);
        public void Log(string line) => InstallerLog.Write(root, line);
    }

    // The real calls behind the launcher window. Update requests are made here and nowhere else in the window code.
    internal sealed class WindowsLauncherOps : ILauncherOps
    {
        readonly string root, bundle;
        readonly bool afterHelper;
        internal WindowsLauncherOps(string root, bool afterHelper = false) { this.root = root; this.afterHelper = afterHelper; bundle = AppDomain.CurrentDomain.BaseDirectory; }
        public bool AfterHelper => afterHelper;
        UpdateEngine Engine(Action<string> progress = null) => new UpdateEngine(root, new WindowsUpdateHost(root), progress);
        UpdateService NewService() => new UpdateService(root, UpdateConfig.Compiled(), new WebHttp(), Engine(), afterHelper);
        public IDisposable Lease() => Program.OpenLease(root);
        public ResumeOutcome Resume() => Engine().Resume();
        public StatusReport Status()
            => StatusReport.Build(root, Engine(), TestedClient.Compiled(), UpdateConfig.Compiled(), new NoBindingManifest(), WindowsHost.IdleFailures(root));
        public UpdateStatus Check(CancellationToken cancel) => NewService().Check(cancel);
        public UpdateResult Apply(bool acceptWarning, CancellationToken cancel) => NewService().Apply(acceptWarning, cancel);
        // The old process must load nothing more once the new release is in place, so it starts the new one and leaves at once.
        public void Restart() {
            GuiHost.ReleaseInstance();
            // A helper is waiting for this process to exit, and it opens the launcher when it finishes.
            if (WindowsUpdateHost.HandedOff) Environment.Exit(0);
            string exe = Path.Combine(root, "app", "SoloStone.exe");
            Process.Start(new ProcessStartInfo(exe, "launch --root " + FirewallPlan.Quote(root)) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) });
            Environment.Exit(0);
        }
        public int Launch(bool acceptWarning, Action<string> progress) {
            var record = InstallRecord.Load(root);
            Safety.Destinations(record, WindowsHost.RegisteredFolders());
            WindowsHost.RequireIdle(root);
            return WindowsHost.Launch(record, bundle, Engine(progress), acceptWarning, progress);
        }
        public RollbackResult Rollback() => Engine().Rollback();
        public RestoreResult RestoreBackup(string id) => Engine().RestoreBackup(id);
        public void SaveUpdatesSetting(bool on) => Settings.Save(root, new Settings { CheckForUpdates = on });
        public void RepairFirewall() {
            var record = InstallRecord.Load(root);
            Safety.Destinations(record, WindowsHost.RegisteredFolders());
            WindowsHost.Firewall(record, true);
        }
        // Goes through the same command as the command line, so every uninstall check still runs. Returns the Inno uninstaller to start after this window closes.
        public string Uninstall(bool deleteSaves, Action<string> progress) {
            var own = new Options { Command = "uninstall", Root = root, DeleteSaves = deleteSaves };
            if (Program.Execute(own, progress) != 0) throw new InvalidOperationException("SoloStone could not be uninstalled. Nothing was removed.");
            return own.Uninstaller;
        }
    }
}

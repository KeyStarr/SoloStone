using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using SoloStone;

// Fakes for the two window models. Every call is recorded so tests can see what a click would have touched.
sealed class FakeWizardOps : IWizardOps
{
    public readonly List<string> Calls = new List<string>();
    public readonly List<string> Logged = new List<string>();
    public InstallState Current = InstallState.None;
    public Exception SetupFails, RecoverFails, FirewallFails; public int FailSetupTimes;
    public Verdict Verdict = Program.AllowVerdict();
    public TargetResult Target = new TargetResult { Required = 12L << 30, Free = 100L << 30 };
    public string[] Clients = new string[0];
    public int Leases, OpenLeases;
    public string[] FindClients() { Calls.Add("FindClients"); return Clients; }
    public string DefaultTarget => @"C:\Users\Public\AppData\Local\SoloStone\client";
    public bool UpdatesConfigured { get; set; } = true;
    public Verdict CheckClient(string folder) { Calls.Add("CheckClient"); return Verdict; }
    public TargetResult CheckTarget(string target, string source) { Calls.Add("CheckTarget"); return Target; }
    public InstallState State() { Calls.Add("State"); return Current; }
    sealed class Lease : IDisposable { readonly FakeWizardOps o; public Lease(FakeWizardOps o) { this.o = o; o.Leases++; o.OpenLeases++; } public void Dispose() { o.OpenLeases--; } }
    public IDisposable Exclusive() { Calls.Add("Exclusive"); return new Lease(this); }
    public InstallRecord Setup(string source, string target, bool acceptedWarning, Action<SetupProgress> progress, CancellationToken cancel) {
        Calls.Add("Setup");
        progress(new SetupProgress { Step = SetupStep.CheckClient, Message = "Checking your client…" });
        cancel.ThrowIfCancellationRequested();
        if (FailSetupTimes > 0) { FailSetupTimes--; throw SetupFails; }
        progress(new SetupProgress { Step = SetupStep.CopyClient, Message = "Copying", Done = 5, Total = 10 });
        Current = InstallState.Committed;
        return new InstallRecord { InstallId = new string('a', 32) };
    }
    public InstallRecord Recover(Action<SetupProgress> progress, CancellationToken cancel) {
        Calls.Add("Recover");
        cancel.ThrowIfCancellationRequested();
        if (RecoverFails != null) throw RecoverFails;
        Current = InstallState.Committed; return new InstallRecord { InstallId = new string('a', 32) };
    }
    public InstallRecord Load() { Calls.Add("Load"); return new InstallRecord { InstallId = new string('a', 32) }; }
    public void ApplyFirewall(InstallRecord record) { Calls.Add("ApplyFirewall"); if (FirewallFails != null) { var e = FirewallFails; FirewallFails = null; throw e; } }
    public void SaveSettings(bool on) { Calls.Add("SaveSettings:" + on); }
    public void RemovePartial() { Calls.Add("RemovePartial"); Current = InstallState.None; }
    public void Log(string line) { Calls.Add("Log"); Logged.Add(line); }
    public string LogPath => @"C:\x\installer.log";
    public IEnumerable<string> Writes => Calls.Where(c => c == "Setup" || c == "Recover" || c == "ApplyFirewall" || c.StartsWith("SaveSettings") || c == "RemovePartial" || c == "Log" || c == "Exclusive");
}
sealed class FakeLauncherOps : ILauncherOps
{
    public readonly List<string> Calls = new List<string>();
    public StatusReport Report = Program.Report();
    public ResumeOutcome Resumed = ResumeOutcome.Nothing;
    public UpdateStatus Checked = new UpdateStatus { State = UpdateState.UpToDate, Message = "SoloStone is up to date." };
    public UpdateResult Applied = new UpdateResult { Outcome = UpdateOutcome.Nothing, Message = "" };
    public RollbackResult RolledBack = new RollbackResult { Outcome = RollbackOutcome.NoPrevious, Message = "There is no previous version to roll back to." };
    public int LaunchCode; public Exception LaunchFails, CheckFails, FirewallFails; public bool? AcceptedWarning; public bool? AppliedAccepting;
    public int OpenLeases;
    sealed class Lease : IDisposable { readonly FakeLauncherOps o; public Lease(FakeLauncherOps o) { this.o = o; o.OpenLeases++; o.Calls.Add("Lease"); } public void Dispose() { o.OpenLeases--; o.Calls.Add("Unlease"); } }
    IDisposable ILauncherOps.Lease() => new Lease(this);
    public Exception ResumeFails;
    public bool AfterHelper { get; set; }
    public ResumeOutcome Resume() { Calls.Add("Resume"); if (ResumeFails != null) throw ResumeFails; return Resumed; }
    public StatusReport Status() { Calls.Add("Status"); return Report; }
    public UpdateStatus Check(CancellationToken cancel) { Calls.Add("Check"); if (CheckFails != null) throw CheckFails; cancel.ThrowIfCancellationRequested(); return Checked; }
    public UpdateResult Apply(bool acceptWarning, CancellationToken cancel) { Calls.Add("Apply"); AppliedAccepting = acceptWarning; cancel.ThrowIfCancellationRequested(); return Applied; }
    public void Restart() { Calls.Add("Restart:leases=" + OpenLeases); }
    public int Launch(bool acceptWarning, Action<string> progress) {
        Calls.Add("Launch"); AcceptedWarning = acceptWarning;
        if (LaunchFails != null) throw LaunchFails;
        progress("Starting SoloStone…"); progress("SoloStone is running. Quit from the game to close this launcher.");
        return LaunchCode;
    }
    public RollbackResult Rollback() { Calls.Add("Rollback"); return RolledBack; }
    public RestoreResult RestoreBackup(string id) { Calls.Add("RestoreBackup:" + id); return new RestoreResult { BackupId = id, Message = "Your saves are back to backup " + id + ". Progress since the update is lost." }; }
    public void SaveUpdatesSetting(bool on) { Calls.Add("SaveUpdatesSetting:" + on); }
    public void RepairFirewall() { Calls.Add("RepairFirewall"); if (FirewallFails != null) { var e = FirewallFails; FirewallFails = null; throw e; } }
}

static partial class Program
{
    internal static Verdict AllowVerdict() => new Verdict { KindName = "Allow", TestedVersion = "37.0.0", FoundVersion = "37.0.0", Reasons = new string[0], Message = ClientPolicy.Sentence("37.0.0", "37.0.0") };
    internal static Verdict WarningVerdict() => new Verdict { KindName = "Warning", TestedVersion = "37.1.0", FoundVersion = "37.0.0", Reasons = new[] { "SoloStone has not been tested with this exact client." },
        Message = ClientPolicy.Sentence("37.1.0", "37.0.0") + " SoloStone has not been tested with this Hearthstone version." };
    internal static Verdict BlockVerdict() => ClientPolicy.BlockedWith("This SoloStone release runs on Hearthstone 38 only, and your client is Hearthstone 37.", "38.0.0", "37.0.0");
    internal static StatusReport Report(Verdict verdict = null, string previous = null, bool configured = true, bool updates = true) => new StatusReport {
        Installed = true, ClientVersion = "37.0.0", TestedVersion = "37.0.0", ReleaseVersion = "0.1.0", PreviousVersion = previous, Verdict = verdict ?? AllowVerdict(),
        UpdatesConfigured = configured, CheckForUpdates = updates, Backups = new string[0] };
    static readonly Action<Action> Inline = a => a();
    static WizardModel Wizard(IWizardOps ops) => new WizardModel(ops.DefaultTarget, ops.UpdatesConfigured);
    // What the window does on each page, minus the controls: only reads happen before step 5.
    static void ChooseSource(WizardModel m, IWizardOps ops, string path, Verdict verdict = null) {
        int generation = m.BeginSourceCheck(path);
        var fromOps = ops.CheckClient(path);
        m.FinishSourceCheck(generation, verdict ?? fromOps);
    }
    static void ChooseTarget(WizardModel m, IWizardOps ops, string path) {
        int generation = m.BeginTargetCheck(path);
        m.FinishTargetCheck(generation, ops.CheckTarget(path, m.Source));
    }
    static WizardModel AtReview(IWizardOps ops, Verdict verdict = null) {
        var m = Wizard(ops);
        Check(m.Next(), "page 1 to 2");
        ChooseSource(m, ops, @"C:\Hearthstone", verdict);
        if (verdict != null && verdict.Kind == VerdictKind.Warning) m.AcceptWarning(true);
        Check(m.Next(), "page 2 to 3");
        ChooseTarget(m, ops, ops.DefaultTarget);
        Check(m.Next() && m.Page == WizardPage.Review, "page 3 to 4");
        return m;
    }
    static void GuiTests() {
        FolderDiscovery();
        SuggestedDestination();
        WizardPages();
        WizardRuns();
        LauncherStart();
        LauncherButtons();
        GuiWording();
    }
    static void FolderDiscovery() {
        string root = Path.Combine(OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath(), "solostone-discovery-" + Guid.NewGuid().ToString("N"));
        try {
            string valid = Path.Combine(root, "game"), empty = Path.Combine(root, "empty"), missing = Path.Combine(root, "missing");
            Directory.CreateDirectory(valid); Directory.CreateDirectory(empty);
            File.WriteAllText(Path.Combine(valid, "Hearthstone.exe"), "test");
            var entries = new[] { ("other", "Hearthstone", valid), ("Hearthstone", "other", empty), ("Hearthstone", "other", missing), ("unrelated", "Other", valid) };
            string[] found = Discovery.UninstallEntries(entries);
            Check(found.Length == 1 && found[0] == valid && Discovery.Prefill("", found) == valid, "uninstall entry pre-fills only an existing game folder");
            Check(Discovery.UninstallEntries(entries.Skip(1)).Length == 0, "missing folder and executable are ignored");
            Check(Discovery.Prefill("typed", found) == "typed", "discovery never replaces a typed path");
            Check(Texts.FolderHint == "Not sure where it is? In Battle.net, open Hearthstone, click the gear next to Play, then Show in Explorer." && Source("Gui/WizardForm.cs").Contains("hint.SetText(Texts.FolderHint)"), "folder hint is shown");
        } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    static void SuggestedDestination() {
        string root = Path.Combine(OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath(), "solostone-target-" + Guid.NewGuid().ToString("N"));
        try {
            string games = Path.Combine(root, "Games"), empty = Path.Combine(root, "empty"), own = Path.Combine(root, "owned"), named = Path.Combine(root, "SoloStone");
            foreach (string folder in new[] { games, empty, own, named }) Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(games, "other"), "x"); File.WriteAllText(Path.Combine(own, InstallRecord.Marker), "owner"); File.WriteAllText(Path.Combine(named, "other"), "x");
            Check(TargetCheck.SuggestedFolder(games) == Path.Combine(games, "SoloStone"), "nonempty parent gets SoloStone child");
            Check(TargetCheck.SuggestedFolder(empty) == empty && TargetCheck.SuggestedFolder(Path.Combine(root, "missing")) == Path.Combine(root, "missing"), "empty or missing destination stays");
            Check(TargetCheck.SuggestedFolder(own, "owner") == own && TargetCheck.SuggestedFolder(named) == named, "owned copy and SoloStone folder stay");
            string child = Path.Combine(games, "SoloStone"); Directory.CreateDirectory(child); File.WriteAllText(Path.Combine(child, "other"), "x");
            Check(TargetCheck.SuggestedFolder(games) == child && TargetCheck.Problem(new TargetFacts { Target = child, Root = Path.Combine(root, "app"), Resolve = p => p, TargetHasContent = true, FreeBytes = long.MaxValue }).Contains("already contains files"), "occupied child remains refused");
        } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    static void WizardPages() {
        var ops = new FakeWizardOps(); var m = Wizard(ops);
        Check(m.Page == WizardPage.HowItWorks && m.CanNext && !m.CanBack && m.CanCancel, "wizard opens on step 1 and may go forward");
        Check(m.Next() && m.Page == WizardPage.Folder && !m.CanNext && m.CanBack, "step 2 waits for a folder check");
        // Allow, stale results, and a changed folder.
        int first = m.BeginSourceCheck(@"C:\One");
        Check(m.Checking && !m.CanNext, "Next stays off while checking");
        int second = m.BeginSourceCheck(@"C:\Two");
        Check(!m.FinishSourceCheck(first, AllowVerdict()) && m.Checking && m.Source == @"C:\Two", "a result for an old folder is ignored");
        Check(m.FinishSourceCheck(second, AllowVerdict()) && m.CanNext && !m.Checking, "Allow enables Next");
        m.AcceptWarning(true); Check(!m.WarningAccepted, "ticking the warning box means nothing on an Allow");
        // Block.
        second = m.BeginSourceCheck(@"C:\Block");
        m.FinishSourceCheck(second, BlockVerdict());
        Check(!m.CanNext, "Next is disabled on Block"); m.AcceptWarning(true);
        Check(!m.WarningAccepted && !m.CanNext && !m.Next() && m.Page == WizardPage.Folder, "a Block cannot be accepted or skipped");
        second = m.BeginSourceCheck(@"C:\Nothing"); m.FailSourceCheck(second, "That folder could not be read.");
        Check(!m.CanNext && m.Verdict.Kind == VerdictKind.Block, "an unreadable folder blocks");
        // Warning needs its own click, and a new folder loses it.
        second = m.BeginSourceCheck(@"C:\Warn"); m.FinishSourceCheck(second, WarningVerdict());
        Check(!m.CanNext && !m.WarningAccepted, "Warning does not enable Next by itself");
        m.AcceptWarning(true); Check(m.WarningAccepted && m.CanNext, "an explicit click enables Next on Warning");
        m.AcceptWarning(false); Check(!m.CanNext, "unticking disables it again"); m.AcceptWarning(true);
        second = m.BeginSourceCheck(@"C:\Warn2"); m.FinishSourceCheck(second, WarningVerdict());
        Check(!m.WarningAccepted && !m.CanNext, "a different folder needs a fresh click");
        m.AcceptWarning(true); Check(m.Next() && m.Page == WizardPage.CopyTo, "step 3 reached");
        Check(!m.CanNext && m.CanBack, "step 3 waits for the location check");
        // A bad target.
        int t = m.BeginTargetCheck(@"C:\Hearthstone\copy");
        Check(!m.CanNext && m.TargetChecking, "checking disables Next");
        m.FinishTargetCheck(t, new TargetResult { Problem = "The copy must be separate from your Hearthstone folder." });
        Check(!m.CanNext && !m.Next() && m.TargetProblem.Contains("separate"), "a bad target disables Next");
        int oldT = m.BeginTargetCheck(@"C:\Good"); int newT = m.BeginTargetCheck(@"C:\Better");
        Check(!m.FinishTargetCheck(oldT, new TargetResult()) && !m.CanNext, "an old location result is ignored");
        m.FinishTargetCheck(newT, new TargetResult { Required = 13L << 30, Free = 50L << 30 });
        Check(m.CanNext && m.Required == 13L << 30, "a good target enables Next");
        // Going back and changing the folder invalidates the location result.
        Check(m.Back() && m.Page == WizardPage.Folder, "Back works"); int g = m.BeginSourceCheck(@"C:\Other"); m.FinishSourceCheck(g, AllowVerdict());
        Check(m.Next() && !m.TargetKnown && !m.CanNext, "a new source forces a new location check");
        ChooseTarget(m, ops, @"C:\Good");
        Check(m.Next() && m.Page == WizardPage.Review && m.NextText == Texts.Install, "step 4 offers Install");
        m.SetUpdates(false); Check(!m.CheckForUpdates, "update checks can be switched off");
        var noUpdates = new WizardModel("x", false); Check(!noUpdates.CheckForUpdates && !noUpdates.UpdatesConfigured, "without update keys the box starts off");
        noUpdates.SetUpdates(true); Check(!noUpdates.CheckForUpdates, "and cannot be turned on");
        Check(m.Back() && m.Page == WizardPage.CopyTo && m.Back() && m.Page == WizardPage.Folder && m.Back() && m.Page == WizardPage.HowItWorks && !m.Back(), "Back walks to step 1 and stops");
        // Cancel writes nothing, from every page before 5.
        foreach (int stop in new[] { 1, 2, 3, 4 }) {
            var fresh = new FakeWizardOps(); var w = Wizard(fresh);
            if (stop >= 2) { w.Next(); ChooseSource(w, fresh, @"C:\Hearthstone"); }
            if (stop >= 3) { w.Next(); ChooseTarget(w, fresh, fresh.DefaultTarget); }
            if (stop >= 4) w.Next();
            Check((int)w.Page == stop, "wizard at step " + stop);
            Check(w.CancelWizard() && w.Closed && w.ExitCode == 0 && !w.WrittenAnything, "Cancel at step " + stop + " just closes");
            Check(!fresh.Writes.Any() && fresh.Calls.All(c => c == "CheckClient" || c == "CheckTarget"), "Cancel at step " + stop + " wrote nothing: " + string.Join(",", fresh.Calls));
        }
        var atReview = AtReview(new FakeWizardOps());
        Check(atReview.Next() && atReview.Page == WizardPage.Run && atReview.Phase == RunPhase.Idle && !atReview.WrittenAnything && !atReview.CanNext && !atReview.CanBack, "Install moves to step 5 without writing yet");
        Check(!atReview.CancelWizard(), "the plain Cancel does not apply on step 5");
        var s = atReview.Snapshot().ToJson(); Check(s.Contains("\"page\":5") && s.Contains("\"window\":\"wizard\""), "wizard state reads as JSON");
    }
    static void WizardRuns() {
        // Success.
        var ops = new FakeWizardOps(); var m = AtReview(ops); m.Next();
        var runner = new WizardRunner(m, ops, Inline);
        runner.Run(m.Args(), CancellationToken.None);
        Check(m.Phase == RunPhase.Done && m.Rows.All(r => r.State == RowState.Done) && m.CanOpenSoloStone && m.ExitCode == 0, "a clean run finishes");
        Check(ops.Calls.IndexOf("Setup") < ops.Calls.IndexOf("SaveSettings:True") && ops.Calls.IndexOf("SaveSettings:True") < ops.Calls.IndexOf("ApplyFirewall"), "copy, then settings, then the Windows prompt");
        Check(ops.OpenLeases == 0 && ops.Leases == 1, "one lease for the whole run, released after");
        Check(ops.Logged.Any(l => l.Contains("Setup started")) && ops.Logged.Any(l => l.Contains("Setup finished")), "the log records the run");
        // Failure before any copy: Retry runs setup again, no partial copy to remove.
        ops = new FakeWizardOps { SetupFails = new InvalidOperationException("Quit Battle.net first."), FailSetupTimes = 1 };
        m = AtReview(ops); m.Next(); runner = new WizardRunner(m, ops, Inline);
        runner.Run(m.Args(), CancellationToken.None);
        Check(m.Phase == RunPhase.Failed && m.Message == "Quit Battle.net first." && m.CanRetry && m.CanOpenLog && !m.CanRemovePartial && m.ExitCode == 1, "a failure before the copy offers Retry and the log");
        Check(m.Rows.Any(r => r.State == RowState.Failed), "the failing step is marked");
        runner.Run(m.Args(), CancellationToken.None);
        Check(m.Phase == RunPhase.Done && ops.Calls.Count(c => c == "Setup") == 2, "Retry starts setup again");
        // Failure after the pending record: Recover, and Remove partial copy is offered.
        ops = new FakeWizardOps { SetupFails = new InvalidOperationException("Setup stopped; files and saves were preserved."), FailSetupTimes = 1 };
        var failing = new FailingSetupOps(ops);
        m = AtReview(failing); m.Next();
        runner = new WizardRunner(m, failing, Inline);
        runner.Run(m.Args(), CancellationToken.None);
        Check(m.Phase == RunPhase.Failed && m.CopyMayExist && m.CanRemovePartial && m.CanRetry && m.CanOpenLog, "a half copy offers Retry, Remove partial copy and Open log");
        runner.Run(m.Args(), CancellationToken.None);
        Check(failing.Calls.Contains("Recover") && failing.Calls.Count(c => c == "Setup") == 1 && m.Phase == RunPhase.Done, "Retry recovers the half copy instead of starting over blindly");
        // Remove partial copy.
        failing = new FailingSetupOps(new FakeWizardOps { SetupFails = new InvalidOperationException("Setup stopped."), FailSetupTimes = 1 });
        m = AtReview(failing); m.Next(); runner = new WizardRunner(m, failing, Inline);
        runner.Run(m.Args(), CancellationToken.None);
        Check(m.CanRemovePartial, "partial copy present");
        runner.RemovePartial();
        Check(m.Page == WizardPage.Review && m.Phase == RunPhase.Idle && !m.CanRemovePartial && failing.Calls.Contains("RemovePartial"), "removing the partial copy returns to the review page");
        // UAC cancelled: the copy stays and Retry never recopies.
        ops = new FakeWizardOps { FirewallFails = new FirewallCancelledException() };
        m = AtReview(ops); m.Next(); runner = new WizardRunner(m, ops, Inline);
        runner.Run(m.Args(), CancellationToken.None);
        Check(m.Phase == RunPhase.NeedsFirewall && m.RetryText == Texts.AskAgain && m.CanRetry && !m.CanRemovePartial && m.Message.Contains("kept"), "a cancelled Windows prompt keeps the copy and offers to ask again");
        Check(m.Rows[3].State == RowState.Done && m.Rows[4].State == RowState.Failed, "copy steps stay done, the prompt step is marked");
        runner.Run(m.Args(), CancellationToken.None);
        Check(m.Phase == RunPhase.Done && ops.Calls.Count(c => c == "Setup") == 1 && ops.Calls.Count(c => c == "Recover") == 0 && ops.Calls.Count(c => c == "ApplyFirewall") == 2 && ops.Calls.Contains("Load"),
            "asking again does not copy again: " + string.Join(",", ops.Calls.Where(c => c != "Log" && c != "State")));
        // Another firewall failure is a failure, not a cancellation.
        ops = new FakeWizardOps { FirewallFails = new InvalidOperationException("Firewall changes failed. The copy will not launch until protection is verified.") };
        m = AtReview(ops); m.Next(); runner = new WizardRunner(m, ops, Inline); runner.Run(m.Args(), CancellationToken.None);
        Check(m.Phase == RunPhase.Failed && m.CanRetry && !m.CanRemovePartial, "a failed firewall change fails the run");
        // Cancelled during the copy.
        ops = new FakeWizardOps(); m = AtReview(ops); m.Next(); runner = new WizardRunner(m, ops, Inline);
        var cts = new CancellationTokenSource(); cts.Cancel();
        ops.Current = InstallState.Pending;
        runner.Run(m.Args(), cts.Token);
        Check(m.Phase == RunPhase.Cancelled && m.CopyMayExist && m.CanRetry && m.CanRemovePartial && m.CanOpenLog && m.ExitCode == 0, "Cancel during the copy offers Retry, Remove partial copy and Open log");
        Check(ops.Calls.Contains("Recover") || ops.Calls.Contains("Setup"), "the cancelled run reached the copy");
        // Cancel button rules in step 5.
        ops = new FakeWizardOps(); m = AtReview(ops); m.Next(); m.RunStarted();
        Check(m.CanCancel && m.RequestCancel() && m.CancelRequested && !m.CanCancel, "Cancel works once while copying");
        m = AtReview(new FakeWizardOps()); m.Next(); m.RunStarted(); m.FirewallStarted();
        Check(!m.CanCancel && !m.RequestCancel(), "Cancel is off while Windows is asking");
        // An unexpected exception is a message, not a crash.
        ops = new FakeWizardOps { SetupFails = new NullReferenceException("boom"), FailSetupTimes = 1 };
        m = AtReview(ops); m.Next(); runner = new WizardRunner(m, ops, Inline); runner.Run(m.Args(), CancellationToken.None);
        Check(m.Phase == RunPhase.Failed && m.Message.Contains("Your original game was not changed") && !m.Message.Contains("boom"), "unexpected errors become a plain message");
        // Long messages stay whole in the model; the window wraps them.
        string longPath = @"C:\" + string.Join(@"\", Enumerable.Repeat("a-very-long-folder-name-that-goes-on", 8));
        ops = new FakeWizardOps { SetupFails = new InvalidOperationException("Cannot use " + longPath + " because it is too long."), FailSetupTimes = 1 };
        m = AtReview(ops); m.Next(); new WizardRunner(m, ops, Inline).Run(m.Args(), CancellationToken.None);
        Check(m.Message.Contains(longPath), "long messages are kept whole");
        m = Wizard(new FakeWizardOps()); m.StartRecovery("An earlier setup stopped half-way.", InstallState.Pending);
        Check(m.Page == WizardPage.Run && m.Phase == RunPhase.Failed && m.CanRetry && m.CanRemovePartial, "the launcher can open the wizard in recovery");
        // Install record states pick the path.
        ops = new FakeWizardOps(); ops.Current = InstallState.Committed;
        m = AtReview(ops); m.Next(); new WizardRunner(m, ops, Inline).Run(m.Args(), CancellationToken.None);
        Check(!ops.Calls.Contains("Setup") && !ops.Calls.Contains("Recover") && ops.Calls.Contains("Load"), "a committed copy is never copied again");
    }
    sealed class FailingSetupOps : IWizardOps
    {
        readonly FakeWizardOps inner; public readonly List<string> Calls;
        public FailingSetupOps(FakeWizardOps inner) { this.inner = inner; Calls = inner.Calls; }
        public string[] FindClients() => inner.FindClients();
        public string DefaultTarget => inner.DefaultTarget; public bool UpdatesConfigured => inner.UpdatesConfigured;
        public Verdict CheckClient(string f) => inner.CheckClient(f);
        public TargetResult CheckTarget(string t, string s) => inner.CheckTarget(t, s);
        public InstallState State() => inner.State();
        public IDisposable Exclusive() => inner.Exclusive();
        public InstallRecord Setup(string a, string b, bool c, Action<SetupProgress> p, CancellationToken t) {
            Calls.Add("Setup");
            if (inner.FailSetupTimes > 0) { inner.FailSetupTimes--; inner.Current = InstallState.Pending; throw inner.SetupFails; }
            inner.Current = InstallState.Committed; return new InstallRecord { InstallId = new string('a', 32) };
        }
        public InstallRecord Recover(Action<SetupProgress> p, CancellationToken t) => inner.Recover(p, t);
        public InstallRecord Load() => inner.Load();
        public void ApplyFirewall(InstallRecord r) => inner.ApplyFirewall(r);
        public void SaveSettings(bool on) => inner.SaveSettings(on);
        public void RemovePartial() => inner.RemovePartial();
        public void Log(string l) => inner.Log(l);
        public string LogPath => inner.LogPath;
    }
    static LauncherModel Started(FakeLauncherOps ops, bool preAccepted = false, string restartNote = null) {
        var m = new LauncherModel(preAccepted);
        var r = new LauncherRunner(m, ops, Inline);
        r.Start(CancellationToken.None);
        return m;
    }
    static void LauncherStart() {
        // Order: lease, resume, status, check, apply. The lease covers all of it.
        var ops = new FakeLauncherOps { Checked = new UpdateStatus { State = UpdateState.Available, Message = "SoloStone 0.2.0 is available." }, Applied = new UpdateResult { Outcome = UpdateOutcome.Nothing, Message = "SoloStone is up to date." } };
        var m = new LauncherModel(); var runner = new LauncherRunner(m, ops, Inline);
        Check(!runner.Start(CancellationToken.None), "no restart without a swap");
        var order = ops.Calls.Where(c => c != "Status").ToList();
        Check(string.Join(",", order) == "Lease,Resume,Check,Apply,Unlease", "start order is resume, check, apply under one lease: " + string.Join(",", ops.Calls));
        Check(ops.Calls.IndexOf("Resume") < ops.Calls.IndexOf("Status") && m.Phase == LauncherPhase.Ready && m.CanPlay, "the window is ready only after the update step");
        // Swapped: restart at once, after the lease is released, and nothing else runs.
        ops = new FakeLauncherOps { Checked = new UpdateStatus { State = UpdateState.Available }, Applied = new UpdateResult { Outcome = UpdateOutcome.Swapped, Version = "0.2.0", Message = "SoloStone 0.2.0 is installed. Restarting." } };
        m = new LauncherModel(); runner = new LauncherRunner(m, ops, Inline);
        Check(runner.Start(CancellationToken.None), "a swap asks for a restart");
        Check(ops.Calls.Last() == "Restart:leases=0" && ops.Calls.Count(c => c.StartsWith("Restart")) == 1, "restart happens once, after the lease was released");
        Check(!ops.Calls.Contains("Launch") && ops.Calls.IndexOf("Apply") < ops.Calls.IndexOf("Unlease") && ops.Calls.Count(c => c == "Status") == 1, "no more work after a swap, and no launch");
        Check(m.Phase == LauncherPhase.Restarting && m.Update == UpdateUi.Swapped && !m.CanPlay, "the window shows the restart");
        // Warning on the new release.
        ops = new FakeLauncherOps { Checked = new UpdateStatus { State = UpdateState.Available },
            Applied = new UpdateResult { Outcome = UpdateOutcome.NeedsConfirmation, Version = "0.2.0", Message = "Latest client we tested with: 37.0.2. Your client: 37.0.0. SoloStone has not been tested with this exact client." } };
        m = new LauncherModel(); runner = new LauncherRunner(m, ops, Inline);
        Check(!runner.Start(CancellationToken.None) && m.Update == UpdateUi.NeedsConfirmation && m.CanConfirmUpdate && m.CanPlay && m.UpdateText.Contains("has not been tested"), "a warning on the new release waits for a click, play still works");
        ops.Applied = new UpdateResult { Outcome = UpdateOutcome.Swapped, Message = "installed" };
        Check(runner.ConfirmUpdate(CancellationToken.None) && ops.AppliedAccepting == true && ops.Calls.Last().StartsWith("Restart"), "confirming applies with the warning accepted and restarts");
        // Held, failed, off, not configured, up to date.
        foreach (var (state, ui) in new[] { (UpdateState.Held, UpdateUi.Held), (UpdateState.Failed, UpdateUi.Failed), (UpdateState.Off, UpdateUi.Off), (UpdateState.NotConfigured, UpdateUi.NotConfigured), (UpdateState.UpToDate, UpdateUi.UpToDate), (UpdateState.Busy, UpdateUi.Failed) }) {
            ops = new FakeLauncherOps { Checked = new UpdateStatus { State = state, Message = "message for " + state } };
            m = Started(ops);
            Check(m.Update == ui && m.CanPlay && m.Phase == LauncherPhase.Ready && !ops.Calls.Contains("Apply"), "update state " + state + " keeps Play on and applies nothing");
        }
        ops = new FakeLauncherOps { Checked = new UpdateStatus { State = UpdateState.Held, Message = "SoloStone 0.2.0 is on hold: it failed its first start." } };
        m = Started(ops); Check(m.UpdateText.Contains("on hold"), "a held update explains itself");
        ops = new FakeLauncherOps { Checked = new UpdateStatus { State = UpdateState.NotConfigured } , Report = Report(configured: false)};
        m = Started(ops); Check(m.UpdateText.Contains("not set up") && !m.CanSwitch && m.CanPlay, "not configured: plain words, switch disabled, play works");
        ops = new FakeLauncherOps { Checked = new UpdateStatus { State = UpdateState.Off }, Report = Report(updates: false) };
        m = Started(ops); Check(!m.UpdatesOn && m.CanSwitch, "updates off shows the switch off");
        // A failing request never blocks play.
        ops = new FakeLauncherOps { CheckFails = new IOException("offline") };
        m = Started(ops); Check(m.Update == UpdateUi.Failed && m.CanPlay && m.UpdateText == "Couldn't check for updates.", "a network failure keeps the current version playable");
        // Skip.
        ops = new FakeLauncherOps { Checked = new UpdateStatus { State = UpdateState.Available } };
        var skip = new CancellationTokenSource(); skip.Cancel();
        m = new LauncherModel(); new LauncherRunner(m, ops, Inline).Start(skip.Token);
        Check(m.Update == UpdateUi.Skipped && m.CanPlay && m.Phase == LauncherPhase.Ready && ops.OpenLeases == 0, "Skip ends the update step and play is available");
        var busy = new LauncherModel(); busy.BeginStart(); busy.ApplyReport(Report()); busy.UpdateChecking();
        Check(busy.CanSkip && !busy.CanPlay && !busy.CanRollback && !busy.CanUninstall && !busy.CanSwitch, "while updating only Skip is live");
        // Resume.
        ops = new FakeLauncherOps { Resumed = ResumeOutcome.Undone };
        m = Started(ops); Check(m.Notice.Contains("undone") && m.NoticeIsProblem && m.CanPlay, "an undone update is reported");
        // Not installed.
        ops = new FakeLauncherOps { Report = new StatusReport { Installed = false, InstallProblem = "SoloStone is not installed here." } };
        m = Started(ops); Check(m.Phase == LauncherPhase.NotInstalled && !ops.Calls.Contains("Check") && !m.CanPlay, "no install record: no update, no play");
    }
    static void LauncherButtons() {
        // Block.
        var ops = new FakeLauncherOps { Report = Report(BlockVerdict()) };
        var m = Started(ops);
        Check(!m.CanPlay && m.Kind == VerdictKind.Block && m.Headline.Contains("38") && m.Headline.Contains("37"), "Block disables Play and says why");
        Check(m.TestedLine.StartsWith("Latest client we tested with:") && m.YourLine.StartsWith("Your client:"), "both versions are always named");
        // Warning.
        ops = new FakeLauncherOps { Report = Report(WarningVerdict()) };
        m = Started(ops);
        Check(m.ShowWarning && !m.CanPlay, "Warning needs a click first");
        m.ConfirmWarning(true); Check(m.CanPlay, "after the click Play is on");
        var runner = new LauncherRunner(m, ops, Inline);
        Check(runner.Play(m.WarningConfirmed) == 0 && ops.AcceptedWarning == true && m.Phase == LauncherPhase.Closing, "Play passes the accepted warning to the launch");
        m = Started(new FakeLauncherOps { Report = Report(WarningVerdict()) }, preAccepted: true); Check(m.CanPlay, "--accept-warning pre-ticks the box");
        m = Started(new FakeLauncherOps { Report = Report(AllowVerdict()) }, preAccepted: true); Check(!m.WarningConfirmed, "an Allow client never holds a stale tick");
        // Play and all network finished.
        ops = new FakeLauncherOps { Checked = new UpdateStatus { State = UpdateState.Available }, Applied = new UpdateResult { Outcome = UpdateOutcome.Nothing, Message = "" } };
        m = new LauncherModel(); runner = new LauncherRunner(m, ops, Inline); runner.Start(CancellationToken.None);
        int before = ops.Calls.Count; runner.Play(false);
        Check(ops.Calls.Skip(before).All(c => c == "Lease" || c == "Launch" || c == "Unlease" || c == "Status") && ops.Calls.IndexOf("Launch") > ops.Calls.LastIndexOf("Apply") && ops.Calls.IndexOf("Launch") > ops.Calls.LastIndexOf("Check"), "every update call finishes before the launch");
        var starting = new LauncherModel(); starting.BeginStart();
        Throws<InvalidOperationException>(() => new LauncherRunner(starting, new FakeLauncherOps(), Inline).Play(false), "Play before the start finished");
        // Launch failures.
        ops = new FakeLauncherOps { LaunchFails = new InvalidOperationException("SoloStone firewall protection is incomplete. Run SoloStone firewall --apply.") };
        m = Started(ops); runner = new LauncherRunner(m, ops, Inline);
        Check(runner.Play(false) == 1 && m.Phase == LauncherPhase.Ready && m.NoticeIsProblem && m.FirewallProblem && m.CanFixFirewall && m.CanPlay, "a firewall refusal offers the fix");
        runner.RepairFirewall(); Check(!m.FirewallProblem && !m.CanFixFirewall && ops.Calls.Contains("RepairFirewall"), "fixing the firewall clears it");
        ops.FirewallFails = new FirewallCancelledException(); runner.RepairFirewall(); Check(m.NoticeIsProblem && m.Notice.Contains("cancelled"), "a cancelled prompt is reported");
        ops = new FakeLauncherOps { LaunchFails = new InvalidOperationException("SoloStone 0.2.0 failed its first start, so 0.1.0 was restored. Start SoloStone again.") };
        m = Started(ops); runner = new LauncherRunner(m, ops, Inline); runner.Play(false);
        Check(m.Phase == LauncherPhase.Ready && m.Notice.Contains("restored") && m.NoticeIsProblem && !m.FirewallProblem, "a restored release is reported and the window stays");
        ops = new FakeLauncherOps { LaunchCode = 0 }; m = Started(ops); runner = new LauncherRunner(m, ops, Inline);
        runner.Play(false); Check(m.Phase == LauncherPhase.Closing && m.ExitCode == 0, "the launcher closes with the game");
        ops = new FakeLauncherOps { LaunchCode = 3 }; m = Started(ops); runner = new LauncherRunner(m, ops, Inline);
        runner.Play(false); Check(m.Phase == LauncherPhase.Ready && m.NoticeIsProblem, "an odd exit code is reported, not hidden");
        // Rollback outcomes.
        ops = new FakeLauncherOps { Report = Report(previous: "0.0.9") };
        m = Started(ops); runner = new LauncherRunner(m, ops, Inline);
        Check(m.CanRollback, "Roll back is offered when a previous version exists");
        // A failed first start restored the previous release: this process's files changed, so Play must not stay enabled.
        { var restored = new FakeLauncherOps { LaunchFails = new UpdateRestoredException("SoloStone 2.0.0 failed its first start, so 1.0.0 was restored.") };
          var rm = new LauncherModel(); var rr = new LauncherRunner(rm, restored, a => a()); rr.Start(CancellationToken.None);
          rr.Play(false);
          Check(restored.Calls.Last() == "Restart:leases=0" && rm.Phase == LauncherPhase.Restarting && !rm.CanPlay, "an automatic restore restarts instead of leaving Play enabled"); }
        ops.RolledBack = new RollbackResult { Outcome = RollbackOutcome.Done, Version = "0.0.9", Message = "SoloStone 0.0.9 is restored. Restart the launcher." };
        runner.RollBack(); Check(ops.Calls.Last().StartsWith("Restart:leases=0") && m.Phase == LauncherPhase.Restarting, "a rollback restarts from the app folder");
        foreach (var outcome in new[] { RollbackOutcome.NoPrevious, RollbackOutcome.NotIdle }) {
            ops = new FakeLauncherOps { Report = Report(previous: "0.0.9"), RolledBack = new RollbackResult { Outcome = outcome, Message = "no " + outcome } };
            m = Started(ops); runner = new LauncherRunner(m, ops, Inline); runner.RollBack();
            Check(m.Notice == "no " + outcome && m.NoticeIsProblem && m.Phase == LauncherPhase.Ready && !ops.Calls.Any(c => c.StartsWith("Restart")), "rollback " + outcome + " explains and stays");
        }
        ops = new FakeLauncherOps { Report = Report(previous: "0.0.9"),
            RolledBack = new RollbackResult { Outcome = RollbackOutcome.SaveSchemaTooNew, OffersBackup = true, BackupId = "20261106T010203Z-0.1.0", Message = "SoloStone 0.0.9 cannot read your current saves. Restoring the pre-update backup makes this possible, but progress since the update is lost." } };
        m = Started(ops); runner = new LauncherRunner(m, ops, Inline); runner.RollBack();
        Check(m.CanRestoreBackup && m.BackupId != null && m.Notice.Contains("progress since the update is lost"), "a save problem offers the backup and states the loss");
        runner.RestoreBackup(m.BackupId);
        Check(!m.CanRestoreBackup && m.Notice.Contains("Progress since the update is lost") && ops.Calls.Any(c => c.StartsWith("RestoreBackup:2026")), "restoring reports the loss");
        // The switch.
        ops = new FakeLauncherOps(); m = Started(ops); runner = new LauncherRunner(m, ops, Inline);
        runner.SetUpdates(false); Check(ops.Calls.Contains("SaveUpdatesSetting:False") && !m.UpdatesOn && m.Update == UpdateUi.Off, "the switch saves and shows off");
        // The state is readable as JSON for the live driver.
        var json = m.Snapshot().ToJson(); Check(json.Contains("\"window\":\"launcher\"") && json.Contains("\"canPlay\""), "launcher state reads as JSON");
    }
    static void GuiWording() {
        // Plain words for the player: no build-process vocabulary in anything the windows say.
        string[] jargon = { "payload", "manifest", " sha", "hash", "json", "token", "journal", "stderr", "exception", "ed25519", "minisign", "bouncy" };
        var texts = typeof(Texts).GetFields().Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue())
            .Concat(new[] { Texts.ReviewBody(@"C:\Hearthstone", @"C:\copy", true), Texts.ReviewBody(@"C:\Hearthstone", @"C:\copy", false), Texts.TestedLine("37.0.0"), Texts.YourLine(null), Texts.SoloStoneLine(null), Texts.SpaceLine(1L << 30, 5L << 30) })
            .Concat(WizardModel.RowTexts).ToList();
        foreach (string text in texts) foreach (string word in jargon) Check(!text.ToLowerInvariant().Contains(word), "no jargon '" + word + "' in: " + text.Substring(0, Math.Min(50, text.Length)));
        Check(Texts.TestedLine("37.0.0") == "Latest client we tested with: 37.0.0" && Texts.YourLine("37.0.0") == "Your client: 37.0.0", "step 2 wording is fixed");
        var warning = WarningVerdict();
        Check(Texts.Detail(warning) == "SoloStone has not been tested with this Hearthstone version." && Texts.Headline(warning).Contains("not been tested"), "detail leaves out the two version lines");
        Check(Texts.Detail(null) == "" && Texts.Headline(ClientPolicy.Blocked("Missing file.")).Contains("Missing file"), "headlines");
        Check(Texts.YourLine("") == "Your client: unknown", "unknown client is said plainly");
        var busy = ClientPolicy.Blocked(ClientPolicy.GameRunning);
        Check(Texts.Headline(busy).Contains("Hearthstone") && Texts.Headline(busy).Contains("Quit"), "running game headline names it");
        Check(Texts.Headline(ClientPolicy.Blocked(ClientPolicy.AgentRunning)).Contains("still closing") && ClientPolicy.IsRunningFailure(ClientPolicy.AgentRunning), "the lingering updater says to wait, and the windows retry");
        Check(Source("WindowsHost.cs").Contains("else if(Process.GetProcessesByName(\"Agent\")"), "the updater is named only when Battle.net itself is closed");
        Check(Texts.VerdictColor(VerdictKind.Block) == VerdictTone.Block && Texts.VerdictColor(VerdictKind.Warning) == VerdictTone.Warning && Texts.VerdictColor(VerdictKind.Allow) == VerdictTone.Neutral, "verdict colours");
        Check(Texts.TestedLine(Texts.Version("37.0.0", "254477")).EndsWith("37.0.0.254477") && Texts.YourLine(Texts.Version("37.0.0", "255790")).EndsWith("37.0.0.255790"), "versions include available builds");
        var allowed = new Verdict { KindName="Allow", TestedBuild="254477", FoundBuild="255790" };
        Check(Texts.Detail(allowed).Contains("build differs"), "different allowed build is noted");
        Check(Texts.FanNotice == "Unofficial fan project, not affiliated with Blizzard. Offline only; use at your own risk.", "fan notice wording");
        string wizardSource = Source("Gui/WizardForm.cs"), launcherSource = Source("Gui/LauncherForm.cs");
        Check(wizardSource.Contains("legal, fanNotice") && wizardSource.Contains("fanNotice.SetText(Texts.FanNotice)"), "step 1 shows fan notice after fine print");
        Check(launcherSource.Contains("uninstallButtons, fanNotice") && launcherSource.Contains("fanNotice.SetText(Texts.FanNotice)") && !launcherSource.Contains("launcher.close"), "launcher shows fan notice and no Close button");
        // Card names and English text ship in the card data file, so the first step must not say that nothing from Blizzard comes along.
        string how = (Texts.HowBody + Texts.LegalLine).ToLowerInvariant();
        Check(!how.Contains("nothing from blizzard") && how.Contains("card names") && how.Contains("names and text") && how.Contains("no hearthstone program files, art or sound") && how.Contains("notice"), "step 1 says what ships from the game");
        Check(!how.Contains("approved") && !how.Contains("legal") && !how.Contains("licensed"), "and makes no legal claim");
        Check(Messages.Friendly(new UnauthorizedAccessException()).Contains("denied") && Messages.Friendly(new IOException()).Contains("Battle.net") && Messages.Friendly(new OperationCanceledException()).StartsWith("Stopped"), "plain messages for system errors");
        Check(new FirewallCancelledException() is InvalidOperationException && new FirewallCancelledException().Message.Contains("cancelled"), "the cancelled prompt is its own error but still an invalid operation");
        // Uninstall removes the rules first, so a refused or timed-out prompt there has removed nothing. Inno shows exit 6 as the same short line.
        string iss = Source("SoloStone.iss");
        Check(new FirewallCancelledException(true).Message == FirewallCancelledException.RemovingText && Source("Program.cs").Contains("e is FirewallCancelledException?6:")
            && iss.Contains("(Code = 6)") && iss.Contains("Permission wasn''t given, so nothing was removed. Click Uninstall again.") && !iss.Contains("unused sibling"), "a refused uninstall prompt says nothing was removed");
    }
}

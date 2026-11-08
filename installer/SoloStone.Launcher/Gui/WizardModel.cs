using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace SoloStone
{
    public enum WizardPage { HowItWorks = 1, Folder = 2, CopyTo = 3, Review = 4, Run = 5 }
    public enum RunPhase { Idle, Running, Failed, NeedsFirewall, Cancelled, Done }
    public enum InstallState { None, Pending, Committed }
    public enum RowState { Pending, Active, Done, Failed }
    public sealed class TargetResult { public string Problem, Target; public long Required, Free; }
    public sealed class WizardRow { public string Text; public RowState State; }

    // Everything the wizard needs from the system. The real one is in WindowsOps.cs; tests use a fake.
    public interface IWizardOps
    {
        string[] FindClients();
        string DefaultTarget { get; }
        bool UpdatesConfigured { get; }
        Verdict CheckClient(string folder);                              // reads only
        TargetResult CheckTarget(string target, string source);          // reads only
        InstallState State();
        IDisposable Exclusive();                                         // the lifecycle lease
        InstallRecord Setup(string source, string target, bool acceptedWarning, Action<SetupProgress> progress, CancellationToken cancel);
        InstallRecord Recover(Action<SetupProgress> progress, CancellationToken cancel);
        InstallRecord Load();
        void ApplyFirewall(InstallRecord record);                        // throws FirewallCancelledException when Windows permission is refused
        void SaveSettings(bool checkForUpdates);
        void RemovePartial();
        void Log(string line);
        string LogPath { get; }
    }

    public sealed class RunArgs
    {
        public string Source, Target; public bool AcceptedWarning, CheckForUpdates;
    }

    // Pure wizard state: which page, what each page allows, and what step 5 shows. No forms, no files, no threads.
    public sealed class WizardModel
    {
        public static readonly string[] RowTexts = {
            "Check your Hearthstone",
            "Check the copy location",
            "Copy your Hearthstone and check every file",
            "Add SoloStone's offline files",
            "Block the copy from the internet (Windows asks permission)" };
        int sourceGeneration, targetGeneration;
        public WizardModel(string defaultTarget, bool updatesConfigured) {
            Target = defaultTarget ?? ""; UpdatesConfigured = updatesConfigured; CheckForUpdates = updatesConfigured;
            Rows = RowTexts.Select(t => new WizardRow { Text = t }).ToList();
        }
        public WizardPage Page { get; private set; } = WizardPage.HowItWorks;
        public string Source { get; private set; } = "";
        public Verdict Verdict { get; private set; }
        public bool Checking { get; private set; }
        public bool WarningAccepted { get; private set; }
        public string Target { get; private set; }
        public bool TargetChecking { get; private set; }
        public bool TargetKnown { get; private set; }
        public string TargetProblem { get; private set; }
        public long Required { get; private set; }
        public long Free { get; private set; }
        public bool UpdatesConfigured { get; }
        public bool CheckForUpdates { get; private set; }
        public RunPhase Phase { get; private set; }
        public string Message { get; private set; } = "";
        public bool CopyMayExist { get; private set; }
        public bool FirewallActive { get; private set; }
        public bool CancelRequested { get; private set; }
        public long ProgressDone { get; private set; }
        public long ProgressTotal { get; private set; }
        public bool Closed { get; private set; }
        public bool OpenLauncherAfter { get; private set; }
        public List<WizardRow> Rows { get; }
        // 0 when the player cancelled or finished, 1 when setup failed and the window was closed anyway.
        public int ExitCode { get; private set; }

        // ---- Source (step 2) ----
        public int BeginSourceCheck(string path) {
            Source = path ?? "";
            Verdict = null; WarningAccepted = false; TargetKnown = false; TargetProblem = null;
            Checking = Source.Trim().Length != 0;
            return ++sourceGeneration;
        }
        // A result for a folder the player has already moved away from is ignored.
        public bool FinishSourceCheck(int generation, Verdict verdict) {
            if (generation != sourceGeneration) return false;
            Checking = false; Verdict = verdict; return true;
        }
        public bool FailSourceCheck(int generation, string message) => FinishSourceCheck(generation, ClientPolicy.Blocked(message));
        public void AcceptWarning(bool accepted) { WarningAccepted = accepted && Verdict != null && Verdict.Kind == VerdictKind.Warning; }

        // ---- Target (step 3) ----
        public int BeginTargetCheck(string path) {
            Target = path ?? ""; TargetKnown = false; TargetProblem = null; TargetChecking = true;
            return ++targetGeneration;
        }
        public bool FinishTargetCheck(int generation, TargetResult result) {
            if (generation != targetGeneration) return false;
            TargetChecking = false; TargetKnown = true; Target = result.Target ?? Target; TargetProblem = result.Problem; Required = result.Required; Free = result.Free; return true;
        }
        public bool FailTargetCheck(int generation, string message) => FinishTargetCheck(generation, new TargetResult { Problem = message });
        public void SetUpdates(bool on) { CheckForUpdates = on && UpdatesConfigured; }

        // ---- Navigation ----
        public bool SourceOk => !Checking && Verdict != null && Source.Trim().Length != 0
            && (Verdict.Kind == VerdictKind.Allow || (Verdict.Kind == VerdictKind.Warning && WarningAccepted));
        public bool TargetOk => TargetKnown && !TargetChecking && TargetProblem == null && Target.Trim().Length != 0;
        public bool CanNext {
            get {
                switch (Page) {
                    case WizardPage.HowItWorks: return true;
                    case WizardPage.Folder: return SourceOk;
                    case WizardPage.CopyTo: return TargetOk;
                    case WizardPage.Review: return true;
                    default: return false;
                }
            }
        }
        public bool CanBack => Page != WizardPage.HowItWorks && Page != WizardPage.Run;
        public string NextText => Page == WizardPage.Review ? Texts.Install : Texts.Next;
        // Cancel closes the wizard without writing anything until step 5 has started. In step 5 it stops a running copy.
        public bool CanCancel => Page != WizardPage.Run || (Phase == RunPhase.Running && !FirewallActive && !CancelRequested);
        public bool WrittenAnything => Page == WizardPage.Run && Phase != RunPhase.Idle;
        public bool Next() {
            if (!CanNext) return false;
            Page = (WizardPage)((int)Page + 1);
            if (Page == WizardPage.Run) Phase = RunPhase.Idle;
            return true;
        }
        public bool Back() {
            if (!CanBack) return false;
            Page = (WizardPage)((int)Page - 1);
            return true;
        }
        // Before step 5 this is a plain exit. The caller must not have touched the disk, and WrittenAnything says so.
        public bool CancelWizard() {
            if (Page == WizardPage.Run) return false;
            Closed = true; ExitCode = 0; return true;
        }
        public bool Close() {
            if (Phase == RunPhase.Running) return false;
            Closed = true; return true;
        }
        public RunArgs Args() => new RunArgs { Source = Source, Target = Target, AcceptedWarning = WarningAccepted, CheckForUpdates = CheckForUpdates };

        // ---- Step 5 ----
        public void RunStarted() {
            Phase = RunPhase.Running; Message = "Starting…"; CancelRequested = false; FirewallActive = false; CopyMayExist = false;
            ProgressDone = 0; ProgressTotal = 0;
            foreach (var row in Rows) row.State = RowState.Pending;
        }
        public bool RequestCancel() {
            if (!CanCancel || Page != WizardPage.Run) return false;
            CancelRequested = true; Message = "Stopping…"; return true;
        }
        static int RowOf(SetupStep step) {
            switch (step) {
                case SetupStep.CheckClient: return 0;
                case SetupStep.CheckTarget: return 1;
                case SetupStep.CopyClient: case SetupStep.VerifyCopy: return 2;
                default: return 3;
            }
        }
        public void Progress(SetupProgress p) {
            int row = RowOf(p.Step);
            for (int i = 0; i < Rows.Count; i++) Rows[i].State = i < row ? RowState.Done : i == row ? RowState.Active : RowState.Pending;
            Message = p.Message ?? ""; ProgressDone = p.Done; ProgressTotal = p.Total;
        }
        public void CopyFinished() {
            for (int i = 0; i < 4; i++) Rows[i].State = RowState.Done;
            ProgressDone = ProgressTotal;
        }
        public void FirewallStarted() {
            FirewallActive = true; Rows[4].State = RowState.Active;
            Message = "Windows will ask permission to block the copy's internet access. Choose Yes.";
        }
        public void FirewallCancelled(string reason) {
            FirewallActive = false; Phase = RunPhase.NeedsFirewall; Rows[4].State = RowState.Failed; CopyMayExist = false; ExitCode = 1;
            Message = "Windows permission was cancelled. Your copy is kept and nothing needs copying again. SoloStone cannot start until the internet block is in place. Click \"" + Texts.AskAgain + "\" to be asked again.";
        }
        public void Done() {
            FirewallActive = false; Phase = RunPhase.Done; ExitCode = 0;
            foreach (var row in Rows) row.State = RowState.Done;
            Message = "SoloStone is ready. Click \"" + Texts.OpenSoloStone + "\" to start it. You can also use the shortcut on your desktop or in the Start menu.";
        }
        public void Failed(string message, InstallState state) {
            FirewallActive = false; Phase = RunPhase.Failed; ExitCode = 1;
            var active = Rows.FirstOrDefault(r => r.State == RowState.Active);
            if (active != null) active.State = RowState.Failed;
            CopyMayExist = state == InstallState.Pending;
            Message = message ?? "";
        }
        public void Cancelled(InstallState state) {
            FirewallActive = false; Phase = RunPhase.Cancelled; ExitCode = 0;
            var active = Rows.FirstOrDefault(r => r.State == RowState.Active);
            if (active != null) active.State = RowState.Failed;
            CopyMayExist = state == InstallState.Pending;
            Message = "Stopped. Your own Hearthstone folder was not changed."
                + (CopyMayExist ? " A partly finished copy remains. Try again to finish it, or remove it." : "");
        }
        // Opened by the launcher when an earlier setup stopped half-way.
        public void StartRecovery(string message, InstallState state) {
            Page = WizardPage.Run; Phase = RunPhase.Failed; ExitCode = 1;
            CopyMayExist = state == InstallState.Pending; Message = message ?? "";
        }
        public void PartialRemoved() {
            Page = WizardPage.Review; Phase = RunPhase.Idle; CopyMayExist = false; ExitCode = 0;
            foreach (var row in Rows) row.State = RowState.Pending;
            Message = "";
        }
        public bool CanRetry => Phase == RunPhase.Failed || Phase == RunPhase.Cancelled || Phase == RunPhase.NeedsFirewall;
        public string RetryText => Phase == RunPhase.NeedsFirewall ? Texts.AskAgain : Texts.Retry;
        public bool CanRemovePartial => (Phase == RunPhase.Failed || Phase == RunPhase.Cancelled) && CopyMayExist;
        public bool CanOpenLog => Phase == RunPhase.Failed || Phase == RunPhase.Cancelled || Phase == RunPhase.NeedsFirewall;
        public bool CanOpenSoloStone => Phase == RunPhase.Done;
        public void RequestOpenLauncher() { if (Phase == RunPhase.Done) OpenLauncherAfter = true; }

        public WizardSnapshot Snapshot() => new WizardSnapshot {
            Window = "wizard", Page = (int)Page, Source = Source, Checking = Checking, Verdict = Verdict?.KindName, TestedVersion = Verdict?.TestedVersion, FoundVersion = Verdict?.FoundVersion,
            Message = Verdict?.Message, WarningAccepted = WarningAccepted, Target = Target, TargetChecking = TargetChecking, TargetKnown = TargetKnown, TargetProblem = TargetProblem,
            CanNext = CanNext, CanBack = CanBack, CanCancel = CanCancel, CheckForUpdates = CheckForUpdates, UpdatesConfigured = UpdatesConfigured,
            Phase = Phase.ToString(), RunMessage = Message, CopyMayExist = CopyMayExist, WrittenAnything = WrittenAnything,
            Rows = Rows.Select(r => r.Text + ": " + r.State).ToArray(), CanRetry = CanRetry, CanRemovePartial = CanRemovePartial, CanOpenLog = CanOpenLog };
    }

    [DataContract] public sealed class WizardSnapshot
    {
        [DataMember(Name = "window")] public string Window;
        [DataMember(Name = "page")] public int Page;
        [DataMember(Name = "source")] public string Source;
        [DataMember(Name = "checking")] public bool Checking;
        [DataMember(Name = "verdict")] public string Verdict;
        [DataMember(Name = "testedVersion")] public string TestedVersion;
        [DataMember(Name = "foundVersion")] public string FoundVersion;
        [DataMember(Name = "message")] public string Message;
        [DataMember(Name = "warningAccepted")] public bool WarningAccepted;
        [DataMember(Name = "target")] public string Target;
        [DataMember(Name = "targetChecking")] public bool TargetChecking;
        [DataMember(Name = "targetKnown")] public bool TargetKnown;
        [DataMember(Name = "targetProblem")] public string TargetProblem;
        [DataMember(Name = "canNext")] public bool CanNext;
        [DataMember(Name = "canBack")] public bool CanBack;
        [DataMember(Name = "canCancel")] public bool CanCancel;
        [DataMember(Name = "checkForUpdates")] public bool CheckForUpdates;
        [DataMember(Name = "updatesConfigured")] public bool UpdatesConfigured;
        [DataMember(Name = "phase")] public string Phase;
        [DataMember(Name = "runMessage")] public string RunMessage;
        [DataMember(Name = "copyMayExist")] public bool CopyMayExist;
        [DataMember(Name = "writtenAnything")] public bool WrittenAnything;
        [DataMember(Name = "rows")] public string[] Rows;
        [DataMember(Name = "canRetry")] public bool CanRetry;
        [DataMember(Name = "canRemovePartial")] public bool CanRemovePartial;
        [DataMember(Name = "canOpenLog")] public bool CanOpenLog;
        public string ToJson() => System.Text.Encoding.UTF8.GetString(JsonBytes.ToBytes(this));
    }

    // Step 5, off the UI thread. Every model change goes through post, so the window stays responsive and tests can run it inline.
    public sealed class WizardRunner
    {
        readonly WizardModel model; readonly IWizardOps ops; readonly Action<Action> post;
        public WizardRunner(WizardModel model, IWizardOps ops, Action<Action> post) { this.model = model; this.ops = ops; this.post = post; }
        public void RemovePartial() {
            if (!model.CanRemovePartial) return;
            string failure = null; InstallState state = InstallState.None;
            try { using (ops.Exclusive()) ops.RemovePartial(); ops.Log("The partial copy was removed."); }
            catch (Exception e) { failure = Messages.Friendly(e); ops.Log("Removing the partial copy failed: " + failure); }
            try { state = ops.State(); } catch (Exception) { }
            post(() => { if (failure == null) model.PartialRemoved(); else model.Failed(failure, state); });
        }
        public void Run(RunArgs args, CancellationToken cancel) {
            post(model.RunStarted);
            ops.Log("Setup started.");
            try {
                using (ops.Exclusive()) {
                    InstallRecord record;
                    Action<SetupProgress> progress = p => { ops.Log(p.Message); post(() => model.Progress(p)); };
                    switch (ops.State()) {
                        case InstallState.Committed:
                            // The copy finished earlier and only Windows permission is missing. Never copy again.
                            ops.Log("The copy is already finished. Asking for Windows permission again.");
                            record = ops.Load(); break;
                        case InstallState.Pending:
                            record = ops.Recover(progress, cancel); break;
                        default:
                            record = ops.Setup(args.Source, args.Target, args.AcceptedWarning, progress, cancel); break;
                    }
                    post(model.CopyFinished);
                    ops.SaveSettings(args.CheckForUpdates);
                    post(model.FirewallStarted);
                    ops.Log("Asking Windows for permission to block the copy's internet access.");
                    ops.ApplyFirewall(record);
                }
                ops.Log("Setup finished.");
                post(model.Done);
            }
            catch (FirewallCancelledException e) { ops.Log(e.Message); post(() => model.FirewallCancelled(e.Message)); }
            catch (OperationCanceledException) { ops.Log("Stopped by the player."); var state = Safe(); post(() => model.Cancelled(state)); }
            catch (Exception e) {
                string message = Messages.Friendly(e); ops.Log("Setup failed: " + message + (e.InnerException != null ? " (" + e.InnerException.GetType().Name + ": " + e.InnerException.Message + ")" : ""));
                var state = Safe(); post(() => model.Failed(message, state));
            }
        }
        InstallState Safe() { try { return ops.State(); } catch (Exception) { return InstallState.None; } }
    }
}

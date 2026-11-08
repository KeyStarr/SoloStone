using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace SoloStone
{
    public enum LauncherPhase { Starting, Ready, Working, Launching, Running, Restarting, NotInstalled, Closing }
    public enum UpdateUi { Pending, NotConfigured, Off, Checking, UpToDate, Applying, NeedsConfirmation, Held, Failed, Skipped, Swapped }

    public interface ILauncherOps
    {
        IDisposable Lease();                                             // the lifecycle lease
        ResumeOutcome Resume();
        bool AfterHelper { get; } // A helper opened this launcher, so its note is news, and the update it reports on must not start again here.
        StatusReport Status();                                           // reads only, no network
        UpdateStatus Check(CancellationToken cancel);                    // the only network use
        UpdateResult Apply(bool acceptWarning, CancellationToken cancel);
        void Restart();                                                  // start Root\app\SoloStone.exe again and exit this process
        int Launch(bool acceptWarning, Action<string> progress);         // no network from here on
        RollbackResult Rollback();
        RestoreResult RestoreBackup(string id);
        void SaveUpdatesSetting(bool on);
        void RepairFirewall();
    }

    // Pure launcher window state. The buttons ask this what they may do.
    public sealed class LauncherModel
    {
        public LauncherPhase Phase { get; private set; } = LauncherPhase.Starting;
        public UpdateUi Update { get; private set; } = UpdateUi.Pending;
        public string UpdateText { get; private set; } = "";
        public StatusReport Report { get; private set; }
        public string Notice { get; private set; } = "";
        public bool NoticeIsProblem { get; private set; }
        public bool WarningConfirmed { get; private set; }
        public UpdateResult PendingUpdate { get; private set; }
        public bool OffersBackup { get; private set; }
        public string BackupId { get; private set; }
        public bool FirewallProblem { get; private set; }
        public int ExitCode { get; private set; }

        public LauncherModel(bool warningPreAccepted = false) { WarningConfirmed = warningPreAccepted; }

        public bool Installed => Report != null && Report.Installed;
        public VerdictKind? Kind => Installed && Report.Verdict != null ? (VerdictKind?)Report.Verdict.Kind : null;
        public bool UpdateBusy => Update == UpdateUi.Checking || Update == UpdateUi.Applying;
        public bool Idle => Phase == LauncherPhase.Ready;
        public bool CanPlay => Idle && Installed && Kind != VerdictKind.Block && (Kind != VerdictKind.Warning || WarningConfirmed) && !UpdateBusy;
        public bool ShowWarning => Installed && Kind == VerdictKind.Warning;
        public bool CanConfirmWarning => ShowWarning && Idle;
        public bool CanSkip => Phase == LauncherPhase.Starting && UpdateBusy;
        public bool CanConfirmUpdate => Idle && Update == UpdateUi.NeedsConfirmation && PendingUpdate != null;
        public bool CanRollback => Idle && Installed && !string.IsNullOrEmpty(Report.PreviousVersion);
        public bool CanRestoreBackup => Idle && OffersBackup;
        public bool CanSwitch => Idle && Installed && Report.UpdatesConfigured;
        public bool UpdatesOn => Installed && Report.CheckForUpdates && Report.UpdatesConfigured;
        public bool CanUninstall => Idle && Installed;
        public bool CanFixFirewall => Idle && Installed && FirewallProblem;
        public bool CanClose => Phase != LauncherPhase.Launching && Phase != LauncherPhase.Working && Phase != LauncherPhase.Restarting && Update != UpdateUi.Applying;
        public string TestedLine => Texts.TestedLine(Texts.Version(Report?.Verdict?.TestedVersion ?? Report?.TestedVersion, Report?.Verdict?.TestedBuild));
        public string YourLine => Texts.YourLine(Texts.Version(Report?.Verdict?.FoundVersion ?? Report?.ClientVersion, Report?.Verdict?.FoundBuild));
        public string VersionLine => Texts.SoloStoneLine(Report?.ReleaseVersion);
        public string Headline => Kind == null ? (Installed ? "" : Texts.NotInstalled) : Texts.Headline(Report.Verdict);
        public string Detail => Texts.Detail(Report?.Verdict);

        public void BeginStart() { Phase = LauncherPhase.Starting; Update = UpdateUi.Pending; UpdateText = ""; }
        public void Resumed(ResumeOutcome outcome) {
            if (outcome == ResumeOutcome.Undone) Say("An update did not finish, so it was undone. Your earlier version is back.", true);
        }
        public void ApplyReport(StatusReport report) {
            Report = report;
            // The signed list can move a client between tiers, so a stale tick must not carry over.
            if (Kind != VerdictKind.Warning) WarningConfirmed = false;
        }
        // Once, at start: later status reads must not talk over what the player just did.
        public void ShowStartNote(StatusReport report) { if (!string.IsNullOrEmpty(report.UpdateNote)) Say(report.UpdateNote, true); }
        public void NotInstalled() { Phase = LauncherPhase.NotInstalled; }
        public void UpdateChecking() { Update = UpdateUi.Checking; UpdateText = "Checking for updates…"; }
        public void UpdateChecked(UpdateStatus status) {
            UpdateText = status.Message ?? "";
            switch (status.State) {
                case UpdateState.NotConfigured: Update = UpdateUi.NotConfigured; UpdateText = "Updates are not set up in this build."; break;
                case UpdateState.Off: Update = UpdateUi.Off; UpdateText = "Update checks are off."; break;
                case UpdateState.UpToDate: Update = UpdateUi.UpToDate; break;
                case UpdateState.Held: Update = UpdateUi.Held; break;
                case UpdateState.Failed: Update = UpdateUi.Failed; break;
                case UpdateState.Busy: Update = UpdateUi.Failed; break;
                default: Update = UpdateUi.Checking; break; // Available: the apply step follows at once
            }
        }
        public void UpdateApplying() { Update = UpdateUi.Applying; UpdateText = "Downloading and checking the update…"; }
        public void UpdateApplied(UpdateResult result) {
            UpdateText = result.Message ?? "";
            switch (result.Outcome) {
                case UpdateOutcome.Swapped: Update = UpdateUi.Swapped; UpdateText = result.Message; break;
                case UpdateOutcome.NeedsConfirmation: Update = UpdateUi.NeedsConfirmation; PendingUpdate = result; break;
                case UpdateOutcome.Held: Update = UpdateUi.Held; break;
                case UpdateOutcome.Nothing: Update = UpdateUi.UpToDate; break;
                default: Update = UpdateUi.Failed; break;
            }
        }
        public void UpdateSkipped() { Update = UpdateUi.Skipped; UpdateText = "Update skipped. You can play now. SoloStone will look again next time."; }
        public void UpdateFailed(string message) { Update = UpdateUi.Failed; UpdateText = "The update did not install, so the current version stays. " + message; }
        public void Ready() { Phase = LauncherPhase.Ready; }
        public void Restarting() { Phase = LauncherPhase.Restarting; }
        public void ConfirmWarning(bool on) { WarningConfirmed = on && ShowWarning; }
        public void SwitchChanged(bool on) { if (Report != null) Report.CheckForUpdates = on; if (!on) { Update = UpdateUi.Off; UpdateText = "Update checks are off."; } }
        public void ClearUpdateOffer() { PendingUpdate = null; }

        void Say(string text, bool problem) { Notice = text ?? ""; NoticeIsProblem = problem; }
        public void Busy(string text) { Phase = LauncherPhase.Working; Say(text, false); }
        public void LaunchStarted() { Phase = LauncherPhase.Launching; FirewallProblem = false; Say("Starting SoloStone…", false); }
        public void LaunchProgress(string message) {
            Say(message, false);
            if ((message ?? "").StartsWith("SoloStone is running", StringComparison.Ordinal)) Phase = LauncherPhase.Running;
        }
        public void LaunchEnded(int code, string error) {
            if (error == null && code == 0) { Phase = LauncherPhase.Closing; ExitCode = 0; return; }
            Phase = LauncherPhase.Ready; ExitCode = 1;
            Say(error ?? "The game stopped. Your installation and saves were kept.", true);
            FirewallProblem = (error ?? "").IndexOf("firewall", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        void Settle() { if (Phase == LauncherPhase.Working || Phase == LauncherPhase.Launching) Phase = LauncherPhase.Ready; }
        public void RolledBack(RollbackResult result) {
            switch (result.Outcome) {
                case RollbackOutcome.Done: Say(result.Message, false); Phase = LauncherPhase.Restarting; break;
                case RollbackOutcome.SaveSchemaTooNew: Say(result.Message, true); OffersBackup = result.OffersBackup; BackupId = result.BackupId; Settle(); break;
                default: Say(result.Message, true); Settle(); break;
            }
        }
        public void Restored(RestoreResult result) { Say(result.Message, false); OffersBackup = false; BackupId = null; Settle(); }
        public void Failure(string message) { Say(message, true); Settle(); }
        public void Info(string message) { Say(message, false); }
        public void FirewallRepaired() { FirewallProblem = false; Say("The internet block is in place again.", false); Settle(); }

        public LauncherSnapshot Snapshot() => new LauncherSnapshot {
            Window = "launcher", Phase = Phase.ToString(), Update = Update.ToString(), UpdateText = UpdateText, Installed = Installed, Verdict = Report?.Verdict?.KindName,
            TestedVersion = Report?.Verdict?.TestedVersion ?? Report?.TestedVersion, ClientVersion = Report?.Verdict?.FoundVersion ?? Report?.ClientVersion, ReleaseVersion = Report?.ReleaseVersion,
            PreviousVersion = Report?.PreviousVersion, HeldVersion = Report?.HeldVersion, HeldReason = Report?.HeldReason, UpdatesConfigured = Report?.UpdatesConfigured ?? false, CheckForUpdates = Report?.CheckForUpdates ?? false,
            Notice = Notice, NoticeIsProblem = NoticeIsProblem, WarningConfirmed = WarningConfirmed, CanPlay = CanPlay, CanSkip = CanSkip, CanConfirmUpdate = CanConfirmUpdate,
            CanRollback = CanRollback, CanRestoreBackup = CanRestoreBackup, CanSwitch = CanSwitch, CanUninstall = CanUninstall, CanFixFirewall = CanFixFirewall,
            Backups = Report?.Backups ?? new string[0], JournalPhase = Report?.JournalPhase };
    }

    [DataContract] public sealed class LauncherSnapshot
    {
        [DataMember(Name = "window")] public string Window;
        [DataMember(Name = "phase")] public string Phase;
        [DataMember(Name = "update")] public string Update;
        [DataMember(Name = "updateText")] public string UpdateText;
        [DataMember(Name = "installed")] public bool Installed;
        [DataMember(Name = "verdict")] public string Verdict;
        [DataMember(Name = "testedVersion")] public string TestedVersion;
        [DataMember(Name = "clientVersion")] public string ClientVersion;
        [DataMember(Name = "releaseVersion")] public string ReleaseVersion;
        [DataMember(Name = "previousVersion")] public string PreviousVersion;
        [DataMember(Name = "heldVersion")] public string HeldVersion;
        [DataMember(Name = "heldReason")] public string HeldReason;
        [DataMember(Name = "updatesConfigured")] public bool UpdatesConfigured;
        [DataMember(Name = "checkForUpdates")] public bool CheckForUpdates;
        [DataMember(Name = "notice")] public string Notice;
        [DataMember(Name = "noticeIsProblem")] public bool NoticeIsProblem;
        [DataMember(Name = "warningConfirmed")] public bool WarningConfirmed;
        [DataMember(Name = "canPlay")] public bool CanPlay;
        [DataMember(Name = "canSkip")] public bool CanSkip;
        [DataMember(Name = "canConfirmUpdate")] public bool CanConfirmUpdate;
        [DataMember(Name = "canRollback")] public bool CanRollback;
        [DataMember(Name = "canRestoreBackup")] public bool CanRestoreBackup;
        [DataMember(Name = "canSwitch")] public bool CanSwitch;
        [DataMember(Name = "canUninstall")] public bool CanUninstall;
        [DataMember(Name = "canFixFirewall")] public bool CanFixFirewall;
        [DataMember(Name = "backups")] public string[] Backups;
        [DataMember(Name = "journalPhase")] public string JournalPhase;
        public string ToJson() => System.Text.Encoding.UTF8.GetString(JsonBytes.ToBytes(this));
    }

    // The launcher's start order from the seams document: resume, check, apply, then play. Everything runs off the UI thread.
    public sealed class LauncherRunner
    {
        readonly LauncherModel model; readonly ILauncherOps ops; readonly Action<Action> post;
        public LauncherRunner(LauncherModel model, ILauncherOps ops, Action<Action> post) { this.model = model; this.ops = ops; this.post = post; }

        // True when a new release was swapped in: the process restarted from Root\app and this one must load nothing more.
        public bool Start(CancellationToken skip) {
            bool restart = false;
            using (ops.Lease()) {
                post(model.BeginStart);
                try {
                    var resumed = ops.Resume(); post(() => model.Resumed(resumed));
                    if (resumed == ResumeOutcome.HandedOff) restart = true;
                    else {
                    var report = ops.Status(); post(() => { model.ApplyReport(report); if (ops.AfterHelper) model.ShowStartNote(report); });
                    if (!report.Installed) { post(model.NotInstalled); return false; }
                    restart = CheckAndApply(false, skip, true);
                    }
                }
                catch (Exception e) when (Messages.Expected(e)) { string message = Messages.Friendly(e); post(() => model.Failure(message)); }
                if (!restart) {
                    try { var report = ops.Status(); post(() => model.ApplyReport(report)); }
                    catch (Exception e) when (Messages.Expected(e)) { string message = Messages.Friendly(e); post(() => model.Failure(message)); }
                    post(model.Ready);
                }
            }
            if (restart) Restart();
            return restart;
        }
        bool CheckAndApply(bool acceptWarning, CancellationToken cancel, bool checkFirst) {
            bool checking = checkFirst;
            try {
                if (checkFirst) {
                    post(model.UpdateChecking);
                    var status = ops.Check(cancel);
                    post(() => model.UpdateChecked(status));
                    if (status.State != UpdateState.Available) return false;
                }
                checking = false;
                post(model.UpdateApplying);
                var result = ops.Apply(acceptWarning, cancel);
                post(() => model.UpdateApplied(result));
                return result.Outcome == UpdateOutcome.Swapped;
            }
            catch (OperationCanceledException) { post(model.UpdateSkipped); return false; }
            catch (Exception e) when (Messages.Expected(e)) { string message = Messages.Friendly(e); post(() => { if (checking) model.UpdateChecked(new UpdateStatus { State = UpdateState.Failed, Message = "Couldn't check for updates." }); else model.UpdateFailed(message); }); return false; }
        }
        // After the player confirmed a Warning verdict on the new release.
        public bool ConfirmUpdate(CancellationToken skip) {
            bool restart;
            using (ops.Lease()) {
                restart = CheckAndApply(true, skip, false);
                if (!restart) { try { var report = ops.Status(); post(() => model.ApplyReport(report)); } catch (Exception e) when (Messages.Expected(e)) { } post(model.Ready); }
            }
            if (restart) Restart();
            return restart;
        }
        void Restart() { post(model.Restarting); ops.Restart(); }

        // Network is finished by now: the window enables Play only after Start returned.
        public int Play(bool acceptWarning) {
            if (model.Phase != LauncherPhase.Ready) throw new InvalidOperationException("Wait until SoloStone finishes starting.");
            post(model.LaunchStarted);
            try {
                int code;
                using (ops.Lease()) code = ops.Launch(acceptWarning, message => post(() => model.LaunchProgress(message)));
                post(() => model.LaunchEnded(code, null));
                return code;
            }
            catch (UpdateRestoredException e) {
                // This process's files were just swapped back. Play must not stay enabled here.
                string message = e.Message;
                post(() => model.Info(message));
                Restart();
                return 1;
            }
            catch (Exception e) {
                string message = Messages.Friendly(e);
                post(() => model.LaunchEnded(1, message));
                try { var report = ops.Status(); post(() => model.ApplyReport(report)); } catch (Exception) { }
                return 1;
            }
        }
        public void RollBack() {
            bool restart = false;
            try {
                using (ops.Lease()) {
                    var result = ops.Rollback();
                    post(() => model.RolledBack(result));
                    restart = result.Outcome == RollbackOutcome.Done;
                    if (!restart) { var report = ops.Status(); post(() => model.ApplyReport(report)); }
                }
            }
            catch (Exception e) when (Messages.Expected(e)) { string message = Messages.Friendly(e); post(() => model.Failure(message)); }
            if (restart) Restart();
        }
        public void RestoreBackup(string id) {
            try {
                using (ops.Lease()) {
                    var result = ops.RestoreBackup(id);
                    post(() => model.Restored(result));
                    var report = ops.Status(); post(() => model.ApplyReport(report));
                }
            }
            catch (Exception e) when (Messages.Expected(e)) { string message = Messages.Friendly(e); post(() => model.Failure(message)); }
        }
        public void SetUpdates(bool on) {
            try {
                using (ops.Lease()) { ops.SaveUpdatesSetting(on); }
                post(() => model.SwitchChanged(on));
            }
            catch (Exception e) when (Messages.Expected(e)) { string message = Messages.Friendly(e); post(() => model.Failure(message)); }
        }
        public void RepairFirewall() {
            try {
                using (ops.Lease()) { ops.RepairFirewall(); }
                post(model.FirewallRepaired);
            }
            catch (Exception e) when (Messages.Expected(e)) { string message = Messages.Friendly(e); post(() => model.Failure(message)); }
        }
    }
}

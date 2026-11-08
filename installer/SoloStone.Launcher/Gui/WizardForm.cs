using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoloStone
{
    // The five-step setup. The form only shows the model and starts background work; the model decides what is allowed.
    internal sealed class WizardForm : Form
    {
        readonly IWizardOps ops; readonly WizardModel model; readonly WizardRunner runner;
        readonly Label title = new Label { Name = "wizard.title", AutoSize = true, UseMnemonic = false };
        readonly Label stepLabel = new Label { Name = "wizard.step", AutoSize = true, UseMnemonic = false };
        readonly Stack how = new Stack("page.how"), folder = new Stack("page.folder"), copy = new Stack("page.copy"), review = new Stack("page.review"), run = new Stack("page.run");
        readonly FlowLayoutPanel footer = Ui.Footer("wizard.footer");
        readonly Button back = Ui.Button("wizard.back", Texts.Back), next = Ui.Button("wizard.next", Texts.Next), cancel = Ui.Button("wizard.cancel", Texts.Cancel), close = Ui.Button("wizard.close", Texts.Close);
        readonly Button retry = Ui.Button("run.retry", Texts.Retry), remove = Ui.Button("run.remove", Texts.RemovePartial), openLog = Ui.Button("run.log", Texts.OpenLog), openApp = Ui.Button("run.open", Texts.OpenSoloStone);
        readonly WrapText howText = new WrapText("how.text") { MaxHeight = 600 };
        readonly WrapText folderHelp = new WrapText("source.help"), sourceHeadline = new WrapText("source.headline"), sourceTested = new WrapText("source.tested"), sourceYours = new WrapText("source.yours"), sourceDetail = new WrapText("source.detail");
        readonly Row sourceRow = new Row("source.path", "source.browse", Texts.FolderBrowse);
        readonly Button recheck = Ui.Button("source.recheck", Texts.Recheck);
        readonly CheckBox acceptWarning = Ui.Check("source.accept", Texts.AcceptWarning);
        readonly WrapText copyHelp = new WrapText("target.help"), space = new WrapText("target.space"), targetProblem = new WrapText("target.problem");
        readonly Row targetRow = new Row("target.path", "target.browse", Texts.FolderBrowse);
        readonly WrapText reviewText = new WrapText("review.text") { MaxHeight = 600 }, reviewNote = new WrapText("review.updates-note");
        readonly CheckBox updates = Ui.Check("review.updates", Texts.ReviewUpdates);
        readonly Label[] rows = new Label[WizardModel.RowTexts.Length];
        readonly ProgressBar bar = new ProgressBar { Name = "run.progress", Height = 20 };
        readonly WrapText runStatus = new WrapText("run.status") { MaxHeight = 300 };
        readonly System.Windows.Forms.Timer retryTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        readonly System.Windows.Forms.Timer sourceTimer = new System.Windows.Forms.Timer { Interval = 450 }, targetTimer = new System.Windows.Forms.Timer { Interval = 450 };
        CancellationTokenSource cts;
        bool updating;
        internal int ExitCode => model.ExitCode;
        internal bool OpenLauncher => model.OpenLauncherAfter;

        internal WizardForm(IWizardOps ops, InstallState startState) {
            this.ops = ops;
            model = new WizardModel(ops.DefaultTarget, ops.UpdatesConfigured);
            runner = new WizardRunner(model, ops, a => Ui.Post(this, () => { a(); Render(); }));
            Icon = Ui.AppIcon(); Name = "wizard"; Text = "SoloStone setup"; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(660, 560); MinimumSize = new Size(520, 460);
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96F, 96F); Font = SystemFonts.MessageBoxFont; MaximizeBox = false;
            title.Font = new Font(Font.FontFamily, Font.Size * 1.4f, FontStyle.Bold);
            var header = new Panel { Name = "wizard.header", Dock = DockStyle.Top, Height = 64, Padding = new Padding(16, 12, 16, 4) };
            title.Left = 16; title.Top = 10; stepLabel.Left = 18; stepLabel.Top = 40;
            header.Controls.Add(title); header.Controls.Add(stepLabel);
            BuildPages();
            foreach (var button in new[] { cancel, next, back, close, retry, remove, openLog, openApp }) footer.Controls.Add(button);
            foreach (var page in new[] { how, folder, copy, review, run }) { page.Dock = DockStyle.Fill; page.Visible = false; Controls.Add(page); }
            Controls.Add(footer); Controls.Add(header);
            AcceptButton = next; CancelButton = null;
            Wire();
            if (startState == InstallState.Pending)
                model.StartRecovery("An earlier setup stopped before it finished. Try again to finish it, or remove the partial copy and start over.", startState);
            FormClosing += OnClosing;
            Shown += (s, e) => { TopMost = true; BringToFront(); Activate(); TopMost = false; AttachHooks(); Render(); };
            Render();
        }
        void AttachHooks() {
#if SOLOSTONE_TEST_HOOKS
            TestControl.Attach(this, () => model.Snapshot().ToJson());
#endif
        }
        void BuildPages() {
            howText.SetText(Texts.HowBody);
            var beforeTitle = Ui.Line("how.before.title", "Before you start");
            beforeTitle.Font = new Font(Font, FontStyle.Bold);
            var before = new WrapText("how.before"); before.SetText(Texts.BeforeBody);
            var legal = new WrapText("how.legal") { ForeColor = SystemColors.GrayText, Font = new Font(Font.FontFamily, Font.Size * 0.9f) }; legal.SetText(Texts.LegalLine);
            var fanNotice = new WrapText("how.fan-notice") { ForeColor = SystemColors.GrayText, Font = new Font(Font.FontFamily, Font.Size * 0.9f) }; fanNotice.SetText(Texts.FanNotice);
            how.Controls.AddRange(new Control[] { howText, beforeTitle, before, legal, fanNotice });
            folderHelp.SetText(Texts.FolderHelp);
            var hint = new WrapText("source.hint") { ForeColor = SystemColors.GrayText, Font = new Font(Font.FontFamily, Font.Size * 0.9f) }; hint.SetText(Texts.FolderHint);
            folder.Controls.AddRange(new Control[] { folderHelp, sourceRow, hint, recheck, sourceHeadline, sourceTested, sourceYours, sourceDetail, acceptWarning });
            copyHelp.SetText(Texts.CopyHelp);
            copy.Controls.AddRange(new Control[] { copyHelp, targetRow, space, targetProblem });
            var reviewHeading = Ui.Line("review.heading", "When you click Install, SoloStone will:");
            reviewHeading.Font = new Font(Font, FontStyle.Bold);
            reviewNote.ForeColor = SystemColors.GrayText; reviewNote.Font = new Font(Font.FontFamily, Font.Size * 0.9f);
            review.Controls.AddRange(new Control[] { reviewHeading, reviewText, updates, reviewNote });
            for (int i = 0; i < rows.Length; i++) { rows[i] = Ui.Line("run.step." + i, WizardModel.RowTexts[i]); run.Controls.Add(rows[i]); }
            run.Controls.Add(bar); run.Controls.Add(runStatus);
            targetRow.Box.Text = model.Target;
        }
        void Wire() {
            back.Click += (s, e) => { if (model.Back()) EnterPage(); };
            next.Click += (s, e) => { if (model.Next()) EnterPage(); };
            cancel.Click += (s, e) => CancelClicked();
            close.Click += (s, e) => { if (model.Close()) Close(); };
            retry.Click += (s, e) => { if (model.CanRetry) StartRun(); };
            remove.Click += (s, e) => RemovePartialCopy();
            openLog.Click += (s, e) => OpenLog();
            openApp.Click += (s, e) => { model.RequestOpenLauncher(); Close(); };
            sourceRow.Box.TextChanged += (s, e) => { if (!updating) { StartSourceCheckSoon(); } };
            sourceRow.Action.Click += (s, e) => Browse(sourceRow.Box, Texts.FolderHelp);
            recheck.Click += (s, e) => StartSourceCheck(sourceRow.Box.Text);
            targetRow.Box.TextChanged += (s, e) => { if (!updating) StartTargetCheckSoon(); };
            targetRow.Action.Click += (s, e) => Browse(targetRow.Box, "Choose or create the folder for the offline copy.");
            acceptWarning.CheckedChanged += (s, e) => { if (!updating) { model.AcceptWarning(acceptWarning.Checked); Render(); } };
            updates.CheckedChanged += (s, e) => { if (!updating) { model.SetUpdates(updates.Checked); Render(); } };
            retryTimer.Tick += (s, e) => { retryTimer.Stop(); if (model.Page == WizardPage.Folder && model.Verdict?.RetryRunning == true && !model.Checking) StartSourceCheck(sourceRow.Box.Text); };
            sourceTimer.Tick += (s, e) => { sourceTimer.Stop(); StartSourceCheck(sourceRow.Box.Text); };
            targetTimer.Tick += (s, e) => { targetTimer.Stop(); StartTargetCheck(targetRow.Box.Text); };
        }
        // ---- Pages ----
        void EnterPage() {
            if (model.Page == WizardPage.Folder && sourceRow.Box.Text.Length == 0 && !model.Checking) Detect();
            if (model.Page == WizardPage.CopyTo) StartTargetCheck(targetRow.Box.Text);
            if (model.Page == WizardPage.Run) StartRun();
            Render();
        }
        void Detect() {
            int generation = model.BeginSourceCheck("");
            sourceHeadline.SetText(Texts.FolderChecking);
            Task.Run(() => {
                string[] found = new string[0]; string failure = null;
                try { found = ops.FindClients(); } catch (Exception e) { failure = Messages.Friendly(e); }
                Ui.Post(this, () => {
                    if (sourceRow.Box.Text.Length != 0 || !model.FinishSourceCheck(generation, null)) return;
                    if (failure != null) { model.FailSourceCheck(generation, failure); Render(); return; }
                    string selected = Discovery.Prefill(sourceRow.Box.Text, found);
                    if (selected.Length != 0) { SetBox(sourceRow.Box, selected); StartSourceCheck(selected); }
                    else Render();
                });
            });
        }
        void SetBox(TextBox box, string value) { updating = true; try { box.Text = value; } finally { updating = false; } }
        void StartSourceCheckSoon() { sourceTimer.Stop(); sourceTimer.Start(); }
        void StartTargetCheckSoon() { targetTimer.Stop(); targetTimer.Start(); }
        void StartSourceCheck(string path) {
            sourceTimer.Stop(); retryTimer.Stop();
            int generation = model.BeginSourceCheck(path);
            Render();
            if (path.Trim().Length == 0) return;
            Task.Run(() => {
                Verdict verdict = null; string failure = null;
                try { verdict = ops.CheckClient(path); } catch (Exception e) { failure = Messages.Friendly(e); }
                Ui.Post(this, () => { if (failure != null) model.FailSourceCheck(generation, failure); else model.FinishSourceCheck(generation, verdict); Render(); });
            });
        }
        void StartTargetCheck(string path) {
            targetTimer.Stop();
            int generation = model.BeginTargetCheck(path);
            string source = model.Source;
            Render();
            Task.Run(() => {
                TargetResult result = null; string failure = null;
                try { result = ops.CheckTarget(path, source); } catch (Exception e) { failure = Messages.Friendly(e); }
                Ui.Post(this, () => { if (targetRow.Box.Text != path) return; if (failure != null) model.FailTargetCheck(generation, failure); else if (model.FinishTargetCheck(generation, result)) SetBox(targetRow.Box, model.Target); Render(); });
            });
        }
        void Browse(TextBox box, string description) {
            using (var picker = new FolderBrowserDialog { Description = description, ShowNewFolderButton = box == targetRow.Box, SelectedPath = Directory.Exists(box.Text) ? box.Text : "" }) {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                updating = true; try { box.Text = picker.SelectedPath; } finally { updating = false; }
                if (box == sourceRow.Box) StartSourceCheck(picker.SelectedPath); else StartTargetCheck(picker.SelectedPath);
            }
        }
        // ---- Step 5 ----
        void StartRun() {
            cts = new CancellationTokenSource();
            var args = model.Args(); var token = cts.Token;
            model.RunStarted();
            Render();
            Task.Run(() => runner.Run(args, token));
        }
        void CancelClicked() {
            if (model.Page != WizardPage.Run) { if (model.CancelWizard()) Close(); return; }
            if (model.RequestCancel()) cts?.Cancel();
            Render();
        }
        void RemovePartialCopy() {
            if (!model.CanRemovePartial) return;
            Task.Run(() => runner.RemovePartial());
        }
        void OpenLog() {
            try {
                if (!File.Exists(ops.LogPath)) { model.Failed("There is no log yet.", InstallState.None); Render(); return; }
                Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"), FirewallPlan.Quote(ops.LogPath)) { UseShellExecute = false });
            } catch (Exception e) when (e is InvalidOperationException || e is System.ComponentModel.Win32Exception) { }
        }
        void OnClosing(object sender, FormClosingEventArgs e) {
            if (model.Phase == RunPhase.Running) {
                // A running copy stops through its token, never by killing the window.
                e.Cancel = true; if (model.RequestCancel()) cts?.Cancel(); Render(); return;
            }
            if (model.Page != WizardPage.Run && !model.Closed) model.CancelWizard();
        }
        // ---- View ----
        void Render() {
            if (IsDisposed) return;
            updating = true;
            try {
                int page = (int)model.Page;
                title.Text = PageTitle(model.Page);
                stepLabel.Text = "Step " + page + " of 5";
                how.Visible = page == 1; folder.Visible = page == 2; copy.Visible = page == 3; review.Visible = page == 4; run.Visible = page == 5;
                back.Visible = page > 1 && page < 5; back.Enabled = model.CanBack;
                next.Visible = page < 5; next.Text = model.NextText; next.Enabled = model.CanNext;
                cancel.Visible = page < 5 || model.Phase == RunPhase.Running; cancel.Enabled = model.CanCancel;
                close.Visible = page == 5 && model.Phase != RunPhase.Running && model.Phase != RunPhase.Idle;
                retry.Visible = page == 5 && model.CanRetry; retry.Text = model.RetryText;
                remove.Visible = page == 5 && model.CanRemovePartial;
                openLog.Visible = page == 5 && model.CanOpenLog;
                openApp.Visible = page == 5 && model.CanOpenSoloStone;
                AcceptButton = openApp.Visible ? openApp : next.Visible ? next : (IButtonControl)close;
                RefreshFolder(); RefreshCopy(); RefreshReview(); RefreshRun();
            } finally { updating = false; }
            foreach (var page in new[] { how, folder, copy, review, run }) if (page.Visible) page.PerformLayout();
        }
        static string PageTitle(WizardPage page) {
            switch (page) {
                case WizardPage.HowItWorks: return Texts.HowTitle;
                case WizardPage.Folder: return Texts.FolderTitle;
                case WizardPage.CopyTo: return Texts.CopyTitle;
                case WizardPage.Review: return Texts.ReviewTitle;
                default: return Texts.RunTitle;
            }
        }
        void RefreshFolder() {
            var v = model.Verdict;
            sourceHeadline.SetText(model.Checking ? Texts.FolderChecking : v != null ? Texts.Headline(v) : model.Source.Trim().Length == 0 ? Texts.FolderNone : "");
            sourceHeadline.ForeColor = WrapText.VerdictColor(model.Checking ? null : (VerdictKind?)v?.Kind);
            if (model.Page == WizardPage.Folder && !model.Checking && v?.RetryRunning == true && !retryTimer.Enabled) retryTimer.Start();
            else if (model.Page != WizardPage.Folder || model.Checking || v?.RetryRunning != true) retryTimer.Stop();
            // Both versions are always named, even when the client is unreadable or blocked.
            bool known = v != null && !model.Checking;
            sourceTested.Visible = known; sourceYours.Visible = known; sourceDetail.Visible = known && Texts.Detail(v).Length != 0;
            if (known) {
                sourceTested.SetText(Texts.TestedLine(Texts.Version(v.TestedVersion, v.TestedBuild))); sourceYours.SetText(Texts.YourLine(Texts.Version(v.FoundVersion, v.FoundBuild))); sourceDetail.SetText(Texts.Detail(v));
            }
            acceptWarning.Visible = known && v.Kind == VerdictKind.Warning; acceptWarning.Checked = model.WarningAccepted;
            recheck.Enabled = !model.Checking && model.Source.Trim().Length != 0;
        }
        void RefreshCopy() {
            space.Visible = model.TargetKnown; targetProblem.Visible = model.TargetKnown && model.TargetProblem != null;
            if (model.TargetChecking) { space.Visible = true; space.SetText("Checking this location…"); }
            else if (model.TargetKnown) space.SetText(Texts.SpaceLine(model.Required, model.Free));
            targetProblem.SetText(model.TargetProblem ?? "");
        }
        void RefreshReview() {
            if (model.Page != WizardPage.Review) return;
            reviewText.SetText(Texts.ReviewBody(model.Source, model.Target, model.UpdatesConfigured));
            updates.Enabled = model.UpdatesConfigured; updates.Checked = model.CheckForUpdates;
            reviewNote.Visible = !model.UpdatesConfigured; reviewNote.SetText(Texts.ReviewUpdatesOff);
        }
        static string Marker(RowState state) => state == RowState.Done ? "Done: " : state == RowState.Active ? "Working: " : state == RowState.Failed ? "Stopped: " : "Waiting: ";
        void RefreshRun() {
            for (int i = 0; i < rows.Length; i++) rows[i].Text = Marker(model.Rows[i].State) + WizardModel.RowTexts[i];
            bool running = model.Phase == RunPhase.Running;
            if (running && model.ProgressTotal == 0 && model.Rows[2].State != RowState.Active) { bar.Style = ProgressBarStyle.Marquee; }
            else {
                bar.Style = ProgressBarStyle.Continuous;
                bar.Value = model.Phase == RunPhase.Done ? 100 : model.ProgressTotal > 0 ? (int)Math.Min(100, model.ProgressDone * 100 / model.ProgressTotal) : 0;
            }
            runStatus.SetText(model.Message);
        }
        protected override void Dispose(bool disposing) {
            if (disposing) { sourceTimer.Dispose(); targetTimer.Dispose(); retryTimer.Dispose(); cts?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}

using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoloStone
{
    // The window behind the shortcut: versions, update status, Play, Roll back, the updates switch and Uninstall.
    internal sealed class LauncherForm : Form
    {
        readonly WindowsLauncherOps ops; readonly LauncherModel model; readonly LauncherRunner runner;
        readonly Stack stack = new Stack("launcher.body");
        readonly Label title = new Label { Name = "launcher.title", Text = Texts.AppName, AutoSize = true, UseMnemonic = false };
        readonly WrapText tested = new WrapText("launcher.tested"), yours = new WrapText("launcher.yours"), release = new WrapText("launcher.release");
        readonly WrapText headline = new WrapText("launcher.verdict"), detail = new WrapText("launcher.detail");
        readonly CheckBox accept = Ui.Check("launcher.accept", Texts.PlayWarning);
        readonly WrapText update = new WrapText("launcher.update") { MaxHeight = 160 };
        readonly ProgressBar bar = new ProgressBar { Name = "launcher.progress", Style = ProgressBarStyle.Marquee, Height = 16 };
        readonly Button skip = Ui.Button("launcher.skip", Texts.Skip), installUpdate = Ui.Button("launcher.update-confirm", Texts.InstallUpdate);
        readonly FlowLayoutPanel updateButtons = new FlowLayoutPanel { Name = "launcher.update-buttons", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
        readonly WrapText notice = new WrapText("launcher.message") { MaxHeight = 240 };
        readonly CheckBox switchBox = Ui.Check("launcher.updates-switch", Texts.UpdatesSwitch);
        readonly WrapText uninstallText = new WrapText("uninstall.text") { Visible = false };
        readonly CheckBox deleteSaves = Ui.Check("uninstall.delete-saves", Texts.UninstallSaves);
        readonly FlowLayoutPanel uninstallButtons = new FlowLayoutPanel { Name = "uninstall.buttons", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
        readonly Button uninstallGo = Ui.Button("uninstall.go", Texts.UninstallGo), uninstallKeep = Ui.Button("uninstall.keep", Texts.UninstallKeep);
        readonly FlowLayoutPanel footer = Ui.Footer("launcher.footer");
        readonly Button play = Ui.Button("launcher.play", Texts.Play), rollback = Ui.Button("launcher.rollback", Texts.RollBack), restore = Ui.Button("launcher.restore-backup", Texts.RestoreBackup),
            firewall = Ui.Button("launcher.firewall", Texts.FixFirewall), uninstall = Ui.Button("launcher.uninstall", Texts.Uninstall);
        readonly System.Windows.Forms.Timer retryTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        readonly CancellationTokenSource skipUpdate = new CancellationTokenSource();
        bool updating, confirmingUninstall, retrying;
        internal string UninstallerPath;
        internal bool DeleteSavesChosen;
        internal int ExitCode => model.ExitCode;

        internal LauncherForm(WindowsLauncherOps ops, bool warningPreAccepted) {
            this.ops = ops;
            model = new LauncherModel(warningPreAccepted);
            runner = new LauncherRunner(model, ops, a => Ui.Post(this, () => { a(); Render(); }));
            Icon = Ui.AppIcon(); Name = "launcher"; Text = Texts.AppName; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(560, 520); MinimumSize = new Size(480, 420);
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96F, 96F); Font = SystemFonts.MessageBoxFont; MaximizeBox = false;
            title.Font = new Font(Font.FontFamily, Font.Size * 1.6f, FontStyle.Bold);
            var header = new Panel { Name = "launcher.header", Dock = DockStyle.Top, Height = 72, Padding = new Padding(20, 12, 20, 12) };
            var logo = new PictureBox { Name = "launcher.icon", Image = Icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(44, 44), Location = new Point(20, 12) };
            title.Location = new Point(76, 20); header.Controls.Add(logo); header.Controls.Add(title);
            headline.Font = new Font(Font, FontStyle.Bold);
            update.ForeColor = SystemColors.GrayText; update.Font = new Font(Font.FontFamily, Font.Size * 0.9f);
            play.AutoSize = false; play.Size = new Size(180, 48); play.MinimumSize = play.Size;
            play.Font = new Font(Font.FontFamily, Font.Size * 1.15f, FontStyle.Bold); play.FlatStyle = FlatStyle.Flat;
            play.BackColor = Color.FromArgb(35, 97, 169); play.ForeColor = Color.White; play.UseVisualStyleBackColor = false;
            uninstall.FlatStyle = FlatStyle.Flat; uninstall.FlatAppearance.BorderSize = 0; uninstall.ForeColor = Color.FromArgb(65, 90, 120);
            uninstall.MinimumSize = Size.Empty; uninstall.Padding = Padding.Empty;
            updateButtons.Controls.AddRange(new Control[] { skip, installUpdate });
            uninstallButtons.Controls.AddRange(new Control[] { uninstallGo, uninstallKeep });
            var fanNotice = new WrapText("launcher.fan-notice") { ForeColor = SystemColors.GrayText, Font = new Font(Font.FontFamily, Font.Size * 0.9f) }; fanNotice.SetText(Texts.FanNotice);
            stack.Controls.AddRange(new Control[] { headline, tested, yours, release, detail, accept, play, update, bar, updateButtons, notice, switchBox, uninstall, uninstallText, deleteSaves, uninstallButtons, fanNotice });
            foreach (var button in new[] { firewall, restore, rollback }) footer.Controls.Add(button);
            stack.Dock = DockStyle.Fill; Controls.Add(stack); Controls.Add(footer); Controls.Add(header);
            AcceptButton = play;
            retryTimer.Tick += (s, e) => {
                retryTimer.Stop();
                if (!model.Idle || model.Report?.Verdict?.RetryRunning != true) return;
                retrying = true;
                Task.Run(() => {
                    StatusReport report = null;
                    try { report = ops.Status(); } catch (Exception) { }
                    Ui.Post(this, () => { retrying = false; if (model.Idle && model.Report?.Verdict?.RetryRunning == true && report != null) model.ApplyReport(report); Render(); });
                });
            };
            FormClosed += (s, e) => retryTimer.Dispose();
            Wire();
            FormClosing += (s, e) => { if (!model.CanClose || model.Phase == LauncherPhase.Running) { e.Cancel = true; if (model.Phase == LauncherPhase.Running) { model.Info("SoloStone is running. Quit from the game to close this window."); Render(); } } };
            Shown += (s, e) => { Activate(); AttachHooks(); Render(); BeginStart(); };
        }
        void AttachHooks() {
#if SOLOSTONE_TEST_HOOKS
            TestControl.Attach(this, () => model.Snapshot().ToJson());
#endif
        }
        // A background failure must reach the window; an unobserved task exception would leave it stuck.
        void Background(Action work) {
            Task.Run(() => {
                try { work(); }
                catch (Exception e) { string message = Messages.Friendly(e); Ui.Post(this, () => { model.Failure(message); model.Ready(); Render(); }); }
            });
        }
        void Wire() {
            accept.CheckedChanged += (s, e) => { if (!updating) { model.ConfirmWarning(accept.Checked); Render(); } };
            skip.Click += (s, e) => { skipUpdate.Cancel(); skip.Enabled = false; };
            installUpdate.Click += (s, e) => {
                if (!model.CanConfirmUpdate) return;
                model.Busy("Installing the update…"); Render();
                Background(() => runner.ConfirmUpdate(CancellationToken.None));
            };
            play.Click += (s, e) => Play();
            rollback.Click += (s, e) => { if (model.CanRollback) { model.Busy("Rolling back…"); Render(); Background(() => runner.RollBack()); } };
            restore.Click += (s, e) => { if (model.CanRestoreBackup) { string id = model.BackupId; model.Busy("Restoring your saves…"); Render(); Background(() => { runner.RestoreBackup(id); }); } };
            firewall.Click += (s, e) => { if (model.CanFixFirewall) { model.Busy("Windows will ask for permission…"); Render(); Background(() => runner.RepairFirewall()); } };
            switchBox.CheckedChanged += (s, e) => { if (!updating && model.CanSwitch) { bool on = switchBox.Checked; Background(() => runner.SetUpdates(on)); } };
            uninstall.Click += (s, e) => { if (model.CanUninstall) { confirmingUninstall = true; Render(); } };
            uninstallKeep.Click += (s, e) => { confirmingUninstall = false; deleteSaves.Checked = false; Render(); };
            uninstallGo.Click += (s, e) => Uninstall();
        }
        void BeginStart() {
            Background(() => { runner.Start(skipUpdate.Token); });
        }
        void Play() {
            if (!model.CanPlay) return;
            bool acceptWarning = model.WarningConfirmed;
            Background(() => {
                runner.Play(acceptWarning);
                Ui.Post(this, () => { if (model.Phase == LauncherPhase.Closing) Close(); });
            });
        }
        void Uninstall() {
            if (!confirmingUninstall || !model.CanUninstall) return;
            bool saves = deleteSaves.Checked;
            model.Busy("Removing SoloStone…"); confirmingUninstall = false; Render();
            Background(() => {
                string failure = null, path = null;
                try { path = ops.Uninstall(saves, message => Ui.Post(this, () => { model.Info(message); Render(); })); }
                catch (Exception e) when (Messages.Expected(e)) { failure = Messages.Friendly(e); }
                Ui.Post(this, () => {
                    if (failure != null) { model.Failure(failure); Render(); return; }
                    UninstallerPath = path; DeleteSavesChosen = saves;
                    model.Info(path == null ? "SoloStone was removed." : "Opening the SoloStone uninstaller…");
                    model.Ready(); Close();
                });
            });
        }
        void Render() {
            if (IsDisposed) return;
            updating = true;
            try {
                if (!retrying && model.Idle && model.Report?.Verdict?.RetryRunning == true) { if (!retryTimer.Enabled) retryTimer.Start(); }
                else retryTimer.Stop();
                bool installed = model.Installed;
                tested.Visible = installed; yours.Visible = installed; release.Visible = installed;
                tested.SetText(model.TestedLine); yours.SetText(model.YourLine); release.SetText(model.VersionLine);
                headline.Visible = model.Phase != LauncherPhase.Starting || installed;
                headline.SetText(model.Phase == LauncherPhase.NotInstalled ? (model.Report?.InstallProblem ?? Texts.NotInstalled) : model.Headline);
                headline.ForeColor = WrapText.VerdictColor(model.Kind);
                detail.Visible = installed && model.Detail.Length != 0; detail.SetText(model.Detail);
                accept.Visible = model.ShowWarning; accept.Enabled = model.CanConfirmWarning; accept.Checked = model.WarningConfirmed;
                update.Visible = model.UpdateText.Length != 0; update.SetText(model.UpdateText);
                bar.Visible = model.UpdateBusy;
                updateButtons.Visible = model.CanSkip || model.CanConfirmUpdate;
                skip.Visible = model.UpdateBusy; skip.Enabled = model.CanSkip && !skipUpdate.IsCancellationRequested;
                installUpdate.Visible = model.CanConfirmUpdate; installUpdate.Enabled = model.CanConfirmUpdate;
                notice.Visible = model.Notice.Length != 0; notice.SetText(model.Notice);
                notice.ForeColor = model.NoticeIsProblem ? Color.Firebrick : SystemColors.ControlText;
                switchBox.Visible = installed && model.Report.UpdatesConfigured; switchBox.Enabled = model.CanSwitch; switchBox.Checked = model.UpdatesOn;
                uninstallText.Visible = confirmingUninstall; uninstallText.SetText(Texts.UninstallAskFor(model.Report?.ClientPath));
                deleteSaves.Visible = confirmingUninstall; uninstallButtons.Visible = confirmingUninstall;
                bool free = !confirmingUninstall;
                play.Enabled = model.CanPlay && free; rollback.Visible = model.CanRollback || (installed && !string.IsNullOrEmpty(model.Report.PreviousVersion)); rollback.Enabled = model.CanRollback && free;
                restore.Visible = model.OffersBackup; restore.Enabled = model.CanRestoreBackup && free;
                firewall.Visible = model.FirewallProblem; firewall.Enabled = model.CanFixFirewall && free;
                uninstall.Visible = installed; uninstall.Enabled = model.CanUninstall && free;
                play.Visible = model.Phase != LauncherPhase.NotInstalled;
            } finally { updating = false; }
            stack.PerformLayout();
        }
        protected override void Dispose(bool disposing) {
            if (disposing) skipUpdate.Dispose();
            base.Dispose(disposing);
        }
    }
}

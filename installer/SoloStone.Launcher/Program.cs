using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoloStone
{
    internal static class Program
    {
        [DllImport("kernel32.dll")] static extern bool AttachConsole(uint pid);
        [ThreadStatic] static bool resolving;
        [STAThread] static int Main(string[] args) {
            // Windows finds SoloStone.exe.config by the path as typed, so an 8.3 start loses its binding redirects. The libraries beside the exe must load either way.
            // Started mid-swap, a library can also be in releases\: the journal says where.
            AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>{
                if(resolving) return null; // Reading the journal must not ask for itself.
                resolving=true;
                try {
                    string file=MovedLibraries.Find(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),new System.Reflection.AssemblyName(e.Name).Name);
                    return file!=null?System.Reflection.Assembly.LoadFrom(file):null;
                } finally { resolving=false; }
            };
            bool quiet=args.Contains("--quiet") || args.Contains("--json");
            if(quiet) AttachConsole(uint.MaxValue);
            // Exit codes SoloStone.iss reads (preflight and uninstall): 3 another SoloStone operation or update is
            // running, 4 the folder holds another or incomplete installation, 5 the folder could not be read
            // (IO/access), 6 the firewall permission prompt was cancelled, 1 anything else. Keep SoloStone.iss in step.
            Options options=null;
            try {
                options=Options.Parse(args);
                // Running elevated is allowed (owner 2026-11-08): with UAC turned off every process of an administrator is elevated, and refusing locked those players out.
                if(quiet) return Finish(options,Execute(options,Console.WriteLine));
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                // A window error says what happened and is logged, instead of .NET's Continue or Quit box.
                Application.ThreadException+=(sender,e)=>Ui.Message(Messages.Unexpected(e.Exception),MessageBoxIcon.Error);
                // The shortcut (no arguments) and "launch" open the launcher window; with no install record it is the setup wizard.
                if(options.Command=="launch" || options.Command=="wizard") return GuiHost.Run(options);
                if(options.Command=="finish-swap" || options.Command=="finish-undo") return Finish(options,GuiHost.RunHelper(options));
                using(var window=new ProgressWindow(options)) { Application.Run(window); return Finish(options,window.Result); }
            } catch(Exception e) { string message=Friendly(e); if(options!=null) InstallerLog.Write(options.Root,options.Command+" failed: "+message); if(quiet) Console.Error.WriteLine(message); else Ui.Message(message,MessageBoxIcon.Error); return e is FirewallCancelledException?6:e is SetupBusyException?3:e is SetupOccupiedException?4:e is IOException || e is UnauthorizedAccessException?5:1; }
        }
        static int Finish(Options options,int code) {
            // Start app removal only after the progress window closes and the lifecycle lease is released.
            if(code==0 && options.Uninstaller!=null) StartUninstaller(options.Uninstaller,options.DeleteSaves);
            // A helper's last act, after its lease is released: open the launcher it just put in place.
            if(options.StartAppAfter) { string exe=Path.Combine(options.Root,"app","SoloStone.exe"); Process.Start(new ProcessStartInfo(exe,"launch --after-helper --root "+FirewallPlan.Quote(options.Root)) { UseShellExecute=false, WorkingDirectory=Path.GetDirectoryName(exe) }).Dispose(); }
            return code;
        }
        internal static void StartUninstaller(string path,bool deleteSaves)
            => Process.Start(new ProcessStartInfo(path,"/SILENT"+(deleteSaves?" /DELETESAVES=1":"")) { UseShellExecute=true });
        internal static string Friendly(Exception e) => Messages.Friendly(e);
        static string[] NoSafetyFailures => new string[0];
        internal static Verdict CheckFolder(ClientFacts facts,Options o,string[] safety)
            => ClientGate.Evaluate(facts,TestedClient.Compiled(),new ManifestStore(o.Root,UpdateConfig.Compiled()).Current(),new NoBindingManifest(),safety);
        internal static int Execute(Options o,Action<string> progress) {
            string bundle=AppDomain.CurrentDomain.BaseDirectory;
            Safety.NoLinks(o.Root);
            if(o.Command=="preflight" || o.Command=="preflight-uninstall") {
                if(o.App==null) throw new InvalidOperationException("Preflight requires the actual --app destination.");
                // Setup's temporary launcher can finish an update a kill cut short. It runs outside the app folder, so it needs no helper.
                if(o.Command=="preflight") Installer.ResumeInterrupted(o.Root,()=>new UpdateEngine(o.Root,new WindowsUpdateHost(o.Root),progress));
                Installer.Preflight(o.Root,o.App,WindowsHost.RegisteredFolders(),o.Command=="preflight-uninstall"); return 0;
            }
            if(o.Command=="check-payload") return CheckPayload(o,o.App??bundle,progress);
            if(o.Command=="check-release") {
                string problem=ReleaseScan.ReleaseProblem(Path.GetFullPath(o.OutFolder),o.Version,!o.NoSetup);
                if(problem!=null) throw new InvalidOperationException(problem);
                progress("Release check passed: "+o.Version+"."); return 0;
            }
            if(o.Command=="check-client") {
                // A staged launcher checks a stopped copy, so running Battle.net must not hold an update. Only the wizard's folder step asks.
                string[] safety=o.Source!=null?WindowsHost.IdleFailures(o.Root):NoSafetyFailures;
                Verdict verdict=CheckClient.Run(o.Root,o.Source,TestedClient.Compiled(),UpdateConfig.Compiled(),new NoBindingManifest(),safety);
                progress(o.Json?verdict.ToJson():verdict.Message); return 0;
            }
            var host=new WindowsUpdateHost(o.Root);
            if(o.Command=="finish-swap" || o.Command=="finish-undo") return FinishInHelper(o,host,progress);
            // Keep the unelevated lifecycle lease while Windows system tools change firewall rules.
            using(var lease=OpenLease(o.Root)) {
                var engine=new UpdateEngine(o.Root,host,progress);
                // Status only reads. Setup has no journal, and uninstall removes whatever a journal names.
                if(o.Command!="setup" && o.Command!="uninstall" && o.Command!="status" && o.Command!="settings") {
                    // A helper now owns the journal and is waiting for this process to exit. Going on would race it.
                    if(engine.Resume()==ResumeOutcome.HandedOff) { progress(Messages.UpdateFinishing); return 0; }
                }
                if(o.Command=="status") {
                    var report=StatusReport.Build(o.Root,engine,TestedClient.Compiled(),UpdateConfig.Compiled(),new NoBindingManifest(),NoSafetyFailures);
                    progress(o.Json?report.ToJson():(report.Installed?"Installed: "+report.ReleaseVersion+". Client: "+report.ClientVersion+". "+report.Verdict?.Message:"Not installed. "+report.InstallProblem)); return 0;
                }
                if(o.Command=="settings") { Settings.Save(o.Root,new Settings { CheckForUpdates=o.UpdatesSwitch=="on" }); progress("Update checks are "+o.UpdatesSwitch+"."); return 0; }
                if(o.Command=="setup") {
                    WindowsHost.RequireIdle(o.Root);
                    string source=o.Source;
                    if(source==null) {
                        string[] found=WindowsHost.FindClients();
                        if(found.Length==1) source=found[0];
                        else throw new InvalidOperationException("Choose your Hearthstone client folder using setup's Browse button, or pass --source <folder>.");
                    }
                    progress("Preparing your offline copy…");
                    var installed=Installer.Setup(new SetupOptions { Source=source, Root=o.Root, ClientPath=o.ClientPath, Bundle=bundle, SaveRoot=WindowsHost.SaveRoot, Registered=WindowsHost.RegisteredFolders(),
                        Check=facts=>CheckFolder(facts,o,WindowsHost.IdleFailures(o.Root)), AcceptedWarning=o.AcceptWarning, Progress=step=>progress(step.Message) });
                    progress("Windows will ask permission to block the copied game's outbound connections…");
                    WindowsHost.Firewall(installed,true);
                    progress("Setup complete. Open SoloStone from your desktop or Start menu."); return 0;
                }
                if(o.Command=="recover") {
                    WindowsHost.RequireIdle(o.Root);
                    Installer.Recover(o.Root,bundle,WindowsHost.RegisteredFolders(),WindowsHost.SaveRoot,step=>progress(step.Message)); return 0;
                }
                if(o.Command=="uninstall" && !File.Exists(Path.Combine(o.Root,"install.json"))) return UninstallAppOnly(o,progress);
                if(!File.Exists(Path.Combine(o.Root,"install.json"))) throw new InvalidOperationException("Installation is incomplete. " + Installer.RecoveryStep(o.Root));
                var record=InstallRecord.Load(o.Root);
                Safety.Destinations(record,WindowsHost.RegisteredFolders());
                if(o.Command=="firewall") {
                    WindowsHost.Firewall(record,!o.Remove,o.DryRun?progress:null);
                    if(!o.DryRun) progress("Firewall rules verified."); return 0;
                }
                if(o.Command=="update") return RunUpdate(o,engine,progress);
                if(o.Command=="rollback") {
                    var result=engine.Rollback(); progress(result.Message);
                    return result.Outcome==RollbackOutcome.Done?0:1;
                }
                if(o.Command=="restore-backup") { progress(engine.RestoreBackup(o.BackupId).Message); return 0; }
                WindowsHost.RequireIdle(o.Root);
                if(o.Command=="launch") return WindowsHost.Launch(record,bundle,engine,o.AcceptWarning,progress);
                string runningFrom=Installer.UninstallProblem(o.Root,AppDomain.CurrentDomain.BaseDirectory);
                if(runningFrom!=null) throw new InvalidOperationException(runningFrom);
                string app=Path.Combine(o.Root,"app");
                if(o.Inno || Directory.Exists(app) || File.Exists(app)) Installer.Preflight(o.Root,app,WindowsHost.RegisteredFolders(),true);
                string uninstaller=Path.Combine(o.Root,"app","unins000.exe");
                if(!o.Inno && File.Exists(uninstaller)) {
                    // Inno's callback needs the intact ownership record, then removes copy/rules itself.
                    o.Uninstaller=uninstaller;
                    progress("Opening the verified SoloStone uninstaller."+(o.DeleteSaves?" Saves will be removed.":" Saves will be kept.")); return 0;
                }
                // This account's HKCU and LocalLow: the account that runs SoloStone is the one the copy wrote to, elevated or not.
                bool own=true;
                Safety.Tree(record.Client).ToArray(); // Uninstall's own walk of the copy, moved up so a link there is refused before the firewall rules go.
                string localData=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string localLow=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData","LocalLow");
                // A link in what uninstall will delete is refused here, before firewall rules or anything else is removed, so a retry is not stuck behind a half-done uninstall.
                Installer.PrecheckAccountTraces(localData,own?new WindowsRegistry():null,own?localLow:null);
                if(WindowsHost.HasRules(record)) WindowsHost.Firewall(record,false);
                Installer.Uninstall(o.Root,WindowsHost.RegisteredFolders(),localData,own?new WindowsRegistry():null,own?localLow:null);
                if(o.DeleteSaves) Installer.DeleteSaves(o.Root,WindowsHost.SaveRoot);
                progress("Copied client removed. "+(o.DeleteSaves?"Saves removed.":"Saves kept in "+WindowsHost.SaveRoot+"."));
                return 0;
            }
        }
        // A launcher outside the app folder, started by the one in it. It waits for that one to exit, because only then can its exe be replaced.
        static int FinishInHelper(Options o,WindowsUpdateHost host,Action<string> progress) {
            if(o.WaitPid!=0) {
                try { using(var old=Process.GetProcessById(o.WaitPid)) if(!old.WaitForExit(120000)) throw new InvalidOperationException("The previous launcher did not exit, so the update waits. Start SoloStone again."); }
                catch(ArgumentException) { } // Already gone.
            }
            Action<string> logged=line=>{ InstallerLog.Write(o.Root,line); progress(line); };
            using(var lease=Lease.Wait(o.Root,TimeSpan.FromSeconds(30))) {
                new UpdateEngine(o.Root,host,logged).RunHelper(o.Command,o.Version);
                o.StartAppAfter=true;
            }
            return 0;
        }
        // The player closed the wizard, so there is no copy: only the app and its state.
        static int UninstallAppOnly(Options o,Action<string> progress) {
            string runningFrom=Installer.UninstallProblem(o.Root,AppDomain.CurrentDomain.BaseDirectory);
            if(runningFrom!=null) throw new InvalidOperationException(runningFrom);
            string uninstaller=Path.Combine(o.Root,"app","unins000.exe");
            Installer.Preflight(o.Root,Path.Combine(o.Root,"app"),WindowsHost.RegisteredFolders(),true);
            if(!o.Inno && File.Exists(uninstaller)) {
                o.Uninstaller=uninstaller;
                progress("Opening the verified SoloStone uninstaller."+(o.DeleteSaves?" Saves will be removed.":" Saves will be kept.")); return 0;
            }
            Installer.UninstallAppOnly(o.Root,WindowsHost.RegisteredFolders());
            if(o.DeleteSaves) Installer.DeleteSaves(o.Root,WindowsHost.SaveRoot);
            progress("SoloStone was never set up here, so only its app files are removed."+(o.DeleteSaves?" Saves removed.":" Saves kept in "+WindowsHost.SaveRoot+"."));
            return 0;
        }
        // The only network path. It returns before any launch, so requests never overlap the game.
        static int RunUpdate(Options o,UpdateEngine engine,Action<string> progress) {
            var service=new UpdateService(o.Root,UpdateConfig.Compiled(),new WebHttp(),engine);
            if(o.CheckOnly) { progress(service.Check(CancellationToken.None).Message); return 0; }
            var result=service.Apply(o.AcceptWarning,CancellationToken.None);
            progress(result.Message);
            return result.Outcome==UpdateOutcome.Failed || result.Outcome==UpdateOutcome.Refused || result.Outcome==UpdateOutcome.NeedsConfirmation?1:0;
        }
        static int CheckPayload(Options o,string app,Action<string> progress) {
            var payload=Json.Read<Payload>(Path.Combine(app,"payload.json")); payload.Verify(Path.Combine(app,"payload"));
            string prefix=app.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string problem=PayloadRules.Problem(payload.Files.Keys,null)
                ??PayloadRules.AppProblem(Safety.Tree(app.TrimEnd(Path.DirectorySeparatorChar)).Select(p=>p.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar,'/')).Where(p=>!p.StartsWith("payload/",StringComparison.Ordinal)))
                ??ReleaseScan.PayloadProblem(app,o.ClientReference,o.HookBuild);
            if(problem!=null) throw new InvalidOperationException(problem);
            progress("Payload rules passed."); return 0;
        }
        internal static IDisposable OpenLease(string root) => Lease.Open(root);
    }
    // Progress window for the command-line style commands (setup, recover, firewall, update and so on) run without --quiet.
    internal sealed class ProgressWindow : Form
    {
        readonly Options options;
        readonly Label status=new Label { Left=20,Top=20,Width=500,Height=80,Text="SoloStone",AccessibleName="Installation progress" };
        readonly ProgressBar bar=new ProgressBar { Left=20,Top=110,Width=500,Style=ProgressBarStyle.Marquee };
        readonly Button close=new Button { Left=420,Top=160,Width=100,Text="Close",Enabled=false };
        bool running; readonly bool helper;
        internal int Result=1;
        internal ProgressWindow(Options options,bool helper=false) {
            this.options=options; this.helper=helper;
            if(helper) { TopMost=true; status.Text=Messages.UpdateFinishing; }
            Text="SoloStone"; Width=560; Height=250; FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen;
            Controls.AddRange(new Control[]{status,bar,close}); close.Click+=(s,e)=>Close();
            FormClosing+=(s,e)=> { if(running) e.Cancel=true; };
            Shown+=async(s,e)=>await Start();
        }
        // A re-click asked to see this window.
        internal void Raise() { if(IsHandleCreated) BeginInvoke((Action)(()=>{ if(WindowState==FormWindowState.Minimized) WindowState=FormWindowState.Normal; Activate(); })); }
        void Progress(string message) { if(IsHandleCreated) BeginInvoke((Action)(()=>status.Text=message)); }
        async Task Start() {
            if(options.Command=="setup" && options.Source==null) {
                string[] found=WindowsHost.FindClients();
                using(var picker=new FolderBrowserDialog { Description="Select your own Hearthstone client. SoloStone will only read it.", SelectedPath=found.FirstOrDefault()??"" }) {
                    if(picker.ShowDialog(this)!=DialogResult.OK) { Close(); return; }
                    options.Source=picker.SelectedPath;
                }
            }
            if(options.DeleteSaves && MessageBox.Show(this,"Permanently delete all saves in "+WindowsHost.SaveRoot+"?","Delete saves",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) { Close(); return; }
            running=true;
            try { Result=await Task.Run(()=>Program.Execute(options,Progress)); }
            catch(Exception e) { status.Text=Program.Friendly(e); Result=1; }
            finally { running=false; bar.Style=ProgressBarStyle.Continuous; bar.Value=Result==0?100:0; close.Enabled=true; close.Focus(); }
            // A helper that finished opens the launcher; one that failed stays, because nothing else can say why.
            if(helper && Result==0) Close();
        }
    }
}

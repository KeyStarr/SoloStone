using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using System.Threading;
using SoloStone;

// The third review's findings.
static partial class Program
{
    static string Parts(Fixture f) { var lines=Directory.GetFiles(f.Client,"*",SearchOption.AllDirectories).Where(p=>!p.Contains("Hearthstone_Data") && !p.EndsWith(InstallRecord.Marker,StringComparison.Ordinal)).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>p.Substring(f.Client.Length)+"="+Hashes.FileHash(p)); return AppHash(f)+"|"+string.Join(",",lines); }
    // A kill anywhere in "newer release over a pending one": exactly one whole version, and the right rollback target.
    static void PendingOverNewer(string temp) {
        var clean=NewFixture(temp); Update(clean,"2.0.0"); var built=BuildRelease(clean,"3.0.0"); clean.Engine().Stage(ZipOf(clean,"3.0.0"),built); clean.Engine().Apply(built,false);
        string third=Parts(clean); FsOps.DeleteTree(clean.Base);
        int crashes=0;
        for(int point=0;;point++) {
            var e=NewFixture(temp); string first=e.Code, saves=TreeHash(e.Saves);
            Check(Update(e,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap"); string second=e.Code;
            var newer=BuildRelease(e,"3.0.0"); e.Engine().Stage(ZipOf(e,"3.0.0"),newer);
            int seen=0; bool crashed=false;
            var engine=e.Engine(label=>{ ExeInPlace(e); if(seen++==point) { crashed=true; throw new SimulatedCrash(); } });
            try { engine.Apply(newer,false); } catch(SimulatedCrash) { }
            if(!crashed) { Check(Parts(e)==third,"no kill: the newer release is fully in place"); FsOps.DeleteTree(e.Base); break; }
            crashes++;
            e.Engine().Resume();
            string version=InstallRecord.Load(e.Root).ReleaseVersion; var journal=e.Engine().Journal;
            if(version=="1.0.0") Check(e.Code==first && journal==null,"a kill left exactly the last good tree, point "+point);
            else if(version=="2.0.0") Check(e.Code==second && journal?.From=="1.0.0" && journal.Phase==SwapJournal.Swapped && File.Exists(Path.Combine(e.Root,"releases","1.0.0","app","SoloStone.exe")),"a kill left exactly the pending tree and its rollback target, point "+point);
            else {
                Check(version=="3.0.0" && Parts(e)==third,"a kill left exactly the newer tree, point "+point);
                Check(journal?.From=="1.0.0" && journal.Phase==SwapJournal.Swapped && Directory.GetDirectories(Path.Combine(e.Root,"releases")).Select(Path.GetFileName).SequenceEqual(new[]{"1.0.0"}),"the rollback target is the last good version, point "+point);
            }
            Check(TreeHash(e.Saves)==saves,"saves untouched, point "+point);
            FsOps.DeleteTree(e.Base);
        }
        Check(crashes>60,"every step of restore-then-swap was faulted: "+crashes);
        // And when it fails too, the last good version returns.
        var g=NewFixture(temp); string good=g.Code; Update(g,"2.0.0"); Update(g,"3.0.0");
        Check(g.Engine().ReportHealth(HealthOutcome.Failed,"exit 42").Restored && g.Code==good && g.Engine().Held().Version=="3.0.0","if it fails too, the last good version returns and only it is held");
    }
    // Item 1: setup's launcher against a live helper.
    static void LeaseRace(string temp) {
        var f=NewFixture(temp); var release=BuildRelease(f,"2.0.0"); f.Engine().Stage(ZipOf(f,"2.0.0"),release);
        int refused=0; string live=null;
        // While the helper is mid-swap, holding the lease as it does in production, setup's launcher tries to recover at every fourth step.
        int seen=0;
        Action<string> attempt=label=>{
            if(seen++%4!=0 || !File.Exists(Path.Combine(f.Root,"updates","journal.json"))) return;
            var fault=f.Host.Fault; f.Host.Fault=null; bool was=f.Host.InApp; f.Host.InApp=false;
            try {
                live=f.Engine().Journal?.Phase;
                RejectsBecause(()=>Installer.ResumeInterrupted(f.Root,()=>new UpdateEngine(f.Root,f.Host)),"finishing an update","setup's launcher waits for a live helper");
                refused++;
            } finally { f.Host.Fault=fault; f.Host.InApp=was; }
        };
        using(Lease.Open(f.Root)) {
            var engine=f.Engine(attempt);
            Check(engine.Apply(release,false).Outcome==UpdateOutcome.Swapped,"apply");
        }
        Check(refused>10 && f.Host.HelperResult.Outcome==UpdateOutcome.Swapped,"every attempt was refused and the swap finished: "+refused);
        Check(InstallRecord.Load(f.Root).ReleaseVersion=="2.0.0" && File.ReadAllText(Path.Combine(f.App,"SoloStone.exe"))=="launcher 2.0.0","record and files agree after the race");
        InstallRecord.Load(f.Root).Payload.Verify(f.Client);
        // With no helper it recovers, and a helper that starts just after waits for the lease.
        var g=NewFixture(temp); string old=g.Code; var r2=BuildRelease(g,"2.0.0"); g.Engine().Stage(ZipOf(g,"2.0.0"),r2);
        var cut=g.Engine(label=>{ if(label=="start:12") throw new SimulatedCrash(); });
        Throws<SimulatedCrash>(()=>cut.Apply(r2,false),"kill");
        g.Host.InApp=false; g.Host.Fault=null; Installer.ResumeInterrupted(g.Root,()=>new UpdateEngine(g.Root,g.Host)); g.Host.InApp=true;
        Check(g.Code==old,"with no helper, setup's launcher recovers");
        using(Lease.Open(g.Root)) Refuses(()=>Lease.Wait(g.Root,TimeSpan.FromMilliseconds(300)),"a lease that stays busy times out");
        using(Lease.Wait(g.Root,TimeSpan.FromSeconds(1))) { } Check(true,"a free lease is taken at once");
    }
    // Item 2: the helper's job and its failure.
    static void HelperBehaviour(string temp) {
        var f=NewFixture(temp); var release=BuildRelease(f,"2.0.0"); var e=f.Engine(); e.Stage(ZipOf(f,"2.0.0"),release);
        // A real failure while restoring: reported, kept, and no reopening.
        var a=NewFixture(temp); Check(Update(a,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap");
        var journal=a.Engine().Journal; journal.Phase=SwapJournal.Undoing; File.WriteAllBytes(Path.Combine(a.Root,"updates","journal.json"),JsonBytes.ToBytes(journal));
        a.Host.InApp=false;
        a.Host.Fault=null;
        var failing=new UpdateEngine(a.Root,a.Host) { Fault=label=>{ if(label=="undo:moved") throw new IOException("disk gone"); } };
        RejectsBecause(()=>failing.RunHelper("finish-undo",null),"could not finish its update","a failed helper reports in words");
        Check(failing.LastNote().Message.Contains("could not finish") && failing.JournalActive,"it leaves a note and the journal to retry");
        failing.Fault=null;
        failing.RunHelper("finish-undo",null);
        Check(!failing.JournalActive && failing.LastNote()==null && InstallRecord.Load(a.Root).ReleaseVersion=="1.0.0","a successful retry clears the note");
        // Arguments the launcher hands off are what the helper parses.
        string root=Path.Combine(temp,"a root with spaces");
        Func<string,string[]> split=line=>Regex.Matches(line,"\"([^\"]*)\"|(\\S+)").Select(m=>m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Value).ToArray();
        var swap=Options.Parse(split(Options.HelperArguments("finish-swap",root,"2.0.0",4242)));
        Check(swap.Command=="finish-swap" && swap.Version=="2.0.0" && swap.Root==root && swap.WaitPid==4242 && !swap.Quiet,"finish-swap round-trips, and is not quiet: it has a window");
        var undo=Options.Parse(split(Options.HelperArguments("finish-undo",root,null,7)));
        Check(undo.Command=="finish-undo" && undo.Version==null && undo.WaitPid==7,"finish-undo round-trips");
        // A helper's log survives where a player and an agent can read it.
        InstallerLog.Write(f.Root,"a line");
        Check(File.ReadAllText(InstallerLog.PathOf(f.Root)).Contains("a line"),"the log is written");
        string host=Source("Gui/GuiHost.cs");
        Check(host.Contains("if (SingleInstance.HelperRunning(options.Root)) { SingleInstance.RaiseHelper(options.Root); return 0; }") && !Source("WindowsHost.cs").Contains("CreateNoWindow=true, WorkingDirectory=Path.GetDirectoryName(exe)"),"a re-click leaves at once, so its image can't block the replace");
    }
    // Item 4: the exe gone, at every step, and only setup's launcher left to recover with.
    static void ExeMissingMatrix(string temp) {
        int recovered=0, refused=0;
        for(int point=0;;point++) {
            var f=NewFixture(temp); string old=f.Code; var release=BuildRelease(f,"2.0.0");
            int seen=0; bool crashed=false; var engine=f.Engine(label=>{ if(seen++==point) { crashed=true; throw new SimulatedCrash(); } });
            engine.Stage(ZipOf(f,"2.0.0"),release);
            try { engine.Apply(release,false); } catch(SimulatedCrash) { }
            if(!crashed) { FsOps.DeleteTree(f.Base); break; }
            File.Delete(Path.Combine(f.App,"SoloStone.exe"));
            f.Host.InApp=false; f.Host.Fault=null;
            bool started=f.Engine().JournalActive;
            try {
                Installer.ResumeInterrupted(f.Root,()=>new UpdateEngine(f.Root,f.Host));
                if(!started) { refused++; FsOps.DeleteTree(f.Base); continue; } // Before any journal, nothing of ours moved; only a reinstall brings the exe back.
                ExeInPlace(f);
                string version=InstallRecord.Load(f.Root).ReleaseVersion;
                Check(version=="1.0.0"?f.Code==old:File.ReadAllText(Path.Combine(f.App,"SoloStone.exe"))=="launcher 2.0.0","with the exe gone, setup's launcher restores one whole version, point "+point);
                recovered++;
            }
            catch(InvalidOperationException e) {
                // Only the instant before the previous launcher was copied.
                Check(e.Message.Contains(InstallRecord.StartOver) && !File.Exists(Path.Combine(f.Root,"releases","1.0.0","app","SoloStone.exe")),"only with no copy of the old launcher does it refuse, in words, point "+point+": "+e.Message);
                refused++;
            }
            FsOps.DeleteTree(f.Base);
        }
        Check(recovered>100 && refused<12,"nearly every step recovers: "+recovered+" recovered, "+refused+" refused");
        // Same with a pending first start and after a restore began.
        var p=NewFixture(temp); Check(Update(p,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap"); File.Delete(Path.Combine(p.App,"SoloStone.exe"));
        p.Host.InApp=false; Check(p.Engine().Resume()==ResumeOutcome.Undone && File.ReadAllText(Path.Combine(p.App,"SoloStone.exe"))=="launcher 1.0.0","a pending release with its exe gone and nothing staged goes back to the kept launcher");
        var q=NewFixture(temp); Check(Update(q,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap"); q.Engine().ReportHealth(HealthOutcome.Confirmed,null); File.Delete(Path.Combine(q.App,"SoloStone.exe"));
        Check(q.Engine().Rollback().Outcome==RollbackOutcome.NoPrevious && q.Engine().PreviousVersion()=="1.0.0","a rollback with the exe gone refuses in words instead of throwing");
        // Publishing replaces, and leaves no temp.
        string dir=Path.Combine(temp,"publish"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir,"a.tmp"),"new"); File.WriteAllText(Path.Combine(dir,"a"),"old");
        Atomic.Publish(Path.Combine(dir,"a.tmp"),Path.Combine(dir,"a"));
        Check(File.ReadAllText(Path.Combine(dir,"a"))=="new" && !File.Exists(Path.Combine(dir,"a.tmp")),"publish replaces");
        File.WriteAllText(Path.Combine(dir,"b.tmp"),"new"); Atomic.Publish(Path.Combine(dir,"b.tmp"),Path.Combine(dir,"b")); Check(File.ReadAllText(Path.Combine(dir,"b"))=="new","publish also creates");
        string fs=Source("Fs.cs"), engineSource=Source("UpdateEngine.cs");
        Check(fs.Contains("MoveFileEx(temp,target,1|8)") && !Regex.IsMatch(engineSource,@"File\.Replace\("),"MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH, and no File.Replace in the swap");
        Check(Source("SoloStone.iss").Contains("FindLauncher(App, Root)"),"uninstall finds a launcher when the app's is gone");
    }
    // Item 3: plugin setup errors. Item 9: 8.3 launch path. Item 10: messages.
    static void SmallFindings(string temp) {
#if SOLOSTONE_INSTALLER
        Func<Action,int> exit=step=>{ try { InstallerRuntime.Prepare(step); return 0; } catch(InstallerRefusal r) { return r.ExitCode; } };
        Check(exit(()=>throw new IOException("disk full"))==Health.ExitSetupFailed && exit(()=>throw new System.Xml.XmlException("bad save"))==Health.ExitSetupFailed && exit(()=>throw new InvalidDataException("bad"))==Health.ExitSetupFailed,"a disk or save problem during startup is not a hook failure");
        Check(exit(()=>throw new InstallerRefusal(47,"firewall"))==47 && exit(()=>{})==0,"a refusal keeps its own code");
        Check(InstallerRuntime.ExitSetupFailed==Health.ExitSetupFailed && Health.Classify(false,false,Health.ExitSetupFailed)==HealthOutcome.NoEvidence,"it neither restores nor holds");
        var a=NewFixture(temp); Check(Update(a,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap"); string kept=a.Code;
        Check(!a.Engine().ReportHealth(Health.Classify(false,false,49),"x").Restored && a.Code==kept && a.Engine().Held()==null,"a full disk at first start restores nothing");
        string probe=PluginSource("OfflineProbe.cs");
        Check(probe.Contains("InstallerRuntime.Prepare(() => OfflineGuard.ResolveHeldLaunch(") && probe.Contains("InstallerRuntime.Prepare(launch.ResolveSaves)") && probe.Contains("InstallerRuntime.Prepare(() => OfflineGuard.SeedProfileIfMissing(") && Regex.IsMatch(probe,@"InstallerRuntime\.Prepare\(\(\) => File\.WriteAllText\(Path\.Combine\(Reports, ""plugin-loaded\.txt"""),"resolve, seed and marker are setup steps, behind the symbol");
#endif
        // Item 8.
        string program=Source("Program.cs");
        Check(program.Contains("if(engine.Resume()==ResumeOutcome.HandedOff) { progress(Messages.UpdateFinishing); return 0; }"),"the CLI stops when a helper took over");
        // Item 9.
        var real=PhysicalPaths.LongName;
        try {
            PhysicalPaths.LongName=p=>p.Replace("SOLOST~1","SoloStone");
            Check(PhysicalPaths.SameFolder(@"E:\Games\SOLOST~1\app",@"E:\Games\SoloStone\app") && PhysicalPaths.SameFolder(Path.Combine(temp,"x")+Path.DirectorySeparatorChar,Path.Combine(temp,"x")) && !PhysicalPaths.SameFolder(@"C:\a\app",@"C:\a\apps"),"a short launch path is still the app folder");
        } finally { PhysicalPaths.LongName=real; }
        // Item 10.
        Check(Tls.Describe(new IOException("disk full")).Contains("available space") && !Tls.Describe(new UnauthorizedAccessException()).Contains("network") && Tls.Describe(new TimeoutException()).Contains("not reachable"),"a disk error is not reported as the network");
        foreach(var (file,flag) in new[]{("NativePlaytest.cs","enable-playtest"),("NativePlaytest.cs","benchmark-ranks"),("NativeGuideCards.cs","guide-cards-fail")}) {
            string released=Regex.Replace(PluginSource(file),@"#if !SOLOSTONE_INSTALLER \|\| SOLOSTONE_TEST_HOOKS.*?#endif","",RegexOptions.Singleline);
            released=Regex.Replace(released,@"#else.*?#endif","",RegexOptions.Singleline);
            Check(!released.Contains(flag) || file=="NativeGuideCards.cs","no read of "+flag+" outside the symbol in "+file);
        }
        // Item 7.
        var ops=new FakeLauncherOps { AfterHelper=true }; ops.Report.UpdateNote="The update could not be installed.";
        var model=new LauncherModel(); var runner=new LauncherRunner(model,ops,x=>x());
        runner.Start(CancellationToken.None);
        Check(model.Notice.Contains("could not be installed"),"the note shows at start");
        model.LaunchEnded(1,"Fix the firewall protection, then play."); model.ApplyReport(ops.Report);
        Check(model.Notice.Contains("firewall") && model.FirewallProblem,"a later status read does not talk over the firewall message");
        model.Restored(new RestoreResult { Message="Your saves are back." }); model.ApplyReport(ops.Report);
        Check(model.Notice.Contains("saves are back"),"or over a restore message");
    }
    // Item 11: Core as a player really gets it, from the built release launcher.
    static void ReleaseCore() {
        string launcher=FindOut("out","app","SoloStone.exe");
        if(launcher==null) { Skip("release Core check: installer/out/app is not built"); return; }
        var context=new AssemblyLoadContext("release-core",true);
        try {
            var assembly=context.LoadFromAssemblyPath(launcher);
            Check(assembly.GetType("SoloStone.LabGuard")==null,"the release launcher has no lab guard type");
            var canonical=assembly.GetType("SoloStone.Safety").GetMethod("Canonical",BindingFlags.Public|BindingFlags.Static);
            foreach(string path in new[]{@"D:\Battle"+@"Net\Hearthstone",@"D:\x\..\Battle"+@"Net\Hearthstone",@"C:\Us"+@"ers\HSOffline"+@"Probe"}) {
                string message=null;
                try { canonical.Invoke(null,new object[]{path}); }
                catch(TargetInvocationException e) { message=e.InnerException.Message; }
                Check(message==null || !message.Contains("protected lab"),"a release has no lab rule for "+path);
            }
            // The same folder, spelled so this machine accepts it, passes the real release code.
            string here=Path.Combine(Path.GetTempPath(),"BattleNet","Hearthstone");
            Check(((string)canonical.Invoke(null,new object[]{here})).EndsWith("Hearthstone",StringComparison.Ordinal),"a player's folder of that name passes release Core");
        } finally { context.Unload(); }
    }
    static void Round3Tests(string temp,string source,string bundle,TestedClient tested,string saves) {
        LeaseRace(temp);
        HelperBehaviour(temp);
        ExeMissingMatrix(temp);
        SmallFindings(temp);
        ReleaseCore();
        string iss=Source("SoloStone.iss");
        Check(iss.Contains("if not UninstallSilent then") && iss.Contains("Anything you added to the copy folder is deleted"),"uninstalling from Windows Settings warns; silent runs from the launcher window warned already");
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SoloStone;

namespace HarmonyLib { static class FakePatcher { internal static void Patch() => throw new NullReferenceException("a patch target was null"); } }

static partial class Program
{
    static void FinalRoundHealth(string temp) {
#if SOLOSTONE_INSTALLER
        // Item 1: Awake's last word. Only a hook that cannot exist is the mod failing for good.
        Func<Exception,int> code=InstallerRuntime.ExitCodeFor;
        Check(code(new NativeCardMismatchException("mismatch"))==Health.ExitHooks,"a card mismatch holds the release");
        var drift=new NativeCardDrift();
        drift.Add("A","COST",1,2); drift.Add("A","ATK",3,4); drift.Add("B","COST",2,2);
        string driftPath=Path.Combine(temp,"drift.tsv"); drift.Write(driftPath);
        Check(drift.Cards==1 && drift.Fields==2 && File.ReadAllLines(driftPath).SequenceEqual(new[]{"id\tfield\tretail\tfacts","A\tCOST\t1\t2","A\tATK\t3\t4"}),"drift counts cards and changed fields");
        drift.Write(Path.Combine(temp,"missing-directory","drift.tsv"));
        Exception thrown=null; try { HarmonyLib.FakePatcher.Patch(); } catch(Exception e) { thrown=e; }
        foreach(var hook in new Exception[]{new MissingMethodException("Game","Gone"),new MissingFieldException("Game","Gone"),new TypeLoadException("gone"),new EntryPointNotFoundException("gone"),
            new TypeInitializationException("Probe",new MissingMethodException("Game","Gone")),new TargetInvocationException(new TypeLoadException("gone")),
            new FileNotFoundException("Could not load file or assembly","Assembly-CSharp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"),thrown})
            Check(code(hook)==Health.ExitHooks,"a hook that cannot exist is exit 42: "+hook.GetType().Name);
        foreach(var disk in new Exception[]{new IOException("There is not enough space on the disk."),new UnauthorizedAccessException("locked"),new TypeInitializationException("Config",new IOException("locked")),
            new TargetInvocationException(new UnauthorizedAccessException("locked")),new PathTooLongException("long"),new DriveNotFoundException("gone")})
            Check(code(disk)==Health.ExitSetupFailed,"the disk says nothing about the release: "+disk.GetType().Name);
        // Not a hook and not this PC's disk: it may be the release, so it is restored at once and retried a day later, and a second time it holds for good.
        foreach(var other in new Exception[]{new NullReferenceException(),new ArgumentException("bad"),new InvalidOperationException("bad"),new KeyNotFoundException("bad"),new FormatException("bad"),
            new FileNotFoundException("no such data file","catalog.json"),new DirectoryNotFoundException("no data folder"),new TypeInitializationException("Catalog",new NullReferenceException())}) {
            Check(code(other)==Health.ExitStartupError,"anything else is a startup error: "+other.GetType().Name);
            Check(Health.Classify(false,false,code(other))==HealthOutcome.RetryLater,"which restores and retries later");
        }
        Check(code(new InstallerRefusal(47,"firewall"))==47 && code(new TypeInitializationException("Guard",new InstallerRefusal(48,"refused")))==48,"a refusal keeps its own code");
        Check(InstallerRuntime.ExitStartupError==Health.ExitStartupError,"plugin and launcher agree on the code");
        Check(Health.Classify(false,false,Health.ExitHooks)==HealthOutcome.Failed,"a missing hook still holds for good at once");
        string probe=PluginSource("OfflineProbe.cs");
        int quit=probe.IndexOf("Offline probe initialization failed",StringComparison.Ordinal);
        string tail=probe.Substring(quit,300);
        Check(tail.Contains("InstallerRuntime.ExitCodeFor(error)") && tail.IndexOf("#if SOLOSTONE_INSTALLER",StringComparison.Ordinal)<tail.IndexOf("Application.Quit(42)",StringComparison.Ordinal),"Awake's catch uses it under the symbol, and only there");
#endif
        // A failed helper copy spends no try, and leaves no reason behind.
        var a=NewFixture(temp); Check(Update(a,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap"); string swapped=a.Code;
        var failing=a.Engine(label=>{ if(label=="helper:copied") throw new IOException("disk full"); });
        Throws<IOException>(()=>failing.ReportHealth(HealthOutcome.RetryLater,"exit 1"),"the helper copy fails");
        var open=a.Engine().Journal;
        Check(open.Phase==SwapJournal.Swapped && open.Retries==0 && open.HoldReason==null && open.HoldRetryAfter==null && a.Code==swapped,"a failed helper copy counts nothing");
        Check(a.Engine().ReportHealth(HealthOutcome.RetryLater,"exit 1").Restored,"the next failure restores");
        var held=a.Engine().Held();
        Check(held.Version=="2.0.0" && held.Retries==1 && held.RetryAfter!=null,"and it is the first of two tries, not a permanent hold with nothing restored");
        // A kill before the restore starts is not a game failure either.
        var k=NewFixture(temp); Check(Update(k,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap");
        Throws<SimulatedCrash>(()=>k.Engine(label=>{ if(label=="helper:copied") throw new SimulatedCrash(); }).ReportHealth(HealthOutcome.Failed,"hooks"),"killed before the restore");
        Check(k.Engine().Journal.Retries==0 && k.Engine().Journal.HoldReason==null,"nothing counted");
        // Once the restore has started the count is durable: a kill there still holds.
        var d=NewFixture(temp); string dGood=d.Code; Check(Update(d,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap");
        Throws<SimulatedCrash>(()=>d.Engine(label=>{ if(label=="helper:ready") throw new SimulatedCrash(); }).ReportHealth(HealthOutcome.RetryLater,"exit 1"),"killed with the helper ready");
        Check(d.Engine().Journal.Phase==SwapJournal.Undoing && d.Engine().Journal.Retries==1,"the count went in with the restore");
        d.Engine().Resume();
        Check(d.Code==dGood && d.Engine().Held().Retries==1 && d.Engine().Held().RetryAfter!=null,"and the restore finishes with its hold");
        // A rollback is for good, whatever the journal carried.
        var r=NewFixture(temp); Check(Update(r,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap");
        var stale=r.Engine().Journal; stale.HoldReason="old"; stale.HoldRetryAfter=DateTime.UtcNow.AddHours(1).ToString("o"); stale.Retries=1; Atomic.Save(Path.Combine(r.Root,"updates","journal.json"),stale);
        Check(r.Engine().Rollback().Outcome==RollbackOutcome.Done,"rollback");
        Check(r.Engine().Held().RetryAfter==null && r.Engine().Held().BlocksAt(DateTime.UtcNow.AddYears(1)),"a stale retry time does not survive into a rollback");
        // Confirming clears what a failed restore attempt could have left.
        var c=NewFixture(temp); Check(Update(c,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap");
        var left=c.Engine().Journal; left.HoldReason="old"; left.HoldRetryAfter="2000-01-01T00:00:00.0000000Z"; Atomic.Save(Path.Combine(c.Root,"updates","journal.json"),left);
        c.Engine().ReportHealth(HealthOutcome.Confirmed,null);
        var kept=Atomic.TryRead<SwapJournal>(Path.Combine(c.Root,"releases","1.0.0","rollback.json"));
        Check(kept.HoldReason==null && kept.HoldRetryAfter==null,"a confirmed release keeps no hold reason");
        // Item 4: the second try gets longer, the first does not.
        var s=NewFixture(temp); Check(s.Engine().MarkerSeconds()==Health.FirstStartSeconds && Health.FirstStartSeconds==120,"no update open: the normal wait");
        Check(Update(s,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap"); Check(s.Engine().MarkerSeconds()==Health.FirstStartSeconds,"the first try: the normal wait");
        s.Engine().ReportHealth(HealthOutcome.RetryLater,"timed out");
        var second=BuildRelease(s,"2.0.0"); var engine=s.Engine(); engine.Stage(ZipOf(s,"2.0.0"),second); engine.Apply(second,false);
        Check(s.Engine().Journal.Retries==1 && s.Engine().MarkerSeconds()==Health.RetryStartSeconds && Health.RetryStartSeconds==300,"the second try waits longer");
        Check(Source("WindowsHost.cs").Contains("engine.MarkerSeconds()"),"the launch uses it");
        // Messages say only what is true.
        string messages=Source("Gui/Messages.cs");
        Check(!messages.Contains("this window. Your game and saves were not changed"),"a window error makes no claim about saves");
    }
    // Item 3: the way out a message names must exist.
    static void FinalRoundRecovery(string temp) {
        foreach(string name in new[]{"Core.cs","CoreLauncher.cs","UpdateEngine.cs","ClientPolicy.cs","WindowsHost.cs","Install.cs","Program.cs","Gui/Messages.cs","Gui/Texts.cs","Gui/LauncherModel.cs"})
            Check(Source(name).IndexOf("reinstall",StringComparison.OrdinalIgnoreCase)<0,"no message offers a reinstall setup refuses: "+name);
        var f=NewFixture(temp); string saved=File.ReadAllText(Path.Combine(f.Saves,"profile.xml"));
        var latest=BuildRelease(f,"2.0.0"); var cut=f.Engine(label=>{ if(label=="start:5") throw new SimulatedCrash(); }); cut.Stage(ZipOf(f,"2.0.0"),latest);
        Throws<SimulatedCrash>(()=>cut.Apply(latest,false),"killed part way through the moves");
        string journal=Path.Combine(f.Root,"updates","journal.json");
        File.WriteAllText(journal,"{ damaged");
        Func<Action,string> said=step=>{ try { step(); return null; } catch(InvalidOperationException e) { return e.Message; } };
        f.Host.Fault=null;
        Check(said(()=>f.Engine().Resume()).Contains(InstallRecord.StartOver),"a damaged journal names the way out");
        Check(said(()=>Installer.ResumeInterrupted(f.Root,()=>f.Engine())).Contains(InstallRecord.StartOver),"setup's launcher says the same");
        Check(said(()=>f.Engine().RequireReadableJournal()).Contains(InstallRecord.StartOver),"and Play refuses before a game starts");
        Check(StatusReport.Build(f.Root,f.Engine(),TestedClient.Compiled(),UpdateConfig.Compiled(),new NoBindingManifest(),new string[0])!=null,"the window can still show its buttons");
        Installer.Preflight(f.Root,f.App,new string[0],true);
        Installer.Uninstall(f.Root,new string[0]);
        FsOps.DeleteTree(f.App); // Inno's part.
        Check(!File.Exists(journal) && !File.Exists(Path.Combine(f.Root,"install.json")) && !Directory.Exists(f.Client),"uninstall works with a damaged journal and half-moved files");
        Check(File.ReadAllText(Path.Combine(f.Saves,"profile.xml"))==saved,"saves are kept");
        Installer.ResumeInterrupted(f.Root,()=>f.Engine());
        Installer.Preflight(f.Root,f.App,new string[0],false);
        count++; // Setup's own preflight passes, so the message is true end to end.
        // The launcher window keeps Uninstall when its start fails on the journal.
        var ops=new FakeLauncherOps { ResumeFails=new InvalidOperationException("The update journal is damaged. "+InstallRecord.StartOver) };
        var model=new LauncherModel(); new LauncherRunner(model,ops,Inline).Start(System.Threading.CancellationToken.None);
        Check(model.CanUninstall && model.Notice.Contains(InstallRecord.StartOver),"the way out is on screen, with its button");
        // A launcher started mid-swap finds every library at every step, without its config.
        int moves=0;
        for(int point=0;;point++) {
            var m=NewFixture(temp);
            string before=File.ReadAllText(Path.Combine(m.App,"OldOnly.dll")), bouncy=File.ReadAllText(Path.Combine(m.App,"BouncyCastle.Cryptography.dll"));
            var release=BuildRelease(m,"2.0.0"); bool reached=false;
            var engine=m.Engine(label=>{ if(label=="start:"+point) { reached=true; throw new SimulatedCrash(); } }); engine.Stage(ZipOf(m,"2.0.0"),release);
            try { engine.Apply(release,false); } catch(SimulatedCrash) { }
            if(!reached) { FsOps.DeleteTree(m.Base); break; }
            moves++;
            string old=MovedLibraries.Find(m.App,"OldOnly"), library=MovedLibraries.Find(m.App,"BouncyCastle.Cryptography");
            Check(old!=null && File.ReadAllText(old)==before,"the old library is found at move "+point);
            Check(library!=null && File.ReadAllText(library)==bouncy,"a library in both releases is found at move "+point);
            Check(MovedLibraries.Find(m.App,"NoSuchLibrary")==null && MovedLibraries.Find(m.App,"OldOnly.resources")==null && MovedLibraries.Find(m.App,"..\\OldOnly")==null,"nothing else is");
            FsOps.DeleteTree(m.Base);
        }
        Check(moves>20,"the walk covered the move list: "+moves);
        var g=NewFixture(temp); string gOld=Path.Combine(g.App,"OldOnly.dll"); var rel=BuildRelease(g,"2.0.0");
        var stop=g.Engine(label=>{ if(label.StartsWith("start:",StringComparison.Ordinal) && !File.Exists(gOld)) throw new SimulatedCrash(); }); stop.Stage(ZipOf(g,"2.0.0"),rel);
        Throws<SimulatedCrash>(()=>stop.Apply(rel,false),"stopped with the old library moved");
        string moved=MovedLibraries.Find(g.App,"OldOnly");
        Check(moved!=null && moved.Contains(Path.Combine("releases","1.0.0")),"it comes from where the journal says");
        File.WriteAllText(moved,"changed"); Check(MovedLibraries.Find(g.App,"OldOnly")==null,"a changed copy is not loaded");
        File.WriteAllText(Path.Combine(g.Root,"updates","journal.json"),"{ damaged"); Check(MovedLibraries.Find(g.App,"OldOnly")==null,"and a damaged journal only means not found");
        string program=Source("Program.cs");
        Check(program.Contains("MovedLibraries.Find(") && program.IndexOf("AssemblyResolve",StringComparison.Ordinal)<program.IndexOf("Options.Parse",StringComparison.Ordinal),"the resolver is in place before anything else runs");
        string runner=Source("Gui/LauncherModel.cs");
        Check(runner.IndexOf("ops.Resume()",StringComparison.Ordinal)<runner.IndexOf("ops.Status()",StringComparison.Ordinal),"Resume runs before the status read, the first thing that uses a library");
    }
    static void FinalRoundRegistry(string temp) {
        // Item 5: our uid, and only ours, goes at uninstall.
        var g=NewFixture(temp); string local=Path.Combine(g.Base,"local"), hearthstone=Path.Combine(local,"Blizzard","Hearthstone");
        foreach(string uid in new[]{"SoloStone","HSNativeUIPrototype","HSNativeUIPrototypeLane1","SoloStone2","RetailUid"}) WriteRel(hearthstone,uid+"/options.txt","x");
        WriteRel(hearthstone,"top.txt","x");
        Installer.Uninstall(g.Root,new string[0],local);
        Check(!Directory.Exists(Path.Combine(hearthstone,"SoloStone")),"our options folder is removed");
        foreach(string keep in new[]{"HSNativeUIPrototype","HSNativeUIPrototypeLane1","SoloStone2","RetailUid"}) Check(File.Exists(Path.Combine(hearthstone,keep,"options.txt")),"not "+keep);
        Check(File.Exists(Path.Combine(hearthstone,"top.txt")),"nor its parent's files");
        Installer.RemoveGameOptions(local); count++; // Nothing there any more: no error.
        var h=NewFixture(temp); string target=Path.Combine(h.Base,"elsewhere-options"); Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target,"keep.txt"),"x");
        string linked=Path.Combine(h.Base,"local","Blizzard","Hearthstone"); Directory.CreateDirectory(linked); LinkDirectory(Path.Combine(linked,"SoloStone"),target);
        Refuses(()=>Installer.Uninstall(h.Root,new string[0],Path.Combine(h.Base,"local")),"a linked options folder stops uninstall before anything is deleted");
        Check(File.Exists(Path.Combine(target,"keep.txt")) && Directory.Exists(h.Client),"and nothing moved");
        // The launch uses it, and nothing is left of the settings guard.
        string host=Source("WindowsHost.cs");
        Check(host.Contains("-uid \"+ClientIdentity.GameUid") && !host.Contains("HSNativeUIPrototype") && ClientIdentity.GameUid=="SoloStone","the installed launcher owns its uid");
        Check(!host.Contains("Guard(") && !host.Contains("Watched") && !host.Contains("watch") && !File.Exists(Path.Combine(AppContext.BaseDirectory,"launcher-source","RegistryGuard.cs")),"the launch no longer saves, polls or restores a registry key");
        Check(!host.Contains("D:\\\\"),"no read of the player's install folder");
        var off=new FakeLauncherOps { Resumed=ResumeOutcome.HandedOff }; new LauncherRunner(new LauncherModel(),off,Inline).Start(System.Threading.CancellationToken.None);
        Check(off.Calls.Contains("Resume") && !off.Calls.Contains("Status"),"a launcher that hands off to a helper reads no status and offers nothing");
        Check(!Source("Program.cs").Contains("BeforeUninstall") && Source("Program.cs").Contains("LocalApplicationData"),"uninstall handles no saved registry copy, and passes the account's folders");
    }
    // The check every release must pass before it is published.
    static void FinalRoundRelease(string temp) {
        string Token(params string[] parts) => string.Concat(parts);
        int n=0;
        Func<Action<string,Dictionary<string,byte[]>>,bool,string> build=(change,setup)=>{
            string folder=Path.Combine(temp,"rel"+(++n)); Directory.CreateDirectory(folder);
            var entries=new Dictionary<string,byte[]>{ ["SoloStone.exe"]=new byte[]{1,2,3}, ["NOTICE.txt"]=new byte[]{65}, ["release.json"]=System.Text.Encoding.ASCII.GetBytes("{\"schema\":1,\"version\":\"1.2.3\",\"saveSchema\":15,\"sourceRevision\":\"r\"}"),
                ["payload/BepInEx/plugins/SoloStone.Plugin.dll"]=new byte[]{4,5,6} };
            foreach(string licence in ReleaseScan.LicenceFiles) entries[licence]=System.Text.Encoding.ASCII.GetBytes("licence text");
            if(setup) File.WriteAllBytes(Path.Combine(folder,"SoloStone-Setup-1.2.3.exe"),new byte[]{7});
            change(folder,entries);
            string zip=Path.Combine(folder,"SoloStone-1.2.3.zip");
            if(entries.Count>0) using(var archive=System.IO.Compression.ZipFile.Open(zip,System.IO.Compression.ZipArchiveMode.Create))
                foreach(var row in entries) using(var stream=archive.CreateEntry(row.Key).Open()) stream.Write(row.Value,0,row.Value.Length);
            string setupFile=Path.Combine(folder,"SoloStone-Setup-1.2.3.exe");
            if(File.Exists(zip)) File.WriteAllText(Path.Combine(folder,"build-result.json"),"{\"hookBuild\":false,\"version\":\"1.2.3\",\"saveSchema\":15,\"zip\":{\"name\":\"SoloStone-1.2.3.zip\",\"size\":1,\"sha256\":\""+Hashes.FileHash(zip)+"\"}"
                +(File.Exists(setupFile)?",\"setup\":{\"name\":\"SoloStone-Setup-1.2.3.exe\",\"size\":1,\"sha256\":\""+Hashes.FileHash(setupFile)+"\"}":"")+"}");
            return folder;
        };
        Func<string,bool,string> check=(folder,setup)=>ReleaseScan.ReleaseProblem(folder,"1.2.3",setup);
        Check(check(build((d,e)=>{},true),true)==null,"a clean release passes");
        Check(check(build((d,e)=>{},false),false)==null,"without a setup when none was asked for");
        Check(check(build((d,e)=>{},false),true).Contains("setup"),"a missing setup is refused");
        Check(check(build((d,e)=>e.Remove("NOTICE.txt"),true),true).Contains("NOTICE"),"a missing NOTICE is refused");
        Check(check(build((d,e)=>e["NOTICE.txt"]=new byte[0],true),true).Contains("NOTICE"),"so is an empty one");
        foreach(string licence in ReleaseScan.LicenceFiles) {
            Check(check(build((d,e)=>e.Remove(licence),true),true)=="The update zip has no "+licence+".","a download without "+licence+" is refused");
            Check(check(build((d,e)=>e[licence]=new byte[0],true),true)=="The update zip has no "+licence+".","and so is an empty "+licence);
        }
        Check(ReleaseScan.LicenceFiles.Contains("LICENSE") && ReleaseScan.LicenceFiles.Contains("NOTICE") && ReleaseScan.LicenceFiles.Contains("LICENSE-ADDITIONAL-PERMISSION.md") && ReleaseScan.LicenceFiles.Any(name=>name.StartsWith("LICENSES/")) && ReleaseScan.LicenceFiles.Contains("SOURCE.txt"),"the required set is the public licence files and the source pointer");
        Check(check(build((d,e)=>e["SoloStone.exe"]=System.Text.Encoding.ASCII.GetBytes("x"+Token("SOLOSTONE_","TEST_CONTROL")),true),true).Contains("launcher carries"),"a hook trace in the launcher is refused");
        Check(check(build((d,e)=>e["payload/BepInEx/plugins/SoloStone.Plugin.dll"]=System.Text.Encoding.Unicode.GetBytes(Token("enable-test","-control")),true),true).Contains("plugin carries"),"and in the plugin");
        Check(check(build((d,e)=>e["payload/BepInEx/plugins/SoloStone.Plugin.dll"]=System.Text.Encoding.Unicode.GetBytes(Token("restore-","request.txt")),true),true).Contains("plugin carries"),"restore-request is a hook trace in a release plugin");
        Check(check(build((d,e)=>File.WriteAllBytes(Path.Combine(d,"SoloStone-TEST-HOOKS-Setup-1.2.3.exe"),new byte[1]),true),true).Contains("live-test"),"a hook file name is refused");
        Check(check(build((d,e)=>File.WriteAllBytes(Path.Combine(d,"SoloStone-1.2.4.zip"),new byte[1]),true),true).Contains("Unexpected"),"a wrong version's file is refused");
        Check(check(build((d,e)=>e.Clear(),true),true).Contains("zip"),"a missing zip is refused");
        Check(check(build((d,e)=>e["release.json"]=System.Text.Encoding.ASCII.GetBytes("{\"schema\":1,\"version\":\"9.9.9\",\"saveSchema\":15,\"sourceRevision\":\"r\"}"),true),true).Contains("9.9.9"),"a zip of another version is refused");
        Check(check(build((d,e)=>e["unins000.exe"]=new byte[1],true),true)!=null,"the app rules apply");
        Check(check(build((d,e)=>{ Directory.CreateDirectory(Path.Combine(d,"app")); File.WriteAllBytes(Path.Combine(d,"app","SoloStone.exe"),new byte[]{1,2,3}); },true),true)==null,"the app folder may sit beside the zip");
        Check(check(build((d,e)=>{ Directory.CreateDirectory(Path.Combine(d,"app")); File.WriteAllBytes(Path.Combine(d,"app","SoloStone.exe"),new byte[]{9}); },true),true).Contains("different"),"but it must be the same launcher");
        // Item 6: the setup is tied to the build's own record.
        Func<Action<string,string>,string> tampered=change=>{ string d=build((_,__)=>{},true); change(d,Path.Combine(d,"build-result.json")); return check(d,true); };
        Check(tampered((d,r)=>File.WriteAllBytes(Path.Combine(d,"SoloStone-Setup-1.2.3.exe"),new byte[]{8,8,8})).Contains("not the file this build made"),"a hook setup renamed into place is refused by its hash");
        Check(tampered((d,r)=>File.WriteAllText(r,File.ReadAllText(r).Replace("\"hookBuild\":false","\"hookBuild\":true"))).Contains("live-test"),"a hook build's record is refused");
        Check(tampered((d,r)=>File.WriteAllText(r,File.ReadAllText(r).Replace("\"hookBuild\":false,",""))).Contains("live-test"),"so is one that does not say");
        Check(tampered((d,r)=>File.Delete(r)).Contains("build-result.json"),"a missing record is refused");
        Check(tampered((d,r)=>File.WriteAllText(r,File.ReadAllText(r).Replace("SoloStone-Setup-1.2.3.exe","SoloStone-TEST-Setup-1.2.3.exe"))).Contains("does not list"),"a record naming another setup is refused");
        Check(tampered((d,r)=>File.WriteAllText(r,File.ReadAllText(r).Replace("\"version\":\"1.2.3\"","\"version\":\"1.2.4\""))).Contains("1.2.4"),"so is another version's");
        Check(tampered((d,r)=>File.WriteAllBytes(Path.Combine(d,"SoloStone-1.2.3.zip"),File.ReadAllBytes(Path.Combine(d,"SoloStone-1.2.3.zip")).Concat(new byte[]{0}).ToArray())).Contains("not the file this build made"),"and a changed zip");
        Check(tampered((d,r)=>File.WriteAllText(r,"{ damaged")).Contains("can't be read"),"and a damaged record");
        Check(ReleaseManifest.ValidAssetName(ReleaseScan.ArtifactBase(true)+"-0.1.0.zip"),"the driver can still serve a hook zip from the mock");
        Check(ReleaseScan.ArtifactBase(false)=="SoloStone" && ReleaseScan.ArtifactBase(true).Contains(ReleaseScan.HookLabel),"names differ");
        string script=Source("build.ps1"), iss=Source("SoloStone.iss");
        Check(script.Contains("'SoloStone-TEST-HOOKS'") && script.Contains("\"SoloStone-TEST-HOOKS-Setup-$Version\"") && script.Contains("/DSetupBase=$setupBase") && iss.Contains("{#SetupBase}"),"a hook build's files are named for it");
        int gate=script.IndexOf("if (-not $HookBuild) {",StringComparison.Ordinal), last=script.IndexOf("'check-release'",StringComparison.Ordinal);
        Check(gate>0 && last>gate && last>script.IndexOf("build-result.json",StringComparison.Ordinal),"the release path of build.ps1 ends with the check");
        Check(Source("Options.cs").Contains("\"check-release\"") && Source("Program.cs").Contains("ReleaseScan.ReleaseProblem("),"the launcher has the command");
#if SOLOSTONE_INSTALLER
        // Compiled out of a release plugin, in both the method and its callers.
        Check(PluginSource("OfflineGuard.cs").Contains("#if !SOLOSTONE_INSTALLER || SOLOSTONE_TEST_HOOKS\n        RejectReparsePoints(Path.Combine(directory, \"restore-request.txt\"))"),"restore-request is out of the guard");
        Check(PluginSource("SelectedProfile.cs").Contains("#if !SOLOSTONE_INSTALLER || SOLOSTONE_TEST_HOOKS\n        LocalProfileBackups.ProcessRestoreRequest"),"and of the caller");
        Check(PluginSource("LocalProfile.cs").Contains("#if !SOLOSTONE_INSTALLER || SOLOSTONE_TEST_HOOKS\n    // Driven by the lab's"),"and of the method");
#endif
    }
    // Lab finding: a killed swap recovered, and then updates stopped. Kill at every move, recover from the shortcut, then the next start must install it and all of it.
    static void KillThenRetry(string temp) {
        var clean=NewFixture(temp); Check(Update(clean,"2.0.0").Outcome==UpdateOutcome.Swapped,"a clean swap, for comparison");
        string cleanApp=AppHash(clean), cleanParts=Parts(clean);
        int points=0;
        for(int point=0;;point++) {
            var f=NewFixture(temp); string good=f.Code;
            var release=BuildRelease(f,"2.0.0"); bool reached=false;
            var engine=f.Engine(label=>{ if(label=="start:"+point) { reached=true; throw new SimulatedCrash(); } }); engine.Stage(ZipOf(f,"2.0.0"),release);
            try { engine.Apply(release,false); } catch(SimulatedCrash) { }
            if(!reached) { FsOps.DeleteTree(f.Base); break; }
            points++; f.Host.Fault=null;
            f.Engine().Resume();
            Check(f.Code==good && !f.Engine().JournalActive,"the shortcut recovers to the old version, move "+point);
            // A helper running from the staged folder can't delete all of it, so a part stays behind.
            string staged=f.Engine().StagedApp("2.0.0"); WriteRel(staged,"BouncyCastle.Cryptography.dll","left by the restore");
            var again=Update(f,"2.0.0");
            Check(again.Outcome==UpdateOutcome.Swapped,"the next start installs it, move "+point+": "+again.Message);
            Check(InstallRecord.Load(f.Root).ReleaseVersion=="2.0.0" && File.ReadAllText(Path.Combine(f.App,"SoloStone.exe"))=="launcher 2.0.0" && AppHash(f)==cleanApp && Parts(f)==cleanParts,"all of the new version is there, move "+point);
            f.Engine().ReportHealth(HealthOutcome.Confirmed,null);
            Check(f.Engine().Rollback().Outcome==RollbackOutcome.Done && f.Code==good,"and the old one is whole behind it, move "+point);
            FsOps.DeleteTree(f.Base);
        }
        Check(points>20,"the walk covered the move list: "+points);
    }
    // Something else reads journal.json for a moment (the driver's poll, antivirus, an indexer). Windows then refuses the rename over it, and that must not end a swap.
    static void BusyFiles(string temp) {
        var a=NewFixture(temp); int refused=0, saves=0;
        Atomic.PublishFault=target=>{ if(Path.GetFileName(target)=="journal.json" && saves++%2==0 && refused<40) { refused++; throw new IOException("The process cannot access the file because it is being used by another process."); } };
        UpdateResult result;
        try { result=Update(a,"2.0.0"); } finally { Atomic.PublishFault=null; }
        Check(result.Outcome==UpdateOutcome.Swapped && refused>=40,"a journal held open for a moment does not end the swap: "+result.Message);
        // Held for good: a real failure, undone whole, after bounded tries.
        var c=NewFixture(temp); string cGood=c.Code; var cRelease=BuildRelease(c,"2.0.0"); var cEngine=c.Engine(); cEngine.Stage(ZipOf(c,"2.0.0"),cRelease);
        int failures=0;
        Atomic.PublishFault=target=>{ if(failures<1000 && Path.GetFileName(target)=="SoloStone.exe") { failures++; throw new IOException("Access is denied."); } };
        UpdateResult stuck;
        try { Atomic.RetryDelayMs=0; stuck=cEngine.Apply(cRelease,false); stuck=c.Host.HelperResult??stuck; } finally { Atomic.PublishFault=null; Atomic.RetryDelayMs=50; }
        Check(stuck.Outcome==UpdateOutcome.Failed && failures>1 && failures<40 && c.Code==cGood,"a file busy for good fails after bounded tries, and is undone whole: "+stuck.Message);
        // A missing file is no reason to wait.
        int calls=0; Exception gone=null;
        try { Atomic.Transient(()=>{ calls++; throw new FileNotFoundException("gone"); }); } catch(FileNotFoundException e) { gone=e; }
        Check(gone!=null && calls==1,"a missing file is not retried");
        calls=0; Atomic.Transient(()=>{ if(++calls<3) throw new UnauthorizedAccessException("held"); }); Check(calls==3,"access denied is, because antivirus looks like that");
        // Item c: the log says what failed and where; the player's sentence stays plain.
        var l=NewFixture(temp); var lRelease=BuildRelease(l,"2.0.0"); var lEngine=l.Engine(label=>{ if(label=="start:3") throw new IOException("Sharing violation on a test file."); }); lEngine.Stage(ZipOf(l,"2.0.0"),lRelease);
        lEngine.Apply(lRelease,false);
        var failed=l.Host.HelperResult; string log=File.ReadAllText(InstallerLog.PathOf(l.Root));
        Check(failed.Outcome==UpdateOutcome.Failed && failed.Message.Contains("A file was busy") && !failed.Message.Contains("Sharing violation") && !failed.Message.Contains(l.Root),"the player reads a plain sentence");
        Check(log.Contains("IOException") && log.Contains("Sharing violation on a test file.") && log.Contains("move 3 of") && log.Contains(Path.Combine(l.App,"")) && log.Contains(Path.Combine(l.Root,"releases","1.0.0")),"the log has the type, the message and the move's source and destination: "+log);
        // Item b: a helper that fails reopens the launcher, which shows it and does not loop. The next start the player makes is a real attempt.
        var rootKey=new TestKey(); var signing=new TestKey(); var config=UpdateConfig.From(rootKey.Public,signing.Public,"owner/repo",0);
        var n=NewFixture(temp); var latest=BuildRelease(n,"2.0.0"); var http=new FakeHttp();
        string json=ManifestJson(1,"2.0.0","SoloStone-2.0.0.zip",latest.Asset.Size,latest.Asset.Sha256,15);
        http.Add(RawBase+"release-manifest.json",Utf8(json)); http.Add(RawBase+"release-manifest.json.minisig",SignManifest(signing,json,1));
        http.Redirect("https://github.com/owner/repo/releases/download/v2.0.0/SoloStone-2.0.0.zip","https://objects.githubusercontent.com/asset");
        http.Add("https://objects.githubusercontent.com/asset",File.ReadAllBytes(ZipOf(n,"2.0.0")));
        var none=System.Threading.CancellationToken.None;
        var first=n.Engine(label=>{ if(label=="start:3") throw new IOException("busy"); });
        var failing=new UpdateService(n.Root,config,http,first).Apply(false,none);
        Check((n.Host.HelperResult??failing).Outcome==UpdateOutcome.Failed && n.Engine().LastNote()!=null,"the first attempt fails and leaves its note");
        n.Host.Fault=null; n.Host.HelperResult=null;
        var reopened=new UpdateService(n.Root,config,http,n.Engine(),true);
        int before=http.Requests.Count;
        Check(reopened.Check(none).State==UpdateState.Failed && reopened.Apply(false,none).Outcome==UpdateOutcome.Nothing && n.Engine().Held()==null && !http.Requests.Skip(before).Any(u=>u.Contains("asset")),"the launcher the helper reopens shows it and does not try again");
        var fresh=new UpdateService(n.Root,config,http,n.Engine());
        Check(fresh.Check(none).State==UpdateState.Available,"a fresh start offers it");
        var retried=fresh.Apply(false,none);
        Check((n.Host.HelperResult??retried).Outcome==UpdateOutcome.Swapped && InstallRecord.Load(n.Root).ReleaseVersion=="2.0.0" && n.Engine().LastNote()==null,"and really installs it, with no note left");
        // An attempt that dies with no result leaves no old note to speak for it.
        var o=NewFixture(temp); var oRelease=BuildRelease(o,"2.0.0"); o.Engine().RecordNote("2.0.0","An old failure."); var oEngine=o.Engine(label=>{ if(label=="start:0") throw new SimulatedCrash(); }); oEngine.Stage(ZipOf(o,"2.0.0"),oRelease);
        try { oEngine.Apply(oRelease,false); } catch(SimulatedCrash) { }
        Check(o.Engine().LastNote()==null,"a real attempt replaces the note");
        // The window: only the launcher a helper opened repeats the note, and only it passes the flag on.
        var quietOps=new FakeLauncherOps(); quietOps.Report.UpdateNote="An old failure."; var quiet=new LauncherModel(); new LauncherRunner(quiet,quietOps,x=>x()).Start(none);
        Check(!quiet.Notice.Contains("old failure"),"a fresh start does not show an old attempt as the result");
        var program=Source("Program.cs"); var host=Source("Gui/GuiHost.cs"); var windows=Source("Gui/WindowsOps.cs");
        Check(program.Contains("\"launch --after-helper --root \"") && host.Contains("new WindowsLauncherOps(options.Root, options.AfterHelper)") && windows.Contains("Engine(), afterHelper)"),"the flag goes from the helper to the update check");
        var parsed=Options.Parse(new[]{"launch","--after-helper","--root",Path.Combine(temp,"p")});
        Check(parsed.AfterHelper,"it parses"); Refuses(()=>Options.Parse(new[]{"update","--after-helper","--root",Path.Combine(temp,"p")}),"only with launch");
    }
    // A swap that keeps failing must not eat the backups a rollback needs, must not download again, and must not stop updates for good.
    sealed class Offered { public FakeHttp Http=new FakeHttp(); public UpdateConfig Config; public TestKey Signing=new TestKey(); public long Seq; }
    static Offered Offer(Fixture f,string version,Offered had=null) {
        var offer=had??new Offered(); if(had==null) offer.Config=UpdateConfig.From(new TestKey().Public,offer.Signing.Public,"owner/repo",0);
        var latest=BuildRelease(f,version); offer.Seq++;
        string json=ManifestJson(offer.Seq,version,"SoloStone-"+version+".zip",latest.Asset.Size,latest.Asset.Sha256,15);
        offer.Http.Add(RawBase+"release-manifest.json",Utf8(json)); offer.Http.Add(RawBase+"release-manifest.json.minisig",SignManifest(offer.Signing,json,offer.Seq));
        offer.Http.Redirect("https://github.com/owner/repo/releases/download/v"+version+"/SoloStone-"+version+".zip","https://objects.githubusercontent.com/asset-"+version);
        offer.Http.Add("https://objects.githubusercontent.com/asset-"+version,File.ReadAllBytes(ZipOf(f,version)));
        return offer;
    }
    static UpdateResult PlayerStart(Fixture f,Offered offer,string failAt=null) {
        var engine=failAt==null?f.Engine():f.Engine(label=>{ if(label==failAt) throw new IOException("busy"); });
        f.Host.HelperResult=null;
        var result=new UpdateService(f.Root,offer.Config,offer.Http,engine).Apply(false,System.Threading.CancellationToken.None);
        f.Host.Fault=null;
        return f.Host.HelperResult??result;
    }
    static void FailingSwaps(string temp) {
        var none=System.Threading.CancellationToken.None;
        var f=NewFixture(temp); var offer=Offer(f,"2.0.0");
        Func<int> downloads=()=>offer.Http.Requests.Count(u=>u.Contains("/asset-"));
        var backups=new Backups(f.Root);
        var older=new[]{backups.Create(f.Saves,Path.Combine(f.Client,"BepInEx","config"),"0.9.0")};
        var one=PlayerStart(f,offer,"start:3");
        Check(one.Outcome==UpdateOutcome.Failed && downloads()==1 && backups.List().Length==2,"the first failure downloaded once and spent one backup");
        string firstBackup=backups.List().First().Id;
        var two=PlayerStart(f,offer,"start:3");
        Check(two.Outcome==UpdateOutcome.Failed,"the second attempt was real: "+two.Message);
        Check(downloads()==1,"a retry downloads nothing: "+downloads());
        Check(backups.List().Length==2 && backups.List().First().Id==firstBackup,"a retry spends no backup: "+backups.List().Length);
        Check(two.Message.Contains("waits a day") && !one.Message.Contains("waits a day"),"the second failure says so, once, in plain words");
        Check(f.Engine().Attempts().Failures==2 && f.Engine().Attempts().Version=="2.0.0","the count is per version");
        // Waiting: no download, no helper, no backup. Not silent, and a window that says it.
        int handOffs=f.Host.HandOffs, got=downloads();
        var waiting=new UpdateService(f.Root,offer.Config,offer.Http,f.Engine()).Check(none);
        Check(waiting.State==UpdateState.Held && waiting.Message.Contains("tries again after") && waiting.Message.Contains("2.0.0"),"the update line says until when: "+waiting.Message);
        var skipped=PlayerStart(f,offer);
        Check(skipped.Outcome==UpdateOutcome.Held && f.Host.HandOffs==handOffs && downloads()==got && backups.List().Length==2,"no attempt during the wait");
        var line=new LauncherModel(); line.UpdateChecked(waiting); Check(line.UpdateText.Contains("tries again after") && line.Notice=="","it is the update line, not a notice each time");
        // The day ends by itself, and it may fail again: never for good.
        Action age=()=>{ var row=f.Engine().Attempts(); row.LastFailedUtc=DateTime.UtcNow.AddHours(-25).ToString("o"); Atomic.Save(Path.Combine(f.Root,"state","swap-attempts.json"),row); };
        age();
        Check(new UpdateService(f.Root,offer.Config,offer.Http,f.Engine()).Check(none).State==UpdateState.Available,"after a day it is offered again");
        Check(PlayerStart(f,offer,"start:3").Outcome==UpdateOutcome.Failed && f.Host.HandOffs>handOffs && f.Engine().Attempts().Failures==3 && downloads()==got && backups.List().Length==2,"a real attempt, with no new download or backup");
        Check(new UpdateService(f.Root,offer.Config,offer.Http,f.Engine()).Check(none).State==UpdateState.Held,"and another day's wait");
        // Bad times never stop updates.
        var future=f.Engine().Attempts(); future.LastFailedUtc=DateTime.UtcNow.AddYears(1).ToString("o"); Atomic.Save(Path.Combine(f.Root,"state","swap-attempts.json"),future);
        Check(new UpdateService(f.Root,offer.Config,offer.Http,f.Engine()).Check(none).State==UpdateState.Available,"a clock that moved does not stop updates");
        File.WriteAllText(Path.Combine(f.Root,"state","swap-attempts.json"),"{ damaged");
        Check(new UpdateService(f.Root,offer.Config,offer.Http,f.Engine()).Check(none).State==UpdateState.Available,"nor does a damaged count");
        var kept=new SwapAttempts { Version="2.0.0", From="1.0.0", Failures=2, LastFailedUtc=DateTime.UtcNow.AddHours(-25).ToString("o"), BackupId=firstBackup };
        Atomic.Save(Path.Combine(f.Root,"state","swap-attempts.json"),kept);
        // A successful swap clears it and spends nothing more. The first backup is still the one a rollback finds, and the old one survived.
        var done=PlayerStart(f,offer);
        Check(done.Outcome==UpdateOutcome.Swapped && InstallRecord.Load(f.Root).ReleaseVersion=="2.0.0" && f.Engine().Attempts()==null,"it installs, and the count is gone: "+done.Message);
        Check(backups.List().Length==2 && backups.List().Any(b=>b.Id==older[0]) && backups.List().Any(b=>b.Id==firstBackup),"no backup was pushed out");
        // A newer version resets the wait, and gets its own backup.
        var n=NewFixture(temp); var nOffer=Offer(n,"2.0.0");
        PlayerStart(n,nOffer,"start:3"); PlayerStart(n,nOffer,"start:3");
        Check(new UpdateService(n.Root,nOffer.Config,nOffer.Http,n.Engine()).Check(none).State==UpdateState.Held,"waiting on 2.0.0");
        Offer(n,"2.1.0",nOffer);
        Check(new UpdateService(n.Root,nOffer.Config,nOffer.Http,n.Engine()).Check(none).State==UpdateState.Available,"a newer version is offered at once");
        int nBackups=new Backups(n.Root).List().Length;
        Check(PlayerStart(n,nOffer,"start:3").Outcome==UpdateOutcome.Failed && n.Engine().Attempts().Version=="2.1.0" && n.Engine().Attempts().Failures==1 && new Backups(n.Root).List().Length==nBackups+1,"its own count and backup");
        // A kill is not a failure, and spends no second backup or download.
        var k=NewFixture(temp); var kOffer=Offer(k,"2.0.0");
        var killed=k.Engine(label=>{ if(label=="start:3") throw new SimulatedCrash(); });
        try { new UpdateService(k.Root,kOffer.Config,kOffer.Http,killed).Apply(false,none); } catch(SimulatedCrash) { }
        k.Host.Fault=null; k.Engine().Resume();
        Check(k.Engine().Attempts().Failures==0 && k.Engine().Attempts().BackupId!=null,"a kill counts nothing, and remembers its backup");
        var afterKill=PlayerStart(k,kOffer);
        Check(afterKill.Outcome==UpdateOutcome.Swapped && kOffer.Http.Requests.Count(u=>u.Contains("/asset-"))==1 && new Backups(k.Root).List().Length==1,"the retry reuses the zip and the backup");
        // A backup that is gone is made again: the swap never runs without one.
        var b=NewFixture(temp); var bOffer=Offer(b,"2.0.0"); PlayerStart(b,bOffer,"start:3");
        FsOps.DeleteTree(new Backups(b.Root).PathOf(b.Engine().Attempts().BackupId));
        PlayerStart(b,bOffer,"start:3");
        Check(new Backups(b.Root).List().Length==1 && new Backups(b.Root).List().First().Id==b.Engine().Attempts().BackupId,"a lost backup is replaced");
        // A hold from a failed first start or a rollback frees the zip: that version won't be tried soon.
        var h=NewFixture(temp); var hOffer=Offer(h,"2.0.0"); PlayerStart(h,hOffer);
        h.Engine().ReportHealth(HealthOutcome.Failed,"hooks");
        Check(!Directory.Exists(Path.Combine(h.Root,"updates","2.0.0")),"a held version leaves nothing in updates");
    }
    // Items 2 to 7 of the last small round.
    static void LastSmallRound(string temp) {
        // Item 4.
#if SOLOSTONE_INSTALLER
        foreach(var locked in new Exception[]{new FileLoadException("locked"),new BadImageFormatException("locked"),new TypeInitializationException("X",new FileLoadException("locked"))}) {
            Check(InstallerRuntime.ExitCodeFor(locked)==Health.ExitStartupError,"antivirus on a load is not a hold for good: "+locked.GetType().Name);
            Check(Health.Classify(false,false,InstallerRuntime.ExitCodeFor(locked))==HealthOutcome.RetryLater,"it retries later");
        }
        Check(InstallerRuntime.ExitCodeFor(new NullReferenceException())==Health.ExitStartupError && !Source("InstallerRuntime.cs").Contains("a null reference is the mod itself"),"a null reference is 50, and the comment says so");
#endif
        // Item 5: every open, delete and move on the swap's way goes through the retry.
        string engine=Source("UpdateEngine.cs"), fs=Source("Fs.cs");
        int from=engine.IndexOf("void MoveFile(",StringComparison.Ordinal), to=engine.IndexOf("// Whichever process runs this",StringComparison.Ordinal);
        Check(from>0 && to>from,"found the swap's file work");
        var raw=System.Text.RegularExpressions.Regex.Matches(System.Text.RegularExpressions.Regex.Replace(engine.Substring(from,to-from),@"static void CopyFresh.*",""),@"(?<!=>)(?<!\.)\bFile\.(Delete|Move|Copy)\(");
        Check(raw.Count==0,"no raw file call in the swap's moves: "+raw.Count);
        Check(fs.Contains("if(FlushToDisk) Transient(") && fs.Contains("Transient(()=>{ using(var stream=new FileStream(temp,FileMode.Create"),"nor in the atomic write");
        // Item 7: out of the release plugin. The default build keeps it, byte for byte.
        Check(PluginSource("OfflineGuard.cs").Contains("#if !SOLOSTONE_INSTALLER\n    public string OptionsUid"),"the old uid is not in an Installer plugin");
    }
    // Fail Monday, play all week, the retry works, and a rollback must not hand back Monday's saves.
    static void ReusedBackups(string temp) {
        var none=System.Threading.CancellationToken.None;
        var f=NewFixture(temp); var offer=Offer(f,"2.0.0"); var backups=new Backups(f.Root); string profile=Path.Combine(f.Saves,"profile.xml");
        PlayerStart(f,offer,"start:3");
        string monday=f.Engine().Attempts().BackupId;
        string week=ProfileXml(15,"")+"<!-- a week of play -->"; File.WriteAllText(profile,week);
        var done=PlayerStart(f,offer);
        Check(done.Outcome==UpdateOutcome.Swapped,"the retry works: "+done.Message);
        Check(backups.List().Length==2 && backups.List().First().Id!=monday,"changed saves get a new backup");
        File.WriteAllText(profile,ProfileXml(16));
        var refused=f.Engine().Rollback();
        Check(refused.Outcome==RollbackOutcome.SaveSchemaTooNew && refused.BackupId==backups.List().First().Id,"a schema-bump rollback offers the newer backup");
        f.Engine().RestoreBackup(refused.BackupId);
        Check(File.ReadAllText(profile)==week,"and it restores the week");
        // Unchanged saves reuse it, whatever else ran.
        var same=NewFixture(temp); var sameOffer=Offer(same,"2.0.0"); PlayerStart(same,sameOffer,"start:3");
        string kept=same.Engine().Attempts().BackupId;
        Check(PlayerStart(same,sameOffer).Outcome==UpdateOutcome.Swapped && new Backups(same.Root).List().Length==1 && new Backups(same.Root).List().First().Id==kept,"unchanged saves reuse the backup");
        // Each kind of change, one comparison at a time.
        var c=NewFixture(temp); var cBackups=new Backups(c.Root); string id=cBackups.Create(c.Saves,Path.Combine(c.Client,"BepInEx","config"),"1.0.0");
        var manifest=cBackups.Verified(id); string saved=Path.Combine(c.Saves,"profile.xml"), text=File.ReadAllText(saved);
        Check(cBackups.SavesUnchanged(manifest,c.Saves),"nothing changed");
        string extra=Path.Combine(c.Saves,"new.xml"); File.WriteAllText(extra,"x"); Check(!cBackups.SavesUnchanged(manifest,c.Saves),"an added file is a change"); File.Delete(extra);
        File.Delete(saved); Check(!cBackups.SavesUnchanged(manifest,c.Saves),"a removed file is"); File.WriteAllText(saved,text);
        File.WriteAllText(saved,text.Substring(0,text.Length-1)+"!"); Check(!cBackups.SavesUnchanged(manifest,c.Saves),"so is the same size with other bytes"); File.WriteAllText(saved,text+" "); Check(!cBackups.SavesUnchanged(manifest,c.Saves),"and another size"); File.WriteAllText(saved,text);
        string moved=Path.Combine(c.Saves,"renamed.xml"); File.Move(saved,moved); Check(!cBackups.SavesUnchanged(manifest,c.Saves),"and another path"); File.Move(moved,saved);
        File.SetUnixFileMode(saved,UnixFileMode.None);
        try { Check(!cBackups.SavesUnchanged(manifest,c.Saves),"an unreadable save counts as changed"); } finally { File.SetUnixFileMode(saved,UnixFileMode.UserRead|UnixFileMode.UserWrite); }
        Check(cBackups.SavesUnchanged(manifest,c.Saves),"and all of that came back");
        var gone=NewFixture(temp); var goneBackups=new Backups(gone.Root); var goneManifest=goneBackups.Verified(goneBackups.Create(gone.Saves,Path.Combine(gone.Client,"BepInEx","config"),"1.0.0"));
        FsOps.DeleteTree(gone.Saves); Check(!goneBackups.SavesUnchanged(goneManifest,gone.Saves),"a save folder that is gone is a change");
        // A pending match still refuses before any of this.
        var p=NewFixture(temp); var pOffer=Offer(p,"2.0.0"); PlayerStart(p,pOffer,"start:3"); int pHandOffs=p.Host.HandOffs;
        File.WriteAllText(Path.Combine(p.Saves,"profile.xml"),ProfileXml(15,"<Match>open</Match>"));
        Check(PlayerStart(p,pOffer).Outcome==UpdateOutcome.Refused && p.Host.HandOffs==pHandOffs && new Backups(p.Root).List().Length==1,"a pending match still refuses, with no backup spent");
    }
    // What the message says is what happens, when the swap fails before it has a journal.
    static void EarlyFailures(string temp) {
        var none=System.Threading.CancellationToken.None;
        foreach(string kind in new[]{"plan","staged"}) {
            var f=NewFixture(temp); var offer=Offer(f,"2.0.0");
            Func<UpdateResult> attempt;
            if(kind=="plan") {
                WriteRel(f.Client,"BepInEx/plugins/newextra.dll","the player's own file"); // Plan refuses to overwrite it.
                attempt=()=>PlayerStart(f,offer);
            } else {
                // Staged files damaged after the launcher checked them: the helper sees them first.
                attempt=()=>{ var engine=f.Engine(); engine.Stage(ZipOf(f,"2.0.0"),BuildRelease(f,"2.0.0")); File.Delete(Path.Combine(engine.StagedApp("2.0.0"),"release.json")); f.Host.InApp=false; try { return engine.FinishSwap("2.0.0"); } finally { f.Host.InApp=true; } };
            }
            var one=attempt(); Check(one.Outcome==UpdateOutcome.Failed && !one.Message.Contains("waits a day"),"first "+kind+" failure: "+one.Message);
            var two=attempt(); Check(two.Outcome==UpdateOutcome.Failed && two.Message.Contains("waits a day"),"second "+kind+" failure says it waits: "+two.Message);
            var row=f.Engine().Attempts();
            Check(row.Failures==2 && row.From=="1.0.0","the installed version is recorded, "+kind+": "+row.From);
            // The wait is real: nothing is unpacked.
            string staged=f.Engine().StagedApp("2.0.0"); WriteRel(staged,"sentinel.txt","untouched"); int handOffs=f.Host.HandOffs;
            var service=new UpdateService(f.Root,offer.Config,offer.Http,f.Engine());
            Check(service.Check(none).State==UpdateState.Held,"it waits, "+kind);
            Check(service.Apply(false,none).Outcome==UpdateOutcome.Held && File.Exists(Path.Combine(staged,"sentinel.txt")) && f.Host.HandOffs==handOffs,"with no re-unpack, "+kind);
            // The wait ends.
            var aged=f.Engine().Attempts(); aged.LastFailedUtc=DateTime.UtcNow.AddHours(-25).ToString("o"); Atomic.Save(Path.Combine(f.Root,"state","swap-attempts.json"),aged);
            Check(service.Check(none).State==UpdateState.Available,"the day ends, "+kind);
            attempt(); Check(!File.Exists(Path.Combine(staged,"sentinel.txt")) && f.Engine().Attempts().Failures==3,"and it really tries, "+kind);
            // A newer version resets it.
            Check(service.Check(none).State==UpdateState.Held,"waiting again, "+kind);
            if(kind=="plan") File.Delete(Path.Combine(f.Client,"BepInEx","plugins","newextra.dll"));
            Offer(f,"2.1.0",offer);
            Check(new UpdateService(f.Root,offer.Config,offer.Http,f.Engine()).Check(none).State==UpdateState.Available,"a newer version is offered at once, "+kind);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SoloStone;

static partial class Program
{
    static TargetFacts Facts(string target,string root,string source,string saves=null) => new TargetFacts { Target=target, Root=root, Source=source, SaveRoot=saves,
        Resolve=path=>path, FreeBytes=100L*1024*1024*1024, RequiredBytes=1024 };
    static void TargetRules(string temp) {
        string root=Path.Combine(temp,"t-root"), source=Path.Combine(temp,"t-source"), saves=Path.Combine(temp,"t-saves"), good=Path.Combine(temp,"t-copy");
        Check(TargetCheck.Problem(Facts(good,root,source,saves))==null,"plain target accepted");
        Check(TargetCheck.Problem(Facts(Path.Combine(root,"client"),root,source,saves))==null,"default target accepted");
        foreach(string name in Safety.RootFolders)
            Check(TargetCheck.Problem(Facts(Path.Combine(root,name),root,source,saves))!=null && TargetCheck.Problem(Facts(Path.Combine(root,name,"x"),root,source,saves))!=null,"target in Root\\"+name+" refused");
        Check(TargetCheck.Problem(Facts(root,root,source))!=null,"target equal to root refused");
        Check(TargetCheck.Problem(Facts(temp,root,source))!=null,"target containing root refused");
        Check(TargetCheck.Problem(Facts(source,root,source))!=null,"target equal to source refused");
        Check(TargetCheck.Problem(Facts(Path.Combine(source,"x"),root,source))!=null,"target inside source refused");
        Check(TargetCheck.Problem(new TargetFacts { Target=Path.Combine(temp,"s"), Root=root, Source=Path.Combine(temp,"s","game"), Resolve=p=>p, FreeBytes=1, RequiredBytes=0 })!=null,"target containing source refused");
        Check(TargetCheck.Problem(Facts(Path.Combine(saves,"x"),root,source,saves))!=null,"target inside saves refused");
        foreach(char bad in new[]{'&','|','<','>','^','%','"','\t'})
            Check(TargetCheck.Problem(Facts(Path.Combine(temp,"bad"+bad+"name"),root,source,saves))!=null,"firewall-unsafe character refused at path check");
        var f=Facts(good,root,source); f.Registered=new[]{Path.Combine(good,"game")};
        Check(TargetCheck.Problem(f)!=null && TargetCheck.Problem(f).Contains("registered game folder"),"target around a registered game folder refused");
        f=Facts(Path.Combine(temp,"reg","copy"),root,source); f.Registered=new[]{Path.Combine(temp,"reg")};
        Check(TargetCheck.Problem(f)!=null,"target inside a registered game folder refused");
        f=Facts(good,root,source); f.Resolve=p=>p==good?Path.Combine(source,"alias"):p;
        Check(TargetCheck.Problem(f)!=null,"alias that resolves into source refused");
        f=Facts(good,root,source); f.TargetIsFile=true; Check(TargetCheck.Problem(f).Contains("file"),"file target refused");
        f=Facts(good,root,source); f.TargetHasContent=true; Check(TargetCheck.Problem(f).Contains("already contains"),"non-empty target refused");
        f=Facts(good,root,source); f.StageExists=true; Check(TargetCheck.Problem(f).Contains("does not own"),"unowned stage refused");
        f=Facts(good,root,source); f.StageExists=true; f.StageOwned=true; Check(TargetCheck.Problem(f)==null,"owned stage may be recopied");
        f=Facts(good,root,source); f.FreeBytes=10; f.RequiredBytes=20; Check(TargetCheck.Problem(f).Contains("available space"),"low space refused");
        f=Facts(good,root,source); f.LongestRelative=TargetCheck.MaxPath; Check(TargetCheck.Problem(f).Contains("too long"),"overlong path refused");
        Check(TargetCheck.Problem(Facts("relative",root,source))!=null,"relative target refused without throwing");
        Check(TargetCheck.Problem(Facts(@"D:\Battle"+@"Net\Hearthstone",root,source))!=null,"protected lab folder refused");
        // Resolve really returns volume paths on Windows. They must compare by containment, never go through the spelling rules.
        const string Volume=@"\\?\Volume{0d1f6a6e-0000-0000-0000-100000000000}\";
        Func<string,string> vol=p=>Volume+Path.GetRelativePath(temp,p).Replace('/','\\');
        f=Facts(good,root,source,saves); f.Resolve=vol;
        Check(TargetCheck.Problem(f)==null,"a resolved volume path does not trip the spelling rules");
        f=Facts(Path.Combine(root,"client"),root,source,saves); f.Resolve=vol; Check(TargetCheck.Problem(f)==null,"default target passes with volume paths");
        f=Facts(good,root,source,saves); f.Resolve=p=>p==good?vol(Path.Combine(source,"x")):vol(p); Check(TargetCheck.Problem(f).Contains("separate from your Hearthstone"),"volume path inside the source refused");
        f=Facts(good,root,source,saves); f.Resolve=p=>p==source?vol(good)+"\\game":vol(p); Check(TargetCheck.Problem(f).Contains("separate from your Hearthstone"),"volume path around the source refused");
        f=Facts(good,root,source,saves); f.Resolve=p=>p==good?vol(Path.Combine(root,"app","x")):vol(p); Check(TargetCheck.Problem(f).Contains("own files"),"volume path inside Root\\app refused");
        f=Facts(good,root,source,saves); f.Resolve=p=>p==good?vol(Path.Combine(saves,"x")):vol(p); Check(TargetCheck.Problem(f).Contains("saves"),"volume path inside saves refused");
        f=Facts(good,root,source,saves); f.Registered=new[]{Path.Combine(temp,"retail")}; f.Resolve=p=>p==good?vol(Path.Combine(temp,"retail","x")):vol(p); Check(TargetCheck.Problem(f).Contains("registered game folder"),"volume path inside a registered folder refused");
        f=Facts(good,root,source,saves); f.Registered=new[]{Path.Combine(temp,"retail")}; f.Resolve=vol; Check(TargetCheck.Problem(f)==null,"separate volume paths accepted beside a registered folder");
        Check(Safety.Within(Volume+@"a\b",Volume+"a") && !Safety.Within(Volume+"ab",Volume+"a") && Safety.Within("/x/y","/x") && !Safety.Within("/xy","/x"),"containment reads both separators and stops at a folder boundary");
        // Real folder facts: empty stays allowed, content and links refuse.
        string empty=Path.Combine(temp,"t-empty"); Directory.CreateDirectory(empty); Directory.CreateDirectory(source);
        Check(TargetCheck.Run(empty,root,source,new string[0],saves)==null,"empty folder accepted");
        File.WriteAllText(Path.Combine(empty,"x"),"y");
        Check(TargetCheck.Run(empty,root,source,new string[0],saves).Contains("already contains"),"folder with content refused");
        string linked=Path.Combine(temp,"t-link"); LinkDirectory(linked,empty);
        Check(TargetCheck.Run(Path.Combine(linked,"copy"),root,source,new string[0],saves).Contains("Linked"),"link ancestor refused");
    }
    static void LayoutTests(string temp,string source,string bundle,TestedClient tested,Payload payload) {
        TargetRules(temp);
        string saves=Path.Combine(temp,"layout-saves"); Directory.CreateDirectory(saves); File.WriteAllText(Path.Combine(saves,"profile.xml"),"keep");
        string root=Path.Combine(temp,"layout-root"), client=Path.Combine(temp,"somewhere","my copy");
        string sourceBefore=TreeHash(source);
        // Selectable client path, with every step reported in order.
        var steps=new List<SetupStep>();
        var options=SetupOf(source,root,bundle,tested,saves,client); options.Progress=p=>{ if(steps.LastOrDefault()!=p.Step || steps.Count==0) steps.Add(p.Step); };
        var record=Installer.Setup(options);
        Check(record.Client==client && record.Stage==client+".solostone-staging" && !Directory.Exists(record.Stage),"client lives at the chosen path");
        Check(steps.SequenceEqual(new[]{SetupStep.CheckClient,SetupStep.CheckTarget,SetupStep.CopyClient,SetupStep.VerifyCopy,SetupStep.InstallMods,SetupStep.Finish}),"setup reports steps for the GUI: "+string.Join(",",steps));
        Check(File.ReadAllText(Path.Combine(client,InstallRecord.Marker))==record.InstallId && !File.Exists(Path.Combine(root,"client",InstallRecord.Marker)),"marker stays in the client folder");
        var loaded=InstallRecord.Load(root);
        Check(loaded.ClientPath==client && loaded.ClientVersion=="37.0.0" && loaded.Schema==2,"schema 2 record reads clientPath");
        Check(FirewallPlan.Programs(loaded).All(p=>p.StartsWith(client,StringComparison.Ordinal)),"firewall targets the chosen path");
        Check(TreeHash(source)==sourceBefore,"Hearthstone folder byte-identical after setup");
        // A record that points somewhere unsafe never loads.
        foreach(string bad in new[]{Path.Combine(root,"app","client"),source,Path.Combine(source,"x"),Path.GetDirectoryName(root),root}) {
            var edited=JsonBytes.Parse<InstallRecord>(JsonBytes.ToBytes(loaded)); edited.ClientPath=bad; Json.Save(Path.Combine(root,"install.json"),edited);
            Refuses(()=>InstallRecord.Load(root),"record with unsafe clientPath refused");
        }
        Json.Save(Path.Combine(root,"install.json"),loaded);
        // Uninstall: recorded client, updates, releases, state go; backups and saves stay.
        foreach(string name in new[]{"updates","releases","state","backups"}) { Directory.CreateDirectory(Path.Combine(root,name)); File.WriteAllText(Path.Combine(root,name,"f"),"x"); }
        Installer.Uninstall(root,new string[0]);
        Check(!Directory.Exists(client) && !File.Exists(Path.Combine(root,"install.json")),"uninstall removes the client at its recorded path");
        Check(new[]{"updates","releases","state"}.All(n=>!Directory.Exists(Path.Combine(root,n))) && File.Exists(Path.Combine(root,"backups","f")) && File.ReadAllText(Path.Combine(saves,"profile.xml"))=="keep","uninstall keeps backups and saves");
        Refuses(()=>Installer.Uninstall(root,new string[0]),"second uninstall has no record");
        Installer.DeleteSaves(root,saves);
        Check(!Directory.Exists(saves) && !Directory.Exists(Path.Combine(root,"backups")),"delete-saves removes saves and backups");
        Directory.CreateDirectory(saves);
        // Cancel mid-copy: an owned stage and a pending record remain, Retry recopies, the source never changes.
        string cancelRoot=Path.Combine(temp,"cancel-root"), cancelClient=Path.Combine(temp,"cancel","copy");
        var cts=new CancellationTokenSource();
        var cancelling=SetupOf(source,cancelRoot,bundle,tested,saves,cancelClient); cancelling.Cancel=cts.Token;
        cancelling.Progress=p=>{ if(p.Step==SetupStep.CopyClient && p.Message.Contains(" 2 of ")) cts.Cancel(); };
        Throws<OperationCanceledException>(()=>Installer.Setup(cancelling),"cooperative cancel stops the copy");
        string stage=InstallRecord.StageOf(cancelClient);
        Check(Directory.Exists(stage) && InstallRecord.OwnsCopy(stage,InstallRecord.ReadRecord(cancelRoot,"install.pending.json").InstallId) && !Directory.Exists(cancelClient),"cancel leaves an owned stage only");
        File.WriteAllText(Path.Combine(stage,"partial-bytes"),"recopied away");
        var retried=Installer.Recover(cancelRoot,bundle,new string[0],saves,_=>{});
        Check(retried.Client==cancelClient && !File.Exists(Path.Combine(cancelClient,"partial-bytes")) && !Directory.Exists(stage) && Directory.GetDirectories(Path.GetDirectoryName(stage)).Length==1,"owned stage is deleted and recopied, no stranded copy");
        Installer.Uninstall(cancelRoot,new string[0]);
        // Remove partial copy: only what carries our marker.
        string partialRoot=Path.Combine(temp,"partial-root"), partialClient=Path.Combine(temp,"partial","copy");
        var cts2=new CancellationTokenSource(); var stopped=SetupOf(source,partialRoot,bundle,tested,saves,partialClient); stopped.Cancel=cts2.Token;
        stopped.Progress=p=>{ if(p.Step==SetupStep.CopyClient) cts2.Cancel(); };
        Throws<OperationCanceledException>(()=>Installer.Setup(stopped),"second cancel");
        string partialStage=InstallRecord.StageOf(partialClient);
        File.WriteAllText(Path.Combine(partialStage,InstallRecord.Marker),"foreign");
        Refuses(()=>Installer.RemovePartial(partialRoot,new string[0]),"remove partial refuses a foreign stage");
        Check(Directory.Exists(partialStage),"foreign stage kept");
        Refuses(()=>Installer.Recover(partialRoot,bundle,new string[0],saves,_=>{}),"recover refuses an unowned stage");
        Check(File.ReadAllText(Path.Combine(partialStage,InstallRecord.Marker))=="foreign","recover kept the unowned stage");
        File.WriteAllText(Path.Combine(partialStage,InstallRecord.Marker),InstallRecord.ReadRecord(partialRoot,"install.pending.json").InstallId);
        Installer.RemovePartial(partialRoot,new string[0]);
        Check(!Directory.Exists(partialStage) && !File.Exists(Path.Combine(partialRoot,"install.pending.json")),"remove partial clears the owned stage and record");
        // Remove partial copy with an installed app: the marker goes too, so setup, preflight and uninstall all still work.
        string appRoot=Path.Combine(temp,"app-partial-root"), appFolder=Path.Combine(appRoot,"app"), appClient=Path.Combine(temp,"app-partial","copy");
        CopyFixture(bundle,appFolder); WriteRel(appFolder,"SoloStone.exe","launcher");
        var cts3=new CancellationTokenSource(); var appStopped=SetupOf(source,appRoot,appFolder,tested,saves,appClient); appStopped.Cancel=cts3.Token;
        appStopped.Progress=p=>{ if(p.Step==SetupStep.CopyClient) cts3.Cancel(); };
        Throws<OperationCanceledException>(()=>Installer.Setup(appStopped),"cancel with an installed app");
        Check(File.Exists(Path.Combine(appFolder,".solostone-app")),"setup claimed the app folder");
        Installer.RemovePartial(appRoot,new string[0]);
        Check(!File.Exists(Path.Combine(appFolder,".solostone-app")),"remove partial clears the app marker");
        Installer.Preflight(appRoot,appFolder,new string[0],true); Check(true,"uninstall preflight still passes after remove partial");
        var again=Installer.Setup(SetupOf(source,appRoot,appFolder,tested,saves,appClient));
        Check(again.Client==appClient,"setup works again after remove partial");
        Installer.Uninstall(appRoot,new string[0]);
        // No record at all: the player closed the wizard. Uninstall still works, and only with a verified bundle.
        string lone=Path.Combine(temp,"lone-root"), loneApp=Path.Combine(lone,"app");
        Directory.CreateDirectory(lone);
        RejectsBecause(()=>Installer.Preflight(lone,loneApp,new string[0],true),"Installation is incomplete","no app and no record");
        CopyFixture(bundle,loneApp); WriteRel(loneApp,"SoloStone.exe","launcher");
        foreach(string name in new[]{"state","updates"}) { Directory.CreateDirectory(Path.Combine(lone,name)); File.WriteAllText(Path.Combine(lone,name,"f"),"x"); }
        Directory.CreateDirectory(Path.Combine(lone,"backups")); File.WriteAllText(Path.Combine(lone,"backups","keep"),"x");
        string goodPayload=File.ReadAllText(Path.Combine(loneApp,"payload","winhttp.dll"));
        File.WriteAllText(Path.Combine(loneApp,"payload","winhttp.dll"),"not ours");
        RejectsBecause(()=>Installer.Preflight(lone,loneApp,new string[0],true),"Installation is incomplete","app-only uninstall refuses a bundle that does not verify");
        Refuses(()=>Installer.UninstallAppOnly(lone,new string[0]),"app-only removal refuses it too");
        Check(File.Exists(Path.Combine(lone,"state","f")),"refused removal changed nothing");
        File.WriteAllText(Path.Combine(loneApp,"payload","winhttp.dll"),goodPayload);
        Installer.Preflight(lone,loneApp,new string[0],true); Check(true,"cancelled wizard passes the uninstall preflight");
        Installer.Preflight(lone,loneApp,new string[0],false); Check(true,"cancelled wizard passes setup preflight");
        File.WriteAllText(Path.Combine(loneApp,"payload","winhttp.dll"),"not ours");
        Refuses(()=>Installer.Preflight(lone,loneApp,new string[0],false),"setup refuses unverified app");
        File.WriteAllText(Path.Combine(loneApp,"payload","winhttp.dll"),goodPayload);
        Installer.UninstallAppOnly(lone,new string[0]);
        Check(!Directory.Exists(Path.Combine(lone,"state")) && !Directory.Exists(Path.Combine(lone,"updates")) && File.Exists(Path.Combine(lone,"backups","keep")),"app-only uninstall removes state, keeps backups");
        // With a partial copy: owned goes, unowned stays where it is.
        string partialLone=Path.Combine(temp,"lone2-root"), partialApp=Path.Combine(partialLone,"app"), partialCopy=Path.Combine(temp,"lone2","copy");
        CopyFixture(bundle,partialApp); WriteRel(partialApp,"SoloStone.exe","launcher");
        var cts4=new CancellationTokenSource(); var loneStop=SetupOf(source,partialLone,partialApp,tested,saves,partialCopy); loneStop.Cancel=cts4.Token;
        loneStop.Progress=p=>{ if(p.Step==SetupStep.CopyClient) cts4.Cancel(); };
        Throws<OperationCanceledException>(()=>Installer.Setup(loneStop),"cancel for app-only removal");
        Installer.UninstallAppOnly(partialLone,new string[0]);
        Check(!Directory.Exists(InstallRecord.StageOf(partialCopy)) && !File.Exists(Path.Combine(partialLone,"install.pending.json")),"app-only uninstall removes an owned partial copy");
        Refuses(()=>Installer.UninstallAppOnly(appRoot.Length==0?"":Path.Combine(temp,"absent-root"),new string[0]),"app-only removal needs an app");
        // A removed source drive does not stop launcher, plugin or uninstall: the copy is self-contained.
        string goneRoot=Path.Combine(temp,"gone-root"), goneClient=Path.Combine(temp,"gone","copy");
        var gone=Installer.Setup(SetupOf(source,goneRoot,bundle,tested,saves,goneClient));
        var moved=JsonBytes.Parse<InstallRecord>(JsonBytes.ToBytes(gone)); moved.Source=@"Z:\removed\Hearthstone"; Json.Save(Path.Combine(goneRoot,"install.json"),moved);
        Check(InstallRecord.Load(goneRoot).Source==moved.Source,"a removed source does not stop the record loading");
        Check(FirewallPlan.VerifiedRecord(goneRoot,new string[0],true).InstallId==gone.InstallId,"firewall planning works without the source");
        Check(Installer.Recover(goneRoot,bundle,new string[0],saves,_=>{}).InstallId==gone.InstallId,"recovery of a finished copy works without the source");
        Installer.Uninstall(goneRoot,new string[0]); Check(!Directory.Exists(goneClient),"uninstall works without the source");
        // Fresh setup with a pre-existing stage nobody owns refuses before any write.
        string squatRoot=Path.Combine(temp,"squat-root"), squatClient=Path.Combine(temp,"squat","copy");
        Directory.CreateDirectory(InstallRecord.StageOf(squatClient));
        RejectsBecause(()=>Installer.Setup(SetupOf(source,squatRoot,bundle,tested,saves,squatClient)),"does not own","fresh setup refuses a stage it does not own");
        Check(!Directory.Exists(squatRoot),"refused setup wrote nothing");
        RejectsBecause(()=>Installer.Setup(SetupOf(source,squatRoot,bundle,tested,saves,Path.Combine(temp,"a&b"))),"Firewall","firewall-unsafe path fails at the path check, not at UAC");
        // Warning client: accepted runs, version recorded.
        string warnRoot=Path.Combine(temp,"warn-root");
        var otherPatch=new TestedClient { Schema=1, Version="37.1.0", Files=tested.Files };
        var warned=Installer.Setup(SetupOf(source,warnRoot,bundle,otherPatch,saves,accept:true));
        Check(warned.ClientVersion=="37.0.0" && warned.Client==Installer.DefaultClient(warnRoot),"accepted warning installs at the default path");
        Installer.Uninstall(warnRoot,new string[0]);
        // Client file in a payload never installs.
        string badBundle=Path.Combine(temp,"bad-bundle"); CopyFixture(bundle,badBundle);
        string smuggled=Path.Combine(badBundle,"payload","Hearthstone_Data","Managed","Assembly-CSharp.dll"); Directory.CreateDirectory(Path.GetDirectoryName(smuggled)); File.WriteAllText(smuggled,"client bytes");
        var evil=Json.Read<Payload>(Path.Combine(badBundle,"payload.json")); evil.Files["Hearthstone_Data/Managed/Assembly-CSharp.dll"]=Hashes.FileHash(smuggled); Json.Save(Path.Combine(badBundle,"payload.json"),evil);
        RejectsBecause(()=>Installer.Setup(SetupOf(source,Path.Combine(temp,"evil-root"),badBundle,tested,saves)),"client file","payload with a client file refused at setup");
    }
}

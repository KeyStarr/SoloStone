using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using SoloStone;

sealed class FakeHost : IUpdateHost
{
    public string SaveRoot { get; set; }
    public string Busy; public Verdict Staged=new Verdict { KindName="Allow", TestedVersion="37.0.0", FoundVersion="37.0.0", Reasons=new string[0], Message="ok" };
    public int StagedRuns; public string LastStaged;
    public string IdleProblem() => Busy;
    // The real host starts another process and this one exits. Here the "other process" runs in place, with its own view of where it runs.
    public string Root; public bool InApp=true; public Action<string> Fault; public Func<string,string,bool> SameVolume; public UpdateResult HelperResult; public int HandOffs; public string LastHelper;
    public bool RunningFrom(string folder) => InApp;
    UpdateEngine Child() { var engine=new UpdateEngine(Root,this) { Fault=Fault }; if(SameVolume!=null) engine.SameVolume=SameVolume; return engine; }
    void AsHelper(Action run) { HandOffs++; bool was=InApp; InApp=false; try { run(); } finally { InApp=was; } }
    public void HandOffSwap(string stagedApp,string version) { LastHelper=stagedApp; AsHelper(()=>HelperResult=Child().FinishSwap(version)); }
    public void HandOffUndo(string helperFolder) { LastHelper=helperFolder; if(!File.Exists(Path.Combine(helperFolder,"SoloStone.exe"))) throw new Exception("helper has no launcher"); AsHelper(()=>Child().FinishUndo()); }
    public Verdict CheckStaged(string stagedApp,string root) { StagedRuns++; LastStaged=stagedApp; if(Staged==null) throw new IOException("staged launcher would not start"); return Staged; }
}
sealed class SimulatedCrash : Exception { }
static partial class Program
{
    sealed class Fixture
    {
        public string Base, Root, App, Client, Saves; public FakeHost Host; public InstallRecord Record;
        public UpdateEngine Engine(Action<string> fault=null) { Host.Fault=fault; var engine=new UpdateEngine(Root,Host) { Fault=fault }; if(Host.SameVolume!=null) engine.SameVolume=Host.SameVolume; return engine; }
        public string Code => TreeHash(App)+"|"+PayloadHash()+"|"+Hashes.FileHash(Path.Combine(Root,"install.json"));
        string PayloadHash() { var lines=Directory.GetFiles(Client,"*",SearchOption.AllDirectories).Where(p=>!p.Contains("Hearthstone_Data")).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>p.Substring(Client.Length)+"="+Hashes.FileHash(p)); return string.Join(",",lines); }
    }
    static string ProfileXml(int version,string pending="",string reward="",string pack="false") => "<LocalProfileData><Version>"+version+"</Version><Pending>"+pending+"</Pending><PendingRewardPresentationId>"+reward+"</PendingRewardPresentationId><PackRevealPending>"+pack+"</PackRevealPending></LocalProfileData>";
    static void WriteRel(string folder,string relative,string text) { string file=Path.Combine(folder,relative.Replace('/',Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file,text); }
    static readonly string[] CorePayload={"winhttp.dll","doorstop_config.ini","client.config","BepInEx/unstripped_corlib/mscorlib.dll","BepInEx/core/BepInEx.dll","BepInEx/core/BepInEx.Preloader.dll","BepInEx/core/0Harmony.dll","BepInEx/plugins/SoloStone.Plugin.dll","BepInEx/plugins/SabberStoneCore.dll"};
    // Writes a launcher folder: exe, libraries, release.json, payload.json and payload\.
    static Payload WriteAppTree(string app,string version,int saveSchema,string pluginText,string[] extraApp,string[] extraPayload) {
        WriteRel(app,"SoloStone.exe","launcher "+version); WriteRel(app,"BouncyCastle.Cryptography.dll","same library in every release");
        foreach(string extra in extraApp??new string[0]) WriteRel(app,extra,"app extra "+version);
        File.WriteAllText(Path.Combine(app,"release.json"),"{\"schema\":1,\"version\":\""+version+"\",\"saveSchema\":"+saveSchema+",\"sourceRevision\":\"rev-"+version+"\"}");
        var payload=new Payload { Schema=1, BepInExVersion="f", DoorstopVersion="f", SourceRevision="rev", Files=new Dictionary<string,string>() };
        foreach(string file in CorePayload) WriteRel(app,"payload/"+file,file.EndsWith("SoloStone.Plugin.dll")?pluginText:"unchanged "+file);
        foreach(string file in extraPayload??new string[0]) WriteRel(app,"payload/"+file,"extra "+version);
        foreach(string file in Safety.Tree(Path.Combine(app,"payload"))) payload.Files[file.Substring(Path.Combine(app,"payload").Length+1).Replace(Path.DirectorySeparatorChar,'/')]=Hashes.FileHash(file);
        payload.PluginSha256=payload.Files["BepInEx/plugins/SoloStone.Plugin.dll"]; payload.EngineSha256=payload.Files["BepInEx/plugins/SabberStoneCore.dll"];
        Json.Save(Path.Combine(app,"payload.json"),payload);
        return payload;
    }
    static int fixtures;
    static Fixture NewFixture(string temp,bool separateClient=true,string profile=null) {
        string b=Path.Combine(temp,"e"+(++fixtures)); Directory.CreateDirectory(b);
        var f=new Fixture { Base=b, Root=Path.Combine(b,"root"), Saves=Path.Combine(b,"saves") };
        f.App=Path.Combine(f.Root,"app"); f.Client=separateClient?Path.Combine(b,"elsewhere","copy"):Path.Combine(f.Root,"client");
        f.Host=new FakeHost { SaveRoot=f.Saves, Root=f.Root };
        var payload=WriteAppTree(f.App,"1.0.0",15,"plugin 1.0.0",new[]{"OldOnly.dll","sub/old.txt"},new[]{"BepInEx/plugins/oldextra.dll"});
        WriteRel(f.App,"unins000.exe","inno uninstaller"); WriteRel(f.App,".solostone-app","owner");
        string source=Path.Combine(b,"game"); var tested=FakeClient(source,"37.0.0");
        var files=new Dictionary<string,string>(tested.Files);
        foreach(string file in Safety.Tree(source)) { string rel=file.Substring(source.Length+1).Replace(Path.DirectorySeparatorChar,'/'); string t=Path.Combine(f.Client,rel.Replace('/',Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(t)); File.Copy(file,t); }
        foreach(var row in payload.Files) { string t=Safety.Child(f.Client,row.Key); Directory.CreateDirectory(Path.GetDirectoryName(t)); File.Copy(Safety.Child(Path.Combine(f.App,"payload"),row.Key),t); }
        WriteRel(f.Client,"BepInEx/config/player.cfg","player settings");
        // A real install carries the patch and its recorded hashes, so every swap, undo and rollback in these tests carries them too.
        f.Record=new InstallRecord { Schema=2, InstallId=Guid.NewGuid().ToString("N"), Root=f.Root, Source=source, ClientPath=f.Client, ClientVersion="37.0.0", ReleaseVersion="1.0.0", Files=files, Payload=payload, IdentityFiles=ClientIdentity.Apply(f.Client) };
        File.WriteAllText(Path.Combine(f.Client,InstallRecord.Marker),f.Record.InstallId);
        File.WriteAllText(Path.Combine(f.App,".solostone-app"),f.Record.InstallId);
        Json.Save(Path.Combine(f.Root,"install.json"),f.Record);
        Directory.CreateDirectory(f.Saves); File.WriteAllText(Path.Combine(f.Saves,"profile.xml"),profile??ProfileXml(15));
        WriteRel(f.Saves,"profiles/"+Guid.NewGuid().ToString("N")+"/profile.xml",ProfileXml(14));
        return f;
    }
    static LatestRelease BuildRelease(Fixture f,string version,int saveSchema=15,Action<string> tamper=null) {
        string work=Path.Combine(f.Base,"build-"+version); FsOps.DeleteTree(work);
        WriteAppTree(work,version,saveSchema,"plugin "+version,new[]{"NewOnly.dll"},new[]{"BepInEx/plugins/newextra.dll"});
        tamper?.Invoke(work);
        string zip=Path.Combine(f.Base,"release-"+version+".zip"); if(File.Exists(zip)) File.Delete(zip);
        ZipFile.CreateFromDirectory(work,zip);
        return Latest(zip,version,saveSchema);
    }
    static LatestRelease Latest(string zip,string version,int saveSchema=15) => new LatestRelease { Version=version, SaveSchema=saveSchema, Asset=new ReleaseAsset { Name="SoloStone-"+version+".zip", Size=new FileInfo(zip).Length, Sha256=Hashes.FileHash(zip) } };
    static string ZipOf(Fixture f,string version) => Path.Combine(f.Base,"release-"+version+".zip");
    static UpdateResult Update(Fixture f,string version,int saveSchema=15,UpdateEngine engine=null,bool accept=false) {
        var latest=BuildRelease(f,version,saveSchema);
        engine=engine??f.Engine();
        engine.Stage(ZipOf(f,version),latest);
        f.Host.HelperResult=null;
        var result=engine.Apply(latest,accept);
        return f.Host.HelperResult??result;
    }
    // The shortcut's target exists and is one whole launcher at this very moment. Asked at every step of swap, undo and restore.
    static void ExeInPlace(Fixture f) {
        string exe=Path.Combine(f.App,"SoloStone.exe");
        if(!File.Exists(exe)) throw new Exception("the shortcut's SoloStone.exe is missing mid-swap");
        string text=File.ReadAllText(exe);
        if(!text.StartsWith("launcher ",StringComparison.Ordinal)) throw new Exception("the shortcut's SoloStone.exe is not a whole launcher: "+text);
    }
    // The app tree without its per-install marker, so two fixtures compare.
    static string AppHash(Fixture f) {
        string marker=Path.Combine(f.App,".solostone-app"), id=File.ReadAllText(marker);
        File.WriteAllText(marker,"same");
        try { return TreeHash(f.App); } finally { File.WriteAllText(marker,id); }
    }
    enum AfterCrash { ShortcutResume, SetupRerun, Uninstall }
    // Crash at every fault point, then recover the way a player would: the shortcut, setup again, or uninstall.
    static void CrashMatrix(string temp,bool sameVolume,AfterCrash after) {
        int crashes=0;
        var clean=NewFixture(temp); // Same shape, used to learn the exact new app tree once.
        var built=BuildRelease(clean,"2.0.0"); var e=clean.Engine(); e.Stage(ZipOf(clean,"2.0.0"),built); e.Apply(built,false);
        string newApp=AppHash(clean);
        FsOps.DeleteTree(clean.Base);
        for(int point=0;;point++) {
            var f=NewFixture(temp);
            string oldCode=f.Code, savesBefore=TreeHash(f.Saves);
            int seen=0; bool crashed=false;
            Action<string> fault=label=>{ ExeInPlace(f); if(seen++==point) { crashed=true; throw new SimulatedCrash(); } };
            if(!sameVolume) f.Host.SameVolume=(a,b)=>false;
            var release=BuildRelease(f,"2.0.0");
            var engine=f.Engine(fault);
            engine.Stage(ZipOf(f,"2.0.0"),release);
            try {
                var result=engine.Apply(release,false);
                if(result.Outcome!=UpdateOutcome.Swapped || f.Host.HelperResult?.Outcome!=UpdateOutcome.Swapped) throw new Exception("unexpected: "+result.Message);
            } catch(SimulatedCrash) { }
            if(!crashed) { Check(AppHash(f)==newApp,"finished run yields the new tree"); break; }
            crashes++;
            ExeInPlace(f);
            Check(File.Exists(Path.Combine(f.App,"unins000.exe")) && File.Exists(Path.Combine(f.App,".solostone-app")),"uninstaller and app marker never move, point "+point);
            if(after==AfterCrash.Uninstall) {
                Installer.Preflight(f.Root,f.App,new string[0],true);
                Installer.Uninstall(f.Root,new string[0]);
                Check(!File.Exists(Path.Combine(f.Root,"install.json")) && !Directory.Exists(f.Client) && !Directory.Exists(Path.Combine(f.Root,"updates")) && !Directory.Exists(Path.Combine(f.Root,"releases")),"uninstall works mid-swap, point "+point);
                Check(TreeHash(f.Saves)==savesBefore,"uninstall mid-swap keeps saves, point "+point);
                FsOps.DeleteTree(f.Base);
                continue;
            }
            if(after==AfterCrash.SetupRerun) {
                // Setup's temporary launcher runs outside the app folder.
                f.Host.InApp=false;
                f.Engine().Resume();
                f.Host.InApp=true;
                RejectsBecause(()=>Installer.Preflight(f.Root,f.App,new string[0],false),"already contains","setup again after a kill says it is installed, point "+point);
            }
            else {
                // A second crash during recovery: resume must still converge from there.
                int resumeSeen=0, resumeAt=point%7;
                var second=f.Engine(label=>{ ExeInPlace(f); if(resumeSeen++==resumeAt) throw new SimulatedCrash(); });
                try { second.Resume(); } catch(SimulatedCrash) { }
                ExeInPlace(f);
                f.Engine().Resume();
            }
            var recovered=f.Engine();
            if(AppHash(f)==newApp) {
                Check(recovered.Journal?.Phase==SwapJournal.Swapped && recovered.Journal.Health=="pending","new tree after a crash waits for its health check, point "+point);
                var done=InstallRecord.Load(f.Root);
                Check(done.ReleaseVersion=="2.0.0","record names the new version, point "+point);
                done.Payload.Verify(f.Client);
                Check(File.ReadAllText(Path.Combine(f.App,"SoloStone.exe"))=="launcher 2.0.0","new launcher in place, point "+point);
            } else {
                Check(f.Code==oldCode,"old tree exactly restored after a crash at point "+point+" ("+(sameVolume?"same":"cross")+" volume)");
                Check(!recovered.JournalActive && !Directory.Exists(Path.Combine(f.Root,"releases","1.0.0")),"undo clears journal and release, point "+point);
                Check(InstallRecord.Load(f.Root).ReleaseVersion=="1.0.0","record names the old version, point "+point);
            }
            Check(TreeHash(f.Saves)==savesBefore,"saves untouched by a crash at point "+point);
            Check(File.ReadAllText(Path.Combine(f.Client,"BepInEx","config","player.cfg"))=="player settings","player config never moves, point "+point);
            FsOps.DeleteTree(f.Base);
        }
        Check(crashes>40,"crash matrix covered every move and phase: "+crashes);
    }
    // A release whose launcher bytes did not change has no replace, so the phase alone is the commit point.
    static void IdenticalLauncher(string temp) {
        for(int point=0;;point++) {
            var f=NewFixture(temp); string oldCode=f.Code;
            File.WriteAllText(Path.Combine(f.App,"SoloStone.exe"),"launcher same");
            oldCode=f.Code;
            var release=BuildRelease(f,"2.0.0",15,work=>File.WriteAllText(Path.Combine(work,"SoloStone.exe"),"launcher same"));
            int seen=0; bool crashed=false;
            var engine=f.Engine(label=>{ ExeInPlace(f); if(seen++==point) { crashed=true; throw new SimulatedCrash(); } });
            engine.Stage(ZipOf(f,"2.0.0"),release);
            try { engine.Apply(release,false); } catch(SimulatedCrash) { }
            if(!crashed) { FsOps.DeleteTree(f.Base); Check(point>20,"identical-launcher matrix covered: "+point); break; }
            f.Engine().Resume();
            bool isNew=InstallRecord.Load(f.Root).ReleaseVersion=="2.0.0";
            Check(isNew?f.Engine().Journal?.Phase==SwapJournal.Swapped && File.Exists(Path.Combine(f.App,"NewOnly.dll")):f.Code==oldCode,"identical launcher converges, point "+point);
            FsOps.DeleteTree(f.Base);
        }
    }
    // Who may do what after a kill: the app folder's launcher hands off, and refuses plainly when nothing can help.
    static void HandOffRules(string temp) {
        // Old launcher, swap cut short: the staged launcher finishes the undo.
        var f=NewFixture(temp); string oldCode=f.Code; var release=BuildRelease(f,"2.0.0");
        var engine=f.Engine(label=>{ if(label.StartsWith("start:",StringComparison.Ordinal) && label.EndsWith(":10",StringComparison.Ordinal)==false && label=="start:12") throw new SimulatedCrash(); });
        engine.Stage(ZipOf(f,"2.0.0"),release);
        Throws<SimulatedCrash>(()=>engine.Apply(release,false),"kill mid-swap");
        int before=f.Host.HandOffs;
        Check(f.Engine().Resume()==ResumeOutcome.HandedOff && f.Host.HandOffs==before+1 && f.Host.LastHelper==f.Engine().StagedApp("2.0.0"),"the shortcut's launcher hands the undo to the staged launcher");
        Check(f.Code==oldCode && !f.Engine().JournalActive,"the handed-off undo restored the old tree");
        // Staged launcher gone, old launcher in place: nothing else can do it, and the old exe need not change, so it undoes in place.
        f=NewFixture(temp); oldCode=f.Code; release=BuildRelease(f,"2.0.0");
        engine=f.Engine(label=>{ if(label=="start:12") throw new SimulatedCrash(); });
        engine.Stage(ZipOf(f,"2.0.0"),release);
        Throws<SimulatedCrash>(()=>engine.Apply(release,false),"second kill");
        File.Delete(Path.Combine(f.Engine().StagedApp("2.0.0"),"SoloStone.exe"));
        // The undo needs the staged files it copied back to nothing: copies are only deleted.
        Check(f.Engine().Resume()==ResumeOutcome.Undone && f.Code==oldCode,"with no helper the old launcher undoes in place");
        // Restore cut short with the new launcher in place and its helper gone: refused in words, nothing half done.
        f=NewFixture(temp); Check(Update(f,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap");
        var failing=f.Engine(label=>{ if(label=="helper:ready") throw new SimulatedCrash(); });
        Throws<SimulatedCrash>(()=>failing.ReportHealth(HealthOutcome.Failed,"exit 42"),"kill as the restore starts");
        FsOps.DeleteTree(Path.Combine(f.Root,"updates","undo-helper"));
        RejectsBecause(()=>f.Engine().Resume(),"restore files are gone","no helper for a committed restore is refused");
        ExeInPlace(f);
        // A launcher outside the app folder can still do it: setup's, or the next release's.
        f.Host.InApp=false; f.Engine().Resume(); f.Host.InApp=true;
        Check(InstallRecord.Load(f.Root).ReleaseVersion=="1.0.0" && File.ReadAllText(Path.Combine(f.App,"SoloStone.exe"))=="launcher 1.0.0","setup's launcher finishes that restore");
        // Refusals from the helper reach the next window.
        f=NewFixture(temp); release=BuildRelease(f,"2.0.0"); engine=f.Engine(); engine.Stage(ZipOf(f,"2.0.0"),release);
        f.Host.Busy=null;
        var note=new UpdateEngine(f.Root,f.Host); f.Host.InApp=false; f.Host.Busy="Quit Battle.net.";
        var refused=note.FinishSwap("2.0.0");
        Check(refused.Outcome==UpdateOutcome.Refused && note.LastNote().Message.Contains("Battle.net"),"a refusal in the helper leaves a note");
        var report=StatusReport.Build(f.Root,note,TestedClient.Compiled(),UpdateConfig.From("","","",0),new NoBindingManifest(),null);
        Check(report.UpdateNote.Contains("Battle.net"),"the status report carries the note");
        var model=new SoloStone.LauncherModel(); model.ApplyReport(report); model.ShowStartNote(report);
        Check(model.Notice.Contains("Battle.net") && model.NoticeIsProblem,"the launcher window shows the note");
        note.ClearNote(); Check(note.LastNote()==null,"the note clears");
        Check(note.FinishSwap("../evil").Outcome==UpdateOutcome.Failed,"a helper takes only a valid version");
    }
    static void SaveScanTests(string temp) {
        string saves=Path.Combine(temp,"scan"); Directory.CreateDirectory(saves);
        Check(Saves.Scan(Path.Combine(temp,"no-saves")).Problem==null && Saves.Scan(Path.Combine(temp,"no-saves")).MaxVersion==0,"no save tree is idle");
        File.WriteAllText(Path.Combine(saves,"profile.xml"),ProfileXml(12));
        string slot=Path.Combine(saves,"profiles",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(slot);
        File.WriteAllText(Path.Combine(slot,"profile.xml"),ProfileXml(15));
        var scan=Saves.Scan(saves); Check(scan.Problem==null && scan.MaxVersion==15 && scan.Count==2,"root and slot saves scanned");
        WriteRel(saves,"profiles/.deleted/x/profile.xml",ProfileXml(99,pending:"old")); WriteRel(saves,"profiles/.creating-1/profile.xml","garbage");
        Check(Saves.Scan(saves).MaxVersion==15 && Saves.Scan(saves).Problem==null,"soft-deleted and half-created slots are not live saves");
        Func<string,SaveScan> with=xml=>{ File.WriteAllText(Path.Combine(slot,"profile.xml"),xml); return Saves.Scan(saves); };
        Check(with(ProfileXml(15,pending:"<Match/>")).Pending!=null,"pending match refused");
        Check(with(ProfileXml(15,reward:"abc")).Pending!=null,"pending reward presentation refused");
        Check(with(ProfileXml(15,pack:"true")).Pending!=null,"pending pack reveal refused");
        Check(with(ProfileXml(15,pending:"   ")).Problem==null,"blank pending is idle");
        foreach(string bad in new[]{"garbage","<Other/>","<LocalProfileData/>","<LocalProfileData><Version>x</Version><PackRevealPending>false</PackRevealPending></LocalProfileData>",
            "<LocalProfileData><Version>15</Version></LocalProfileData>","<LocalProfileData><Version>15</Version><PackRevealPending>maybe</PackRevealPending></LocalProfileData>",
            "<LocalProfileData><Version>15</Version><Version>16</Version><PackRevealPending>false</PackRevealPending></LocalProfileData>",
            "<!DOCTYPE x [<!ENTITY e \"y\">]><LocalProfileData><Version>15</Version><PackRevealPending>false</PackRevealPending></LocalProfileData>"})
            Check(with(bad).Unreadable!=null,"unreadable save refused: "+bad.Substring(0,Math.Min(30,bad.Length)));
        File.WriteAllText(Path.Combine(slot,"profile.xml"),ProfileXml(15));
        string target=Path.Combine(temp,"scan-target"); Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target,"profile.xml"),ProfileXml(15));
        File.Delete(Path.Combine(slot,"profile.xml")); File.CreateSymbolicLink(Path.Combine(slot,"profile.xml"),Path.Combine(target,"profile.xml"));
        Check(Saves.Scan(saves).Unreadable!=null,"linked save refused");
    }
    static void StageRefusals(string temp) {
        var f=NewFixture(temp); string code=f.Code;
        Func<LatestRelease,string,Action> stage=(latest,version)=>()=>f.Engine().Stage(ZipOf(f,version),latest);
        var good=BuildRelease(f,"2.0.0");
        // Tampered zip: size and hash.
        var wrongHash=new LatestRelease { Version="2.0.0", SaveSchema=15, Asset=new ReleaseAsset { Name=good.Asset.Name, Size=good.Asset.Size, Sha256=new string('A',64) } };
        RejectsBecause(stage(wrongHash,"2.0.0"),"hash check","tampered zip refused by hash");
        var wrongSize=new LatestRelease { Version="2.0.0", SaveSchema=15, Asset=new ReleaseAsset { Name=good.Asset.Name, Size=good.Asset.Size+1, Sha256=good.Asset.Sha256 } };
        RejectsBecause(stage(wrongSize,"2.0.0"),"hash check","tampered zip refused by size");
        // Malformed archives.
        Func<string,Action<ZipArchive>,Action> crafted=(name,write)=>{
            string work=Path.Combine(f.Base,"build-crafted"); FsOps.DeleteTree(work);
            WriteAppTree(work,"3.0.0",15,"plugin 3",null,null);
            string zip=ZipOf(f,"3.0.0"); if(File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(work,zip);
            using(var archive=ZipFile.Open(zip,ZipArchiveMode.Update)) write(archive);
            var latest=Latest(zip,"3.0.0");
            return ()=>f.Engine().Stage(zip,latest);
        };
        foreach(string evil in new[]{"../evil.txt","sub/../../evil.txt","/abs/evil.txt","C:/evil.txt","sub\\evil.txt","a:b.txt","sub//x.txt","NUL.txt","trailing./x.txt"}) {
            Refuses(crafted(evil,a=>{ using(var w=new StreamWriter(a.CreateEntry(evil).Open())) w.Write("x"); }),"zip entry refused: "+evil);
            Check(!File.Exists(Path.Combine(f.Root,"evil.txt")) && !File.Exists(Path.Combine(f.Base,"evil.txt")) && !File.Exists("/abs/evil.txt"),"zip-slip wrote nothing: "+evil);
        }
        RejectsBecause(crafted("link",a=>{ var e=a.CreateEntry("link.dll"); e.ExternalAttributes=unchecked((int)(0xA1FF0000)); using(var w=new StreamWriter(e.Open())) w.Write("/etc/passwd"); }),"link","symlink entry refused");
        RejectsBecause(crafted("dup",a=>{ using(var w=new StreamWriter(a.CreateEntry("SOLOSTONE.EXE").Open())) w.Write("x"); }),"twice","case-duplicate entry refused");
        Refuses(crafted("many",a=>{ for(int i=0;i<UpdateEngine.MaxEntries+1;i++) a.CreateEntry("f"+i); }),"too many entries refused");
        // Payload rules on a well-formed archive.
        Func<Action<string>,string,Action> tampered=(edit,version)=>{ var latest=BuildRelease(f,version,15,edit); return ()=>f.Engine().Stage(ZipOf(f,version),latest); };
        Action<string,string,string> addPayload=(work,rel,text)=>{ WriteRel(work,"payload/"+rel,text); var p=Json.Read<Payload>(Path.Combine(work,"payload.json")); p.Files[rel]=Hashes.FileHash(Safety.Child(Path.Combine(work,"payload"),rel)); Json.Save(Path.Combine(work,"payload.json"),p); };
        RejectsBecause(tampered(w=>addPayload(w,"tool.exe","x"),"3.0.1"),"program","exe in payload refused");
        RejectsBecause(tampered(w=>addPayload(w,"Hearthstone.exe","x"),"3.0.2"),"program","client exe in payload refused");
        RejectsBecause(tampered(w=>addPayload(w,"UnityPlayer.dll","x"),"3.0.3"),"client file","client DLL in payload refused");
        RejectsBecause(tampered(w=>addPayload(w,"Hearthstone_Data/Managed/Assembly-CSharp.dll","x"),"3.0.4"),"client file","client managed DLL in payload refused");
        RejectsBecause(tampered(w=>addPayload(w,"Hearthstone_Data/foo.bin","x"),"3.0.5"),"client file","client folder in payload refused");
        RejectsBecause(tampered(w=>addPayload(w,"Hearthstone_Data/globalgamemanagers","x"),"3.0.13"),"client file","a file setup patches can't ride in a payload: globalgamemanagers");
        RejectsBecause(tampered(w=>addPayload(w,"Hearthstone_Data/app.info","x"),"3.0.14"),"client file","a file setup patches can't ride in a payload: app.info");
        RejectsBecause(tampered(w=>WriteRel(w,"payload/unlisted.dll","x"),"3.0.6"),"mod list","payload file outside its manifest refused");
        RejectsBecause(tampered(w=>File.WriteAllText(Path.Combine(w,"payload","winhttp.dll"),"changed"),"3.0.7"),"File check","payload file changed after its manifest refused");
        RejectsBecause(tampered(w=>File.WriteAllText(Path.Combine(w,"release.json"),"{\"schema\":1,\"version\":\"9.9.9\",\"saveSchema\":15,\"sourceRevision\":\"r\"}"),"3.0.8"),"release file","release.json version mismatch refused");
        RejectsBecause(tampered(w=>File.Delete(Path.Combine(w,"SoloStone.exe")),"3.0.9"),"launcher","update without its launcher refused");
        RejectsBecause(tampered(w=>WriteRel(w,"Assembly-CSharp.dll","x"),"3.0.10"),"client file","client file beside the launcher refused");
        RejectsBecause(tampered(w=>WriteRel(w,"unins000.exe","x"),"3.0.11"),"manages itself","uninstaller in an update refused");
        RejectsBecause(tampered(w=>WriteRel(w,"helper.exe","x"),"3.0.12"),"unexpected program","second program in an update refused");
        Check(f.Code==code && !f.Engine().JournalActive,"no refused update touched the running version");
        Check(PayloadRules.IsClientFile("Blizzard.T5.Core.dll") && PayloadRules.IsClientFile("sub/UnityEngine.CoreModule.dll") && PayloadRules.IsClientFile("x.unity3d") && !PayloadRules.IsClientFile("BepInEx/core/BepInEx.dll") && !PayloadRules.IsClientFile("BepInEx/plugins/SoloStone.Plugin.dll"),"known Blizzard names only");
    }
    static void HoldAndGateTests(string temp) {
        // Block holds; not retried; a newer version is offered again.
        var f=NewFixture(temp); string code=f.Code;
        f.Host.Staged=ClientPolicy.Blocked("The new version needs Hearthstone 38.");
        var result=Update(f,"2.0.0");
        var engine=f.Engine();
        Check(result.Outcome==UpdateOutcome.Held && f.Host.LastStaged.EndsWith(Path.Combine("2.0.0","app"),StringComparison.Ordinal),"staged launcher decides, and a Block holds");
        Check(f.Code==code && !Directory.Exists(Path.Combine(f.Root,"updates","2.0.0")),"held update leaves the current version running");
        Check(engine.Held().Version=="2.0.0" && engine.Held().Reason.Contains("Hearthstone 38"),"held.json records version and reason");
        Check(UpdateService.OfferProblem(new LatestRelease { Version="2.0.0" },"1.0.0",engine.Held())!=null,"held version not retried");
        f.Host.Staged=new FakeHost().Staged;
        result=Update(f,"2.0.1");
        Check(result.Outcome==UpdateOutcome.Swapped && engine.Held()==null,"newer version installs and clears the hold");
        // A check that could not run says nothing about the release: no hold, no Allow, and the next start tries again.
        f=NewFixture(temp); code=f.Code; f.Host.Staged=null;
        var transient=Update(f,"2.0.0");
        Check(transient.Outcome==UpdateOutcome.Failed && f.Code==code && f.Engine().Held()==null && !f.Engine().JournalActive,"a staged check that could not run holds nothing");
        f.Host.Staged=new FakeHost().Staged;
        Check(Update(f,"2.0.0").Outcome==UpdateOutcome.Swapped,"the same version installs on the next try");
        // A launcher that started and crashed is a verdict: WindowsUpdateHost reports it as a Block.
        f=NewFixture(temp); code=f.Code; f.Host.Staged=ClientPolicy.Blocked("The new version's launcher failed its client check (exit 3).");
        Check(Update(f,"2.0.0").Outcome==UpdateOutcome.Held && f.Code==code && f.Engine().Held().Version=="2.0.0","a crashed staged launcher holds");
        Check(Verdict.TryParse("garbage")==null && Verdict.TryParse(ClientPolicy.Blocked("x").ToJson()).Kind==VerdictKind.Block,"unreadable output is no verdict");
        // Warning asks. Acceptance installs.
        f=NewFixture(temp); code=f.Code;
        f.Host.Staged=new Verdict { KindName="Warning", TestedVersion="37.4.0", FoundVersion="37.0.0", Reasons=new string[0], Message="untested" };
        result=Update(f,"2.0.0");
        Check(result.Outcome==UpdateOutcome.NeedsConfirmation && f.Code==code && Directory.Exists(f.Engine().StagedApp("2.0.0")),"warning waits for confirmation with the stage intact");
        Check(f.Engine().Apply(BuildRelease(f,"2.0.0"),true).Outcome==UpdateOutcome.Swapped,"confirmed warning installs");
        // Idle gate.
        f=NewFixture(temp); code=f.Code; f.Host.Busy="A game client is already running.";
        result=Update(f,"2.0.0"); Check(result.Outcome==UpdateOutcome.Refused && result.Message.Contains("already running") && f.Code==code,"running client refuses the swap");
        Check(new Backups(f.Root).List().Length==0,"refusal makes no backup");
        f.Host.Busy=null; string slotProfile=Directory.GetFiles(Path.Combine(f.Saves,"profiles"),"profile.xml",SearchOption.AllDirectories).Single();
        File.WriteAllText(slotProfile,ProfileXml(15,pending:"m"));
        result=f.Engine().Apply(BuildRelease(f,"2.0.0"),false); Check(result.Outcome==UpdateOutcome.Refused && result.Message.Contains("pending") && f.Code==code,"pending match in any slot refuses the swap");
        File.WriteAllText(slotProfile,"garbage");
        result=f.Engine().Apply(BuildRelease(f,"2.0.0"),false); Check(result.Outcome==UpdateOutcome.Refused && result.Message.Contains("cannot be read") && f.Code==code,"unreadable save refuses the swap with a reason");
        // Never not-newer.
        f=NewFixture(temp); code=f.Code;
        foreach(string version in new[]{"1.0.0","0.9.0"}) {
            var latest=BuildRelease(f,version); var e=f.Engine(); e.Stage(ZipOf(f,version),latest);
            Refuses(()=>e.Apply(latest,false),"downgrade or same version refused by the engine: "+version);
        }
        Check(f.Code==code,"downgrade attempts change nothing");
    }
    static void BackupTests(string temp) {
        var f=NewFixture(temp); string saves=TreeHash(f.Saves);
        var backups=new Backups(f.Root);
        string config=Path.Combine(f.Client,"BepInEx","config");
        string first=backups.Create(f.Saves,config,"1.0.0");
        Check(TreeHash(f.Saves)==saves && Directory.GetFiles(f.Saves).Length==1,"backup never writes inside the save tree");
        var manifest=backups.Verified(first);
        Check(manifest.SaveSchema==15 && manifest.From=="1.0.0" && manifest.Files.Keys.Count(k=>k.StartsWith("saves/"))==2 && manifest.Files.ContainsKey("config/player.cfg"),"backup holds saves, config and a hash manifest");
        Check(File.ReadAllText(Path.Combine(backups.PathOf(first),"saves","profile.xml"))==ProfileXml(15),"backup copied the save");
        File.WriteAllText(Path.Combine(backups.PathOf(first),"saves","profile.xml"),"bitrot");
        Refuses(()=>backups.Verified(first),"damaged backup refused");
        for(int i=0;i<4;i++) backups.Create(f.Saves,config,"1.0."+i);
        Check(backups.List().Length==Backups.Keep && !Directory.Exists(backups.PathOf(first)),"keeps the newest three");
        Check(backups.List().Select(b=>b.From).SequenceEqual(new[]{"1.0.3","1.0.2","1.0.1"}),"newest first");
        foreach(string bad in new[]{"../x","20260101T000000000Z-../../x","x",null}) Refuses(()=>backups.PathOf(bad),"backup id refused: "+bad);
    }
    static void HealthTests(string temp) {
        Check(Health.Classify(true,false,null)==HealthOutcome.Confirmed,"plugin marker confirms");
        Check(Health.Classify(false,false,40)==HealthOutcome.Failed && Health.Classify(false,false,42)==HealthOutcome.Failed,"exit 40 and 42 fail");
        Check(Health.Classify(false,true,null)==HealthOutcome.RetryLater,"a marker timeout retries later");
        foreach(int crash in new[]{1,-1,unchecked((int)0xC0000005),44,45}) Check(Health.Classify(false,false,crash)==HealthOutcome.RetryLater,"a crash-like exit retries later: "+crash);
        foreach(int refusal in new[]{0,41,46,Health.ExitFirewallRefused,Health.ExitRefused}) Check(Health.Classify(false,false,refusal)==HealthOutcome.NoEvidence,"this exit says nothing about the release: "+refusal);
        Check(Health.Classify(false,false,null)==HealthOutcome.NoEvidence,"no exit code is no evidence");
        // A timeout restores and holds for a day, so the restart cannot loop; one more try, then a permanent hold.
        var slow=NewFixture(temp); string slowBefore=slow.Code;
        Check(Update(slow,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap for timeout");
        var slowHealth=slow.Engine().ReportHealth(HealthOutcome.RetryLater,"The game did not confirm the mod in time.");
        var slowHeld=slow.Engine().Held();
        Check(slowHealth.Restored && slow.Code==slowBefore && slowHeld!=null && slowHeld.Version=="2.0.0" && slowHeld.Retries==1 && slowHeld.RetryAfter!=null,"a timeout restores and holds with a retry time");
        Check(UpdateService.OfferProblem(new LatestRelease { Version="2.0.0" },"1.0.0",slowHeld)!=null,"the restart right after a timeout does not offer that version again");
        Check(slowHeld.BlocksAt(DateTime.UtcNow.AddHours(23)) && !slowHeld.BlocksAt(DateTime.UtcNow.AddHours(25)),"the retry opens after a day");
        Check(UpdateService.OfferProblem(new LatestRelease { Version="2.0.0" },"1.0.0",new HeldUpdate { Version="2.0.0", Reason="x", RetryAfter=DateTime.UtcNow.AddHours(-1).ToString("o") })==null,"an expired retry time offers the version once more");
        Check(UpdateService.OfferProblem(new LatestRelease { Version="3.0.0" },"1.0.0",slowHeld)==null,"a newer release is not held by the timeout");
        Check(new HeldUpdate { Version="2.0.0" }.BlocksAt(DateTime.UtcNow.AddYears(5)),"a hold without a retry time is permanent");
        // Confirmed keeps the previous release for rollback and clears the journal.
        var f=NewFixture(temp); string oldCode=f.Code;
        Check(Update(f,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap");
        var engine=f.Engine(); string newCode=f.Code;
        Check(engine.Resume()==ResumeOutcome.HealthPending && engine.Journal.Health=="pending" && f.Code==newCode,"next start finds a pending health check");
        Check(!engine.ReportHealth(HealthOutcome.NoEvidence,null).Restored && engine.JournalActive,"no evidence leaves the check pending");
        Check(!engine.ReportHealth(HealthOutcome.Confirmed,null).Restored && !engine.JournalActive && engine.Resume()==ResumeOutcome.Nothing,"plugin confirmation clears the journal");
        Check(engine.PreviousVersion()=="1.0.0" && File.Exists(Path.Combine(f.Root,"releases","1.0.0","app","SoloStone.exe")) && File.Exists(Path.Combine(f.Root,"releases","1.0.0","client","winhttp.dll")),"previous release kept");
        Check(!Directory.Exists(Path.Combine(f.Root,"updates","2.0.0")),"staged files consumed");
        // Only the previous release stays.
        Check(Update(f,"3.0.0").Outcome==UpdateOutcome.Swapped,"second swap");
        Check(Directory.GetDirectories(Path.Combine(f.Root,"releases")).Select(Path.GetFileName).SequenceEqual(new[]{"2.0.0"}),"keeps only the previous release");
        f.Engine().ReportHealth(HealthOutcome.Confirmed,null);
        // Failed health restores, holds, reports. Each crash point during the restore still converges.
        for(int point=0;;point++) {
            var g=NewFixture(temp); string before=g.Code, savesBefore=TreeHash(g.Saves);
            Check(Update(g,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap for health failure");
            int seen=0; bool crashed=false;
            var failing=g.Engine(label=>{ ExeInPlace(g); if(seen++==point) { crashed=true; throw new SimulatedCrash(); } });
            try {
                var health=failing.ReportHealth(HealthOutcome.Failed,"The game exited 42.");
                Check(health.Restored && health.Message.Contains("1.0.0") && !health.Message.Contains("42"),"failed health reports the restore; the exit code stays in the hold reason");
            } catch(SimulatedCrash) {
                g.Engine().Resume();
                // Killed before the restore began: the release is still the one in place, and the next failed start restores it.
                if(g.Engine().Journal?.Phase==SwapJournal.Swapped) { ExeInPlace(g); g.Engine().ReportHealth(HealthOutcome.Failed,"The game exited 42."); }
            }
            Check(g.Code==before && !g.Engine().JournalActive && TreeHash(g.Saves)==savesBefore,"failed health restores exactly the old tree, point "+point);
            var held=g.Engine().Held();
            Check(held!=null && held.Version=="2.0.0" && held.Reason.Contains("42"),"failed version is held, point "+point);
            bool done=!crashed; FsOps.DeleteTree(g.Base);
            if(done) { Check(point>10,"restore crash matrix covered: "+point); break; }
        }
    }
    static void RunningExeTests(string temp) {
        // Windows will rename the running launcher but not delete it. The restore must still finish and report.
        var f=NewFixture(temp); string before=f.Code;
        Check(Update(f,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap");
        var locked=f.Engine(label=>{ if(label=="undo:staged") throw new IOException("in use"); });
        var health=locked.ReportHealth(HealthOutcome.Failed,"exit 42");
        Check(health.Restored && f.Code==before && !locked.JournalActive && locked.Held().Version=="2.0.0","restore finishes when the running exe cannot be deleted");
        Directory.CreateDirectory(f.Engine().StagedApp("2.0.0"));
        var next=BuildRelease(f,"2.0.1"); var e=f.Engine(); e.Stage(ZipOf(f,"2.0.1"),next);
        Check(!Directory.Exists(Path.Combine(f.Root,"updates","2.0.0")),"next stage clears the leftover");
        // A real failure mid-swap (not a crash) undoes in-process and reports.
        var g=NewFixture(temp); string code=g.Code; int seen=0;
        var latest=BuildRelease(g,"2.0.0"); var failing=g.Engine(label=>{ if(label.StartsWith("start:",StringComparison.Ordinal) && seen++==20) throw new IOException("sharing violation"); });
        failing.Stage(ZipOf(g,"2.0.0"),latest);
        failing.Apply(latest,false);
        var failed=g.Host.HelperResult;
        Check(failed.Outcome==UpdateOutcome.Failed && g.Code==code && !failing.JournalActive,"file error mid-swap restores the old version in-process");
    }
    static void RollbackTests(string temp) {
        // No previous release.
        var f=NewFixture(temp);
        Check(f.Engine().Rollback().Outcome==RollbackOutcome.NoPrevious,"no previous release");
        string oldCode=f.Code;
        Check(Update(f,"2.0.0",saveSchema:16).Outcome==UpdateOutcome.Swapped,"swap");
        // Manual rollback while the health check is still pending.
        var r=f.Engine().Rollback();
        Check(r.Outcome==RollbackOutcome.Done && f.Code==oldCode && f.Engine().Held().Version=="2.0.0","rollback during pending health restores and holds that version");
        Check(f.Engine().PreviousVersion()==null,"nothing older remains");
        f.Host.Busy=null;
        // Rollback after confirmation, with every crash point.
        for(int point=0;;point++) {
            var g=NewFixture(temp); string before=g.Code, savesBefore=TreeHash(g.Saves);
            Check(Update(g,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap for rollback");
            g.Engine().ReportHealth(HealthOutcome.Confirmed,null);
            string after=g.Code;
            int seen=0; bool crashed=false;
            var engine=g.Engine(label=>{ ExeInPlace(g); if(seen++==point) { crashed=true; throw new SimulatedCrash(); } });
            try {
                var result=engine.Rollback();
                Check(result.Outcome==RollbackOutcome.Done && result.Version=="1.0.0" && result.Message.Contains("1.0.0"),"manual rollback");
            } catch(SimulatedCrash) {
                g.Engine().Resume();
                // Killed before the restore began: nothing moved, so the player simply rolls back again.
                if(InstallRecord.Load(g.Root).ReleaseVersion=="2.0.0") { ExeInPlace(g); Check(g.Code==after,"a rollback killed before it began changed nothing, point "+point); g.Engine().Rollback(); }
            }
            Check(g.Code==before && TreeHash(g.Saves)==savesBefore,"rollback yields exactly the old tree, point "+point);
            bool done=!crashed; FsOps.DeleteTree(g.Base);
            if(done) { Check(point>10,"rollback crash matrix covered: "+point); break; }
        }
        // Refused while busy or pending.
        var h=NewFixture(temp);
        Check(Update(h,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap"); h.Engine().ReportHealth(HealthOutcome.Confirmed,null);
        string code=h.Code;
        h.Host.Busy="running"; Check(h.Engine().Rollback().Outcome==RollbackOutcome.NotIdle && h.Code==code,"rollback refused while a client runs"); h.Host.Busy=null;
        File.WriteAllText(Path.Combine(h.Saves,"profile.xml"),ProfileXml(15,pending:"m"));
        Check(h.Engine().Rollback().Outcome==RollbackOutcome.NotIdle && h.Code==code,"rollback refused with a pending match");
        // Refused by schema, then restore path. The save moved to a version the previous plugin cannot read.
        File.WriteAllText(Path.Combine(h.Saves,"profile.xml"),ProfileXml(16));
        string newer=TreeHash(h.Saves);
        var refused=h.Engine().Rollback();
        Check(new Backups(h.Root).Verified(refused.BackupId).SaveSchema==15,"the offered backup is one the previous version can read");
        Check(refused.Outcome==RollbackOutcome.SaveSchemaTooNew && refused.OffersBackup && refused.BackupId!=null && refused.Message.Contains("progress since the update is lost") && h.Code==code,"schema refusal offers the backup and says what is lost");
        Check(TreeHash(h.Saves)==newer,"refused rollback leaves saves alone");
        var engineH=h.Engine();
        h.Host.Busy="running"; Refuses(()=>engineH.RestoreBackup(refused.BackupId),"restore refused while a client runs"); h.Host.Busy=null;
        File.WriteAllText(Path.Combine(h.Saves,"profile.xml"),ProfileXml(16,pending:"m"));
        string pendingTree=TreeHash(h.Saves);
        Refuses(()=>engineH.RestoreBackup(refused.BackupId),"restore refused with a pending match");
        Check(TreeHash(h.Saves)==pendingTree,"refused restore leaves saves alone");
        File.WriteAllText(Path.Combine(h.Saves,"profile.xml"),ProfileXml(16));
        File.WriteAllText(Path.Combine(h.Client,"BepInEx","config","player.cfg"),"changed after backup");
        var restored=engineH.RestoreBackup(refused.BackupId);
        Check(restored.Message.Contains("Progress since the update is lost"),"restore states the lost progress");
        Check(File.ReadAllText(Path.Combine(h.Saves,"profile.xml"))==ProfileXml(15),"backup back in the save tree");
        Check(File.ReadAllText(Path.Combine(restored.MovedAsideTo,"saves","profile.xml"))==ProfileXml(16) && TreeHash(Path.Combine(restored.MovedAsideTo,"saves"))==newer,"current saves moved aside, never deleted");
        Check(File.ReadAllText(Path.Combine(h.Client,"BepInEx","config","player.cfg"))=="player settings" && File.ReadAllText(Path.Combine(restored.MovedAsideTo,"config","player.cfg"))=="changed after backup","config restored, the replaced one kept");
        Check(Path.GetDirectoryName(restored.MovedAsideTo)==new Backups(h.Root).Folder && new Backups(h.Root).List().Length==1,"replaced tree is not a pruned backup");
        var now=h.Engine().Rollback();
        Check(now.Outcome==RollbackOutcome.Done,"rollback possible after restoring the pre-update saves");
        // Newest backup too new for the previous version: the older one is offered, and with none left nothing is.
        var k=NewFixture(temp); Check(Update(k,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap"); k.Engine().ReportHealth(HealthOutcome.Confirmed,null);
        string oldest=new Backups(k.Root).List().Single().Id;
        File.WriteAllText(Path.Combine(k.Saves,"profile.xml"),ProfileXml(16));
        new Backups(k.Root).Create(k.Saves,Path.Combine(k.Client,"BepInEx","config"),"2.0.0");
        var filtered=k.Engine().Rollback();
        Check(filtered.Outcome==RollbackOutcome.SaveSchemaTooNew && filtered.BackupId==oldest,"a newer backup than the previous version can read is not offered");
        FsOps.DeleteTree(new Backups(k.Root).PathOf(oldest));
        var noBackup=k.Engine().Rollback();
        Check(noBackup.Outcome==RollbackOutcome.SaveSchemaTooNew && !noBackup.OffersBackup && noBackup.BackupId==null && !noBackup.Message.Contains("Restoring"),"with no readable backup nothing is offered");
        // A refused plan spends no backup.
        var n=NewFixture(temp); var same=BuildRelease(n,"1.0.0"); var ne=n.Engine(); ne.Stage(ZipOf(n,"1.0.0"),same);
        Refuses(()=>ne.Apply(same,false),"same version refused");
        Check(new Backups(n.Root).List().Length==0,"a plan that refuses makes no backup");
        // Journals name only places SoloStone owns, whatever a damaged file says.
        var j=NewFixture(temp); Check(Update(j,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap"); j.Engine().ReportHealth(HealthOutcome.Confirmed,null);
        string saved=Path.Combine(j.Root,"releases","1.0.0","rollback.json"); var journal=JsonBytes.Parse<SwapJournal>(File.ReadAllBytes(saved));
        string goodDst=journal.Moves[0].Dst; journal.Moves[0].Dst=Path.Combine(j.Saves,"profile.xml"); File.WriteAllBytes(saved,JsonBytes.ToBytes(journal));
        Check(j.Engine().Rollback().Outcome==RollbackOutcome.NoPrevious && File.Exists(Path.Combine(j.Saves,"profile.xml")),"rollback journal aimed outside SoloStone refused");
        File.WriteAllBytes(Path.Combine(j.Root,"updates","journal.json"),JsonBytes.ToBytes(journal));
        Refuses(()=>j.Engine().Resume(),"active journal aimed outside SoloStone refused");
        File.WriteAllText(Path.Combine(j.Root,"updates","journal.json"),"garbage");
        Refuses(()=>j.Engine().Resume(),"damaged journal refused");
        File.Delete(Path.Combine(j.Root,"updates","journal.json"));
        // Damaged backup refused before anything moves.
        var d=NewFixture(temp); Check(Update(d,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap"); d.Engine().ReportHealth(HealthOutcome.Confirmed,null);
        var id=new Backups(d.Root).List().Single().Id; string live=TreeHash(d.Saves);
        File.WriteAllText(Path.Combine(new Backups(d.Root).PathOf(id),"saves","profile.xml"),"bitrot");
        Refuses(()=>d.Engine().RestoreBackup(id),"damaged backup refused"); Check(TreeHash(d.Saves)==live,"damaged backup leaves saves alone");
        Refuses(()=>d.Engine().RestoreBackup("20200101T000000000Z-absent"),"unknown backup refused");
        // No prior save tree, crash part-way through the stage: the partial stage is never promoted.
        var bare=NewFixture(temp); Check(Update(bare,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap"); bare.Engine().ReportHealth(HealthOutcome.Confirmed,null);
        string bareId=new Backups(bare.Root).List().Single().Id;
        FsOps.DeleteTree(bare.Saves);
        int bareSeen=0; var bareCrash=bare.Engine(label=>{ if(label=="restore:file" && bareSeen++==1) throw new SimulatedCrash(); });
        Throws<SimulatedCrash>(()=>bareCrash.RestoreBackup(bareId),"crash inside the stage copy");
        Check(Directory.Exists(bare.Saves+".solostone-restore"),"partial stage on disk");
        bare.Engine().Resume();
        Check(!Directory.Exists(bare.Saves) && !Directory.Exists(bare.Saves+".solostone-restore"),"a partial stage never becomes the save tree");
        var bareDone=bare.Engine(label=>{ if(label=="restore:aside") throw new SimulatedCrash(); });
        Throws<SimulatedCrash>(()=>bareDone.RestoreBackup(bareId),"crash after a whole stage");
        bare.Engine().Resume();
        Check(File.Exists(Path.Combine(bare.Saves,"profile.xml")) && !File.Exists(bare.Saves+".solostone-restore.verified"),"a verified stage is finished on the next start");
        // Restore crash points: saves are always a whole tree, old or backup.
        for(int point=0;point<8;point++) {
            var c=NewFixture(temp); Check(Update(c,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap"); c.Engine().ReportHealth(HealthOutcome.Confirmed,null);
            string backupId=new Backups(c.Root).List().Single().Id;
            string backupTree=TreeHash(Path.Combine(new Backups(c.Root).PathOf(backupId),"saves"));
            File.WriteAllText(Path.Combine(c.Saves,"profile.xml"),ProfileXml(16));
            string changed=TreeHash(c.Saves);
            int seen=0; var crashing=c.Engine(label=>{ if(seen++==point) throw new SimulatedCrash(); });
            bool hit=false;
            try { crashing.RestoreBackup(backupId); } catch(SimulatedCrash) { hit=true; }
            if(!hit) { FsOps.DeleteTree(c.Base); break; }
            Check(c.Engine().Resume()==ResumeOutcome.Nothing,"restore resumes on the next start");
            string tree=TreeHash(c.Saves);
            Check(tree==changed || tree==backupTree,"saves are whole after a restore crash, point "+point);
            Check(!Directory.Exists(c.Saves+".solostone-restore"),"restore stage cleared, point "+point);
            FsOps.DeleteTree(c.Base);
        }
    }
    static void ServiceTests(string temp) {
        var rootKey=new TestKey(); var signing=new TestKey();
        var config=UpdateConfig.From(rootKey.Public,signing.Public,"owner/repo",0);
        var f=NewFixture(temp);
        var latest=BuildRelease(f,"2.0.0");
        string zip=ZipOf(f,"2.0.0");
        var http=new FakeHttp();
        Action<string,long> publish=(json,seq)=>{ http.Add(RawBase+"release-manifest.json",Utf8(json)); http.Add(RawBase+"release-manifest.json.minisig",SignManifest(signing,json,seq)); };
        string manifest=ManifestJson(1,"2.0.0","SoloStone-2.0.0.zip",latest.Asset.Size,latest.Asset.Sha256,15);
        publish(manifest,1);
        http.Redirect("https://github.com/owner/repo/releases/download/v2.0.0/SoloStone-2.0.0.zip","https://objects.githubusercontent.com/asset");
        http.Add("https://objects.githubusercontent.com/asset",File.ReadAllBytes(zip));
        var engine=f.Engine(); var service=new UpdateService(f.Root,config,http,engine);
        var none=CancellationToken.None;
        // Not configured / off: no request at all.
        var off=new UpdateService(f.Root,UpdateConfig.From("","","",0),http,engine).Check(none);
        Check(off.State==UpdateState.NotConfigured && off.Message==UpdateConfig.NotConfigured && http.Requests.Count==0,"unconfigured build never touches the network");
        Settings.Save(f.Root,new Settings { CheckForUpdates=false });
        Check(service.Check(none).State==UpdateState.Off && service.Apply(false,none).Outcome==UpdateOutcome.Nothing && http.Requests.Count==0,"off switch: no request");
        Settings.Save(f.Root,new Settings { CheckForUpdates=true });
        // Full path.
        var status=service.Check(none);
        Check(status.State==UpdateState.Available && status.Latest.Version=="2.0.0" && status.Installed=="1.0.0","newer signed version offered");
        var result=service.Apply(false,none);
        Check(result.Outcome==UpdateOutcome.Swapped && InstallRecord.Load(f.Root).ReleaseVersion=="2.0.0" && File.ReadAllText(Path.Combine(f.App,"SoloStone.exe"))=="launcher 2.0.0","signed update installs end to end");
        Check(http.Requests.All(u=>u.StartsWith("https://",StringComparison.Ordinal)) && http.Requests.All(u=>UpdateClient.PinnedHosts.Contains(new Uri(u).Host)),"every request stayed on the pinned hosts");
        Check(service.Check(none).State==UpdateState.UpToDate,"a pending health check blocks nothing, and the same version is not offered");
        engine.ReportHealth(HealthOutcome.Confirmed,null);
        Check(service.Check(none).State==UpdateState.UpToDate,"same version not offered again");
        // Failures keep the last verified values and never throw.
        var s=new ManifestStore(f.Root,config);
        Check(s.Current().Seq==1,"last verified kept");
        http.Routes.Remove(RawBase+"release-manifest.json");
        var failed=service.Check(none); Check(failed.State==UpdateState.Failed && s.Current().Seq==1,"offline keeps the last verified manifest");
        http.Routes[RawBase+"release-manifest.json"]=()=>throw new System.Net.WebException("no route");
        var offline=service.Check(none); Check(offline.State==UpdateState.Failed && offline.Message=="Couldn't check for updates.","network exception becomes a status, read as offline");
        http.Add(RawBase+"release-manifest.json",Utf8(ManifestJson(1,"9.0.0")));
        var forged=service.Check(none);
        Check(forged.State==UpdateState.Failed && s.Current().Latest.Version=="2.0.0","bad signature changes nothing");
        // A forged file must not read like being offline (lab flow 2026-11-07: the reason had been dropped).
        Check(forged.Message.StartsWith("Update refused: ") && forged.Message.ToLowerInvariant().Contains("signature"),"a bad signature says the update was refused and why: "+forged.Message);
        // A revocation reaches a store that still trusts the compiled key, without any rotation.
        var fresh=NewFixture(temp); var freshHttp=new FakeHttp(); var freshConfig=UpdateConfig.From(rootKey.Public,signing.Public,"owner/repo",0);
        string revokeJson=ManifestJson(2,"2.0.0"); freshHttp.Add(RawBase+"release-manifest.json",Utf8(revokeJson)); freshHttp.Add(RawBase+"release-manifest.json.minisig",SignManifest(signing,revokeJson,2));
        var freshService=new UpdateService(fresh.Root,freshConfig,freshHttp,fresh.Engine());
        Check(freshService.Check(none).State==UpdateState.Available,"no statement published: the compiled key works");
        var revoke=Statement(rootKey,new TestKey(),1,signing.IdHex);
        freshHttp.Add(RawBase+"signing-key.json",revoke.File); freshHttp.Add(RawBase+"signing-key.json.minisig",revoke.Signature);
        string afterRevoke=ManifestJson(3,"2.0.0"); freshHttp.Add(RawBase+"release-manifest.json",Utf8(afterRevoke)); freshHttp.Add(RawBase+"release-manifest.json.minisig",SignManifest(signing,afterRevoke,3));
        var revokedCheck=freshService.Check(none);
        Check(revokedCheck.State==UpdateState.Failed && revokedCheck.Message.StartsWith("Update refused: ") && File.ReadAllText(InstallerLog.PathOf(fresh.Root)).Contains("revoked"),"a revoked compiled key is refused as soon as the statement is published");
        // Replay and downgrade through the service.
        publish(ManifestJson(5,"3.0.0"),5); service.Check(none);
        publish(ManifestJson(4,"9.0.0"),4);
        Check(service.Check(none).State==UpdateState.Failed && s.Current().Seq==5,"seq replay refused through the service");
        publish(ManifestJson(6,"1.5.0"),6);
        Check(service.Check(none).State==UpdateState.UpToDate,"signed older version is never offered");
        // Tampered asset: the download fails, the install stays.
        string code=f.Code;
        publish(ManifestJson(7,"3.0.0","SoloStone-3.0.0.zip",9,new string('A',64)),7);
        http.Add("https://github.com/owner/repo/releases/download/v3.0.0/SoloStone-3.0.0.zip",Utf8("evil data"));
        var evil=service.Apply(false,none);
        Check(evil.Outcome==UpdateOutcome.Failed && f.Code==code,"tampered asset leaves the install alone");
        // Status report: local facts for the launcher window.
        var report=StatusReport.Build(f.Root,engine,TestedClient.Compiled(),config,new NoBindingManifest(),null);
        Check(report.Installed && report.ReleaseVersion=="2.0.0" && report.PreviousVersion=="1.0.0" && report.ClientPath==f.Client && report.LatestVersion=="3.0.0" && report.UpdatesConfigured && report.Backups.Length==1,"status report");
        Check(report.Verdict.Kind==VerdictKind.Allow && !report.Verdict.Exact && report.Verdict.TestedVersion=="37.0.0","status includes the live verdict: "+report.Verdict.Message);
        var parsed=JsonBytes.Parse<StatusReport>(Encoding.UTF8.GetBytes(report.ToJson()));
        Check(parsed.ReleaseVersion=="2.0.0" && parsed.Verdict.Kind==VerdictKind.Allow,"status --json round trip");
        var empty=StatusReport.Build(Path.Combine(temp,"status-none"),new UpdateEngine(Path.Combine(temp,"status-none"),new FakeHost { SaveRoot=Path.Combine(temp,"status-saves") }),TestedClient.Compiled(),config,new NoBindingManifest(),null);
        Check(!empty.Installed && empty.InstallProblem!=null,"status without an install");
        // check-client: installed copy, explicit folder, broken record.
        var viaRoot=CheckClient.Run(f.Root,null,TestedClient.Compiled(),config,new NoBindingManifest(),null);
        Check(viaRoot.Kind==VerdictKind.Allow && !viaRoot.Exact && viaRoot.Provisional,"check-client reads the installed copy");
        Check(CheckClient.Run(f.Root,f.Client,TestedClient.Compiled(),config,new NoBindingManifest(),new[]{"Quit Battle.net"}).Kind==VerdictKind.Block,"wizard check blocks on a safety failure");
        Check(CheckClient.Run(Path.Combine(temp,"status-none"),null,TestedClient.Compiled(),config,new NoBindingManifest(),null).Kind==VerdictKind.Block,"check-client without an install blocks");
        // Signed list reaches the gate through a real manifest.
        var facts=ClientFacts.Read(f.Client);
        var signed=ManifestJson(8,"3.0.0",tested:"{\"version\":\"37.0.0\",\"files\":{"+string.Join(",",facts.Files.Select(r=>"\""+r.Key+"\":\""+r.Value+"\""))+"}}");
        publish(signed,8); service.Check(none);
        Check(CheckClient.Run(f.Root,null,TestedClient.Compiled(),config,new NoBindingManifest(),null).Exact,"signed tested list makes this client exactly tested");
        var blocked=ManifestJson(9,"3.0.0",knownBad:"{\"files\":{"+string.Join(",",facts.Files.Select(r=>"\""+r.Key+"\":\""+r.Value+"\""))+"}}");
        publish(blocked,9); service.Check(none);
        Check(CheckClient.Run(f.Root,null,TestedClient.Compiled(),config,new NoBindingManifest(),null).Kind==VerdictKind.Block,"signed known-bad list blocks this exact client");
        // Rotation through the service.
        var newKey=new TestKey(); string rotated=ManifestJson(10,"3.0.0");
        http.Add(RawBase+"release-manifest.json",Utf8(rotated)); http.Add(RawBase+"release-manifest.json.minisig",SignManifest(newKey,rotated,10));
        var statement=Statement(rootKey,newKey,1,signing.IdHex);
        http.Add(RawBase+"signing-key.json",statement.File); http.Add(RawBase+"signing-key.json.minisig",statement.Signature);
        service.Check(none); Check(s.Current().Seq==10,"rotated signing key fetched and verified through the service");
    }
    static void EngineTests(string temp) {
        Atomic.FlushToDisk=false;
        SaveScanTests(temp);
        StageRefusals(temp);
        HoldAndGateTests(temp);
        BackupTests(temp);
        CrashMatrix(temp,true,AfterCrash.ShortcutResume);
        CrashMatrix(temp,false,AfterCrash.ShortcutResume);
        CrashMatrix(temp,true,AfterCrash.SetupRerun);
        CrashMatrix(temp,true,AfterCrash.Uninstall);
        IdenticalLauncher(temp);
        HandOffRules(temp);
        PendingHealthTests(temp);
        HealthTests(temp);
        RunningExeTests(temp);
        RollbackTests(temp);
        ServiceTests(temp);
    }
}

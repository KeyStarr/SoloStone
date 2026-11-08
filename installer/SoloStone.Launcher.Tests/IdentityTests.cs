using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SoloStone;

// The copy's own company name (ClientIdentity): the pure patch, setup's refusal and order, recovery, the launch check, payload protection and uninstall.
static partial class Program
{
    // HKCU\Software outside any game key: path -> number of values. A key's subkeys are the paths below it.
    sealed class FakeRegistry : IUserRegistry
    {
        public Dictionary<string,int> Soft=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase); public List<string> SoftCalls=new List<string>();
        bool HasChild(string path) => Soft.Keys.Any(k=>k.StartsWith(path+"\\",StringComparison.OrdinalIgnoreCase));
        public void DeleteSoftwareTree(string path) { SoftCalls.Add("tree "+path); foreach(string k in Soft.Keys.Where(k=>string.Equals(k,path,StringComparison.OrdinalIgnoreCase) || k.StartsWith(path+"\\",StringComparison.OrdinalIgnoreCase)).ToList()) Soft.Remove(k); }
        public bool SoftwareKeyEmpty(string path) => Soft.TryGetValue(path,out int values) && values==0 && !HasChild(path);
        public void DeleteEmptySoftwareKey(string path) { SoftCalls.Add("empty "+path); if(!SoftwareKeyEmpty(path)) throw new InvalidOperationException("not empty"); Soft.Remove(path); }
    }
    const string GlobalManagers="Hearthstone_Data/globalgamemanagers", AppInfo="Hearthstone_Data/app.info";
    static string Rel(string folder,string relative) => Path.Combine(folder,relative.Replace('/',Path.DirectorySeparatorChar));
    static int Named(string file,string name) => ClientIdentity.Matches(File.ReadAllBytes(file),name).Count;
    static byte[] Text(string text) => System.Text.Encoding.Latin1.GetBytes(text);
    static int identityCases;
    // A fresh fake client, optionally altered, and the options to install it.
    static (string source,string root,string saves,TestedClient tested) IdentityCase(string temp,Action<string> alter=null) {
        string b=Path.Combine(temp,"identity"+(++identityCases)); string source=Path.Combine(b,"source");
        var tested=FakeClient(source,"37.0.0"); alter?.Invoke(source);
        return (source,Path.Combine(b,"root"),Path.Combine(b,"saves"),tested);
    }
    static void IdentityIntact(Fixture f,Dictionary<string,string> expected,string when) {
        var record=InstallRecord.Load(f.Root);
        Check(record.IdentityFiles!=null && record.IdentityFiles.Count==2 && ClientIdentity.Paths.All(p=>record.IdentityFiles[p]==expected[p]),"the recorded patch hashes are kept "+when);
        ClientIdentity.Verify(f.Client,record.IdentityFiles); count++; // And the files still match them.
    }
    static void ClientIdentityTests(string temp,string bundle) {
        string old=ClientIdentity.OldName, current=ClientIdentity.NewName;
        Check(old=="Blizzard Entertainment" && current=="SoloStone Offline Mods" && old.Length==current.Length && ClientIdentity.Paths.SequenceEqual(new[]{GlobalManagers,AppInfo}),"the names have the same length and the paths are fixed");
        // ---- The pure part: exactly one match, same length, bytes only.
        Check(ClientIdentity.Problem(AppInfo,Text(old+"\nHearthstone\n"))==null,"one match at offset 0 is fit");
        var atZero=ClientIdentity.Patch(AppInfo,Text(old+"\nHearthstone\n"));
        Check(atZero.Length==old.Length+"\nHearthstone\n".Length && System.Text.Encoding.ASCII.GetString(atZero)==current+"\nHearthstone\n","a match at offset 0 is replaced in place, same length");
        byte[] deep=Text("head\0\u0001"+old+"\0tailÿ");
        var patched=ClientIdentity.Patch(GlobalManagers,deep);
        Check(patched.Length==deep.Length && ClientIdentity.Matches(patched,current).Count==1 && ClientIdentity.Matches(patched,old).Count==0,"a match inside binary data is replaced");
        Check(patched.Take(6).SequenceEqual(deep.Take(6)) && patched.Skip(6+old.Length).SequenceEqual(deep.Skip(6+old.Length)),"nothing but the name's bytes changes");
        Check(ClientIdentity.Matches(Text(old+old),old).Count==2 && ClientIdentity.Matches(new byte[0],old).Count==0 && ClientIdentity.Matches(Text("Blizzard Entertainmen"),old).Count==0,"matches are counted without decoding, a partial name is none");
        foreach(var bad in new[]{ new byte[0], Text("no name here"), Text(old+"\0"+old), Text(old+" "+current) }) {
            string problem=ClientIdentity.Problem(GlobalManagers,bad);
            Check(problem!=null && problem.Contains(GlobalManagers) && problem.Contains("aren't what SoloStone expects") && !problem.Contains("changed"),"zero, two, or an already renamed match is refused and names the file, with no claim about what was changed");
            RejectsBecause(()=>ClientIdentity.Patch(GlobalManagers,bad),GlobalManagers,"patching refuses what is not exactly one match");
        }
        // A source: one match in each file, or the first bad file is named.
        var ok=IdentityCase(temp);
        Check(ClientIdentity.SourceProblem(ok.source)==null,"the fake client is fit");
        foreach(var broken in new[]{ (GlobalManagers,Text("none")), (GlobalManagers,Text(old+old)), (AppInfo,Text("none")), (AppInfo,Text(old+"|"+old)) }) {
            var c=IdentityCase(temp,s=>File.WriteAllBytes(Rel(s,broken.Item1),broken.Item2));
            string problem=ClientIdentity.SourceProblem(c.source);
            Check(problem!=null && problem.Contains(broken.Item1),"one bad file is named: "+broken.Item1);
        }
        var noInfo=IdentityCase(temp,s=>File.Delete(Rel(s,AppInfo)));
        Check(ClientIdentity.SourceProblem(noInfo.source).Contains(AppInfo),"a missing file is refused and named");
        // ---- Setup refuses before copying, writes nothing, never touches the source.
        foreach(var broken in new[]{ (GlobalManagers,Text("none")), (GlobalManagers,Text(old+old)), (AppInfo,Text("none")), (AppInfo,Text(old+old)) }) {
            var c=IdentityCase(temp,s=>File.WriteAllBytes(Rel(s,broken.Item1),broken.Item2));
            string before=TreeHash(c.source); var progress=new List<SetupStep>();
            var options=SetupOf(c.source,c.root,bundle,c.tested,c.saves); options.Progress=p=>progress.Add(p.Step);
            RejectsBecause(()=>Installer.Setup(options),"No files were changed","setup refuses a source with a wrong match count: "+broken.Item1+" "+broken.Item2.Length);
            Check(!Directory.Exists(c.root) && !Directory.Exists(InstallRecord.StageOf(Installer.DefaultClient(c.root))) && !progress.Contains(SetupStep.CopyClient),"nothing is created and no copy starts: "+broken.Item1);
            Check(TreeHash(c.source)==before,"and the source is untouched: "+broken.Item1);
        }
        // ---- Setup: patched copy, hashes recorded, source untouched, the patch is in before the payload.
        var good=IdentityCase(temp);
        string sourceBefore=TreeHash(good.source);
        var sourceSizes=ClientIdentity.Paths.Select(p=>new FileInfo(Rel(good.source,p)).Length).ToArray();
        var order=new List<string>(); string stage=InstallRecord.StageOf(Installer.DefaultClient(good.root));
        var setup=SetupOf(good.source,good.root,bundle,good.tested,good.saves);
        setup.Progress=p=>{
            if(p.Step!=SetupStep.InstallMods) return;
            // The step that installs the payload has just been announced: the patch is already in, the payload not yet.
            order.Add("patched="+ClientIdentity.Paths.All(x=>Named(Rel(stage,x),current)==1 && Named(Rel(stage,x),old)==0)+" payload="+File.Exists(Path.Combine(stage,"winhttp.dll")));
        };
        var installed=Installer.Setup(setup);
        Check(order.SequenceEqual(new[]{"patched=True payload=False"}),"the patch goes in before the payload: "+string.Join(",",order));
        Check(TreeHash(good.source)==sourceBefore,"setup leaves both source files and everything else byte-identical");
        Check(ClientIdentity.Paths.All(p=>Named(Rel(good.source,p),old)==1 && Named(Rel(good.source,p),current)==0),"the source still holds the old name once");
        for(int i=0;i<2;i++) {
            string path=ClientIdentity.Paths[i], copy=Rel(installed.Client,path);
            Check(Named(copy,current)==1 && Named(copy,old)==0 && new FileInfo(copy).Length==sourceSizes[i],"the copy holds the new name once, the old none, same size: "+path);
            Check(installed.IdentityFiles[path]==Hashes.FileHash(copy) && Hashes.FileHash(copy)!=Hashes.FileHash(Rel(good.source,path)),"its patched hash is recorded and differs from the source's: "+path);
            Check(!installed.Files.ContainsKey(path),"and Files keeps only what the source also holds: "+path);
        }
        var loaded=InstallRecord.Load(good.root);
        Check(loaded.IdentityFiles.Count==2 && ClientIdentity.Paths.All(p=>loaded.IdentityFiles[p]==installed.IdentityFiles[p]),"the hashes survive the install record");
        ClientIdentity.Verify(installed.Client,loaded.IdentityFiles); count++;
        // ---- The launch check (WindowsHost.RunGame calls it next to the other hash checks): a copy that lost the patch is refused.
        string managers=Rel(installed.Client,GlobalManagers);
        byte[] keep=File.ReadAllBytes(managers);
        File.WriteAllBytes(managers,File.ReadAllBytes(Rel(good.source,GlobalManagers)));
        RejectsBecause(()=>ClientIdentity.Verify(installed.Client,loaded.IdentityFiles),"no longer carry SoloStone's own name","a reverted file is refused");
        // A hand-edited record that blesses the unpatched file's hash still fails on the names.
        var forged=new Dictionary<string,string>(loaded.IdentityFiles); forged[GlobalManagers]=Hashes.FileHash(managers);
        RejectsBecause(()=>ClientIdentity.Verify(installed.Client,forged),"no longer carry SoloStone's own name","a record edited to match an unpatched file is refused");
        File.WriteAllBytes(managers,keep); ClientIdentity.Verify(installed.Client,loaded.IdentityFiles); count++;
        File.Delete(Rel(installed.Client,AppInfo));
        RejectsBecause(()=>ClientIdentity.Verify(installed.Client,loaded.IdentityFiles),"no longer carry","a missing file is refused");
        // A file with both names or the old one only, by size-neutral edit.
        File.WriteAllBytes(Rel(installed.Client,AppInfo),Text(old+"\nHearthstone\n"));
        RejectsBecause(()=>ClientIdentity.Verify(installed.Client,loaded.IdentityFiles),"no longer carry","an unpatched app.info is refused");
        // An old record has no hashes: refused with its own sentence, never launched unpatched, and never a crash.
        foreach(var none in new IDictionary<string,string>[]{ null, new Dictionary<string,string>(), new Dictionary<string,string>{{GlobalManagers,new string('A',64)}} })
            RejectsBecause(()=>ClientIdentity.Verify(installed.Client,none),"made before SoloStone kept its settings apart","an old or short record is refused clearly");
        string host=Source("WindowsHost.cs");
        int hashCheck=host.IndexOf("Hashes.Verify(record.Client,record.Files);",StringComparison.Ordinal), identityCheck=host.IndexOf("ClientIdentity.Verify(record.Client,record.IdentityFiles);",StringComparison.Ordinal), grant=host.IndexOf("LaunchGrant.Write(",StringComparison.Ordinal);
        Check(hashCheck>0 && identityCheck>hashCheck && grant>identityCheck && host.IndexOf("Process.Start(start)",grant,StringComparison.Ordinal)>grant,"RunGame checks the patch next to its hash checks, before a grant is written or the game starts");
        File.Delete(Rel(installed.Client,AppInfo));
        // Swaps, undos and rollbacks keep the recorded hashes (NewFixture's record carries them).
        var swap=NewFixture(temp); var recordedBefore=new Dictionary<string,string>(swap.Record.IdentityFiles);
        Check(Update(swap,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap");
        IdentityIntact(swap,recordedBefore,"after a swap");
        Check(swap.Engine().ReportHealth(HealthOutcome.RetryLater,"exit 1").Restored,"a failed first start restores");
        IdentityIntact(swap,recordedBefore,"after the undo");
        var back=NewFixture(temp); var backBefore=new Dictionary<string,string>(back.Record.IdentityFiles);
        Check(Update(back,"2.0.0").Outcome==UpdateOutcome.Swapped && back.Engine().Rollback().Outcome==RollbackOutcome.Done,"swap and rollback");
        IdentityIntact(back,backBefore,"after a rollback");
        // ---- Old record on disk: the key is absent, or null. Both load, both refuse to launch, and uninstall still works.
        foreach(bool absent in new[]{false,true}) {
            var o=NewFixture(temp); o.Record.IdentityFiles=null; Json.Save(Path.Combine(o.Root,"install.json"),o.Record); // As a record from before the patch existed.
            if(absent) {
                string json=File.ReadAllText(Path.Combine(o.Root,"install.json")); int at=json.IndexOf("\"identityFiles\"",StringComparison.Ordinal);
                Check(at>0,"a saved record names the field");
                File.WriteAllText(Path.Combine(o.Root,"install.json"),json.Remove(json.IndexOf("\"identityFiles\"",StringComparison.Ordinal),json.IndexOf("null,",at,StringComparison.Ordinal)-at+5));
            }
            var old1=InstallRecord.Load(o.Root);
            Check(old1.IdentityFiles==null,"an old record loads with no hashes: "+(absent?"field absent":"field null"));
            RejectsBecause(()=>ClientIdentity.Verify(old1.Client,old1.IdentityFiles),"made before","and refuses a launch: "+absent);
            Installer.Uninstall(o.Root,new string[0]);
            Check(!Directory.Exists(o.Client) && !File.Exists(Path.Combine(o.Root,"install.json")),"and uninstall removes it: "+absent);
        }
        // ---- Recover. A stage with a good payload but no patch is not complete: it is deleted and recopied, now patched.
        var rec=IdentityCase(temp);
        var first=Installer.Setup(SetupOf(rec.source,rec.root,bundle,rec.tested,rec.saves));
        string recStage=first.Stage; string wholeClient=first.Client;
        File.Delete(Path.Combine(rec.root,"install.json"));
        var pendingRecord=InstallRecord.FromBytes(JsonBytes.ToBytes(first)); pendingRecord.IdentityFiles=null; // The record a stage had before the patch existed.
        Json.Save(Path.Combine(rec.root,"install.pending.json"),pendingRecord);
        Directory.Move(wholeClient,recStage);
        foreach(string p in ClientIdentity.Paths) File.WriteAllBytes(Rel(recStage,p),File.ReadAllBytes(Rel(rec.source,p))); // Payload, firewall list and marker intact; patch gone.
        Check(Named(Rel(recStage,GlobalManagers),old)==1,"the stage is whole except for the patch");
        File.WriteAllText(Path.Combine(recStage,"leftover-from-the-unpatched-stage"),"x");
        var recovered=Installer.Recover(rec.root,bundle,new string[0],rec.saves,_=>{});
        Check(!File.Exists(Path.Combine(recovered.Client,"leftover-from-the-unpatched-stage")) && !Directory.Exists(recStage),"an unpatched stage is not called complete: it is recopied");
        ClientIdentity.Verify(recovered.Client,recovered.IdentityFiles); count++;
        Check(ClientIdentity.Paths.All(p=>Named(Rel(recovered.Client,p),current)==1),"and the recopy is patched");
        // A finished copy that lost the patch is refused by recovery and left as it is.
        File.WriteAllBytes(Rel(recovered.Client,GlobalManagers),File.ReadAllBytes(Rel(rec.source,GlobalManagers)));
        string damaged=TreeHash(recovered.Client);
        RejectsBecause(()=>Installer.Recover(rec.root,bundle,new string[0],rec.saves,_=>{}),"Committed client integrity failed","recovery refuses a committed copy that lost the patch");
        Check(TreeHash(recovered.Client)==damaged,"and changes nothing");
        // A committed record from before the patch: the same refusal.
        var oldCommitted=InstallRecord.Load(rec.root); oldCommitted.IdentityFiles=null; Json.Save(Path.Combine(rec.root,"install.json"),oldCommitted);
        File.WriteAllBytes(Rel(recovered.Client,GlobalManagers),File.ReadAllBytes(Rel(rec.source,GlobalManagers)));
        RejectsBecause(()=>Installer.Recover(rec.root,bundle,new string[0],rec.saves,_=>{}),"Committed client integrity failed","recovery refuses an old record's copy, which would write retail's key");
        // A recopy checks the source again: one that changed since setup is refused before any stage exists, and says so truthfully.
        var again=IdentityCase(temp); string againRoot=again.root;
        Directory.CreateDirectory(againRoot);
        var pendingAgain=new InstallRecord { Schema=InstallRecord.CurrentSchema, InstallId=Guid.NewGuid().ToString("N"), Root=againRoot, Source=again.source, ClientPath=Installer.DefaultClient(againRoot),
            ClientVersion="37.0.0", Files=new Dictionary<string,string>(again.tested.Files), Payload=Json.Read<Payload>(Path.Combine(bundle,"payload.json")) };
        Json.Save(Path.Combine(againRoot,"install.pending.json"),pendingAgain);
        File.WriteAllBytes(Rel(again.source,AppInfo),Text("the client was updated"));
        string againHash=TreeHash(again.source);
        InvalidOperationException recopy=null;
        try { Installer.Recover(againRoot,bundle,new string[0],again.saves,_=>{}); } catch(InvalidOperationException e) { recopy=e; }
        Check(recopy!=null && recopy.InnerException!=null && recopy.InnerException.Message.Contains("aren't what SoloStone expects") && recopy.InnerException.Message.Contains("no new copy was started") && !recopy.InnerException.Message.Contains("No files were changed"),"a recopy refuses a source that changed, without the setup-time claim");
        Check(!Directory.Exists(pendingAgain.Stage) && !Directory.Exists(pendingAgain.Client) && TreeHash(again.source)==againHash,"and starts no stage and touches nothing");
        // The wizard's folder verdict says so before the Install step (not for the installed copy, which is patched on purpose).
        var verdictCase=IdentityCase(temp); var config=UpdateConfig.From("","","",0);
        var good1=CheckClient.Run(Path.Combine(verdictCase.root,"state"),verdictCase.source,verdictCase.tested,config,new NoBindingManifest(),null);
        Check(good1.Kind==VerdictKind.Allow,"a client that setup would take is Allow in the wizard: "+good1.Message);
        File.WriteAllBytes(Rel(verdictCase.source,AppInfo),Text("no company name here"));
        var bad1=CheckClient.Run(Path.Combine(verdictCase.root,"state"),verdictCase.source,verdictCase.tested,config,new NoBindingManifest(),null);
        Check(bad1.Kind==VerdictKind.Block && bad1.Message.Contains(AppInfo) && bad1.FoundVersion=="37.0.0" && bad1.TestedVersion=="37.0.0","one that setup would refuse is Block in the wizard, naming the file and both versions: "+bad1.Message);
        var live=IdentityCase(temp); Installer.Setup(SetupOf(live.source,live.root,bundle,live.tested,live.saves));
        var installedCopy=CheckClient.Run(live.root,null,live.tested,config,new NoBindingManifest(),null);
        Check(installedCopy.Kind==VerdictKind.Allow,"the installed copy is patched on purpose and still Allow: "+installedCopy.Message);
        // ---- Updates may not carry either patched path, recorded or not.
        foreach(string path in new[]{GlobalManagers,AppInfo,GlobalManagers.ToUpperInvariant(),AppInfo.ToLowerInvariant()}) {
            Check(PayloadRules.Problem(new[]{path},null)!=null && PayloadRules.Problem(new[]{path},new string[0])!=null && PayloadRules.Problem(new[]{path},new[]{"Hearthstone.exe"})!=null,"a payload with a patched path is refused: "+path);
        }
        Check(PayloadRules.Problem(new[]{"BepInEx/plugins/SoloStone.Plugin.dll","winhttp.dll"},ClientIdentityRecorded())==null,"an ordinary payload still passes");
        // Through the update engine's staging, with a record that does not list them.
        var f=NewFixture(temp);
        Func<string,Action> stagedWith=rel=>{
            var latest=BuildRelease(f,"4.0."+(++identityCases),15,w=>{ WriteRel(w,"payload/"+rel,"x"); var p=Json.Read<Payload>(Path.Combine(w,"payload.json")); p.Files[rel]=Hashes.FileHash(Safety.Child(Path.Combine(w,"payload"),rel)); Json.Save(Path.Combine(w,"payload.json"),p); });
            return ()=>f.Engine().Stage(ZipOf(f,latest.Version),latest);
        };
        RejectsBecause(stagedWith(GlobalManagers),"client file","an update carrying globalgamemanagers is refused");
        RejectsBecause(stagedWith(AppInfo),"client file","an update carrying app.info is refused");
    }
    static string[] ClientIdentityRecorded() => new[]{"Hearthstone.exe","UnityPlayer.dll"};
    // ---- Uninstall removes exactly SoloStone's own key and LocalLow folder.
    static void ClientIdentityUninstallTests(string temp) {
        string company=ClientIdentity.NewName, product=ClientIdentity.Product;
        Func<(Fixture f,string local,string low,FakeRegistry reg)> make=()=>{
            var f=NewFixture(temp); string low=Path.Combine(f.Base,"LocalLow");
            WriteRel(low,company+"/"+product+"/Player/prefs.txt","ours"); WriteRel(low,company+"/"+product+"/a.txt","ours");
            WriteRel(low,"Blizzard Entertainment/"+product+"/retail.txt","retail's"); WriteRel(low,"Unity/Web Player/x.txt","other");
            WriteRel(Path.Combine(f.Base,"local"),"Blizzard/Hearthstone/SoloStone/options.txt","ours");
            var reg=new FakeRegistry();
            foreach(var row in new Dictionary<string,int>{{company,0},{company+"\\"+product,24},{company+"\\"+product+"\\Sub",2},{"Blizzard Entertainment",0},{"Blizzard Entertainment\\"+product,30},{"Blizzard Entertainment\\"+product+"\\Sub",3}}) reg.Soft[row.Key]=row.Value;
            return (f,Path.Combine(f.Base,"local"),low,reg);
        };
        Action<FakeRegistry,string> retailIntact=(reg,when)=>Check(reg.Soft.ContainsKey("Blizzard Entertainment") && reg.Soft["Blizzard Entertainment\\"+product]==30 && reg.Soft.ContainsKey("Blizzard Entertainment\\"+product+"\\Sub"),"retail's key is untouched "+when);
        // Normal: ours is gone, the emptied parents too, nothing of retail's.
        var a=make(); Installer.Uninstall(a.f.Root,new string[0],a.local,a.reg,a.low);
        Check(!a.reg.Soft.Keys.Any(k=>k.StartsWith(company,StringComparison.OrdinalIgnoreCase)),"our key, its subkeys and the emptied parent key are removed");
        Check(!Directory.Exists(Path.Combine(a.low,company)) && File.ReadAllText(Path.Combine(a.low,"Blizzard Entertainment",product,"retail.txt"))=="retail's" && File.Exists(Path.Combine(a.low,"Unity","Web Player","x.txt")),"our LocalLow folder and its emptied parent go; retail's and Unity's stay");
        retailIntact(a.reg,"after uninstall");
        Check(!File.Exists(Path.Combine(a.local,"Blizzard","Hearthstone","SoloStone","options.txt")),"and the options folder goes in the same pass");
        Check(a.reg.SoftCalls.SequenceEqual(new[]{"tree "+company+"\\"+product,"empty "+company}) && !a.f.Record.ClientPath.Equals(null) && !Directory.Exists(a.f.Client),"exactly the two registry calls, and the copy is removed too");
        // A non-empty parent stays, in the registry and on disk.
        var b=make(); b.reg.Soft[company+"\\Other"]=1; WriteRel(b.low,company+"/Other/keep.txt","someone else's");
        Installer.Uninstall(b.f.Root,new string[0],b.local,b.reg,b.low);
        Check(b.reg.Soft.ContainsKey(company) && b.reg.Soft.ContainsKey(company+"\\Other") && !b.reg.Soft.ContainsKey(company+"\\"+product) && !b.reg.Soft.ContainsKey(company+"\\"+product+"\\Sub"),"a parent key with another subkey stays; only ours goes");
        Check(File.ReadAllText(Path.Combine(b.low,company,"Other","keep.txt"))=="someone else's" && !Directory.Exists(Path.Combine(b.low,company,product)),"a parent folder with another folder stays; only ours goes");
        retailIntact(b.reg,"with a shared parent");
        var c=make(); c.reg.Soft[company]=1; // A value in the parent key.
        Installer.Uninstall(c.f.Root,new string[0],c.local,c.reg,c.low);
        Check(c.reg.Soft.ContainsKey(company) && !c.reg.SoftCalls.Any(x=>x.StartsWith("empty")),"a parent key that holds a value is not removed");
        var d=make(); d.reg.Soft.Remove(company); d.reg.Soft.Remove(company+"\\"+product); d.reg.Soft.Remove(company+"\\"+product+"\\Sub"); Directory.Delete(Path.Combine(d.low,company),true);
        Installer.Uninstall(d.f.Root,new string[0],d.local,d.reg,d.low);
        Check(!Directory.Exists(d.f.Client) && d.reg.SoftCalls.All(x=>!x.Contains("Blizzard")),"nothing of ours there is fine");
        // Skipped without the account's folders: no localData, or no registry or LocalLow handed in.
        var e=make(); Installer.Uninstall(e.f.Root,new string[0],null,e.reg,e.low);
        Check(e.reg.Soft.ContainsKey(company+"\\"+product) && Directory.Exists(Path.Combine(e.low,company,product)) && e.reg.SoftCalls.Count==0,"no localData: neither the registry nor LocalLow is touched");
        var g=make(); Installer.Uninstall(g.f.Root,new string[0],g.local,null,g.low);
        Check(Directory.Exists(Path.Combine(g.low,company,product)),"no registry: LocalLow is left too");
        var h=make(); Installer.Uninstall(h.f.Root,new string[0],h.local,h.reg,null);
        Check(h.reg.Soft.ContainsKey(company+"\\"+product) && h.reg.SoftCalls.Count==0,"no LocalLow: the registry is left too");
        // A linked LocalLow folder stops everything before the first deletion.
        var k=make(); string elsewhere=Path.Combine(k.f.Base,"elsewhere"); WriteRel(elsewhere,"keep.txt","x");
        FsOps.DeleteTree(Path.Combine(k.low,company,product)); LinkDirectory(Path.Combine(k.low,company,product),elsewhere);
        Refuses(()=>Installer.Uninstall(k.f.Root,new string[0],k.local,k.reg,k.low),"a linked folder stops uninstall");
        Check(File.Exists(Path.Combine(elsewhere,"keep.txt")) && k.reg.SoftCalls.Count==0 && Directory.Exists(k.f.Client),"and nothing was deleted, in the registry, on disk or in the copy");
        Check(File.Exists(Path.Combine(k.local,"Blizzard","Hearthstone","SoloStone","options.txt")) && k.reg.Soft.ContainsKey(company+"\\"+product),"the options folder and the key survive it too, so a retry is not stuck behind half an uninstall");
        // A link nested inside the folder is found by the same up-front walk, not after the key is gone.
        var m=make(); string inner=Path.Combine(m.f.Base,"inner-target"); WriteRel(inner,"keep.txt","x");
        LinkDirectory(Path.Combine(m.low,company,product,"Player","inner"),inner);
        Refuses(()=>Installer.Uninstall(m.f.Root,new string[0],m.local,m.reg,m.low),"a nested link stops uninstall");
        Check(File.Exists(Path.Combine(inner,"keep.txt")) && m.reg.Soft.ContainsKey(company+"\\"+product) && m.reg.SoftCalls.Count==0 && File.Exists(Path.Combine(m.local,"Blizzard","Hearthstone","SoloStone","options.txt")) && Directory.Exists(m.f.Client),"and the key, the options folder and the copy are all still there");
        Refuses(()=>Installer.PrecheckAccountTraces(m.local,m.reg,m.low),"the up-front check alone refuses it");
        Installer.PrecheckAccountTraces(null,m.reg,m.low); Installer.PrecheckAccountTraces(a.local,null,null); count++; // Nothing to check without the account's folders.
        // The real registry only ever sees our company key; uninstall cleans the running account's traces, elevated or not.
        string program=Source("Program.cs"), winreg=Source("WindowsRegistry.cs");
        Check(program.Contains("bool own=true;") && program.Contains("own?new WindowsRegistry():null"),"uninstall cleans the running account's registry and LocalLow");
        int pre=program.IndexOf("Installer.PrecheckAccountTraces(",StringComparison.Ordinal), rules=program.IndexOf("if(WindowsHost.HasRules(record)) WindowsHost.Firewall(record,false);",StringComparison.Ordinal), remove=program.IndexOf("Installer.Uninstall(o.Root",StringComparison.Ordinal);
        Check(pre>0 && pre<rules && rules<remove,"uninstall checks for links before it removes the firewall rules or anything else");
        Check(!winreg.Contains("RegSetValueEx") && !winreg.Contains("Blizzard") && !winreg.Contains("OpenSubKey(Guarded") && winreg.IndexOf("Registry.CurrentUser",StringComparison.Ordinal)>0,"WindowsRegistry has no code for the old key: no reads, writes or Blizzard name");
        int own=winreg.IndexOf("static string Own(",StringComparison.Ordinal);
        Check(own>0 && winreg.Substring(own).Contains("ClientIdentity.NewName") && winreg.Contains("DeleteSubKeyTree(Own(path),false)") && winreg.Contains("DeleteSubKey(Own(path),false)") && !winreg.Substring(own).Contains("Blizzard"),"WindowsRegistry's new methods accept our company key only and name nothing of Blizzard's");
    }
}

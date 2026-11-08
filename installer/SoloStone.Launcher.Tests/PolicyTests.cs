using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SoloStone;

static partial class Program
{
    static ClientFacts FactsOf(string version,Dictionary<string,string> files) => new ClientFacts { Folder="/x", Version=version, Files=new Dictionary<string,string>(files) };
    static Dictionary<string,string> Hashed(char c) => ClientFacts.PinnedFiles.ToDictionary(p=>p,_=>new string(c,64));
    static string FindReferenceDll() {
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir!=null;dir=dir.Parent) {
            string candidate=Path.Combine(dir.FullName,"references","game","Assembly-CSharp.dll");
            if(File.Exists(candidate)) return candidate;
        }
        return null;
    }
    static void ClientVersionTests(string temp) {
        Func<string,string> dllIn=name=>Path.Combine(temp,"cv-"+name,"Hearthstone_Data","Managed","Assembly-CSharp.dll");
        Func<string,string> read=name=>ClientVersion.Read(Path.Combine(temp,"cv-"+name));
        CraftAssembly(dllIn("good"),"37.0",new[]{"37.0.0"}); Check(read("good")=="37.0.0","version from Network.get_BranchName");
        CraftAssembly(dllIn("build"),"37.0",new[]{"37.0.0"},programVersion:"37.0.0.255790");
        var buildFacts=ClientFacts.Read(Path.Combine(temp,"cv-build"));
        Check(buildFacts.Build=="37.0.0.255790" && Texts.Version(buildFacts.Version,buildFacts.Build)=="37.0.0.255790","program build read from client getter");
        Check(ClientVersion.ReadProgramVersion(Path.Combine(temp,"cv-good"))==null,"missing program build falls back");
        CraftAssembly(dllIn("patch"),"37.0",new[]{"37.0.2"}); Check(read("patch")=="37.0.2","patch version read");
        CraftAssembly(dllIn("minor"),"37.4",new[]{"37.4.1"}); Check(read("minor")=="37.4.1","minor version read");
        CraftAssembly(dllIn("branch-plus"),"37.0",new[]{"","37.0.0"},new[]{"1.2.3"}); Check(read("branch-plus")=="37.0.0","getter with empty and version literals as on the real client");
        CraftAssembly(dllIn("heap"),"37.0",new[]{"live"},new[]{"37.0.5","1.2.3","9.9.9"}); Check(read("heap")=="37.0.5","unique agreeing literal when the getter holds none");
        foreach(var bad in new (string name,string cosmetic,string[] branch,string[] other)[] {
            ("disagree","37.0",new[]{"38.0.0"},null), ("no-cosmetic",null,new[]{"37.0.0"},null), ("bad-cosmetic","37",new[]{"37.0.0"},null),
            ("cosmetic-words","thirty",new[]{"37.0.0"},null), ("two-in-getter","37.0",new[]{"37.0.0","37.0.1"},null), ("four-part","37.0",new[]{"37.0.0.1"},null),
            ("leading-zero","37.0",new[]{"37.0.01"},null), ("none","37.0",null,null), ("two-on-heap","37.0",new[]{"live"},new[]{"37.0.1","37.0.2"}), ("trailing","37.0",new[]{"37.0.0 "},null) })
        { CraftAssembly(dllIn(bad.name),bad.cosmetic,bad.branch,bad.other); Check(read(bad.name)==null,"unknown version: "+bad.name); }
        CraftAssembly(dllIn("wrong-type"),"37.0",new[]{"37.0.0"},typeName:"NotNetwork"); Check(read("wrong-type")==null,"no Network type is Unknown");
        CraftAssembly(dllIn("namespaced"),"37.0",new[]{"37.0.0"},typeName:"Other.Network"); Check(read("namespaced")==null,"a namespaced Network type is not the client's");
        CraftAssembly(dllIn("wrong-field"),"37.0",new[]{"37.0.0"},cosmeticField:"Cosmetic"); Check(read("wrong-field")==null,"missing CosmeticVersion field is Unknown");
        Directory.CreateDirectory(Path.GetDirectoryName(dllIn("garbage"))); File.WriteAllText(dllIn("garbage"),"not a dll"); Check(read("garbage")==null,"garbage DLL is Unknown");
        File.WriteAllBytes(dllIn("garbage"),new byte[0]); Check(read("garbage")==null,"empty DLL is Unknown");
        File.WriteAllBytes(dllIn("garbage"),new byte[]{0x4D,0x5A,0,0,1,2,3}); Check(read("garbage")==null,"truncated PE is Unknown");
        Check(ClientVersion.Read(Path.Combine(temp,"cv-absent"))==null,"absent client is Unknown");
        string real=FindReferenceDll();
        if(real==null) Skip("real Assembly-CSharp check: references/game is not present");
        else {
            string managed=Path.GetDirectoryName(real), client=Path.Combine(temp,"cv-real","Hearthstone_Data","Managed");
            Directory.CreateDirectory(client); File.Copy(real,Path.Combine(client,"Assembly-CSharp.dll"));
            Check(ClientVersion.Read(Path.Combine(temp,"cv-real"))=="37.0.0","real client DLL reads 37.0.0");
            Check(Hashes.FileHash(Path.Combine(client,"Assembly-CSharp.dll"))==TestedClient.Compiled().Files[ClientVersion.AssemblyPath],"reference DLL is the tested client's");
            File.Delete(Path.Combine(client,"Assembly-CSharp.dll"));
        }
    }
    static void ClientPolicyTests(string temp) {
        ClientVersionTests(temp);
        var tested=TestedClient.Compiled();
        Check(tested.Version=="37.0.0" && tested.Build=="254477" && tested.Files.Count==4 && tested.Files.Keys.All(ClientFacts.PinnedFiles.Contains),"embedded tested client");
        var pins=tested.Files; var other=Hashed('B');
        Func<ClientFacts,IEnumerable<TestedClient>,IEnumerable<KnownBadClient>,BindingResult,string[],Verdict> decide=(f,t,k,b,s)=>ClientPolicy.Decide(f,tested,t,k,b,s);
        var none=BindingResult.Passed;
        Check(ClientPolicy.RunningGameFailure("C:\\copy\\Hearthstone.exe","C:\\copy\\Hearthstone.exe")==ClientPolicy.CopyRunning && ClientPolicy.RunningGameFailure("C:\\retail\\Hearthstone.exe","C:\\copy\\Hearthstone.exe")==ClientPolicy.GameRunning, "running game names match executable");
        var retry=decide(FactsOf("37.0.0",pins),null,null,none,new[]{ClientPolicy.BattleRunning,ClientPolicy.GameRunning});
        Check(retry.RetryRunning && Texts.Headline(retry).Contains("Battle.net") && Texts.Headline(retry).Contains("Hearthstone"), "running processes block and retry");
        Check(decide(FactsOf("38.0.0",pins),null,null,none,new[]{ClientPolicy.GameRunning}).RetryRunning, "retry reveals client blockers after the game closes");
        var mixed=decide(FactsOf("37.0.0",pins),null,null,none,new[]{ClientPolicy.BattleRunning,"Firewall"});
        Check(!mixed.RetryRunning && Texts.Headline(mixed).Contains("Battle.net") && Texts.Headline(mixed).Contains("Firewall"), "running and other safety failures are both visible");
        Check(!decide(FactsOf("37.0.0",pins),null,null,none,new[]{"Firewall"}).RetryRunning, "other safety failures do not retry");
        var v=decide(FactsOf("37.0.0",pins),null,null,none,null);
        Check(v.Kind==VerdictKind.Allow && !v.Provisional && v.TestedVersion=="37.0.0" && v.FoundVersion=="37.0.0","exact tested version and hashes allow");
        Check(v.Exact,"exactly tested is marked exact");
        Check(v.Message.Contains("tested with this SoloStone: 37.0.0"),"tested version sentence on allow");
        // The owner's rule, 2026-11-06: patch runs silently, minor warns, major blocks.
        v=decide(FactsOf("37.0.0",other),null,null,none,null); Check(v.Kind==VerdictKind.Allow && !v.Exact && v.Reasons.Length==0,"same version, other hashes run silently");
        v=decide(FactsOf("37.0.2",other),null,null,none,null); Check(v.Kind==VerdictKind.Allow && !v.Exact && v.Reasons.Length==0 && v.Message.Contains("37.0.0") && v.Message.Contains("37.0.2"),"other patch runs silently, still naming both versions");
        v=decide(FactsOf("37.0.2",pins),null,null,none,null); Check(v.Kind==VerdictKind.Allow && !v.Exact,"other patch with the tested hashes runs silently and is not exact");
        v=decide(FactsOf("37.4.0",other),null,null,none,null); Check(v.Kind==VerdictKind.Warning && v.Message.Contains("37.4.0"),"other minor warns");
        v=decide(FactsOf("37.1.0",pins),null,null,none,null); Check(v.Kind==VerdictKind.Warning,"other minor warns even with the tested hashes");
        Check(ClientPolicy.SameMinor("37.0.0","37.0.9") && !ClientPolicy.SameMinor("37.0.0","37.1.0") && !ClientPolicy.SameMinor("37.0.0","3.70.0"),"same minor compares major and minor as numbers' text");
        v=decide(FactsOf("36.6.0",other),null,null,none,null); Check(v.Kind==VerdictKind.Block && v.Message.Contains("37.0.0"),"older major blocks with the sentence");
        v=decide(FactsOf("38.0.0",other),null,null,none,null); Check(v.Kind==VerdictKind.Block,"newer major blocks");
        foreach(string junk in new string[]{null,"","37","37.0","37.0.0.1","v37.0.0","37.0.x"}) {
            v=decide(FactsOf(junk,other),null,null,none,null); Check(v.Kind==VerdictKind.Block && v.Message.Contains("unknown"),"unknown version blocks: "+junk);
        }
        var missing=FactsOf("37.0.0",pins); missing.Files.Remove("UnityPlayer.dll"); missing.Missing.Add("UnityPlayer.dll");
        Check(decide(missing,null,null,none,null).Kind==VerdictKind.Block,"incomplete client blocks");
        // Signed data: a tested minor runs silently, and exact means version and all hashes.
        var signed=new[]{new TestedClient { Version="37.0.2", Files=other }};
        v=decide(FactsOf("37.0.2",other),signed,null,none,null); Check(v.Kind==VerdictKind.Allow && v.Exact && v.TestedVersion=="37.0.2","signed tested list marks an exact match");
        Check(!decide(FactsOf("37.0.2",Hashed('C')),signed,null,none,null).Exact,"exact needs all hashes");
        Check(!decide(FactsOf("37.0.3",other),signed,null,none,null).Exact,"exact needs the version");
        var signedMinor=new[]{new TestedClient { Version="37.4.1", Files=other }};
        v=decide(FactsOf("37.4.0",Hashed('C')),signedMinor,null,none,null); Check(v.Kind==VerdictKind.Allow && !v.Exact,"a signed tested client's minor runs silently");
        Check(decide(FactsOf("37.2.0",Hashed('C')),signedMinor,null,none,null).Kind==VerdictKind.Warning,"a minor nobody tested still warns");
        var partial=new Dictionary<string,string>(other); partial.Remove(ClientFacts.PinnedFiles[0]);
        Check(!decide(FactsOf("37.0.2",other),new[]{new TestedClient { Version="37.0.2", Files=partial }},null,none,null).Exact,"a partial hash entry is never exact");
        Check(decide(FactsOf("38.0.0",other),new[]{new TestedClient { Version="38.0.0", Files=other }},null,none,null).Kind==VerdictKind.Block,"signed data cannot lift a major block");
        Check(decide(FactsOf("37.0.2",other),signed,null,none,new[]{"Firewall"}).Kind==VerdictKind.Block,"signed data cannot lift a safety block");
        Check(decide(FactsOf("37.0.2",other),signed,null,BindingResult.Failed("gone"),null).Kind==VerdictKind.Block,"signed data cannot lift a binding block");
        Check(decide(FactsOf(null,other),new[]{new TestedClient { Version=null, Files=other }},null,none,null).Kind==VerdictKind.Block,"signed data cannot lift an unknown version");
        Check(decide(FactsOf("37.0.0",other),new[]{new TestedClient { Version="38.1.0", Files=Hashed('D') }},null,none,null).TestedVersion=="37.0.0","other-major signed entry never becomes the tested version");
        // Known bad: exact set only, never the compiled client.
        var bad=new[]{new KnownBadClient { Files=other }};
        v=decide(FactsOf("37.0.2",other),null,bad,none,null); Check(v.Kind==VerdictKind.Block && v.Message.Contains("known-bad"),"exact known-bad set blocks");
        Check(decide(FactsOf("37.0.2",other),signed,bad,none,null).Kind==VerdictKind.Block,"known bad beats a signed tested entry");
        Check(decide(FactsOf("37.0.2",Hashed('C')),null,bad,none,null).Kind==VerdictKind.Allow,"different hash set not blocked");
        Check(decide(FactsOf("37.0.2",other),null,new[]{new KnownBadClient { Files=partial }},none,null).Kind==VerdictKind.Allow,"subset never blocks: exact equality only");
        var ownBad=new[]{new KnownBadClient { Files=pins }};
        Check(decide(FactsOf("37.0.0",pins),null,ownBad,none,null).Kind==VerdictKind.Allow,"known-bad list can never block the compiled tested client");
        // Binding and safety.
        v=decide(FactsOf("37.0.0",pins),null,null,BindingResult.Failed("missing method"),null); Check(v.Kind==VerdictKind.Block && v.Message.Contains("missing method"),"binding failure blocks");
        v=decide(FactsOf("37.1.0",other),null,null,BindingResult.NotAvailable,null); Check(v.Kind==VerdictKind.Warning && v.Provisional && v.Message.Contains("game may still reject"),"no binding manifest marks the verdict provisional");
        v=decide(FactsOf("37.0.2",other),null,null,BindingResult.Failed("gone"),null); Check(v.Kind==VerdictKind.Block,"a failed binding blocks a patch-only difference too");
        v=decide(FactsOf("37.0.0",pins),null,null,BindingResult.NotAvailable,null); Check(v.Kind==VerdictKind.Allow && v.Provisional && !v.Message.Contains("game may still reject"),"provisional allow has no warning");
        Check(new NoBindingManifest().Check("/x").State==BindingState.NotAvailable,"only binding check today reports NotAvailable");
        v=decide(FactsOf("37.0.0",pins),null,null,none,new[]{"Quit Battle.net"}); Check(v.Kind==VerdictKind.Block && v.Message.Contains("Quit Battle.net") && v.Message.Contains("37.0.0"),"safety failure blocks the tested client too");
        // Staged launcher output.
        v=decide(FactsOf("37.1.2",other),null,null,BindingResult.NotAvailable,null);
        var parsed=Verdict.FromJson(v.ToJson()); Check(parsed.Kind==VerdictKind.Warning && parsed.TestedVersion=="37.0.0" && parsed.FoundVersion=="37.1.2" && parsed.Provisional && !parsed.Exact,"verdict JSON round trip");
        Check(Verdict.FromJson(decide(FactsOf("37.0.0",pins),null,null,none,null).ToJson()).Exact,"exact survives the round trip");
        foreach(string junk in new[]{"","{","null","{\"verdict\":\"Maybe\"}","[1]"}) Check(Verdict.FromJson(junk).Kind==VerdictKind.Block,"unreadable staged output counts as Block");
        // Launch acceptance: explicit, backed up once per client and release.
        string root=Path.Combine(temp,"accept"); Directory.CreateDirectory(root);
        int backups=0; Action backup=()=>backups++;
        var warning=decide(FactsOf("37.1.0",other),null,null,none,null); var facts=FactsOf("37.1.0",other);
        Refuses(()=>LaunchGate.Admit(root,warning,facts,"1.0.0",false,backup),"warning without acceptance refused");
        Check(backups==0,"unaccepted warning makes no backup");
        Check(LaunchGate.Admit(root,warning,facts,"1.0.0",true,backup)=="warning" && backups==1,"first acceptance backs up saves");
        Check(LaunchGate.Admit(root,warning,facts,"1.0.0",true,backup)=="warning" && backups==1,"second launch does not back up again");
        LaunchGate.Admit(root,warning,facts,"1.1.0",true,backup); Check(backups==2,"new release is a new acceptance");
        LaunchGate.Admit(root,warning,FactsOf("37.0.2",Hashed('C')),"1.1.0",true,backup); Check(backups==3,"new client hash is a new acceptance");
        var allow=decide(FactsOf("37.0.0",pins),null,null,none,null);
        Check(LaunchGate.Admit(root,allow,FactsOf("37.0.0",pins),"1.0.0",false,backup)=="allow" && backups==3,"exactly tested needs nothing");
        // A silent patch difference: no question, but the same one backup before play. A later block in the plugin leaves saves as they were.
        var patch=decide(FactsOf("37.0.5",Hashed('E')),null,null,none,null); var patchFacts=FactsOf("37.0.5",Hashed('E'));
        Check(LaunchGate.Admit(root,patch,patchFacts,"1.0.0",false,backup)=="allow" && backups==4,"a silent patch difference backs up without asking");
        Check(LaunchGate.Admit(root,patch,patchFacts,"1.0.0",false,backup)=="allow" && backups==4,"once per client and release");
        RejectsBecause(()=>LaunchGate.Admit(root,decide(FactsOf("38.0.0",other),null,null,none,null),facts,"1.0.0",true,backup),"tested with","a block can never be accepted");
        Check(LaunchGrant.Text("r","allow","37.0.0")=="schema=1\nrunId=r\nverdict=allow\nclientVersion=37.0.0\n","grant format");
    }
}

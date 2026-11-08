using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using SoloStone;

// What runs inside Unity's Mono: the hand-written reader and the registry firewall check.
static partial class Program
{
    static InstallRecord SampleRecord(string root,string client) => new InstallRecord { Schema=InstallRecord.CurrentSchema, InstallId=new string('a',32), Root=root, Source=Path.Combine(Path.GetDirectoryName(root),"源 \"quoted\" client"),
        ClientPath=client, ClientVersion="37.0.0", ReleaseVersion=null, Files=new Dictionary<string,string>{{"Hearthstone.exe",new string('A',64)},{"Hearthstone_Data/Managed/å.dll",new string('B',64)}},
        Payload=new Payload { Schema=1, BepInExVersion=null, DoorstopVersion="4.0", SourceRevision="rev", EngineSha256=new string('C',64), PluginSha256=new string('D',64), Files=new Dictionary<string,string>{{"winhttp.dll",new string('E',64)}} } };
    static void PlainJsonTests(string temp) {
        // The launcher writes with DataContractJsonSerializer; the plugin reads by hand. They must agree on real output, escapes included.
        string root=Path.Combine(temp,"json-root"), client=Path.Combine(root,"client");
        var record=SampleRecord(root,client);
        var read=InstallRecord.FromBytes(JsonBytes.ToBytes(record));
        Check(read.Schema==2 && read.InstallId==record.InstallId && read.Root==root && read.Source==record.Source && read.ClientPath==client && read.ClientVersion=="37.0.0" && read.ReleaseVersion==null,"reader agrees with the launcher's writer");
        Check(read.Files.Count==2 && read.Files["Hearthstone_Data/Managed/å.dll"]==new string('B',64),"file map survives, non-ASCII key included");
        Check(read.Payload.Schema==1 && read.Payload.DoorstopVersion=="4.0" && read.Payload.BepInExVersion==null && read.Payload.PluginSha256==new string('D',64) && read.Payload.Files["winhttp.dll"]==new string('E',64),"nested payload survives");
        Check(InstallRecord.FromBytes(Utf8("{\"schema\":2,\"files\":{},\"extra\":[1,{\"a\":[true,false,null,-3]}]}")).Schema==2,"unknown fields of every type are skipped");
        Check(InstallRecord.FromBytes(Utf8("{\"schema\":2,\"root\":\"\\u6e90\\/\\\\\\\"\\b\\f\\n\\r\\t\"}")).Root=="源/\\\"\b\f\n\r\t","all string escapes");
        Check(InstallRecord.FromBytes(Utf8(" \r\n{ \"schema\" : 2 } \n")).Schema==2,"surrounding whitespace");
        foreach(string bad in new[]{"","{","}","[","{\"schema\":2","{\"schema\":2}x","{\"schema\":2,}","{,}","{\"a\"}","{\"a\":}","{a:1}","'x'","{\"schema\":\"2\"}","{\"schema\":2.5}","{\"schema\":1e3}","{\"schema\":02}","{\"schema\":-}","{\"schema\":99999999999}",
            "{\"schema\":2,\"schema\":2}","{\"schema\":2,\"root\":\"a\nb\"}","{\"schema\":2,\"root\":\"\\x\"}","{\"schema\":2,\"root\":\"\\u12\"}","{\"schema\":2,\"root\":\"a}","{\"schema\":2,\"root\":3}","{\"schema\":2,\"files\":[]}","{\"schema\":2,\"files\":{\"a\":1}}","{\"schema\":2,\"payload\":3}","[1]","null","tru","{\"schema\":tru}"})
            Refuses(()=>InstallRecord.FromBytes(Utf8(bad)),"malformed record refused: "+bad);
        Refuses(()=>InstallRecord.FromBytes(Utf8(new string('[',64)+new string(']',64))),"too deep refused");
        Refuses(()=>PlainJson.Parse(new byte[PlainJson.MaxBytes+1]),"oversized refused");
        Refuses(()=>PlainJson.Parse(new byte[]{0x22,0xC3,0x28,0x22}),"invalid UTF-8 refused");
        // Byte order mark: .NET Framework's reader rejects it, so the core must, on every platform, for every file.
        byte[] bom=new byte[]{0xEF,0xBB,0xBF}.Concat(JsonBytes.ToBytes(record)).ToArray();
        RejectsBecause(()=>InstallRecord.FromBytes(bom),"byte order mark","install record with a BOM refused");
        RejectsBecause(()=>JsonBytes.Parse<Payload>(new byte[]{0xEF,0xBB,0xBF}.Concat(JsonBytes.ToBytes(record.Payload)).ToArray()),"byte order mark","the launcher's reader refuses a BOM here too");
        string folder=Path.Combine(temp,"json-bom"); Directory.CreateDirectory(folder); string file=Path.Combine(folder,"payload.json");
        File.WriteAllBytes(file,new byte[]{0xEF,0xBB,0xBF}.Concat(JsonBytes.ToBytes(record.Payload)).ToArray());
        RejectsBecause(()=>Json.Read<Payload>(file),"byte order mark","payload.json with a BOM refused");
        File.WriteAllBytes(Path.Combine(folder,"release.json"),new byte[]{0xEF,0xBB,0xBF}.Concat(Utf8("{\"schema\":1,\"version\":\"1.0.0\",\"saveSchema\":15,\"sourceRevision\":\"r\"}")).ToArray());
        Check(ReleaseInfo.TryRead(folder)==null,"release.json with a BOM reads as absent");
        File.WriteAllBytes(Path.Combine(folder,"release.json"),Utf8("{\"schema\":1,\"version\":\"1.0.0\",\"saveSchema\":15,\"sourceRevision\":\"r\"}"));
        Check(ReleaseInfo.TryRead(folder)?.Version=="1.0.0","release.json without a BOM reads");
    }
    static void FirewallRegistryTests(string temp) {
        string root=Path.Combine(temp,"fw-root"), client=Path.Combine(root,"client"); Directory.CreateDirectory(client);
        string exe=Path.Combine(client,"Hearthstone.exe"), helper=Path.Combine(client,"Crash Handler (x64)!.exe"); File.WriteAllText(exe,"x"); File.WriteAllText(helper,"x");
        var record=SampleRecord(root,client); string[] exes={exe,helper};
        string name=FirewallPlan.Name(record,exe), helperName=FirewallPlan.Name(record,helper);
        Func<string,string,string> netsh=(n,e)=>"v2.33|Action=Block|Active=TRUE|Dir=Out|App="+e+"|Name="+n+"|";
        // Real-format shapes: what netsh writes for profile=any protocol=any, other people's rules, and the all-profiles spelling.
        var others=new[]{"v2.33|Action=Allow|Active=TRUE|Dir=In|Protocol=6|Profile=Domain|Profile=Private|LPort=5985|RA4=LocalSubnet|RA6=LocalSubnet|App=System|Name=@FirewallAPI.dll,-30253|Desc=@FirewallAPI.dll,-30256|EmbedCtxt=@FirewallAPI.dll,-30267|",
            "v2.10|Action=Block|Active=FALSE|Dir=Out|Protocol=17|Profile=Public|App=%SystemRoot%\\system32\\svchost.exe|Svc=Dnscache|Name=Core Networking|","garbage","","v2.33|broken"};
        Func<string,FirewallSnapshot> with=rule=>new FirewallSnapshot { Rules=others.Concat(new[]{rule,netsh(helperName,helper)}).ToList() };
        FirewallRegistry.Check(record,exes,with(netsh(name,exe))); Check(true,"netsh's rule text accepted among other rules");
        var parsed=FirewallRuleText.Parse(others[0]);
        Check(parsed.One("Action")=="Allow" && parsed.Fields["Profile"].Count==2 && parsed.One("LPort")=="5985" && parsed.One("Profile")==null,"rule text parses, repeated fields included");
        Check(FirewallRuleText.Parse("garbage")==null && FirewallRuleText.Parse("")==null && FirewallRuleText.Parse(null)==null && FirewallRuleText.Parse("v2.33|broken")==null,"non-rule text is not a rule");
        foreach(string ok in new[]{"v2.33|Action=Block|Active=TRUE|Dir=Out|Profile=Domain|Profile=Private|Profile=Public|App="+exe+"|Name="+name+"|","v2.33|Action=Block|Active=TRUE|Dir=Out|Protocol=256|App="+exe+"|Name="+name+"|",
            "v2.33|Action=Block|Active=TRUE|Dir=Out|IFType=All|App="+exe+"|Name="+name+"|Desc=x|EmbedCtxt=SoloStone|","v2.33|action=block|active=true|dir=out|app="+exe.ToUpperInvariant()+"|name="+name+"|"})
            if(ok.Contains("action=block")) { var lower=FirewallRuleText.Parse(ok); Check(lower.One("Name")==name,"field names are case-insensitive"); FirewallRegistry.Check(record,exes,with(ok)); Check(true,"lower-case rule accepted"); }
            else { FirewallRegistry.Check(record,exes,with(ok)); Check(true,"equivalent rule spelling accepted"); }
        foreach(string bad in new[]{"Action=Allow|Active=TRUE|Dir=Out|App="+exe,"Action=Block|Active=FALSE|Dir=Out|App="+exe,"Action=Block|Active=TRUE|Dir=In|App="+exe,"Action=Block|Active=TRUE|Dir=Out|App="+Path.Combine(temp,"other.exe"),
            "Active=TRUE|Dir=Out|App="+exe,"Action=Block|Dir=Out|App="+exe,"Action=Block|Active=TRUE|App="+exe,"Action=Block|Active=TRUE|Dir=Out",
            "Action=Block|Active=TRUE|Dir=Out|App="+exe+"|Protocol=6","Action=Block|Active=TRUE|Dir=Out|App="+exe+"|RA4=1.1.1.1","Action=Block|Active=TRUE|Dir=Out|App="+exe+"|LA4=127.0.0.1","Action=Block|Active=TRUE|Dir=Out|App="+exe+"|RA6=::1",
            "Action=Block|Active=TRUE|Dir=Out|App="+exe+"|RPort=443","Action=Block|Active=TRUE|Dir=Out|App="+exe+"|LPort=1","Action=Block|Active=TRUE|Dir=Out|App="+exe+"|Profile=Public","Action=Block|Active=TRUE|Dir=Out|App="+exe+"|Profile=Domain|Profile=Private",
            "Action=Block|Active=TRUE|Dir=Out|App="+exe+"|IFType=Wireless","Action=Block|Active=TRUE|Dir=Out|App="+exe+"|IF={guid}","Action=Block|Active=TRUE|Dir=Out|App="+exe+"|Svc=x","Action=Block|Active=TRUE|Dir=Out|App="+exe+"|EmbedCtxt=Other",
            "Action=Block|Action=Allow|Active=TRUE|Dir=Out|App="+exe,"Action=Block|Active=TRUE|Dir=Out|App="+exe+"|FutureNarrowing=1"})
            RejectsBecause(()=>FirewallRegistry.Check(record,exes,with("v2.33|"+bad+"|Name="+name+"|")),"protection is incomplete","rule that does not cover everything refused: "+bad.Substring(0,Math.Min(30,bad.Length)));
        RejectsBecause(()=>FirewallRegistry.Check(record,exes,new FirewallSnapshot { Rules=new List<string>{netsh(helperName,helper)} }),"missing or duplicated","missing rule refused");
        RejectsBecause(()=>FirewallRegistry.Check(record,exes,new FirewallSnapshot()),"missing or duplicated","no rules refused");
        RejectsBecause(()=>FirewallRegistry.Check(record,exes,new FirewallSnapshot { Rules=new List<string>{netsh(name,exe),netsh(name,exe),netsh(helperName,helper)} }),"missing or duplicated","duplicate rule refused");
        Func<Action<FirewallSnapshot>,FirewallSnapshot> tweak=change=>{ var s=with(netsh(name,exe)); change(s); return s; };
        RejectsBecause(()=>FirewallRegistry.Check(record,exes,tweak(s=>s.ProfileOff=true)),"must be enabled","firewall off refused");
        RejectsBecause(()=>FirewallRegistry.Check(record,exes,tweak(s=>s.PolicyOff=true)),"must be enabled","firewall off by policy refused");
        RejectsBecause(()=>FirewallRegistry.Check(record,exes,tweak(s=>s.PolicyNoLocalRules=true)),"Windows policy prevents","policy ignoring local rules refused");
        RejectsBecause(()=>FirewallRegistry.Check(record,exes,tweak(s=>s.ExcludedInterface=true)),"excludes a network interface","excluded interface refused");
    }
    static string CraftPlugin(string dll,params MethodInfo[] calls) {
        var builder=new PersistedAssemblyBuilder(new AssemblyName(Path.GetFileNameWithoutExtension(dll)),typeof(object).Assembly);
        var type=builder.DefineDynamicModule("m").DefineType("Probe",TypeAttributes.Public);
        var il=type.DefineMethod("Run",MethodAttributes.Public|MethodAttributes.Static).GetILGenerator();
        foreach(var call in calls) il.Emit(OpCodes.Call,call);
        il.Emit(OpCodes.Ret); type.CreateType(); builder.Save(dll); return dll;
    }
    static void CraftLibrary(string dll,string name,bool withMember) {
        var builder=new PersistedAssemblyBuilder(new AssemblyName(name),typeof(object).Assembly);
        var type=builder.DefineDynamicModule("m").DefineType("Lib.Api",TypeAttributes.Public);
        var il=type.DefineMethod("Always",MethodAttributes.Public|MethodAttributes.Static).GetILGenerator(); il.Emit(OpCodes.Ret);
        if(withMember) { il=type.DefineMethod("Sometimes",MethodAttributes.Public|MethodAttributes.Static).GetILGenerator(); il.Emit(OpCodes.Ret); }
        type.CreateType(); Directory.CreateDirectory(Path.GetDirectoryName(dll)); builder.Save(dll);
    }
    static void MonoGuardTests(string temp) {
        // Throwaway assemblies built here reference this runtime's corelib; a real plugin references mscorlib.
        Func<MonoGuard.Report,List<string>> F=r=>r.Failures.Where(f=>!f.Contains("System.Private.CoreLib")).ToList();
        string folder=Path.Combine(temp,"guard"), full=Path.Combine(folder,"full","Unity.Lib.dll"), stripped=Path.Combine(folder,"stripped","Unity.Lib.dll");
        CraftLibrary(full,"Unity.Lib",true); CraftLibrary(stripped,"Unity.Lib",false);
        var loaded=Assembly.LoadFile(full).GetType("Lib.Api");
        string baseline=CraftPlugin(Path.Combine(folder,"baseline.dll"),loaded.GetMethod("Always"));
        string plugin=CraftPlugin(Path.Combine(folder,"plugin.dll"),loaded.GetMethod("Always"),loaded.GetMethod("Sometimes"));
        var missing=MonoGuard.Check(plugin,baseline,new string[0],new[]{Path.GetDirectoryName(stripped)});
        Check(F(missing).Single().Contains("missing from the client") && F(missing).Single().Contains("Sometimes"),"a member the client's DLL lacks fails the guard");
        var present=MonoGuard.Check(plugin,baseline,new string[0],new[]{Path.GetDirectoryName(full)});
        Check(F(present).Count==0 && present.Verified>=1,"a member the client's DLL has passes");
        // A BCL API the default build never used: checked in the client's DLL when we have it, otherwise named in the assumed list. Never silent.
        string bclFull=Path.Combine(folder,"bcl-full","System.Xml.dll"), bclStripped=Path.Combine(folder,"bcl-stripped","System.Xml.dll");
        CraftLibrary(bclFull,"System.Xml",true); CraftLibrary(bclStripped,"System.Xml",false);
        var bcl=Assembly.LoadFile(bclFull).GetType("Lib.Api");
        string bclBase=CraftPlugin(Path.Combine(folder,"bcl-base.dll"),bcl.GetMethod("Always")), bclPlugin=CraftPlugin(Path.Combine(folder,"bcl-plugin.dll"),bcl.GetMethod("Always"),bcl.GetMethod("Sometimes"));
        var row=MonoGuard.References(bclPlugin).Single(r=>r.Contains("Sometimes"));
        Check(row.StartsWith("System.Xml|Lib.Api|Sometimes|",StringComparison.Ordinal),"member reference read from metadata: "+row);
        Check(F(MonoGuard.Check(bclPlugin,bclBase,new string[0],new string[0])).Single().Contains("mono-assumed.txt"),"a new BCL member nobody listed fails");
        var listed=MonoGuard.Check(bclPlugin,bclBase,new[]{row},new string[0]);
        Check(F(listed).Count==0 && listed.Assumed.Single()==row,"a listed member passes as assumed, and the report says so");
        Check(F(MonoGuard.Check(bclPlugin,bclBase,new[]{row},new[]{Path.GetDirectoryName(bclStripped)})).Single().Contains("missing from the client"),"the client's real DLL beats the assumed list");
        Check(MonoGuard.Check(bclPlugin,bclBase,new string[0],new[]{Path.GetDirectoryName(bclFull)}).Verified>=1,"the client's real DLL proves a member");
        string stranger=Path.Combine(folder,"Stranger.Lib.dll"); CraftLibrary(stranger,"Stranger.Lib",true);
        string strangerPlugin=CraftPlugin(Path.Combine(folder,"stranger.dll"),Assembly.LoadFile(stranger).GetType("Lib.Api").GetMethod("Always"));
        Check(MonoGuard.Check(strangerPlugin,strangerPlugin,new string[0],new string[0]).Failures.Any(f=>f.Contains("assembly not in the client")),"an assembly the client does not ship fails");
        // The two refusals real builds hit: the serializer stub and the dynamic binder.
        foreach(var (assemblyName,typeName) in new[]{("System.Runtime.Serialization","System.Runtime.Serialization.Json.DataContractJsonSerializer"),("Microsoft.CSharp","Microsoft.CSharp.RuntimeBinder.Binder")}) {
            string lib=Path.Combine(folder,assemblyName+".dll");
            var builder=new PersistedAssemblyBuilder(new AssemblyName(assemblyName),typeof(object).Assembly);
            int dot=typeName.LastIndexOf('.'); var t=builder.DefineDynamicModule("m").DefineType(typeName,TypeAttributes.Public);
            var il=t.DefineMethod("Touch",MethodAttributes.Public|MethodAttributes.Static).GetILGenerator(); il.Emit(OpCodes.Ret); t.CreateType(); builder.Save(lib);
            string uses=CraftPlugin(Path.Combine(folder,"uses-"+assemblyName+".dll"),Assembly.LoadFile(lib).GetType(typeName).GetMethod("Touch"));
            // Even with the stub present in the client folder and in the baseline, it fails.
            Check(MonoGuard.Check(uses,uses,new string[0],new[]{folder}).Failures.Any(f=>f.Contains("unavailable on Unity's Mono")),"guard refuses "+assemblyName);
        }
    }
    static void PluginSubsetTests(string temp) {
        PlainJsonTests(temp);
        FirewallRegistryTests(temp);
        MonoGuardTests(temp);
    }
}

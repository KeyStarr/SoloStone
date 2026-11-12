using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using SoloStone;

// A generic and a nested class: BindingScan reads these from metadata, ClientBindings from reflection, and the two must spell them alike.
class BindingFixture<T>
{
    public T Value; public List<T>[] Lists; public Dictionary<string,T> Map; public static int Count; public int[,] Grid;
    public class Inner { public T Held; public void Use(T item,BindingFixture<T>.Inner other) { } }
    public void Plain() { }
    public int Overload(int a) => a;
    public int Overload(ref string a,out long b,params object[] rest) { b=0; return 0; }
    public U Generic<U>(U one,List<U> many,T owner) => one;
    public T Property { get; set; }
    public BindingFixture(T value) { Value=value; }
    public static void Pointer(IntPtr p,bool? flag,DayOfWeek day,(int,string) tuple) { }
    public System.Collections.IEnumerator Iterate() { yield return 1; }
}
class BindingFixtureChild : BindingFixture<int> { public BindingFixtureChild() : base(1) { } }

static partial class Program
{
    static readonly ClientBindings.Client ThisProcess=new ClientBindings.Client {
        Assembly=name=>AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name==name),
        Type=(assembly,name)=>AppDomain.CurrentDomain.GetAssemblies().Where(a=>assembly=="*" || a.GetName().Name==assembly).Select(a=>a.GetType(name,false)).FirstOrDefault(t=>t!=null),
        Iterator=method=>method.Name=="Iterate"
    };
    static string[] FixtureRows() {
        string own=typeof(BindingFixture<>).Assembly.GetName().Name, dll=typeof(BindingFixture<>).Assembly.Location;
        var rows=new List<string>();
        using(var pe=new PEReader(File.OpenRead(dll))) {
            var md=pe.GetMetadataReader();
            foreach(var h in md.TypeDefinitions) {
                string name=BindingScan.TypeName(md,h);
                if(!name.StartsWith("BindingFixture",StringComparison.Ordinal) || name.Contains("<>") || name.Contains("<Iterate>")) continue;
                var def=md.GetTypeDefinition(h);
                rows.Add("T\t"+own+"\t"+name+"\t\t");
                foreach(var f in def.GetFields()) { var field=md.GetFieldDefinition(f); string n=md.GetString(field.Name); if(!n.StartsWith("<",StringComparison.Ordinal)) rows.Add("F\t"+own+"\t"+name+"\t"+n+"\t"+BindingScan.Signature(md,field)); }
                foreach(var m in def.GetMethods()) { var method=md.GetMethodDefinition(m); string n=md.GetString(method.Name); rows.Add((n==".ctor"?"C":"M")+"\t"+own+"\t"+name+"\t"+n+"\t"+BindingScan.Signature(md,method)); }
            }
        }
        return rows.ToArray();
    }
    // A throwaway plugin that binds the way the real one does: a constant target, a target through a helper, a target it cannot know.
    // A plugin with an entry class, the ways the runtime reaches code without a call token, and optionally code nothing reaches.
    static string CraftReach(string dir,bool withOrphan,out string client,bool unreadLookup=false,bool shopTable=false) {
        Directory.CreateDirectory(dir);
        string harmonyDll=Path.Combine(dir,"0Harmony.dll"), bepinDll=Path.Combine(dir,"BepInEx.dll"), gameDll=Path.Combine(dir,"client","Client.Game.dll"); client=gameDll; Directory.CreateDirectory(Path.GetDirectoryName(gameDll));
        var harmony=new PersistedAssemblyBuilder(new AssemblyName("0Harmony"),typeof(object).Assembly); var hm=harmony.DefineDynamicModule("m");
        var tools=hm.DefineType("HarmonyLib.AccessTools",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed);
        var body=tools.DefineMethod("Method",MethodAttributes.Public|MethodAttributes.Static,typeof(MethodInfo),new[]{typeof(Type),typeof(string)}).GetILGenerator(); body.Emit(OpCodes.Ldnull); body.Emit(OpCodes.Ret);
        tools.CreateType(); harmony.Save(harmonyDll);
        var bepin=new PersistedAssemblyBuilder(new AssemblyName("BepInEx"),typeof(object).Assembly); var bm=bepin.DefineDynamicModule("m");
        var pluginBase=bm.DefineType("BepInEx.BaseUnityPlugin",TypeAttributes.Public); pluginBase.DefineDefaultConstructor(MethodAttributes.Public); pluginBase.CreateType(); bepin.Save(bepinDll);
        var method=Assembly.LoadFile(harmonyDll).GetType("HarmonyLib.AccessTools").GetMethod("Method"); var entryBase=Assembly.LoadFile(bepinDll).GetType("BepInEx.BaseUnityPlugin");
        StripClient(gameDll,"Direct","ViaHelper","LoopFirst","LoopSecond");
        var game=AssemblyLoadContextFor(gameDll).GetType("Game.Target"); var typeFromHandle=typeof(Type).GetMethod("GetTypeFromHandle");
        var plugin=new PersistedAssemblyBuilder(new AssemblyName("Reach"),typeof(object).Assembly); var module=plugin.DefineDynamicModule("m");
        Action<ILGenerator,string> bind=(il,name)=>{ il.Emit(OpCodes.Ldtoken,game); il.Emit(OpCodes.Call,typeFromHandle); il.Emit(OpCodes.Ldstr,name); il.Emit(OpCodes.Call,method); il.Emit(OpCodes.Pop); };
        const MethodAttributes Static=MethodAttributes.Public|MethodAttributes.Static;
        // A Harmony patch, named by string with its type in the same body.
        var hooks=module.DefineType("Hooks",TypeAttributes.Public); var prefix=hooks.DefineMethod("Prefix",Static,typeof(void),Type.EmptyTypes).GetILGenerator(); bind(prefix,"Direct"); prefix.Emit(OpCodes.Ret);
        // Installed by a built name, "Prefix" + a suffix.
        hooks.DefineMethod("PrefixBuilt",Static,typeof(void),Type.EmptyTypes).GetILGenerator().Emit(OpCodes.Ret);
        // A patch named by string with its type inside the helper the name is passed to.
        var far=module.DefineType("FarHooks",TypeAttributes.Public); var farPrefix=far.DefineMethod("FarPrefix",Static,typeof(void),Type.EmptyTypes).GetILGenerator(); bind(farPrefix,"LoopFirst"); farPrefix.Emit(OpCodes.Ret);
        var helpers=module.DefineType("Helpers",TypeAttributes.Public); var helper=helpers.DefineMethod("PatchFar",Static,typeof(void),new[]{typeof(string)}); var hi=helper.GetILGenerator(); hi.Emit(OpCodes.Ldtoken,far); hi.Emit(OpCodes.Pop); hi.Emit(OpCodes.Ret);
        // A component: touching the type is all the plugin does, and the runtime calls the rest.
        var component=module.DefineType("Comp",TypeAttributes.Public); component.DefineDefaultConstructor(MethodAttributes.Public); var update=component.DefineMethod("Update",MethodAttributes.Public,typeof(void),Type.EmptyTypes).GetILGenerator(); bind(update,"LoopSecond"); update.Emit(OpCodes.Ret);
        // A public accessor only a serializer calls.
        var saved=component.DefineMethod("get_Saved",MethodAttributes.Public|MethodAttributes.SpecialName,typeof(int),Type.EmptyTypes).GetILGenerator(); saved.Emit(OpCodes.Ldc_I4_0); saved.Emit(OpCodes.Ret);
        TypeBuilder lookups=null, shop=null;
        if(unreadLookup) {
            lookups=module.DefineType("Lookups",TypeAttributes.Public);
            var held=lookups.DefineField("Held",typeof(MethodInfo),FieldAttributes.Public|FieldAttributes.Static); var read=lookups.DefineField("ReadOne",typeof(MethodInfo),FieldAttributes.Public|FieldAttributes.Static);
            var init=lookups.DefineTypeInitializer().GetILGenerator(); init.Emit(OpCodes.Ldnull); init.Emit(OpCodes.Stsfld,held); init.Emit(OpCodes.Ldnull); init.Emit(OpCodes.Stsfld,read); init.Emit(OpCodes.Ret);
            var reader=lookups.DefineMethod("Update",MethodAttributes.Public,typeof(void),Type.EmptyTypes).GetILGenerator(); reader.Emit(OpCodes.Ldsfld,read); reader.Emit(OpCodes.Pop); reader.Emit(OpCodes.Ret);
        }
        if(shopTable) {
            // NativeShop's row table: a ui row binds on use, so a row no string names has no user.
            shop=module.DefineType("NativeShop",TypeAttributes.Public);
            var table=shop.DefineField("ReflectionBindings",typeof(string[][]),FieldAttributes.Private|FieldAttributes.Static);
            var init=shop.DefineTypeInitializer().GetILGenerator();
            string[][] rows={new[]{"Method","Game.Target","LoopFirst","*","","ui"},new[]{"Method","Game.Target","ViaHelper","*","","ui"}};
            init.Emit(OpCodes.Ldc_I4,rows.Length); init.Emit(OpCodes.Newarr,typeof(string[]));
            for(int r=0;r<rows.Length;r++) {
                init.Emit(OpCodes.Dup); init.Emit(OpCodes.Ldc_I4,r); init.Emit(OpCodes.Ldc_I4,rows[r].Length); init.Emit(OpCodes.Newarr,typeof(string));
                for(int c=0;c<rows[r].Length;c++) { init.Emit(OpCodes.Dup); init.Emit(OpCodes.Ldc_I4,c); init.Emit(OpCodes.Ldstr,rows[r][c]); init.Emit(OpCodes.Stelem_Ref); }
                init.Emit(OpCodes.Stelem_Ref);
            }
            init.Emit(OpCodes.Stsfld,table); init.Emit(OpCodes.Ret);
        }
        var entry=module.DefineType("Entry",TypeAttributes.Public,entryBase); entry.DefineDefaultConstructor(MethodAttributes.Public);
        var awake=entry.DefineMethod("Awake",MethodAttributes.Private,typeof(void),Type.EmptyTypes).GetILGenerator();
        awake.Emit(OpCodes.Ldtoken,hooks); awake.Emit(OpCodes.Pop); awake.Emit(OpCodes.Ldstr,"Prefix"); awake.Emit(OpCodes.Pop);
        awake.Emit(OpCodes.Ldstr,"FarPrefix"); awake.Emit(OpCodes.Call,helper);
        awake.Emit(OpCodes.Ldtoken,component); awake.Emit(OpCodes.Pop);
        if(lookups!=null) { awake.Emit(OpCodes.Ldtoken,lookups); awake.Emit(OpCodes.Pop); }
        // The string that would keep the orphan alive if any string named any method of that name.
        awake.Emit(OpCodes.Ldstr,"OrphanRun"); awake.Emit(OpCodes.Pop); awake.Emit(OpCodes.Ret);
        TypeBuilder orphan=null;
        if(withOrphan) { orphan=module.DefineType("Orphan",TypeAttributes.Public); var run=orphan.DefineMethod("OrphanRun",Static,typeof(void),Type.EmptyTypes).GetILGenerator(); bind(run,"ViaHelper"); run.Emit(OpCodes.Ret); }
        hooks.CreateType(); far.CreateType(); helpers.CreateType(); component.CreateType(); entry.CreateType(); orphan?.CreateType(); lookups?.CreateType(); shop?.CreateType();
        string dll=Path.Combine(dir,"reach.dll"); plugin.Save(dll); return dll;
    }
    static string CraftBinder(string dir,bool withUnknown,out string client,bool bodyFixture=false) {
        Directory.CreateDirectory(dir);
        string harmonyDll=Path.Combine(dir,"0Harmony.dll"), gameDll=Path.Combine(dir,"client","Client.Game.dll"); client=gameDll; Directory.CreateDirectory(Path.GetDirectoryName(gameDll));
        var harmony=new PersistedAssemblyBuilder(new AssemblyName("0Harmony"),typeof(object).Assembly);
        var module=harmony.DefineDynamicModule("m");
        var tools=module.DefineType("HarmonyLib.AccessTools",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed);
        var lookup=tools.DefineMethod("Method",MethodAttributes.Public|MethodAttributes.Static,typeof(MethodInfo),new[]{typeof(Type),typeof(string)}).GetILGenerator(); lookup.Emit(OpCodes.Ldnull); lookup.Emit(OpCodes.Ret);
        var patches=module.DefineType("HarmonyLib.Harmony",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed);
        var patchBody=patches.DefineMethod("Patch",MethodAttributes.Public|MethodAttributes.Static,typeof(void),new[]{typeof(MethodBase)}).GetILGenerator(); patchBody.Emit(OpCodes.Ret);
        tools.CreateType(); patches.CreateType(); harmony.Save(harmonyDll);
        var loaded=Assembly.LoadFile(harmonyDll);
        var method=loaded.GetType("HarmonyLib.AccessTools").GetMethod("Method"); var patch=loaded.GetType("HarmonyLib.Harmony").GetMethod("Patch");
        if(bodyFixture) {
            var lib=new PersistedAssemblyBuilder(new AssemblyName("Client.Game"),typeof(object).Assembly);
            var mod=lib.DefineDynamicModule("m");
            var baseType=mod.DefineType("Game.Base",TypeAttributes.Public|TypeAttributes.Abstract);
            baseType.DefineMethod("Target",MethodAttributes.Public|MethodAttributes.Abstract|MethodAttributes.Virtual,typeof(void),Type.EmptyTypes);
            baseType.DefineMethod("Overloaded",MethodAttributes.Public|MethodAttributes.Abstract|MethodAttributes.Virtual,typeof(void),Type.EmptyTypes);
            baseType.DefineMethod("Overloaded",MethodAttributes.Public,typeof(void),new[]{typeof(int)}).GetILGenerator().Emit(OpCodes.Ret);
            var derived=mod.DefineType("Game.Derived",TypeAttributes.Public,baseType);
            derived.DefineMethod("Target",MethodAttributes.Public|MethodAttributes.Virtual,typeof(void),Type.EmptyTypes).GetILGenerator().Emit(OpCodes.Ret);
            derived.DefineMethod("Overloaded",MethodAttributes.Public|MethodAttributes.Virtual,typeof(void),Type.EmptyTypes).GetILGenerator().Emit(OpCodes.Ret);
            baseType.CreateType(); derived.CreateType(); lib.Save(gameDll);
        } else StripClient(gameDll,"Direct","ViaHelper","LoopFirst","LoopSecond");
        var gameAssembly=AssemblyLoadContextFor(gameDll); var gameType=gameAssembly.GetType(bodyFixture?"Game.Base":"Game.Target");
        var plugin=new PersistedAssemblyBuilder(new AssemblyName("Binder"),typeof(object).Assembly);
        var p=plugin.DefineDynamicModule("m").DefineType("Binder",TypeAttributes.Public);
        var typeFromHandle=typeof(Type).GetMethod("GetTypeFromHandle");
        var helper=p.DefineMethod("Hook",MethodAttributes.Public|MethodAttributes.Static,typeof(void),new[]{typeof(Type),typeof(string)});
        var h=helper.GetILGenerator(); h.Emit(OpCodes.Ldarg_0); h.Emit(OpCodes.Ldarg_1); h.Emit(OpCodes.Call,method); h.Emit(OpCodes.Call,patch); h.Emit(OpCodes.Ret);
        var run=p.DefineMethod("Run",MethodAttributes.Public|MethodAttributes.Static,typeof(void),Type.EmptyTypes).GetILGenerator();
        if(bodyFixture) {
            run.Emit(OpCodes.Ldtoken,gameType); run.Emit(OpCodes.Call,typeFromHandle); run.Emit(OpCodes.Ldstr,"Target"); run.Emit(OpCodes.Call,helper);
            run.Emit(OpCodes.Ldtoken,gameAssembly.GetType("Game.Derived")); run.Emit(OpCodes.Call,typeFromHandle); run.Emit(OpCodes.Ldstr,"Target"); run.Emit(OpCodes.Call,method); run.Emit(OpCodes.Call,patch);
            run.Emit(OpCodes.Ret); p.CreateType(); string fixture=Path.Combine(dir,"binder.dll"); plugin.Save(fixture); return fixture;
        }
        run.Emit(OpCodes.Ldtoken,gameType); run.Emit(OpCodes.Call,typeFromHandle); run.Emit(OpCodes.Ldstr,"Direct"); run.Emit(OpCodes.Call,method); run.Emit(OpCodes.Call,patch);
        run.Emit(OpCodes.Ldtoken,gameType); run.Emit(OpCodes.Call,typeFromHandle); run.Emit(OpCodes.Ldstr,"ViaHelper"); run.Emit(OpCodes.Call,helper);
        // foreach over a literal array: every element is a target, not only the one the loop index starts at.
        var names=run.DeclareLocal(typeof(string[])); var index=run.DeclareLocal(typeof(int));
        run.Emit(OpCodes.Ldc_I4_2); run.Emit(OpCodes.Newarr,typeof(string));
        run.Emit(OpCodes.Dup); run.Emit(OpCodes.Ldc_I4_0); run.Emit(OpCodes.Ldstr,"LoopFirst"); run.Emit(OpCodes.Stelem_Ref);
        run.Emit(OpCodes.Dup); run.Emit(OpCodes.Ldc_I4_1); run.Emit(OpCodes.Ldstr,"LoopSecond"); run.Emit(OpCodes.Stelem_Ref);
        run.Emit(OpCodes.Stloc,names); run.Emit(OpCodes.Ldc_I4_0); run.Emit(OpCodes.Stloc,index);
        run.Emit(OpCodes.Ldtoken,gameType); run.Emit(OpCodes.Call,typeFromHandle); run.Emit(OpCodes.Ldloc,names); run.Emit(OpCodes.Ldloc,index); run.Emit(OpCodes.Ldelem_Ref); run.Emit(OpCodes.Call,method); run.Emit(OpCodes.Call,patch);
        // A type no client DLL here holds, known only by name.
        run.Emit(OpCodes.Ldstr,"Absent.Type"); run.Emit(OpCodes.Call,typeof(Type).GetMethod("GetType",new[]{typeof(string)})); run.Emit(OpCodes.Ldstr,"Member"); run.Emit(OpCodes.Call,method); run.Emit(OpCodes.Call,patch);
        run.Emit(OpCodes.Ret);
        if(withUnknown) { var u=p.DefineMethod("FromElsewhere",MethodAttributes.Public|MethodAttributes.Static,typeof(void),new[]{typeof(MethodBase)}).GetILGenerator(); u.Emit(OpCodes.Ldarg_0); u.Emit(OpCodes.Call,patch); u.Emit(OpCodes.Ret); }
        p.CreateType();
        string dll=Path.Combine(dir,"binder.dll"); plugin.Save(dll); return dll;
    }
    static Assembly AssemblyLoadContextFor(string file) => new System.Runtime.Loader.AssemblyLoadContext(Guid.NewGuid().ToString(),true).LoadFromAssemblyPath(file);
    static void StripClient(string client,params string[] keep) {
        var lib=new PersistedAssemblyBuilder(new AssemblyName("Client.Game"),typeof(object).Assembly);
        var t=lib.DefineDynamicModule("m").DefineType("Game.Target",TypeAttributes.Public);
        foreach(string name in keep) { var il=t.DefineMethod(name,MethodAttributes.Public|MethodAttributes.Static).GetILGenerator(); il.Emit(OpCodes.Ret); }
        t.CreateType(); lib.Save(client);
    }
    static void ClientCheckTests(string temp) {
        // V1 at run time: metadata and reflection agree on every spelling, and a planted missing hook fails.
        var rows=FixtureRows();
        Check(rows.Length>12 && rows.Any(r=>r.Contains("Generic")) && rows.Any(r=>r.Contains("+Inner")),"the fixture produced rows: "+rows.Length);
        var missing=ClientBindings.Missing(rows,ThisProcess);
        Check(missing.Count==0,"metadata and reflection spell signatures alike: "+string.Join("; ",missing));
        string own=typeof(BindingFixture<>).Assembly.GetName().Name;
        foreach(var planted in new[]{
            ("M\t*\tBindingFixture`1\tNoSuchHook\t*","method"), ("F\t"+own+"\tBindingFixture`1\tNoSuchField\t*","field"), ("P\t*\tBindingFixture`1\tNoSuchProperty\t*","property"),
            ("T\t*\tNoSuch.Type\t\t","type"), ("A\tNoSuch.Assembly\t\t\t","assembly"), ("C\t*\tBindingFixture`1\t.ctor\t(System.Double)","method"),
            ("M\t*\tBindingFixture`1\tPlain\t(System.Int32)","method"), ("F\t*\tBindingFixture`1\tValue\tSystem.Int32","field"), ("E\t*\tBindingFixture`1\tPlain\t*","method"), ("not a row","unreadable") }) {
            var found=ClientBindings.Missing(rows.Concat(new[]{planted.Item1}),ThisProcess);
            Check(found.Count==1 && found[0].StartsWith(planted.Item2,StringComparison.Ordinal),"a planted missing target fails V1: "+planted.Item1+" -> "+string.Join("; ",found));
        }
        Check(ClientBindings.Missing(new[]{"E\t*\tBindingFixture`1\tIterate\t*","M\t*\tBindingFixtureChild\tPlain\t*","F\t*\tBindingFixtureChild\tValue\t*","M\t*\tBindingFixture`1\tOverload\t*"},ThisProcess).Count==0,"an iterator, inherited members and any-overload rows pass");
        var throwing=new ClientBindings.Client { Assembly=_=>throw new InvalidOperationException("boom"), Type=(a,n)=>throw new TypeLoadException("boom"), Iterator=_=>false };
        Check(ClientBindings.Missing(new[]{"A\tx\t\t\t","T\t*\ty\t\t"},throwing).Count==2,"a lookup that throws counts as missing");
        RejectsBecause(()=>ClientBindings.ReadRows(null),"missing","no manifest refuses");
        RejectsBecause(()=>ClientBindings.ReadRows(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("# only a comment\n"))),"empty","an empty manifest refuses");
        Check(ClientBindings.ReadRows(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("# c\nT\t*\tX\t\t\n"))).Single()=="T\t*\tX\t\t","rows skip comments");
        Check(ClientBindings.ReadRows(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("# c\r\nT\t*\tX\t\t\r\nA\tY\t\t\t\r\n"))).SequenceEqual(new[]{"T\t*\tX\t\t","A\tY\t\t\t"}),"a CRLF manifest reads the same rows");
        // V2: a planted missing pool card. Numbers and text never reach this: only presence of a record does.
        Check(ClientBindings.MissingCards(new[]{"A","B","A"},id=>id!="A").Single()=="A" && ClientBindings.MissingCards(new[]{"A"},_=>true).Count==0,"a pool card with no client record is missing, once");
        string many=ClientBindings.Summary("cards",new[]{"A","B","C","D","E","F"});
        Check(many.StartsWith("6 cards missing: A, B, C, D, E",StringComparison.Ordinal) && many.EndsWith("and more",StringComparison.Ordinal),"a block names a count and the first few");
        // The real registrations against the rows. Compiler-made iterator classes count as their method.
        var iterator=typeof(BindingFixture<>).GetNestedTypes(BindingFlags.NonPublic).Single(t=>t.Name.StartsWith("<Iterate>",StringComparison.Ordinal));
        var none=new MethodBase[0];
        var covered=ClientBindings.Uncovered(new MethodBase[]{typeof(BindingFixture<>).GetMethod("Plain"),iterator.GetMethod("MoveNext",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public),typeof(string).GetMethod("Trim",Type.EmptyTypes),typeof(Console).GetMethod("SetOut")},
            new[]{"M\t*\tBindingFixture`1\tPlain\t*","E\t*\tBindingFixture`1\tIterate\t*"},none);
        Check(covered.Count==0,"named patches are covered, and the runtime's own patched methods are not the client's: "+string.Join(";",covered));
        Check(ClientBindings.Uncovered(new MethodBase[]{typeof(BindingFixture<>).GetMethod("Plain")},new[]{"M\t*\tBindingFixture`1\tOther\t*"},none).Single()=="BindingFixture`1.Plain","a patch no row names is reported");
        var observed=typeof(BindingFixture<>).GetMethod("Plain");
        Check(ClientBindings.Uncovered(new MethodBase[]{observed},new string[0],new[]{(MethodBase)observed}).Count==0,"an observe-only filter patch is not required");
        // Only a method nothing but the lab owner patches is excused. A shared patch with no row stays a gap, alone or beside a lab patch.
        Func<string[],bool> labOnlyFor=owners=>ClientBindings.LabOnly(new MethodBase[]{observed},method=>owners).Count==1;
        Check(labOnlyFor(new[]{ClientBindings.LabOwner}),"a patch only the lab owner makes is lab-only");
        Check(!labOnlyFor(new[]{ClientBindings.LabOwner,"solostone.plugin.patches"}) && !labOnlyFor(new[]{"solostone.plugin.patches"}),"a target shared code patches stays required");
        Check(!labOnlyFor(new string[0]) && !labOnlyFor(null),"no owner information stays required");
        Check(ClientBindings.Uncovered(new MethodBase[]{observed},new string[0],ClientBindings.LabOnly(new MethodBase[]{observed},method=>new[]{ClientBindings.LabOwner,"solostone.plugin.patches"})).Single()=="BindingFixture`1.Plain","a shared patch beside a lab patch is still reported");
        foreach(string name in new[]{"mscorlib","System","System.Core","netstandard","Mono.Security","MonoMod.Utils","BepInEx","0Harmony","SabberStoneCore","HSNativeUIProbe"}) Check(!ClientBindings.IsClientAssembly(name),"not the client's: "+name);
        foreach(string name in new[]{"Assembly-CSharp","blizzard.bgsclient","UnityEngine.CoreModule","Blizzard.T5.Core"}) Check(ClientBindings.IsClientAssembly(name),"the client's: "+name);

        // The launcher's reason for exit 42 comes from this launch's report only.
        string reports=Path.Combine(temp,"check-reports"); Directory.CreateDirectory(reports);
        string run=Guid.NewGuid().ToString("D");
        Check(ClientCheckFile.BlockDetail(reports,run)==null,"no report, no reason");
        Check(!ClientCheckFile.Holding(reports,run),"no report, no hold");
        ClientCheckReport.Write(reports,run,ClientCheckReport.Held,"cards",null); Check(ClientCheckFile.BlockDetail(reports,run)==null,"a held launch is not a block");
        Check(ClientCheckFile.Holding(reports,run) && !ClientCheckFile.Holding(reports,Guid.NewGuid().ToString("D")),"this launch's hold keeps the launcher waiting");
        Check(Health.ChecksWaitSeconds>ClientCheckReport.CardWaitSeconds,"the launcher outwaits the plugin's own card limit, so the plugin reports a stuck load itself");
        ClientCheckReport.Write(reports,run,ClientCheckReport.Refused,"cards","3 cards\nmissing"); Check(ClientCheckFile.BlockDetail(reports,run)=="3 cards missing","a refused report gives its detail on one line");
        Check(!ClientCheckFile.Holding(reports,run),"a refused launch is no longer held");
        ClientCheckReport.Write(reports,run,ClientCheckReport.Refused,"native card audit","Effective native card tag mismatch: DINO_410:referenced:TAUNT");
        Check(ClientCheckFile.BlockDetail(reports,run).Contains("DINO_410:referenced:TAUNT") && Health.ExitHooks==42,"audit refusal reaches launcher with card and field");
        ClientCheckReport.Write(reports,run,ClientCheckReport.Released,"cards",null); Check(!ClientCheckFile.Holding(reports,run) && ClientCheckFile.BlockDetail(reports,run)==null,"a released launch is neither");
        Check(ClientCheckFile.BlockDetail(reports,Guid.NewGuid().ToString("D"))==null,"another launch's report says nothing");
        Check(ClientCheckFile.FileName==ClientCheckReport.FileName,"plugin and launcher agree on the file");
        Check(Messages.ClientBlocked("3 cards missing").Contains("3 cards missing") && Messages.ClientBlocked(null).Contains("saves were not changed"),"exit 42 tells the player what and that saves are safe");
        Check(Health.Classify(false,false,ClientChecks42)==HealthOutcome.Failed,"a blocked release holds and restores, like any hook failure");

        // The gate node: a target not covered fails.
        string dir=Path.Combine(temp,"binder"), client; string plugin=CraftBinder(dir,false,out client);
        PatchBodyTests(temp);
        var full=new BindingScan.Client(new[]{Path.GetDirectoryName(client)});
        string extra=Path.Combine(dir,"extra.txt"), manifest=Path.Combine(dir,"manifest.txt"), references=Path.Combine(dir,"references");
        var scan=BindingScan.Build(new[]{plugin},full,extra);
        Check(scan.Failures.Count==0,"a clean plugin passes: "+string.Join("; ",scan.Failures));
        Check(scan.Rows.Contains("M\tClient.Game\tGame.Target\tDirect\t*"),"a constant target is a row");
        Check(scan.Rows.Contains("M\tClient.Game\tGame.Target\tViaHelper\t*"),"a target through a helper is a row, which a source scan of the helper would not show");
        Check(BindingScan.Build(new[]{plugin},StrippedClient(dir,client,"Direct"),extra).Failures.Any(f=>f.Contains("ViaHelper")),"a target the client DLLs lack fails the build");
        // An iterator row means what HarmonyX needs, not only that the method exists. Lane 1 blocked on the real client when it meant less.
        var fixtureClient=new BindingScan.Client(new[]{Path.GetDirectoryName(typeof(BindingFixture<>).Assembly.Location)});
        Check(fixtureClient.Problem("E\t"+own+"\tBindingFixture`1\tIterate\t*")==null,"a real iterator is an iterator row");
        Check(fixtureClient.Problem("E\t"+own+"\tBindingFixture`1\tPlain\t*")!=null && fixtureClient.Problem("M\t"+own+"\tBindingFixture`1\tPlain\t*")==null,"a plain method exists, and is not an iterator row");
        string referenceDll=FindReferenceDll();
        if(referenceDll==null) Skip("real iterator rows: references/game is not present");
        else {
            var real=new BindingScan.Client(new[]{Path.GetDirectoryName(referenceDll)});
            Check(real.Problem("E\tAssembly-CSharp\tDeckTrayDeckListContent\tCreateTraySections\t*")!=null,"CreateTraySections makes a closure, not an iterator: the row that blocked lane 1 fails here");
            Check(real.Problem("M\tAssembly-CSharp\tDeckTrayDeckListContent\tCreateTraySections\t*")==null,"and it exists as the plain method the plugin patches");
            foreach(var iterator2 in new[]{"DeckTrayDeckListContent\tDeleteDeckAnimation","PackOpeningDirector\tNewHighlightsReadyImpl","ShopBrowser\tLoadSectionsCoroutine"})
                Check(real.Problem("E\tAssembly-CSharp\t"+iterator2+"\t*")==null,"a real client iterator passes: "+iterator2);
            // Every iterator row V1 ships must hold on the tested client's own DLL.
            string shipped=Path.Combine(AppContext.BaseDirectory,"plugin-source","client-bindings.txt");
            foreach(string row in File.ReadAllLines(shipped).Where(l=>l.StartsWith("E\t",StringComparison.Ordinal)))
                Check(row.Split('\t')[1]=="*" || real.Problem(row)==null,"shipped iterator row holds on the tested client: "+row);
        }
        Check(scan.Rows.Contains("M\tClient.Game\tGame.Target\tLoopFirst\t*") && scan.Rows.Contains("M\tClient.Game\tGame.Target\tLoopSecond\t*"),"foreach over a literal array covers every element");
        Check(scan.Rows.Contains("M\t*\tAbsent.Type\tMember\t*"),"a target outside the DLLs here is not dropped: V1 checks it on the real client");
        string unknown=CraftBinder(Path.Combine(temp,"binder2"),true,out string client2);
        var clientTwo=new BindingScan.Client(new[]{Path.GetDirectoryName(client2)});
        string extra2=Path.Combine(temp,"binder2","extra.txt");
        var site=BindingScan.Build(new[]{unknown},clientTwo,extra2).Failures.Single(f=>f.Contains("FromElsewhere"));
        Check(site.Contains("not covered"),"a patch whose target the IL does not show is not covered: "+site);
        string siteLine=site.Substring(site.IndexOf(": ",StringComparison.Ordinal)+2);
        File.WriteAllText(extra2,"row M Game.Target Direct # fixture\nsite "+siteLine+" # reached by a table\n");
        Check(BindingScan.Build(new[]{unknown},clientTwo,extra2).Failures.Count==0,"naming the site and its targets covers it");
        File.WriteAllText(extra2,"site "+siteLine+" no reason\n");
        Check(BindingScan.Build(new[]{unknown},clientTwo,extra2).Failures.Any(f=>f.Contains("without a reason")),"an extra line needs a reason");
        File.WriteAllText(extra2,"site "+siteLine+" # x\nsite Gone.Method | Patch | y # stale\n");
        Check(BindingScan.Build(new[]{unknown},clientTwo,extra2).Failures.Any(f=>f.Contains("stale site")),"a stale site fails, so the list cannot rot");
        File.WriteAllText(extra2,"site "+siteLine+" # x\nrow M Game.Target NoSuchMethod # wrong\n");
        Check(BindingScan.Build(new[]{unknown},clientTwo,extra2).Failures.Any(f=>f.Contains("NoSuchMethod")),"a wrong row fails against the client DLLs");
        File.WriteAllText(extra2,"skip Binder.FromElsewhere # tolerates a miss\n");
        Check(BindingScan.Build(new[]{unknown},clientTwo,extra2).Failures.Count==0,"code that tolerates a miss can be skipped, with a reason");
        File.WriteAllText(extra2,"skip Binder.FromElsewhere # tolerates a miss\nskip Gone.Method # stale\n");
        Check(BindingScan.Build(new[]{unknown},clientTwo,extra2).Failures.Any(f=>f.Contains("stale skip") && f.Contains("Gone.Method")),"a stale skip fails, so it cannot hide a later binding");
        // A row only dead code binds would block a player over nothing, and a test hook left in a public build is exactly that.
        string reachClient, reachExtra=Path.Combine(temp,"reach-extra.txt");
        string clean=CraftReach(Path.Combine(temp,"reach-clean"),false,out reachClient);
        var reachClean=BindingScan.Build(new[]{clean},new BindingScan.Client(new[]{Path.GetDirectoryName(reachClient)}),reachExtra);
        Check(reachClean.Failures.Count==0,"a named patch, a patch named through a helper and a component's messages are live: "+string.Join("; ",reachClean.Failures));
        Check(reachClean.Rows.Contains("M\tClient.Game\tGame.Target\tDirect\t*") && reachClean.Rows.Contains("M\tClient.Game\tGame.Target\tLoopFirst\t*") && reachClean.Rows.Contains("M\tClient.Game\tGame.Target\tLoopSecond\t*"),"live code keeps its rows");
        string dead=CraftReach(Path.Combine(temp,"reach-dead"),true,out reachClient);
        var reachDead=BindingScan.Build(new[]{dead},new BindingScan.Client(new[]{Path.GetDirectoryName(reachClient)}),reachExtra);
        Check(reachDead.Failures.Any(f=>f.Contains("cannot reach binds this row") && f.Contains("ViaHelper") && f.Contains("Orphan.OrphanRun")),"a row only dead code binds fails, names the code, and a string of the same name does not excuse it: "+string.Join("; ",reachDead.Failures));
        Check(reachDead.DeadMethods.Any(m=>m.StartsWith("Orphan.OrphanRun",StringComparison.Ordinal)),"the dead method is listed");
        Check(reachClean.LiveMethods.Contains("Hooks.PrefixBuilt") && reachClean.LiveMethods.Contains("Comp.get_Saved"),"a method installed by a built name and a serializer's accessor are not called dead");
        var reachUnread=BindingScan.Build(new[]{CraftReach(Path.Combine(temp,"reach-unread"),false,out reachClient,unreadLookup:true)},new BindingScan.Client(new[]{Path.GetDirectoryName(reachClient)}),reachExtra);
        Check(reachUnread.Failures.Any(f=>f.Contains("nothing reads") && f.Contains("Lookups.Held")) && !reachUnread.Failures.Any(f=>f.Contains("Lookups.ReadOne")),"a lookup in a static nothing reads fails, and one something reads does not: "+string.Join("; ",reachUnread.Failures));
        var reachShop=BindingScan.Build(new[]{CraftReach(Path.Combine(temp,"reach-shop"),false,out reachClient,shopTable:true)},new BindingScan.Client(new[]{Path.GetDirectoryName(reachClient)}),reachExtra);
        Check(reachShop.Failures.Any(f=>f.Contains("cannot reach binds this row") && f.Contains("ViaHelper")) && !reachShop.Failures.Any(f=>f.Contains("LoopFirst")),"a ui table row no string names is dead, and a named one is not: "+string.Join("; ",reachShop.Failures));
        string reachExtraRows=Path.Combine(temp,"reach-extra-rows.txt"); File.WriteAllText(reachExtraRows,"row M Game.Target Direct # named by live code\nrow M Game.Target ViaHelper # its reader has gone\n");
        var reachStale=BindingScan.Build(new[]{clean},new BindingScan.Client(new[]{Path.GetDirectoryName(reachClient)}),reachExtraRows);
        Check(reachStale.Failures.Any(f=>f.Contains("no string in live code") && f.Contains("ViaHelper")) && !reachStale.Failures.Any(f=>f.Contains("Direct")),"an extra row no live string names fails, and a named one does not: "+string.Join("; ",reachStale.Failures));
        // The node itself: the committed manifest must equal the plugin.
        string game=Path.Combine(references,"game"); Directory.CreateDirectory(game); File.Copy(client,Path.Combine(game,"Client.Game.dll"),true);
        Func<bool,int> node=write=>BindingScan.Run(new[]{"--references",references,"--manifest",manifest,"--extra",extra,plugin}.Concat(write?new[]{"--write"}:new string[0]).ToArray());
        Check(node(false)==1,"no manifest fails the node");
        Check(node(true)==0 && node(false)==0,"--write then check passes");
        string good=File.ReadAllText(manifest);
        File.WriteAllText(manifest,good.Replace("M\tClient.Game\tGame.Target\tViaHelper\t*\n","")); Check(node(false)==1,"a target missing from the manifest fails the node");
        File.WriteAllText(manifest,good+"M\tClient.Game\tGame.Target\tGone\t*\n"); Check(node(false)==1,"a stale manifest row fails the node");
        File.WriteAllText(manifest,good);

        // The order that makes the hold real. Source checks, because Awake only runs on a client.
        string probe=PluginSource("OfflineProbe.cs");
        int engage=probe.IndexOf("SaveHold.Engage()",StringComparison.Ordinal), held=probe.IndexOf("ResolveHeldLaunch(",StringComparison.Ordinal), bindings=probe.IndexOf("ClientChecks.CheckBindings(",StringComparison.Ordinal), hooks=probe.IndexOf("new Harmony(",StringComparison.Ordinal);
        Check(engage>=0 && engage<held && held<bindings && bindings<hooks,"Awake: hold, then the save-free launch, then V1, then the first hook");
        Check(!probe.Substring(0,probe.IndexOf("private void TickSaveHold",StringComparison.Ordinal)).Contains("OfflineGuard.SeedProfileIfMissing"),"Awake seeds nothing");
        int release=probe.IndexOf("SaveHold.Release()",StringComparison.Ordinal), step=probe.IndexOf("ClientChecks.Step step =",StringComparison.Ordinal);
        Check(step>=0 && step<release && release<probe.IndexOf("InstallerRuntime.Prepare(launch.ResolveSaves)",StringComparison.Ordinal) && release<probe.IndexOf("ClientBridge.OpenSaves()",StringComparison.Ordinal),"the hold is released only after V2, and saves resolve only after that");
        string bridge=PluginSource("ClientBridge.cs");
        string initialize=bridge.Substring(bridge.IndexOf("public static void Initialize(",StringComparison.Ordinal));
        initialize=initialize.Substring(0,initialize.IndexOf("internal static void OpenSaves()",StringComparison.Ordinal));
        Check(!initialize.Contains("BindStartupPath") && Regex.IsMatch(initialize,@"#if STANDARD_PROOF\s+OpenSaves\(\);"),"ClientBridge.Initialize binds nothing, except a candidate's fixed snapshot");
        string results=PluginSource("NativeResultsUI.cs");
        Check(Regex.IsMatch(results,@"BlockNetCacheRequest\(method\.Name\)\)\s+harmony\.Patch\(method, prefix: new HarmonyMethod\(typeof\(NativeResultsUI\), nameof\(BeforeCacheRequest\)\) \{ priority = Priority\.First \}\);\s+//[^\n]*\n\s+else ClientChecks\.NoteSelfAdapting\(method\);"),"only the observing patches are optional: a blocked NetCache request stays required");
        foreach(string blocked in new[]{"RegisterScreenEndOfGame","RequestNetCacheObject","NetCacheReload_Internal"}) Check(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"plugin-source","client-bindings.txt")).Contains("M\tAssembly-CSharp\tNetCache\t"+blocked+"\t"),"a blocked NetCache request is a V1 row: "+blocked);
        string checks=PluginSource("ClientChecks.cs");
        Check(Regex.IsMatch(checks,@"#if !SOLOSTONE_INSTALLER \|\| SOLOSTONE_TEST_HOOKS\s+string path=?\s*=?\s*Path\.Combine\(reportsFolder, ""client-check-plant\.txt""\)"),"the plant file is compiled out of a release");
        // The manifest comes from the public build, so a lab build's own patches have no row. They must run on the lab owner, or the lab's coverage check fails on them.
        string manifestRows=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"plugin-source","client-bindings.txt"));
        foreach(string labPatch in new[]{"DeckTrayDeckListContent\tUpdateDragToReorder","DialogManager\tHandleKeyboardInput","AlertPopup\tHandleKeyboardInput","InputManager\tUpdate","InputManager\tSetDragging","InputCollection\tGetMouseButtonDown","UIBScrollable\tm_ScrollBounds","NetCache\tm_cacheRequests","ShopBrowser\tm_sections"}) Check(!manifestRows.Contains("\t"+labPatch+"\t"),"a lab-only patch is not a row, so no player can be blocked over it: "+labPatch);
        foreach(Match region in Regex.Matches(probe,@"#if !SOLOSTONE_RELEASE\b.*?#endif",RegexOptions.Singleline)) Check(!Regex.IsMatch(region.Value,@"\bharmony\b"),"lab-only code registers on the lab owner, not the shared one: "+Regex.Match(region.Value,@"[^\n]*\bharmony\b[^\n]*").Value.Trim());
        string installerRuntime=Source("InstallerRuntime.cs");
        Check(!Regex.IsMatch(installerRuntime,@"\b37\.\d") && Regex.IsMatch(installerRuntime,@"CompiledMajor=37;"),"the plugin pins the major only: no minor, patch or build");
    }
    const int ClientChecks42=42;
    static void PatchBodyTests(string temp) {
        string bodyPlugin=CraftBinder(Path.Combine(temp,"binder-body"),false,out string bodyClient,true);
        var bodyReferences=new BindingScan.Client(new[]{Path.GetDirectoryName(bodyClient)});
        var bodyScan=BindingScan.Build(new[]{bodyPlugin},bodyReferences,Path.Combine(temp,"no-extra"));
        Check(bodyReferences.NoBody("Game.Base","Overloaded","()") && !bodyReferences.NoBody("Game.Base","Overloaded","(System.Int32)") && !bodyReferences.NoBody("Game.Base","Overloaded","*") && !bodyReferences.NoBody("Game.Base","Overloaded","(System.String)"),"body checks respect overloads and unresolved targets");
        Check(bodyScan.Failures.Count==1 && bodyScan.Failures[0].Contains("Binder.Run") && bodyScan.Failures[0].Contains("Game.Base.Target") && bodyScan.Failures[0].Contains("concrete override"),"abstract patch fails and concrete override passes: "+string.Join("; ",bodyScan.Failures));
    }
    static BindingScan.Client StrippedClient(string dir,string client,params string[] keep) {
        string folder=Path.Combine(dir,"stripped"); Directory.CreateDirectory(folder);
        StripClient(Path.Combine(folder,"Client.Game.dll"),keep);
        return new BindingScan.Client(new[]{folder});
    }
}

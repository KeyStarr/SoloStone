using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

// The default plugin runs on Unity's Mono every day, so its references are proven. Everything the Installer=true build adds
// must exist there too: checked in the client's own DLLs when a Managed folder is given, otherwise named in mono-assumed.txt.
static class MonoGuard
{
    sealed class Names : ISignatureTypeProvider<string,object>
    {
        readonly MetadataReader md; public Names(MetadataReader md) { this.md=md; }
        public string GetPrimitiveType(PrimitiveTypeCode code) => code.ToString();
        public string GetTypeFromDefinition(MetadataReader r,TypeDefinitionHandle h,byte kind) => TypeName(r,h);
        public string GetTypeFromReference(MetadataReader r,TypeReferenceHandle h,byte kind) => TypeName(r,h);
        public string GetTypeFromSpecification(MetadataReader r,object c,TypeSpecificationHandle h,byte kind) => r.GetTypeSpecification(h).DecodeSignature(this,c);
        public string GetSZArrayType(string e) => e+"[]";
        public string GetArrayType(string e,ArrayShape s) => e+"["+new string(',',s.Rank-1)+"]";
        public string GetByReferenceType(string e) => e+"&";
        public string GetPointerType(string e) => e+"*";
        public string GetPinnedType(string e) => e;
        public string GetGenericInstantiation(string g,ImmutableArray<string> args) => g+"<"+string.Join(",",args)+">";
        public string GetGenericTypeParameter(object c,int i) => "!"+i;
        public string GetGenericMethodParameter(object c,int i) => "!!"+i;
        public string GetFunctionPointerType(MethodSignature<string> s) => "fn";
        public string GetModifiedType(string m,string t,bool required) => t;
    }
    static string TypeName(MetadataReader r,TypeDefinitionHandle h) {
        var t=r.GetTypeDefinition(h);
        if(!t.GetDeclaringType().IsNil) return TypeName(r,t.GetDeclaringType())+"+"+r.GetString(t.Name);
        string space=r.GetString(t.Namespace);
        return (space.Length==0?"":space+".")+r.GetString(t.Name);
    }
    static string TypeName(MetadataReader r,TypeReferenceHandle h) {
        var t=r.GetTypeReference(h);
        if(t.ResolutionScope.Kind==HandleKind.TypeReference) return TypeName(r,(TypeReferenceHandle)t.ResolutionScope)+"+"+r.GetString(t.Name);
        string space=r.GetString(t.Namespace);
        return (space.Length==0?"":space+".")+r.GetString(t.Name);
    }
    static string Scope(MetadataReader r,TypeReferenceHandle h) {
        var scope=r.GetTypeReference(h).ResolutionScope;
        if(scope.Kind==HandleKind.TypeReference) return Scope(r,(TypeReferenceHandle)scope);
        return scope.Kind==HandleKind.AssemblyReference?r.GetString(r.GetAssemblyReference((AssemblyReferenceHandle)scope).Name):null;
    }
    static string Open(string name) { int at=name.IndexOf('<'); return at<0?name:name.Substring(0,at); }
    // assembly|type|member|signature. Instantiations are reduced to the open generic so "List<string>.Add" and "List<int>.Add" are one API.
    public static SortedSet<string> References(string dll) {
        var result=new SortedSet<string>(StringComparer.Ordinal);
        using(var pe=new PEReader(File.OpenRead(dll))) {
            var md=pe.GetMetadataReader(); var names=new Names(md);
            foreach(var handle in md.TypeReferences) { string scope=Scope(md,handle); if(scope!=null) result.Add(scope+"|"+TypeName(md,handle)+"||"); }
            foreach(var handle in md.MemberReferences) {
                var member=md.GetMemberReference(handle);
                TypeReferenceHandle parent=default(TypeReferenceHandle); string type=null;
                if(member.Parent.Kind==HandleKind.TypeReference) parent=(TypeReferenceHandle)member.Parent;
                else if(member.Parent.Kind==HandleKind.TypeSpecification) {
                    var reader=md.GetBlobReader(md.GetTypeSpecification((TypeSpecificationHandle)member.Parent).Signature);
                    if(reader.ReadSignatureTypeCode()==SignatureTypeCode.GenericTypeInstance) { reader.ReadCompressedInteger(); var inner=reader.ReadTypeHandle(); if(inner.Kind==HandleKind.TypeReference) parent=(TypeReferenceHandle)inner; }
                }
                if(parent.IsNil) continue;
                type=TypeName(md,parent);
                string scope=Scope(md,parent);
                if(scope==null) continue;
                result.Add(scope+"|"+type+"|"+md.GetString(member.Name)+"|"+Signature(md,member.Signature,names));
            }
        }
        return result;
    }
    static string Signature(MetadataReader md,BlobHandle signature,Names names) {
        var decoder=new SignatureDecoder<string,object>(names,md,null);
        var peek=md.GetBlobReader(signature);
        var reader=md.GetBlobReader(signature);
        if(peek.ReadSignatureHeader().Kind==SignatureKind.Field) return "field:"+decoder.DecodeFieldSignature(ref reader);
        var method=decoder.DecodeMethodSignature(ref reader);
        return method.ReturnType+"("+string.Join(",",method.ParameterTypes)+")";
    }
    // A folder of real DLLs, such as a client's Managed folder.
    sealed class Library
    {
        readonly List<(PEReader pe,MetadataReader md)> modules=new List<(PEReader,MetadataReader)>();
        public readonly HashSet<string> Assemblies=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Library(IEnumerable<string> folders) {
            foreach(string folder in folders.Where(Directory.Exists))
                foreach(string dll in Directory.GetFiles(folder,"*.dll")) {
                    try { var pe=new PEReader(File.OpenRead(dll)); var md=pe.GetMetadataReader(); modules.Add((pe,md)); Assemblies.Add(Path.GetFileNameWithoutExtension(dll)); }
                    catch(BadImageFormatException) { }
                }
        }
        (MetadataReader md,TypeDefinition def)? Find(string type) {
            foreach(var (pe,md) in modules)
                foreach(var handle in md.TypeDefinitions)
                    if(TypeName(md,handle)==type) return (md,md.GetTypeDefinition(handle));
            return null;
        }
        readonly Dictionary<string,(MetadataReader md,TypeDefinition def)?> cache=new Dictionary<string,(MetadataReader md,TypeDefinition def)?>();
        (MetadataReader md,TypeDefinition def)? Cached(string type) { if(!cache.TryGetValue(type,out var found)) cache[type]=found=Find(type); return found; }
        public bool Has(string type,string member,string signature) {
            var found=Cached(type);
            if(found==null) return false;
            if(member.Length==0) return true;
            for(int depth=0;found!=null && depth<16;depth++) {
                var (md,def)=found.Value; var names=new Names(md);
                bool isField=signature.StartsWith("field:",StringComparison.Ordinal);
                if(isField) { foreach(var h in def.GetFields()) { var f=md.GetFieldDefinition(h); if(md.GetString(f.Name)==member && "field:"+f.DecodeSignature(names,null)==signature) return true; } }
                else foreach(var h in def.GetMethods()) {
                    var m=md.GetMethodDefinition(h);
                    if(md.GetString(m.Name)!=member) continue;
                    var s=m.DecodeSignature(names,null);
                    if(s.ReturnType+"("+string.Join(",",s.ParameterTypes)+")"==signature) return true;
                }
                var parent=def.BaseType;
                if(parent.IsNil) return false;
                string baseName=parent.Kind==HandleKind.TypeDefinition?TypeName(md,(TypeDefinitionHandle)parent)
                    :parent.Kind==HandleKind.TypeReference?TypeName(md,(TypeReferenceHandle)parent):null;
                if(baseName==null) return false;
                found=Cached(baseName);
            }
            return false;
        }
    }
    static readonly string[] Banned={"System.Runtime.Serialization","System.Text.Json","System.ServiceModel","Newtonsoft","Microsoft.CSharp","System.Dynamic"};
    static bool BannedName(string name) => Banned.Any(b=>name==b || name.StartsWith(b+".",StringComparison.Ordinal));
    public sealed class Report { public List<string> Failures=new List<string>(); public List<string> Assumed=new List<string>(); public int Verified; }
    public static Report Check(string plugin,string baseline,IEnumerable<string> assumed,IEnumerable<string> dllFolders) {
        var report=new Report();
        var library=new Library(dllFolders);
        var known=new HashSet<string>(library.Assemblies,StringComparer.OrdinalIgnoreCase){"mscorlib","System","System.Core","System.Xml","netstandard","BepInEx","0Harmony","SabberStoneCore"};
        var allowed=new HashSet<string>(assumed,StringComparer.Ordinal);
        var old=References(baseline);
        foreach(string row in References(plugin)) {
            string[] p=row.Split('|');
            string assembly=p[0], type=p[1], member=p[2], signature=p[3];
            // Serializers and the dynamic binder fail wherever they appear, baseline or not.
            if(BannedName(assembly) || BannedName(type) || type.StartsWith("System.Runtime.CompilerServices.CallSite",StringComparison.Ordinal) || type=="DataContractAttribute" || type=="DataMemberAttribute") { report.Failures.Add("unavailable on Unity's Mono: "+row); continue; }
            if(!known.Contains(assembly)) { report.Failures.Add("assembly not in the client: "+assembly+" ("+type+")"); continue; }
            if(old.Contains(row)) continue;
            if(library.Assemblies.Contains(assembly)) {
                if(library.Has(type,member,signature)) report.Verified++;
                else report.Failures.Add("missing from the client's "+assembly+".dll: "+type+"."+member+" "+signature);
            }
            else if(allowed.Contains(row)) report.Assumed.Add(row);
            else report.Failures.Add("new Mono API, neither checked nor listed in mono-assumed.txt: "+row);
        }
        return report;
    }
    // tool mono-guard PLUGIN BASELINE ASSUMED_FILE REFERENCES_DIR [MANAGED_DIR] [--write]
    public static int Run(string[] args) {
        bool write=args.Contains("--write");
        var rest=args.Where(a=>a!="--write").ToArray();
        if(rest.Length<4 || rest.Length>5) { Console.Error.WriteLine("Usage: tool mono-guard PLUGIN BASELINE ASSUMED_FILE REFERENCES_DIR [MANAGED_DIR] [--write]"); return 2; }
        var folders=new List<string>{Path.Combine(rest[3],"game"),Path.Combine(rest[3],"bepinex")};
        if(rest.Length==5) folders.Add(rest[4]);
        var assumed=File.Exists(rest[2])?File.ReadAllLines(rest[2]).Where(l=>l.Length!=0 && l[0]!='#'):new string[0];
        if(write) {
            var report0=Check(rest[0],rest[1],new string[0],folders);
            var lines=report0.Failures.Where(f=>f.StartsWith("new Mono API",StringComparison.Ordinal)).Select(f=>f.Substring(f.IndexOf(": ",StringComparison.Ordinal)+2));
            File.WriteAllLines(rest[2],new[]{"# APIs the Installer=true plugin adds to the default build, in Unity Mono's BCL. Generated by: tool mono-guard ... --write","# Assumed until `installer_live.py mono-check` verifies them against the client's Managed folder."}.Concat(lines));
            foreach(string f in report0.Failures.Where(f=>!f.StartsWith("new Mono API",StringComparison.Ordinal))) Console.Error.WriteLine(f);
            return report0.Failures.Any(f=>!f.StartsWith("new Mono API",StringComparison.Ordinal))?1:0;
        }
        var report=Check(rest[0],rest[1],assumed,folders);
        if(report.Failures.Count!=0) { foreach(string f in report.Failures) Console.Error.WriteLine(f); Console.Error.WriteLine("FAIL Mono guard: "+report.Failures.Count+" problems"); return 1; }
        Console.WriteLine("PASS Mono guard: "+report.Verified+" new references checked in client DLLs, "+report.Assumed.Count+" assumed (see mono-assumed.txt)");
        return 0;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace SoloStone
{
    // Reads metadata only: no client code loads or runs. .product.db is never used (real path, region, wrong version).
    public static class ClientVersion
    {
        public const string AssemblyPath="Hearthstone_Data/Managed/Assembly-CSharp.dll";
        static readonly Regex Two=new Regex(@"^(0|[1-9]\d{0,3})\.(0|[1-9]\d{0,3})$",RegexOptions.CultureInvariant);
        static readonly Regex Three=new Regex(@"^(0|[1-9]\d{0,3})\.(0|[1-9]\d{0,3})\.(0|[1-9]\d{0,5})$",RegexOptions.CultureInvariant);
        public static bool IsVersion(string value) => value!=null && Three.IsMatch(value);
        public static int Major(string version) => int.Parse(version.Substring(0,version.IndexOf('.')),System.Globalization.CultureInfo.InvariantCulture);
        // Null means Unknown, and Unknown blocks.
        public static string Read(string clientFolder) {
            try {
                string dll=Safety.Child(clientFolder,AssemblyPath);
                if(!File.Exists(dll) || new FileInfo(dll).Length>1024L*1024*1024) return null;
                using(var stream=new FileStream(dll,FileMode.Open,FileAccess.Read,FileShare.Read))
                using(var pe=new PEReader(stream)) return Read(pe);
            }
            catch(BadImageFormatException) { return null; } catch(InvalidOperationException) { return null; } catch(IOException) { return null; }
            catch(UnauthorizedAccessException) { return null; } catch(ArgumentException) { return null; }
        }
        static string Read(PEReader pe) {
            var md=pe.GetMetadataReader();
            TypeDefinition? network=null;
            foreach(var handle in md.TypeDefinitions) {
                var type=md.GetTypeDefinition(handle);
                if(md.GetString(type.Name)!="Network" || type.GetDeclaringType().IsNil==false || md.GetString(type.Namespace).Length!=0) continue;
                if(network!=null) return null;
                network=type;
            }
            if(network==null) return null;
            string cosmetic=Literal(md,network.Value,"CosmeticVersion");
            if(cosmetic==null || !Two.IsMatch(cosmetic)) return null;
            string prefix=cosmetic+".";
            var inMethod=BranchNameStrings(pe,md,network.Value).Where(IsVersion).Distinct().ToArray();
            if(inMethod.Length>1) return null;
            if(inMethod.Length==1) return inMethod[0].StartsWith(prefix,StringComparison.Ordinal)?inMethod[0]:null;
            // get_BranchName returned it on 37.0.0. If a build stops doing so, accept only a unique agreeing literal.
            var anywhere=new List<string>();
            for(var us=MetadataTokens.UserStringHandle(1);!us.IsNil;us=md.GetNextHandle(us)) {
                string text=md.GetUserString(us);
                if(text.Length<=16 && IsVersion(text) && text.StartsWith(prefix,StringComparison.Ordinal) && !anywhere.Contains(text)) anywhere.Add(text);
            }
            return anywhere.Count==1?anywhere[0]:null;
        }
        public static string ReadProgramVersion(string clientFolder) {
            try {
                string dll=Safety.Child(clientFolder,AssemblyPath);
                if(!File.Exists(dll) || new FileInfo(dll).Length>1024L*1024*1024) return null;
                using(var stream=new FileStream(dll,FileMode.Open,FileAccess.Read,FileShare.Read))
                using(var pe=new PEReader(stream)) {
                    var md=pe.GetMetadataReader();
                    var types=md.TypeDefinitions.Select(h=>md.GetTypeDefinition(h)).Where(t=>md.GetString(t.Name)=="TelemetryManager" && t.GetDeclaringType().IsNil && md.GetString(t.Namespace).Length==0).ToArray();
                    if(types.Length!=1) return null;
                    var values=BranchNameStrings(pe,md,types[0],"get_ProgramVersion").Where(v=>Regex.IsMatch(v,@"^(0|[1-9]\d{0,3})\.(0|[1-9]\d{0,3})\.(0|[1-9]\d{0,5})\.(0|[1-9]\d{0,9})$",RegexOptions.CultureInvariant)).Distinct().ToArray();
                    return values.Length==1 ? values[0] : null;
                }
            } catch(BadImageFormatException) { return null; } catch(InvalidOperationException) { return null; } catch(IOException) { return null; }
            catch(UnauthorizedAccessException) { return null; } catch(ArgumentException) { return null; }
        }
        static string Literal(MetadataReader md,TypeDefinition type,string name) {
            foreach(var handle in type.GetFields()) {
                var field=md.GetFieldDefinition(handle);
                if(md.GetString(field.Name)!=name || (field.Attributes&FieldAttributes.Literal)==0) continue;
                var value=field.GetDefaultValue();
                if(value.IsNil) return null;
                var constant=md.GetConstant(value);
                if(constant.TypeCode!=ConstantTypeCode.String) return null;
                var blob=md.GetBlobReader(constant.Value);
                return blob.ReadUTF16(blob.Length);
            }
            return null;
        }
        static IEnumerable<string> BranchNameStrings(PEReader pe,MetadataReader md,TypeDefinition type, string methodName="get_BranchName") {
            foreach(var handle in type.GetMethods()) {
                var method=md.GetMethodDefinition(handle);
                if(md.GetString(method.Name)!=methodName || method.RelativeVirtualAddress==0) continue;
                var il=pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
                if(il==null || il.Length>256) yield break;
                // ldstr carries a user-string token (0x70xxxxxx). A stray match in this tiny getter still has to read as a version.
                for(int i=0;i+5<=il.Length;i++) {
                    if(il[i]!=0x72 || il[i+4]!=0x70) continue;
                    string text=null;
                    try { text=md.GetUserString(MetadataTokens.UserStringHandle(BitConverter.ToInt32(il,i+1)&0xFFFFFF)); }
                    catch(BadImageFormatException) { }
                    if(text!=null) yield return text;
                }
            }
        }
    }
    public sealed class ClientFacts
    {
        public static readonly string[] PinnedFiles={"Hearthstone.exe","UnityPlayer.dll",ClientVersion.AssemblyPath,"Hearthstone_Data/Managed/blizzard.bgsclient.dll"};
        public string Folder, Version, Build;
        public Dictionary<string,string> Files=new Dictionary<string,string>();
        public List<string> Missing=new List<string>();
        public static ClientFacts Read(string folder) {
            var facts=new ClientFacts { Folder=folder, Version=ClientVersion.Read(folder), Build=ClientVersion.ReadProgramVersion(folder) };
            foreach(string relative in PinnedFiles) {
                string file=Safety.Child(folder,relative);
                if(File.Exists(file)) facts.Files[relative]=Hashes.FileHash(file); else facts.Missing.Add(relative);
            }
            return facts;
        }
        // Stable identity of this client's pinned files, for "accepted once" and bug reports.
        public string HashSetId() {
            using(var sha=System.Security.Cryptography.SHA256.Create())
                return FsOps.Hex(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",Files.OrderBy(r=>r.Key,StringComparer.Ordinal).Select(r=>r.Key+"="+r.Value.ToUpperInvariant())))));
        }
    }
    [DataContract] public sealed class TestedClient
    {
        [DataMember(Name="schema")] public int Schema;
        [DataMember(Name="version")] public string Version;
        [DataMember(Name="build")] public string Build;
        [DataMember(Name="files")] public Dictionary<string,string> Files;
        // Embedded so a release's own tested client cannot be edited beside the exe.
        public static TestedClient Compiled() {
            using(var stream=typeof(TestedClient).Assembly.GetManifestResourceStream("tested-client.json")) {
                if(stream==null) throw new InvalidOperationException("This SoloStone build is missing its tested-client list. "+InstallRecord.StartOver);
                var memory=new MemoryStream(); stream.CopyTo(memory);
                var spec=JsonBytes.Parse<TestedClient>(memory.ToArray());
                if(spec.Schema!=1 || !ClientVersion.IsVersion(spec.Version) || !ClientPolicy.ValidFiles(spec.Files)) throw new InvalidOperationException("This SoloStone build has an invalid tested-client list. "+InstallRecord.StartOver);
                return spec;
            }
        }
    }
    [DataContract] public sealed class KnownBadClient
    {
        [DataMember(Name="files")] public Dictionary<string,string> Files;
    }
    public enum BindingState { Passed, Failed, NotAvailable }
    public sealed class BindingResult
    {
        public BindingState State; public string Reason;
        public static readonly BindingResult Passed=new BindingResult { State=BindingState.Passed };
        public static readonly BindingResult NotAvailable=new BindingResult { State=BindingState.NotAvailable };
        public static BindingResult Failed(string reason) => new BindingResult { State=BindingState.Failed, Reason=reason };
    }
    public interface IBindingCheck { BindingResult Check(string clientFolder); }
    // The only implementation until task V1 ships client-bindings.json.
    public sealed class NoBindingManifest : IBindingCheck { public BindingResult Check(string clientFolder) => BindingResult.NotAvailable; }
    public enum VerdictKind { Allow, Warning, Block }
    [DataContract] public sealed class Verdict
    {
        [DataMember(Name="verdict")] public string KindName;
        [DataMember(Name="testedVersion")] public string TestedVersion;
        [DataMember(Name="foundVersion")] public string FoundVersion;
        [DataMember(Name="testedBuild")] public string TestedBuild;
        [DataMember(Name="foundBuild")] public string FoundBuild;
        [DataMember(Name="retryRunning")] public bool RetryRunning;
        [DataMember(Name="reasons")] public string[] Reasons;
        [DataMember(Name="provisional")] public bool Provisional;
        // Exactly a tested client: version and hashes. Anything else gets one backup before play, warned or not.
        [DataMember(Name="exact")] public bool Exact;
        [DataMember(Name="message")] public string Message;
        public VerdictKind Kind => KindName=="Allow"?VerdictKind.Allow:KindName=="Warning"?VerdictKind.Warning:VerdictKind.Block;
        // A staged launcher's output is untrusted until read back here. Anything else counts as a Block.
        public static Verdict FromJson(string json) => TryParse(json)??ClientPolicy.Blocked("The new version's client check returned no readable result.");
        // Null when the output is not a verdict at all, which says nothing about the client.
        public static Verdict TryParse(string json) {
            try {
                var verdict=JsonBytes.Parse<Verdict>(System.Text.Encoding.UTF8.GetBytes(json??""));
                if(verdict!=null && (verdict.KindName=="Allow" || verdict.KindName=="Warning" || verdict.KindName=="Block")) return verdict;
            } catch(InvalidOperationException) { }
            return null;
        }
        public string ToJson() => System.Text.Encoding.UTF8.GetString(JsonBytes.ToBytes(this));
    }
    public static class ClientPolicy
    {
        const string LaterBlock="The game may still reject this client.";
        public static bool ValidFiles(Dictionary<string,string> files) => files!=null && files.Count>0 && files.Count<=64
            && files.All(r=>ValidRelative(r.Key) && r.Value!=null && r.Value.Length==64 && r.Value.All(Uri.IsHexDigit));
        static bool ValidRelative(string path) => !string.IsNullOrEmpty(path) && path.Length<=260 && !path.Contains(":") && !path.Contains("\\") && !path.StartsWith("/")
            && path.Split('/').All(p=>p.Length!=0 && p!="." && p!="..");
        static bool SameHashes(Dictionary<string,string> found,Dictionary<string,string> wanted) => wanted!=null && found.Count==wanted.Count
            && wanted.All(r=>found.TryGetValue(r.Key,out string hash) && Safety.Same(hash,r.Value));
        public static Verdict Blocked(string reason,string tested=null) => Build(VerdictKind.Block,tested,null,new[]{reason},false);
        public static Verdict BlockedWith(string reason,string tested,string found) => Build(VerdictKind.Block,tested,found,new[]{reason},false);
        // The version rule, owner 2026-11-06: same major and minor as a tested client runs silently, whatever the patch, build or hashes. Another minor warns. Another major blocks.
        public static bool SameMinor(string a,string b) => a.Substring(0,a.LastIndexOf('.'))==b.Substring(0,b.LastIndexOf('.'));
        // Pure. Signed data can move a client from Warning to Allow or block an exact hash set. It can never lift a Block.
        public static Verdict Decide(ClientFacts facts,TestedClient compiled,IEnumerable<TestedClient> signedTested,IEnumerable<KnownBadClient> signedKnownBad,BindingResult binding,IEnumerable<string> safetyFailures)
        {
            var tested=(signedTested??new TestedClient[0]).Where(t=>t!=null && ClientVersion.IsVersion(t.Version)).ToArray();
            string testedVersion=NewestSameMajor(compiled,tested);
            var safety=(safetyFailures??new string[0]).Where(s=>!string.IsNullOrEmpty(s)).ToArray();
            string found=ClientVersion.IsVersion(facts.Version)?facts.Version:null;
            Func<string[],Verdict> block=reasons=>WithBuild(Build(VerdictKind.Block,testedVersion,found,reasons,false),compiled,tested,facts);
            if(safety.Length!=0) {
                var blocked=block(safety);
                blocked.RetryRunning=safety.All(IsRunningFailure);

                return blocked;
            }
            if(found==null) return block(new[]{"SoloStone could not read it from the client files."});
            if(ClientVersion.Major(facts.Version)!=ClientVersion.Major(compiled.Version))
                return block(new[]{"This SoloStone release runs on Hearthstone "+ClientVersion.Major(compiled.Version)+" only, and your client is Hearthstone "+ClientVersion.Major(facts.Version)+"."});
            if(facts.Missing.Count!=0) return block(new[]{"Your client folder is incomplete: "+facts.Missing[0]+" is missing."});
            bool isCompiled=facts.Version==compiled.Version && SameHashes(facts.Files,compiled.Files);
            // Plan 2a: an exact hash set only, and never the release's own compiled tested client.
            if(!isCompiled && (signedKnownBad??new KnownBadClient[0]).Any(bad=>bad!=null && SameHashes(facts.Files,bad.Files)))
                return block(new[]{"This exact client is on SoloStone's known-bad list."});
            if(binding.State==BindingState.Failed) return block(new[]{"This client's code no longer matches SoloStone: "+binding.Reason});
            bool provisional=binding.State==BindingState.NotAvailable;
            bool exact=isCompiled || tested.Any(t=>t.Version==facts.Version && SameHashes(facts.Files,t.Files));
            if(exact || SameMinor(facts.Version,compiled.Version) || tested.Any(t=>SameMinor(facts.Version,t.Version))) {
                var allow=WithBuild(Build(VerdictKind.Allow,testedVersion,facts.Version,new string[0],provisional),compiled,tested,facts);
                allow.Exact=exact;
                return allow;
            }
            return WithBuild(Build(VerdictKind.Warning,testedVersion,facts.Version,new[]{"SoloStone has not been tested with this Hearthstone version. Matches can break, and card text can differ from the rules."},provisional),compiled,tested,facts);
        }
        public const string BattleRunning="Battle.net is running. Quit Battle.net before using SoloStone.";
        // Agent.exe has no window to quit, and it exits by itself soon after Battle.net; the windows retry every 2 seconds.
        public const string AgentRunning="Battle.net is still closing. This usually takes a minute, then SoloStone continues by itself.";
        public const string GameRunning="Hearthstone is running. Quit Hearthstone before using SoloStone.";
        public const string CopyRunning="SoloStone's game is running. Quit it before using SoloStone.";
        public static string RunningGameFailure(string path,string copy) => path!=null && copy!=null && Safety.Same(path,copy) ? CopyRunning : GameRunning;
        public static bool IsRunningFailure(string reason) => reason==BattleRunning || reason==AgentRunning || reason==GameRunning || reason==CopyRunning;
        static string TestedBuild(TestedClient compiled,TestedClient[] tested,string version) => version==compiled.Version ? compiled.Build : tested.FirstOrDefault(t=>t.Version==version)?.Build;
        static Verdict WithBuild(Verdict v,TestedClient compiled,TestedClient[] tested,ClientFacts facts) { v.TestedBuild=TestedBuild(compiled,tested,v.TestedVersion); v.FoundBuild=facts.Build ?? (facts.Version==compiled.Version && SameHashes(facts.Files,compiled.Files) ? compiled.Build : tested.FirstOrDefault(t=>t.Version==facts.Version && SameHashes(facts.Files,t.Files))?.Build); v.Message=v.Message.Replace(Sentence(v.TestedVersion,v.FoundVersion), Sentence(DisplayVersion(v.TestedVersion,v.TestedBuild),DisplayVersion(v.FoundVersion,v.FoundBuild))); return v; }
        static string DisplayVersion(string version,string build) => string.IsNullOrEmpty(version) || string.IsNullOrEmpty(build) ? version : (build.IndexOf('.')>=0 ? build : version+"."+build);
        static string NewestSameMajor(TestedClient compiled,TestedClient[] signed) {
            string newest=compiled.Version; int major=ClientVersion.Major(compiled.Version);
            foreach(var entry in signed)
                if(ClientVersion.Major(entry.Version)==major && SemVer.Compare(entry.Version,newest)>0) newest=entry.Version;
            return newest;
        }
        static Verdict Build(VerdictKind kind,string tested,string found,string[] reasons,bool provisional) {
            var parts=new List<string>();
            if(kind==VerdictKind.Block) parts.AddRange(reasons);
            parts.Add(Sentence(tested,found));
            if(kind!=VerdictKind.Block) parts.AddRange(reasons);
            if(provisional && kind==VerdictKind.Warning) parts.Add(LaterBlock);
            return new Verdict { KindName=kind.ToString(), TestedVersion=tested, FoundVersion=found, Reasons=reasons, Provisional=provisional, Message=string.Join(" ",parts) };
        }
        public static string Sentence(string tested,string found) {
            string first=tested==null?"":"Latest client tested with this SoloStone: "+tested+". ";
            return first+(found==null?"Your client's version is unknown.":"Your client: "+found+".");
        }
    }
    public static class ClientGate
    {
        // Single entry point for launcher, wizard and staged check. The signed list comes from a verified manifest or is null.
        public static Verdict Evaluate(ClientFacts facts,TestedClient compiled,ReleaseManifest signed,IBindingCheck binding,IEnumerable<string> safetyFailures)
            => ClientPolicy.Decide(facts,compiled,signed?.TestedClients,signed?.KnownBad,binding.Check(facts.Folder),safetyFailures);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace SoloStone
{
    // Shared with the build step: nothing from Blizzard's client can ship, whatever a build folder holds.
    public static class PayloadRules
    {
        static readonly string[] Names={"hearthstone.exe","hearthstone beta launcher.exe","unityplayer.dll","unitycrashhandler64.exe","unitycrashhandler32.exe","mono-2.0-bdwgc.dll","baselib.dll","gameassembly.dll",".product.db",".build.info"};
        static readonly string[] Prefixes={"blizzard.","unityengine.","unityengine-","assembly-csharp","pegasus","hearthstone.","hearthstone_"};
        static readonly string[] Folders={"hearthstone_data","monobleedingedge"};
        static readonly string[] Extensions={".unity3d",".assets",".ress",".resource",".assetbundle"};
        public static bool IsClientFile(string relative) {
            string[] parts=relative.ToLowerInvariant().Split('/','\\');
            string name=parts[parts.Length-1];
            return Names.Contains(name) || Prefixes.Any(p=>name.StartsWith(p,StringComparison.Ordinal))
                || parts.Any(Folders.Contains) || Extensions.Any(e=>name.EndsWith(e,StringComparison.Ordinal));
        }
        // Files bound for the client folder. recordedClientFiles comes from install.json when one exists. The two files setup patches are always protected, recorded or not.
        public static string Problem(IEnumerable<string> payloadFiles,IEnumerable<string> recordedClientFiles) {
            var recorded=new HashSet<string>((recordedClientFiles??new string[0]).Concat(ClientIdentity.Paths),StringComparer.OrdinalIgnoreCase);
            foreach(string file in payloadFiles) {
                if(file.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)) return "The mod payload contains a program ("+file+"), which SoloStone never ships.";
                if(recorded.Contains(file) || IsClientFile(file)) return "The mod payload contains a Hearthstone client file ("+file+"), which SoloStone never ships.";
            }
            return null;
        }
        // Files bound for the app folder: our launcher and its libraries only.
        public static string AppProblem(IEnumerable<string> appFiles) {
            foreach(string file in appFiles) {
                string name=Path.GetFileName(file);
                if(IsClientFile(file)) return "The update contains a Hearthstone client file ("+file+"), which SoloStone never ships.";
                if(name.StartsWith("unins",StringComparison.OrdinalIgnoreCase) || Safety.Same(name,".solostone-app") || Safety.Same(name,InstallRecord.Marker)) return "The update contains a file SoloStone manages itself ("+file+").";
                if(name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) && !Safety.Same(file,"SoloStone.exe")) return "The update contains an unexpected program ("+file+").";
            }
            return null;
        }
    }
    // What a release binary must not carry. The words are assembled at run time so this file can ship without tripping its own scan.
    [DataContract] public sealed class BuildResult
    {
        [DataContract] public sealed class Artifact
        {
            [DataMember(Name="name")] public string Name;
            [DataMember(Name="sha256")] public string Sha256;
        }
        [DataMember(Name="hookBuild")] public bool? HookBuild;
        [DataMember(Name="version")] public string Version;
        [DataMember(Name="zip")] public Artifact Zip;
        [DataMember(Name="setup")] public Artifact Setup;
    }
    public static class ReleaseScan
    {
        static string W(params string[] parts) => string.Concat(parts);
        static string[] Lab() => new[]{W("BattleNet","\\","Hearthstone"),W("HSOffline","Probe")};
        public static string[] LauncherTokens() => Lab().Concat(new[]{W("SOLOSTONE_","TEST_CONTROL"),W("SOLOSTONE_","TEST_DISCOVERY"),W("SOLOSTONE_","TEST_UPDATE_BASE"),W("elevate-","request.txt"),W("Test","Control"),W("Test","Elevation"),W("Test","Discovery"),W("BF","FM")}).ToArray();
        public static string[] PluginTokens() => Lab().Concat(new[]{W("client-check","-plant"),W("restore-","request"),W("enable-test","-control"),W("Tcp","Listener"),W("HS test ","control"),W("enable-","playtest"),W("benchmark-","ranks"),W("playtest-","opponent"),W("guide-cards-","fail.txt"),W("test-loaner-","group"),W("screenshot-","request"),W("hs-native-ui-","prototype"),W("HSNative","UIProbe"),W("Hearthstone","Offline"),W("local.hs.","native.ui.probe"),W("local.hs.","offline.probe")}).ToArray();
        public static string[] Found(byte[] binary,IEnumerable<string> tokens) => tokens.Where(token=>Has(binary,System.Text.Encoding.ASCII.GetBytes(token)) || Has(binary,System.Text.Encoding.Unicode.GetBytes(token))).ToArray();
        static bool Has(byte[] data,byte[] token) {
            for(int i=0;i+token.Length<=data.Length;i++) {
                if(data[i]!=token[0]) continue;
                int j=1; while(j<token.Length && data[i+j]==token[j]) j++;
                if(j==token.Length) return true;
            }
            return false;
        }
        // The whole release check for a built app folder: run by build.ps1 through the launcher itself, on every path including -Staged.
        public static string PayloadProblem(string app,string clientReference,bool hookBuild) {
            if(string.IsNullOrWhiteSpace(clientReference)) return "The payload check needs --client-reference, a Hearthstone client folder to compare against.";
            try { clientReference=Safety.Canonical(clientReference); } // In a hook build this is also the lab guard.
            catch(InvalidOperationException e) { return e.Message; }
            if(!Directory.Exists(clientReference)) return "The payload check needs --client-reference, a Hearthstone client folder to compare against.";
            string[] inClient;
            try { inClient=ClientFilesInPayload(Path.Combine(app,"payload"),clientReference); }
            catch(InvalidOperationException e) { return e.Message; }
            if(inClient.Length!=0) return "The payload holds files identical to the client's, which SoloStone never ships: "+string.Join(", ",inClient.Take(5))+".";
            var launcher=Found(File.ReadAllBytes(Path.Combine(app,"SoloStone.exe")),LauncherTokens());
            if(hookBuild) return launcher.Length>=3?null:"A hook build should carry the test hooks, but its launcher has "+launcher.Length+" of them.";
            if(launcher.Length!=0) return "A release launcher carries test hooks or lab paths: "+string.Join(", ",launcher)+".";
            var plugin=Found(File.ReadAllBytes(Path.Combine(app,"payload","BepInEx","plugins",PublicNames.PluginFile)),PluginTokens());
            if(plugin.Length!=0) return "A release plugin carries test control or lab paths: "+string.Join(", ",plugin)+".";
            return null;
        }
        // The licence texts and the source pointer travel with every download: the AGPL wants them with the binary.
        // build.ps1 puts them in the app folder, so they are in the update zip too. Paths are zip entries.
        public static readonly string[] LicenceFiles={"NOTICE","LICENSE","LICENSE-ADDITIONAL-PERMISSION.md","SOURCE.txt",
            "LICENSES/AGPL-3.0-or-later.txt","LICENSES/CC-BY-4.0.txt","LICENSES/LGPL-2.1-only.txt","LICENSES/MIT.txt"};
        public const string HookLabel="TEST-HOOKS";
        public static string ArtifactBase(bool hookBuild) => hookBuild?"SoloStone-"+HookLabel:"SoloStone";
        // Mandatory before publishing, and the last step of build.ps1's release path: the folder of finished files, as they would be uploaded.
        // Inno compresses the setup exe, so its contents can't be scanned: the same build's app folder and update zip are, and the file names say which build it came from.
        public static string ReleaseProblem(string folder,string version,bool expectSetup) {
            if(!Directory.Exists(folder)) return "No build folder at "+folder+".";
            string zipName="SoloStone-"+version+".zip", setupName="SoloStone-Setup-"+version+".exe";
            foreach(string file in Directory.GetFiles(folder).Select(Path.GetFileName)) {
                if(file.IndexOf(HookLabel,StringComparison.OrdinalIgnoreCase)>=0) return "This folder holds a live-test build ("+file+"). It can never be published.";
                bool artifact=file.EndsWith(".zip",StringComparison.OrdinalIgnoreCase) || file.EndsWith(".exe",StringComparison.OrdinalIgnoreCase);
                if(artifact && !Safety.Same(file,zipName) && !(expectSetup && Safety.Same(file,setupName))) return "Unexpected file name for version "+version+": "+file+".";
            }
            string zip=Path.Combine(folder,zipName), setup=Path.Combine(folder,setupName);
            if(!File.Exists(zip)) return "The update zip "+zipName+" is missing.";
            if(expectSetup && !File.Exists(setup)) return "The setup "+setupName+" is missing.";
            try {
                // The build's own record. A file renamed into place has another hash, and a hook build's record says what it is.
                string recorded=Path.Combine(folder,"build-result.json");
                if(!File.Exists(recorded)) return "build-result.json is missing, so these files can't be tied to a build.";
                var result=JsonBytes.Parse<BuildResult>(File.ReadAllBytes(recorded));
                if(result.HookBuild!=false) return "build-result.json marks this as a live-test build, or does not say. It can never be published.";
                if(result.Version!=version) return "build-result.json is for version "+result.Version+", not "+version+".";
                Func<BuildResult.Artifact,string,string,string> bound=(row,name,path)=>row==null || row.Name!=name?"build-result.json does not list "+name+".":Safety.Same(row.Sha256,Hashes.FileHash(path))?null:name+" is not the file this build made.";
                string unbound=bound(result.Zip,zipName,zip)??(expectSetup?bound(result.Setup,setupName,setup):null);
                if(unbound!=null) return unbound;
                using(var archive=ZipFile.OpenRead(zip)) {
                    Func<string,byte[]> read=name=>{
                        var entry=archive.Entries.FirstOrDefault(row=>row.FullName==name);
                        if(entry==null) return null;
                        using(var stream=entry.Open()) using(var bytes=new MemoryStream()) { stream.CopyTo(bytes); return bytes.ToArray(); }
                    };
                    byte[] notice=read("NOTICE.txt"), launcher=read("SoloStone.exe"), plugin=read("payload/"+PublicNames.PluginPayloadPath), release=read("release.json");
                    if(notice==null || notice.Length==0) return "The update zip has no NOTICE.txt.";
                    foreach(string licence in LicenceFiles) {
                        byte[] text=read(licence);
                        if(text==null || text.Length==0) return "The update zip has no "+licence+".";
                    }
                    if(launcher==null) return "The update zip has no SoloStone.exe.";
                    if(plugin==null) return "The update zip has no plugin.";
                    if(release==null) return "The update zip has no release.json.";
                    var info=JsonBytes.Parse<ReleaseInfo>(release);
                    if(info.Version!=version) return "The update zip's release.json says "+info.Version+", not "+version+".";
                    var inLauncher=Found(launcher,LauncherTokens());
                    if(inLauncher.Length!=0) return "A release launcher carries test hooks or lab paths: "+string.Join(", ",inLauncher)+".";
                    var inPlugin=Found(plugin,PluginTokens());
                    if(inPlugin.Length!=0) return "A release plugin carries test control or lab paths: "+string.Join(", ",inPlugin)+".";
                    string problem=PayloadRules.AppProblem(archive.Entries.Select(row=>row.FullName).Where(name=>!name.StartsWith("payload/",StringComparison.Ordinal)));
                    if(problem!=null) return problem;
                    string app=Path.Combine(folder,"app");
                    if(Directory.Exists(app)) {
                        string own=Path.Combine(app,"SoloStone.exe");
                        if(!File.Exists(own) || !File.ReadAllBytes(own).SequenceEqual(launcher)) return "The app folder and the update zip hold different launchers, so the setup can't be vouched for.";
                    }
                }
            }
            catch(Exception e) when(e is IOException || e is InvalidDataException || e is InvalidOperationException) { return "The update zip can't be read: "+e.Message; }
            return null;
        }
        // A payload file with the same bytes as a file of this client copy would be Blizzard's to ship. Sizes first, because a client is 12 GB.
        // This catches only that client's bytes: a file from a different client build can't be caught by hash, so provenance stays a release decision.
        public static string[] ClientFilesInPayload(string payloadFolder,string clientReference) {
            if(!File.Exists(Path.Combine(clientReference,"Hearthstone.exe")) || !File.Exists(Path.Combine(clientReference,"Hearthstone_Data","Managed","Assembly-CSharp.dll")))
                throw new InvalidOperationException("The client reference folder is not a Hearthstone client: "+clientReference);
            var bySize=Installer.SourceFiles(clientReference).ToLookup(path=>new FileInfo(path).Length);
            var found=new List<string>();
            string prefix=payloadFolder.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            foreach(string file in Safety.Tree(payloadFolder.TrimEnd(Path.DirectorySeparatorChar))) {
                string hash=null;
                foreach(string candidate in bySize[new FileInfo(file).Length]) {
                    hash=hash??Hashes.FileHash(file);
                    if(Safety.Same(hash,Hashes.FileHash(candidate))) { found.Add(file.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar,'/')); break; }
                }
            }
            return found.ToArray();
        }
    }
    [DataContract] public sealed class ReleaseInfo
    {
        [DataMember(Name="schema")] public int Schema;
        [DataMember(Name="version")] public string Version;
        [DataMember(Name="saveSchema")] public int SaveSchema;
        [DataMember(Name="sourceRevision")] public string SourceRevision;
        public static ReleaseInfo TryRead(string appFolder) {
            var info=Atomic.TryRead<ReleaseInfo>(Path.Combine(appFolder,"release.json"));
            return info!=null && info.Schema==1 && SemVer.Valid(info.Version) && info.SaveSchema>0 && !string.IsNullOrEmpty(info.SourceRevision)?info:null;
        }
    }
    public sealed class SaveScan
    {
        public int MaxVersion, Count; public string Pending, Unreadable;
        public string Problem => Pending??Unreadable;
    }
    public static class Saves
    {
        const int MaxProfile=32*1024*1024;
        // Reads only. DTDs are prohibited because a save is local data, not ours to trust.
        public static SaveScan Scan(string saveRoot) {
            var scan=new SaveScan();
            try {
                Safety.NoLinks(saveRoot);
                var profiles=new List<string>();
                string rootProfile=Path.Combine(saveRoot,"profile.xml"), slots=Path.Combine(saveRoot,"profiles");
                if(File.Exists(rootProfile)) profiles.Add(rootProfile);
                if(Directory.Exists(slots)) foreach(string slot in Directory.GetDirectories(slots).OrderBy(p=>p,StringComparer.Ordinal)) {
                    if(Path.GetFileName(slot).StartsWith(".",StringComparison.Ordinal)) continue; // .deleted and .creating-* are not live saves.
                    string profile=Path.Combine(slot,"profile.xml");
                    if(File.Exists(profile)) profiles.Add(profile);
                }
                foreach(string profile in profiles) Read(profile,scan);
            }
            catch(IOException) { scan.Unreadable=scan.Unreadable??"A save could not be read. Close anything using it and try again."; }
            catch(UnauthorizedAccessException) { scan.Unreadable=scan.Unreadable??"Windows denied access to a save."; }
            catch(InvalidOperationException e) { scan.Unreadable=scan.Unreadable??e.Message; }
            return scan;
        }
        static bool HasContent(XmlNode field) => field!=null && (field.SelectNodes("*").Count!=0 || !string.IsNullOrWhiteSpace(field.InnerText));
        static void Read(string profile,SaveScan scan) {
            string name=Path.GetFileName(Path.GetDirectoryName(profile));
            Func<string,string> bad=why=>scan.Unreadable=scan.Unreadable??("The save in "+name+" cannot be read ("+why+"). Nothing was changed.");
            Safety.NoLinks(profile);
            if(new FileInfo(profile).Length>MaxProfile) { bad("too large"); return; }
            if(File.Exists(profile+".lock")) {
                Safety.NoLinks(profile+".lock");
                try { using(new FileStream(profile+".lock",FileMode.Open,FileAccess.Read,FileShare.None)) { } }
                catch(IOException) { scan.Pending=scan.Pending??"A running game is using a save. Quit it first."; return; }
            }
            var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=MaxProfile };
            var doc=new XmlDocument { XmlResolver=null };
            try { using(var reader=XmlReader.Create(profile,settings)) doc.Load(reader); }
            catch(XmlException) { bad("damaged"); return; }
            var node=doc.DocumentElement;
            if(node==null || node.Name!="LocalProfileData") { bad("unknown format"); return; }
            foreach(string field in new[]{"Version","Pending","PackRevealPending","PendingRewardPresentationId"})
                if(node.SelectNodes(field).Count>1) { bad("duplicate "+field); return; }
            int version;
            if(!int.TryParse(node.SelectSingleNode("Version")?.InnerText,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out version) || version<=0) { bad("unknown version"); return; }
            scan.Count++; scan.MaxVersion=Math.Max(scan.MaxVersion,version);
            string pack=node.SelectSingleNode("PackRevealPending")?.InnerText;
            if(pack!="false" && pack!="true") { bad("unknown pack state"); return; }
            if(HasContent(node.SelectSingleNode("Pending"))) scan.Pending=scan.Pending??"A match is still pending. Finish or concede it in the game first.";
            if(HasContent(node.SelectSingleNode("PendingRewardPresentationId"))) scan.Pending=scan.Pending??"Match rewards are still waiting. Open the game and collect them first.";
            if(pack=="true") scan.Pending=scan.Pending??"A pack reveal is still pending. Open the game and finish it first.";
        }
    }
    [DataContract] public sealed class BackupManifest
    {
        [DataMember(Name="schema")] public int Schema;
        [DataMember(Name="id")] public string Id;
        [DataMember(Name="from")] public string From;
        [DataMember(Name="utc")] public string Utc;
        [DataMember(Name="saveSchema")] public int SaveSchema;
        [DataMember(Name="files")] public Dictionary<string,string> Files;
    }
    // Backups live outside the save tree and never write inside it. Replaced trees stay until the player removes them.
    public sealed class Backups
    {
        public const int Keep=3;
        static readonly Regex IdShape=new Regex(@"^\d{8}T\d{9}Z-[0-9A-Za-z.-]{1,64}$",RegexOptions.CultureInvariant);
        readonly string folder;
        public Backups(string root) { folder=Path.Combine(root,"backups"); }
        public string Folder => folder;
        public string PathOf(string id) {
            if(id==null || !IdShape.IsMatch(id) || id.Contains("..")) throw new InvalidOperationException("That backup name is not valid.");
            return Path.Combine(folder,id);
        }
        static void CopyTree(string from,string target,string prefix,Dictionary<string,string> files) {
            if(!Directory.Exists(from)) return;
            foreach(string file in Safety.Tree(from)) {
                string relative=file.Substring(from.Length+1).Replace(Path.DirectorySeparatorChar,'/');
                string destination=Safety.Child(target,prefix+"/"+relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                files[prefix+"/"+relative]=FsOps.CopyHashed(file,destination,CancellationToken.None);
            }
        }
        public string Create(string saveRoot,string clientConfig,string fromVersion) {
            var scan=Saves.Scan(saveRoot);
            DateTime now=DateTime.UtcNow; string id;
            do { id=now.ToString("yyyyMMdd'T'HHmmssfff'Z'",System.Globalization.CultureInfo.InvariantCulture)+"-"+(fromVersion??"unknown"); now=now.AddMilliseconds(1); }
            while(Directory.Exists(PathOf(id)));
            string partial=Path.Combine(folder,".partial-"+id), final=PathOf(id);
            FsOps.DeleteTree(partial); Directory.CreateDirectory(partial);
            var manifest=new BackupManifest { Schema=1, Id=id, From=fromVersion, Utc=now.ToString("o"), SaveSchema=scan.MaxVersion, Files=new Dictionary<string,string>() };
            CopyTree(saveRoot,partial,"saves",manifest.Files);
            CopyTree(clientConfig,partial,"config",manifest.Files);
            if(manifest.Files.Count!=0) Hashes.Verify(partial,manifest.Files);
            Atomic.Save(Path.Combine(partial,"backup.json"),manifest);
            Directory.Move(partial,final);
            Prune();
            return id;
        }
        public BackupManifest[] List() {
            if(!Directory.Exists(folder)) return new BackupManifest[0];
            return Directory.GetDirectories(folder).Select(Path.GetFileName).Where(n=>IdShape.IsMatch(n)).OrderByDescending(n=>n,StringComparer.Ordinal)
                .Select(n=>Atomic.TryRead<BackupManifest>(Path.Combine(folder,n,"backup.json"))).Where(m=>m!=null).ToArray();
        }
        void Prune() {
            foreach(string partial in Directory.GetDirectories(folder,".partial-*")) FsOps.DeleteTree(partial);
            foreach(var old in List().Skip(Keep)) FsOps.DeleteTree(PathOf(old.Id));
        }
        // Would restoring this backup lose nothing? Same relative paths, sizes and hashes for every save. A file that can't be read counts as changed.
        public bool SavesUnchanged(BackupManifest manifest,string saveRoot) {
            const string prefix="saves/";
            try {
                var backedUp=manifest.Files.Where(row=>row.Key.StartsWith(prefix,StringComparison.Ordinal)).ToDictionary(row=>row.Key.Substring(prefix.Length),row=>row.Value,StringComparer.Ordinal);
                if(!Directory.Exists(saveRoot)) return backedUp.Count==0;
                string[] files=Safety.Tree(saveRoot).ToArray();
                if(files.Length!=backedUp.Count) return false;
                string backup=PathOf(manifest.Id);
                foreach(string file in files) {
                    string relative=file.Substring(saveRoot.Length+1).Replace(Path.DirectorySeparatorChar,'/'), hash;
                    if(!backedUp.TryGetValue(relative,out hash) || new FileInfo(file).Length!=new FileInfo(Safety.Child(backup,prefix+relative)).Length || !Safety.Same(Hashes.FileHash(file),hash)) return false;
                }
                return true;
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException) { return false; }
        }
        public BackupManifest Verified(string id) {
            string path=PathOf(id);
            var manifest=Atomic.TryRead<BackupManifest>(Path.Combine(path,"backup.json"));
            if(manifest==null || manifest.Schema!=1 || manifest.Id!=id || manifest.Files==null) throw new InvalidOperationException("That backup is missing or damaged. Nothing was changed.");
            if(manifest.Files.Count!=0) Hashes.Verify(path,manifest.Files);
            return manifest;
        }
    }
    public interface IUpdateHost
    {
        string SaveRoot { get; }
        string IdleProblem(); // Game or Battle.net running. Null when clear.
        // Runs the STAGED launcher's check-client. A verdict or crashed launcher is a result. Throw when the check itself could not run: that is retried.
        Verdict CheckStaged(string stagedApp,string root);
        // True when this process's own image is the app folder's launcher. That file can only be replaced from another process.
        bool RunningFrom(string folder);
        // These start another launcher and return. The caller's process then exits, and the other one waits for that before it touches anything.
        void HandOffSwap(string stagedApp,string version);
        void HandOffUndo(string helperFolder);
    }
    [DataContract] public sealed class HeldUpdate
    {
        [DataMember(Name="version")] public string Version;
        [DataMember(Name="reason")] public string Reason;
        [DataMember(Name="utc")] public string Utc;
        // Set only after a first start that timed out or crashed: one more try is allowed from this time on. Without it the hold is permanent.
        [DataMember(Name="retryAfter")] public string RetryAfter;
        [DataMember(Name="retries")] public int Retries;
        public bool BlocksAt(DateTime utc) {
            DateTime after;
            return RetryAfter==null || !DateTime.TryParse(RetryAfter,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out after) || utc<after.ToUniversalTime();
        }
    }
    [DataContract] public sealed class SwapMove
    {
        [DataMember(Name="op")] public string Op; // move | copy | keep (a copy that stays until the journal ends: the previous launcher)
        [DataMember(Name="src")] public string Src;
        [DataMember(Name="dst")] public string Dst;
        [DataMember(Name="sha")] public string Sha;
    }
    [DataContract] public sealed class SwapJournal
    {
        public const string Planned="planned", Moved="moved", Recorded="recorded", Swapped="swapped", Undoing="undoing";
        [DataMember(Name="schema")] public int Schema;
        [DataMember(Name="id")] public string Id;
        [DataMember(Name="from")] public string From;
        [DataMember(Name="to")] public string To;
        [DataMember(Name="fromSaveSchema")] public int FromSaveSchema;
        [DataMember(Name="toSaveSchema")] public int ToSaveSchema;
        [DataMember(Name="phase")] public string Phase;
        [DataMember(Name="health")] public string Health;
        [DataMember(Name="holdReason")] public string HoldReason;
        [DataMember(Name="holdRetryAfter")] public string HoldRetryAfter;
        [DataMember(Name="retries")] public int Retries;
        // SoloStone.exe is not in Moves. It is replaced in one atomic step, last on the way in and last on the way out.
        [DataMember(Name="exeOld")] public string ExeOld;
        [DataMember(Name="exeNew")] public string ExeNew;
        [DataMember(Name="helper")] public string Helper;
        [DataMember(Name="helperSha")] public string HelperSha;
        [DataMember(Name="started")] public int Started=-1; // Highest move index begun. Moves run strictly in order, so this settles every overlap.
        [DataMember(Name="moves")] public List<SwapMove> Moves;
        [DataMember(Name="before")] public InstallRecord Before;
        [DataMember(Name="after")] public InstallRecord After;
    }
    public enum UpdateOutcome { Nothing, Swapped, Held, NeedsConfirmation, Refused, Failed }
    public sealed class UpdateResult
    {
        public UpdateOutcome Outcome; public string Version, Message; public Verdict Verdict;
    }
    public enum ResumeOutcome { Nothing, HealthPending, Undone, HandedOff }
    // Failed: restore and hold for good. RetryLater: restore, hold a day, and for good the second time. NoEvidence: nothing changes and the check stays pending.
    public enum HealthOutcome { Confirmed, Failed, RetryLater, NoEvidence }
    // Swaps that failed, per version: what keeps a failing update from eating backups and downloads on every start, without ever stopping updates for good.
    [DataContract] public sealed class SwapAttempts
    {
        public const int FailuresBeforeWait=2, WaitHours=24;
        [DataMember(Name="version")] public string Version;
        [DataMember(Name="from")] public string From;
        [DataMember(Name="failures")] public int Failures;
        [DataMember(Name="lastFailedUtc")] public string LastFailedUtc;
        [DataMember(Name="backupId")] public string BackupId;
        // Null when nothing makes it wait. An unreadable time or one in the future (the clock moved) must not stop updates.
        public DateTime? WaitsUntil(DateTime utc) {
            DateTime failed;
            if(Failures<FailuresBeforeWait || !DateTime.TryParse(LastFailedUtc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out failed)) return null;
            failed=failed.ToUniversalTime();
            return failed<=utc && utc<failed.AddHours(WaitHours)?failed.AddHours(WaitHours):(DateTime?)null;
        }
    }
    [DataContract] public sealed class UpdateNote
    {
        [DataMember(Name="version")] public string Version;
        [DataMember(Name="message")] public string Message;
    }
    // The launcher's own files just changed under it, so the only safe next step is a restart from the app folder.
    public sealed class UpdateRestoredException : InvalidOperationException { public UpdateRestoredException(string message) : base(message) { } }
    public sealed class HealthResult { public bool Restored; public string Message; }
    public enum RollbackOutcome { Done, NoPrevious, NotIdle, SaveSchemaTooNew }
    public sealed class RollbackResult
    {
        public RollbackOutcome Outcome; public string Message, Version; public bool OffersBackup; public string BackupId;
    }
    public sealed class RestoreResult { public string BackupId, MovedAsideTo, Message; }
    public static class Health
    {
        public const int ExitRejected=40, ExitCanary=41, ExitHooks=42, ExitSaveSwitch=46, ExitFirewallRefused=47, ExitRefused=48, ExitSetupFailed=49, ExitStartupError=50;
        // Marker wait. The retry comes after a first try that may have timed out on a slow PC, and it is the last one: a second timeout holds that version for good.
        // Waiting five minutes costs that PC nothing, and a game that truly hangs still fails.
        public const int FirstStartSeconds=120, RetryStartSeconds=300;
        // The plugin confirms only after its card check, and that can outlast a slow first start. While it reports a hold it is alive and working, so it gets this long, past its own limit.
        public const int ChecksWaitSeconds=330;
        // Health means the mod loaded: the plugin's marker. Its save picker can wait for a person, so a bound save is not part of it.
        // 40 and 42: the mod itself failed. A timeout, a startup error (50) or any other crash may be this machine: restore, wait a day, try once more.
        // Exit 0 means the player closed it. 41, 46, 47 and 48 are the plugin refusing this launch, and 49 a full disk or bad save, which say nothing about the release.
        public static HealthOutcome Classify(bool pluginConfirmed,bool markerTimedOut,int? exitCode) {
            if(pluginConfirmed) return HealthOutcome.Confirmed;
            if(markerTimedOut) return HealthOutcome.RetryLater;
            if(exitCode==null || exitCode==0 || exitCode==ExitCanary || exitCode==ExitSaveSwitch || exitCode==ExitFirewallRefused || exitCode==ExitRefused || exitCode==ExitSetupFailed) return HealthOutcome.NoEvidence;
            return exitCode==ExitRejected || exitCode==ExitHooks?HealthOutcome.Failed:HealthOutcome.RetryLater;
        }
    }
    // A launcher started mid-swap has lost its config and may have lost libraries. Its first job is Resume, which needs none of them and hands off to a staged launcher,
    // whose folder is whole. Should anything load a library before that, this finds it: beside the exe, or where the journal says it moved.
    public static class MovedLibraries
    {
        public static string Find(string ownFolder,string simpleName) {
            if(simpleName.IndexOfAny(new[]{'/','\\',':'})>=0 || simpleName.EndsWith(".resources",StringComparison.OrdinalIgnoreCase)) return null;
            string file=simpleName+".dll", beside=Path.Combine(ownFolder,file);
            if(File.Exists(beside)) return beside;
            var journal=Atomic.TryRead<SwapJournal>(Path.Combine(Path.GetDirectoryName(ownFolder),"updates","journal.json"));
            if(journal?.Moves==null || journal.Before==null || string.IsNullOrEmpty(journal.Before.Root)) return null;
            string app=Path.Combine(journal.Before.Root,"app"), releases=Path.Combine(journal.Before.Root,"releases");
            var move=journal.Moves.FirstOrDefault(row=>row.Op=="move" && !string.IsNullOrEmpty(row.Src) && !string.IsNullOrEmpty(row.Dst) && Safety.Same(row.Src,Path.Combine(app,file)));
            if(move==null || !Safety.Within(Path.GetFullPath(move.Dst),releases) || !File.Exists(move.Dst) || !Safety.Same(Hashes.FileHash(move.Dst),move.Sha)) return null;
            return move.Dst;
        }
    }
    public sealed class UpdateEngine
    {
        public const long MaxUnpacked=2L*1024*1024*1024; public const int MaxEntries=5000;
        readonly string root; readonly IUpdateHost host; readonly Action<string> progress;
        public Action<string> Fault; // Crash-point hook for tests. Never set in production.
        public Func<string,string,bool> SameVolume=(a,b)=>Safety.Same(Path.GetPathRoot(a),Path.GetPathRoot(b));
        public UpdateEngine(string root,IUpdateHost host,Action<string> progress=null) { this.root=Safety.Canonical(root); this.host=host; this.progress=progress??(_=>{}); }
        string App => Path.Combine(root,"app");
        string Updates => Path.Combine(root,"updates");
        string Releases => Path.Combine(root,"releases");
        string JournalPath => Path.Combine(Updates,"journal.json");
        string HeldPath => Path.Combine(root,"state","held.json");
        string AttemptsPath => Path.Combine(root,"state","swap-attempts.json");
        public SwapAttempts Attempts() => Atomic.TryRead<SwapAttempts>(AttemptsPath);
        // A failed swap's whole cost is here. The count must survive whatever the undo does next, and must never itself end the swap's reporting.
        int CountFailure(string version,string from) {
            try {
                var had=Attempts();
                if(from==null) { try { from=InstallRecord.Load(root).ReleaseVersion; } catch(InvalidOperationException) { } } // No journal yet: the launcher's wait compares this with the installed version.
                var attempts=had!=null && had.Version==version && (from==null || had.From==from)?had:new SwapAttempts { Version=version, From=from };
                attempts.Failures++; attempts.LastFailedUtc=DateTime.UtcNow.ToString("o");
                Atomic.Save(AttemptsPath,attempts);
                return attempts.Failures;
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException) { InstallerLog.Write(root,"The failed-swap count could not be saved: "+e.GetType().Name); return 0; }
        }
        static string WaitWords(int failures) => failures>=SwapAttempts.FailuresBeforeWait?" This was its second failure in a row, so SoloStone waits a day before trying again.":"";
        // One backup per version and release while the saves stand still. Backups keep 3, so a retry on every start would push out the one a rollback needs.
        // Saves played since then need their own: a schema-bump rollback restores this backup, and must not lose that play.
        void BackUpOnce(string version,InstallRecord record,string staged) {
            var had=Attempts();
            var backups=new Backups(root);
            if(had!=null && had.Version==version && had.From==record.ReleaseVersion && had.BackupId!=null) {
                try { if(backups.SavesUnchanged(backups.Verified(had.BackupId),host.SaveRoot)) { Say("Your saves were already backed up for this update."); return; } }
                catch(InvalidOperationException) { } // Gone or damaged: this try needs its own.
            }
            Say("Backing up your saves…");
            string id=backups.Create(host.SaveRoot,ClientConfig(record),record.ReleaseVersion);
            bool same=had!=null && had.Version==version && had.From==record.ReleaseVersion;
            Atomic.Save(AttemptsPath,new SwapAttempts { Version=version, From=record.ReleaseVersion, Failures=same?had.Failures:0, LastFailedUtc=same?had.LastFailedUtc:null, BackupId=id });
        }
        string RecordPath => Path.Combine(root,"install.json");
        string RestoreStage => host.SaveRoot+".solostone-restore";
        string AppExe => Path.Combine(App,"SoloStone.exe");
        string HelperFolder => Path.Combine(Updates,"undo-helper");
        string NotePath => Path.Combine(root,"state","update-note.json");
        public string StagedApp(string version) => Path.Combine(Updates,version,"app");
        public string AssetPath(string version) => Path.Combine(Updates,version,"asset.zip");
        void Point(string label) => Fault?.Invoke(label);
        public bool JournalActive => File.Exists(JournalPath);
        public SwapJournal Journal => Atomic.TryRead<SwapJournal>(JournalPath);
        public HeldUpdate Held() => Atomic.TryRead<HeldUpdate>(HeldPath);
        void Hold(string version,string reason,string retryAfter=null,int retries=0) => Atomic.Save(HeldPath,new HeldUpdate { Version=version, Reason=reason, Utc=DateTime.UtcNow.ToString("o"), RetryAfter=retryAfter, Retries=retries });
        void Say(string message) => progress(message);
        // ---- Stage ----
        static string EntryPath(string unpack,string name) {
            string path=Safety.Child(unpack,name);
            foreach(string part in name.Split('/')) {
                string stem=part.Split('.')[0].ToUpperInvariant();
                if(part.EndsWith(".") || part.EndsWith(" ") || part.IndexOfAny(new[]{'*','?','<','>','|','"'})>=0 || part.Any(char.IsControl)
                    || new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(stem))
                    throw new InvalidOperationException("The update contains an unsafe file name.");
            }
            return path;
        }
        static void Unpack(string zip,string unpack,CancellationToken cancel) {
            long total=0; var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using(var stream=new FileStream(zip,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var archive=new ZipArchive(stream,ZipArchiveMode.Read)) {
                if(archive.Entries.Count>MaxEntries) throw new InvalidOperationException("The update contains too many files.");
                foreach(var entry in archive.Entries) {
                    cancel.ThrowIfCancellationRequested();
                    string name=entry.FullName;
                    int unixMode=(entry.ExternalAttributes>>16)&0xF000;
                    if(unixMode==0xA000 || (entry.ExternalAttributes&0x400)!=0) throw new InvalidOperationException("The update contains a link, which SoloStone refuses.");
                    bool directory=name.EndsWith("/",StringComparison.Ordinal);
                    string target=EntryPath(unpack,directory?name.Substring(0,name.Length-1):name);
                    if(!seen.Add(name.TrimEnd('/'))) throw new InvalidOperationException("The update lists a file twice.");
                    if(directory) { if(entry.Length!=0) throw new InvalidOperationException("The update is malformed."); Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using(var input=entry.Open())
                    using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                        var buffer=new byte[256*1024]; int read;
                        // Entry headers can lie, so the cap counts real bytes.
                        while((read=input.Read(buffer,0,buffer.Length))>0) {
                            total+=read; if(total>MaxUnpacked) throw new InvalidOperationException("The update is larger than SoloStone allows.");
                            output.Write(buffer,0,read);
                        }
                    }
                }
            }
        }
        // Everything here happens in Root\updates, so any refusal leaves the running version untouched.
        public string Stage(string zip,LatestRelease latest,CancellationToken cancel=default(CancellationToken)) {
            if(!NoSwapInProgress) throw new InvalidOperationException("Another update is still finishing. Start SoloStone again.");
            if(!File.Exists(zip) || new FileInfo(zip).Length!=latest.Asset.Size || !Safety.Same(Hashes.FileHash(zip),latest.Asset.Sha256))
                throw new InvalidOperationException("The downloaded update failed its size or hash check.");
            var record=InstallRecord.Load(root);
            string versionFolder=Path.Combine(Updates,latest.Version), staged=StagedApp(latest.Version), unpack=staged+".unpacking";
            if(Directory.Exists(Updates)) foreach(string other in Directory.GetDirectories(Updates)) if(!Safety.Same(other,versionFolder)) FsOps.DeleteTree(other);
            FsOps.DeleteTree(unpack); FsOps.DeleteTree(staged); Directory.CreateDirectory(unpack);
            try {
                Unpack(zip,unpack,cancel);
                CheckStagedFiles(unpack,latest,record);
                Directory.Move(unpack,staged);
            } catch { FsOps.DeleteTree(unpack); throw; }
            return staged;
        }
        static void CheckStagedFiles(string unpack,LatestRelease latest,InstallRecord record) {
            var info=ReleaseInfo.TryRead(unpack);
            if(info==null || info.Version!=latest.Version || info.SaveSchema!=latest.SaveSchema)
                throw new InvalidOperationException("The update's release file does not match its signed manifest.");
            if(!File.Exists(Path.Combine(unpack,"SoloStone.exe"))) throw new InvalidOperationException("The update does not contain the launcher.");
            string payloadJson=Path.Combine(unpack,"payload.json");
            if(!File.Exists(payloadJson)) throw new InvalidOperationException("The update does not contain its mod list.");
            var payload=Json.Read<Payload>(payloadJson);
            string folder=Path.Combine(unpack,"payload"); payload.Verify(folder);
            string prefix=folder+Path.DirectorySeparatorChar;
            var inFolder=Safety.Tree(folder).Select(p=>p.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar,'/')).ToArray();
            if(!new HashSet<string>(inFolder,StringComparer.OrdinalIgnoreCase).SetEquals(payload.Files.Keys))
                throw new InvalidOperationException("The update's mod files do not match its mod list.");
            string problem=PayloadRules.Problem(inFolder,record.Files.Keys);
            if(problem!=null) throw new InvalidOperationException(problem);
            string appPrefix=unpack+Path.DirectorySeparatorChar;
            problem=PayloadRules.AppProblem(Safety.Tree(unpack).Select(p=>p.Substring(appPrefix.Length).Replace(Path.DirectorySeparatorChar,'/')).Where(p=>!p.StartsWith("payload/",StringComparison.Ordinal)));
            if(problem!=null) throw new InvalidOperationException(problem);
        }
        // ---- Apply ----
        // A release waiting for its first start is no obstacle: FinishSwap restores the last good version first, so that stays the rollback target.
        bool NoSwapInProgress => !JournalActive || Journal?.Phase==SwapJournal.Swapped;
        // Runs in the current launcher. It decides, then hands the swap to the new launcher, because a running exe cannot be replaced.
        public UpdateResult Apply(LatestRelease latest,bool acceptWarning) {
            if(!NoSwapInProgress) return new UpdateResult { Outcome=UpdateOutcome.Refused, Version=latest.Version, Message="Another update is still finishing. Start SoloStone again." };
            string staged=StagedApp(latest.Version);
            if(!Directory.Exists(staged)) throw new InvalidOperationException("The update is not staged.");
            var installed=InstallRecord.Load(root);
            if(!SemVer.Valid(installed.ReleaseVersion) || !SemVer.Newer(latest.Version,installed.ReleaseVersion)) throw new InvalidOperationException("SoloStone never installs a version that is not newer.");
            Verdict verdict;
            try { verdict=host.CheckStaged(staged,root); }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is System.ComponentModel.Win32Exception) { verdict=null; }
            if(verdict==null) return new UpdateResult { Outcome=UpdateOutcome.Failed, Version=latest.Version, Message="The new version's client check could not run, so the update waits. SoloStone will try again next time." };
            if(verdict.Kind==VerdictKind.Block) {
                string reason=verdict.Message;
                Hold(latest.Version,reason); FsOps.DeleteTree(Path.Combine(Updates,latest.Version));
                return new UpdateResult { Outcome=UpdateOutcome.Held, Version=latest.Version, Verdict=verdict, Message="SoloStone "+latest.Version+" was held back: "+reason+" Your current version keeps working." };
            }
            if(verdict.Kind==VerdictKind.Warning && !acceptWarning)
                return new UpdateResult { Outcome=UpdateOutcome.NeedsConfirmation, Version=latest.Version, Verdict=verdict, Message=verdict.Message };
            string idle=IdleProblem();
            if(idle!=null) return new UpdateResult { Outcome=UpdateOutcome.Refused, Version=latest.Version, Verdict=verdict, Message=idle };
            Say("Installing SoloStone "+latest.Version+"…");
            ClearNote(); // From here on only this attempt's result may speak.
            host.HandOffSwap(staged,latest.Version);
            return new UpdateResult { Outcome=UpdateOutcome.Swapped, Version=latest.Version, Verdict=verdict, Message="Installing SoloStone "+latest.Version+". The launcher opens again when it finishes." };
        }
        // Runs in the staged launcher once the old one has exited, so nothing here touches a running image.
        // Order: everything else first, then the record, then the atomic exe replace. Until that replace the old launcher is in place and undoes;
        // from then on the new one is in place and finishes. At no step is the shortcut's target missing.
        public UpdateResult FinishSwap(string version) {
            Func<UpdateOutcome,string,UpdateResult> stop=(outcome,message)=>{ Note(version,message); return new UpdateResult { Outcome=outcome, Version=version, Message=message }; };
            if(!SemVer.Valid(version)) return new UpdateResult { Outcome=UpdateOutcome.Failed, Message="The update's version is not valid." };
            string staged=StagedApp(version);
            var info=ReleaseInfo.TryRead(staged);
            if(info==null || info.Version!=version) { int bad=CountFailure(version,null); return stop(UpdateOutcome.Failed,"The staged update is damaged, so the previous version stayed in place."+WaitWords(bad)); }
            if(!NoSwapInProgress) return stop(UpdateOutcome.Refused,"Another update is still finishing. Start SoloStone again.");
            string idle=IdleProblem();
            if(idle!=null) return stop(UpdateOutcome.Refused,idle);
            SwapJournal journal=null;
            try {
                if(JournalActive) {
                    var pending=LoadJournal(); pending.HoldReason=null; pending.HoldRetryAfter=null;
                    // This launcher stays the helper if a kill interrupts the restore. The pending release's own staged files are long gone.
                    pending.Helper=staged; pending.HelperSha=Hashes.FileHash(Path.Combine(staged,"SoloStone.exe"));
                    Undo(pending); Point("pending:restored");
                }
                var record=InstallRecord.Load(root);
                // Plan refuses first, so an update that cannot apply never spends one of the three kept backups.
                journal=Plan(record,new LatestRelease { Version=version, SaveSchema=info.SaveSchema },staged);
                BackUpOnce(version,record,staged);
                Point("backup:done");
                Say("Installing SoloStone "+version+"…");
                Save(journal); Point("phase:"+journal.Phase);
                for(int i=0;i<journal.Moves.Count;i++) {
                    journal.Started=i; Save(journal); Point("start:"+i);
                    Forward(journal.Moves[i]);
                }
                VerifyForward(journal);
                journal.Phase=SwapJournal.Moved; Save(journal); Point("phase:"+journal.Phase);
                Atomic.Save(RecordPath,journal.After); Point("record:saved");
                ReplaceExe(Path.Combine(staged,"SoloStone.exe"),journal.ExeNew);
                journal.Phase=SwapJournal.Recorded; Save(journal); Point("phase:"+journal.Phase);
                FinishForward(journal);
            } catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException) {
                LogFailure("The swap to "+version+" failed",journal,e);
                int failures=CountFailure(version,journal?.From);
                if(journal==null) return stop(UpdateOutcome.Failed,"The update did not install, so the previous version stayed in place. "+FriendlyError(e)+WaitWords(failures));
                Undo(journal);
                return stop(UpdateOutcome.Failed,"The update could not be installed, so the previous version stayed in place. "+FriendlyError(e)+WaitWords(failures));
            }
            ClearNote();
            return new UpdateResult { Outcome=UpdateOutcome.Swapped, Version=version, Message="SoloStone "+version+" is installed." };
        }
        void Note(string version,string message) => Atomic.Save(NotePath,new UpdateNote { Version=version, Message=message });
        public void RecordNote(string version,string message) => Note(version,message);
        // What a helper process had to say, for the launcher window it starts. Kept until the next successful game start.
        public UpdateNote LastNote() => Atomic.TryRead<UpdateNote>(NotePath);
        public void ClearNote() { if(File.Exists(NotePath)) File.Delete(NotePath); }
        // The player's sentence stays plain, so the cause has to be here.
        void LogFailure(string what,SwapJournal journal,Exception e) {
            string at="";
            if(journal?.Moves!=null && journal.Started>=0 && journal.Started<journal.Moves.Count) {
                var move=journal.Moves[journal.Started];
                at=" at move "+journal.Started+" of "+journal.Moves.Count+" ("+move.Op+" "+move.Src+" -> "+move.Dst+", phase "+journal.Phase+")";
            }
            InstallerLog.Write(root,what+at+": "+e.GetType().FullName+" (0x"+e.HResult.ToString("X8")+"): "+e.Message);
        }
        static string FriendlyError(Exception e) => e is InvalidOperationException?e.Message:"A file was busy or could not be changed.";
        string IdleProblem() {
            string busy=host.IdleProblem();
            if(busy!=null) return busy;
            return Saves.Scan(host.SaveRoot).Problem;
        }
        static string ClientConfig(InstallRecord record) => Path.Combine(record.Client,"BepInEx","config");
        SwapJournal Plan(InstallRecord record,LatestRelease latest,string staged) {
            if(!SemVer.Valid(record.ReleaseVersion)) throw new InvalidOperationException("The installed version is unknown. "+InstallRecord.StartOver);
            if(!SemVer.Newer(latest.Version,record.ReleaseVersion)) throw new InvalidOperationException("SoloStone never installs a version that is not newer.");
            record.Payload.Verify(record.Client);
            string oldRelease=Path.Combine(Releases,record.ReleaseVersion);
            FsOps.DeleteTree(oldRelease);
            var payload=Json.Read<Payload>(Path.Combine(staged,"payload.json"));
            var moves=new List<SwapMove>(); SwapMove keepExe=null;
            string appPrefix=App+Path.DirectorySeparatorChar;
            foreach(string file in Safety.Tree(App).OrderBy(p=>p,StringComparer.Ordinal)) {
                string relative=file.Substring(appPrefix.Length).Replace(Path.DirectorySeparatorChar,'/');
                string name=Path.GetFileName(relative);
                if(!relative.Contains("/") && (name.StartsWith("unins",StringComparison.OrdinalIgnoreCase) || name==".solostone-app")) continue;
                var row=new SwapMove { Op="move", Src=file, Dst=Safety.Child(Path.Combine(oldRelease,"app"),relative), Sha=Hashes.FileHash(file) };
                if(!relative.Contains("/") && Safety.Same(name,"SoloStone.exe")) { row.Op="keep"; keepExe=row; } else moves.Add(row);
            }
            // First of all: from here on a copy of the previous launcher exists, whatever happens to the file in place.
            if(keepExe==null) throw new InvalidOperationException("The launcher file is missing. "+InstallRecord.StartOver);
            moves.Insert(0,keepExe);
            foreach(var row in record.Payload.Files.OrderBy(r=>r.Key,StringComparer.Ordinal))
                moves.Add(new SwapMove { Op="move", Src=Safety.Child(record.Client,row.Key), Dst=Safety.Child(Path.Combine(oldRelease,"client"),row.Key), Sha=row.Value.ToUpperInvariant() });
            string stagedPrefix=staged+Path.DirectorySeparatorChar;
            foreach(string file in Safety.Tree(staged).OrderBy(p=>p,StringComparer.Ordinal)) {
                string relative=file.Substring(stagedPrefix.Length).Replace(Path.DirectorySeparatorChar,'/');
                if(Safety.Same(relative,"SoloStone.exe")) continue;
                // Copies, because the launcher doing this runs from the staged folder.
                moves.Add(new SwapMove { Op="copy", Src=file, Dst=Safety.Child(App,relative), Sha=Hashes.FileHash(file) });
            }
            foreach(var row in payload.Files.OrderBy(r=>r.Key,StringComparer.Ordinal)) {
                string target=Safety.Child(record.Client,row.Key);
                if(File.Exists(target) && !record.Payload.Files.Keys.Any(k=>Safety.Same(k,row.Key))) throw new InvalidOperationException("The update would overwrite a file it does not own ("+row.Key+"). Nothing was changed.");
                moves.Add(new SwapMove { Op="copy", Src=Safety.Child(App,"payload/"+row.Key), Dst=target, Sha=row.Value.ToUpperInvariant() });
            }
            var after=JsonBytes.Parse<InstallRecord>(JsonBytes.ToBytes(record));
            after.Payload=payload; after.ReleaseVersion=latest.Version;
            return new SwapJournal { Schema=1, Id=Guid.NewGuid().ToString("N"), From=record.ReleaseVersion, To=latest.Version, FromSaveSchema=ReleaseInfo.TryRead(App)?.SaveSchema??0,
                ToSaveSchema=latest.SaveSchema, Phase=SwapJournal.Planned, Started=-1, Moves=moves, Before=record, After=after,
                ExeOld=keepExe.Sha, ExeNew=Hashes.FileHash(Path.Combine(staged,"SoloStone.exe")) };
        }
        void Save(SwapJournal journal) => Atomic.Save(JournalPath,journal);
        // One file, idempotent from any crash state of this same move. Plan proved dst was absent, so a wrong dst is our own partial write.
        void MoveFile(string from,string to,string sha,string label) {
            string temp=to+".solostone-tmp", reverseTemp=from+".solostone-tmp";
            Atomic.Delete(temp);
            Atomic.Delete(reverseTemp); // A crash may have left it going the other way.
            bool hasFrom=File.Exists(from), hasTo=File.Exists(to);
            if(hasTo && Safety.Same(Hashes.FileHash(to),sha)) {
                if(hasFrom) { Point(label+":drop"); Atomic.Transient(()=>File.Delete(from)); Point(label+":dropped"); }
                return;
            }
            if(!hasFrom || !Safety.Same(Hashes.FileHash(from),sha)) throw new InvalidOperationException("An update file is missing or changed ("+Path.GetFileName(from)+"). "+InstallRecord.StartOver);
            if(hasTo) { Point(label+":clear"); Atomic.Transient(()=>File.Delete(to)); }
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            if(SameVolume(from,to)) { Point(label+":rename"); Atomic.Transient(()=>File.Move(from,to)); Point(label+":renamed"); return; }
            Point(label+":copy"); CopyFresh(from,temp); Point(label+":copied");
            if(!Safety.Same(Hashes.FileHash(temp),sha)) { Atomic.Delete(temp); throw new InvalidOperationException("An update file changed while it was copied."); }
            Atomic.Transient(()=>File.Move(temp,to)); Point(label+":placed"); Atomic.Transient(()=>File.Delete(from)); Point(label+":dropped");
        }
        static void CopyFresh(string from,string to) => Atomic.Transient(()=>{ if(File.Exists(to)) File.Delete(to); File.Copy(from,to,false); });
        void Forward(SwapMove move) {
            if(move.Op=="move") { MoveFile(move.Src,move.Dst,move.Sha,"move:"+Path.GetFileName(move.Dst)); return; }
            string temp=move.Dst+".solostone-tmp";
            Atomic.Delete(temp);
            if(File.Exists(move.Dst) && Safety.Same(Hashes.FileHash(move.Dst),move.Sha)) return;
            Atomic.Delete(move.Dst);
            Directory.CreateDirectory(Path.GetDirectoryName(move.Dst));
            Point("copy:"+Path.GetFileName(move.Dst)); CopyFresh(move.Src,temp); Point("copied:"+Path.GetFileName(move.Dst));
            if(!Safety.Same(Hashes.FileHash(temp),move.Sha)) { Atomic.Delete(temp); throw new InvalidOperationException("An update file changed while it was copied."); }
            Atomic.Transient(()=>File.Move(temp,move.Dst)); Point("placed:"+Path.GetFileName(move.Dst));
        }
        void Backward(SwapMove move) {
            if(move.Op=="move") { MoveFile(move.Dst,move.Src,move.Sha,"undo:"+Path.GetFileName(move.Src)); return; }
            if(move.Op=="keep") return; // The previous launcher: Undo replaces the exe from this copy last.
            string temp=move.Dst+".solostone-tmp";
            Atomic.Delete(temp);
            if(File.Exists(move.Dst)) { Point("undo-copy:"+Path.GetFileName(move.Dst)); Atomic.Transient(()=>File.Delete(move.Dst)); Point("undo-copied:"+Path.GetFileName(move.Dst)); }
        }
        void VerifyForward(SwapJournal journal) {
            foreach(var move in journal.Moves)
                if(!File.Exists(move.Dst) || !Safety.Same(Hashes.FileHash(move.Dst),move.Sha)) throw new InvalidOperationException("The new files failed their hash check.");
            journal.After.Payload.Verify(journal.After.Client);
        }
        // One atomic step. Never run by a process whose image is this file: Windows will not replace a running exe.
        void ReplaceExe(string source,string sha) {
            string temp=AppExe+".solostone-new";
            Atomic.Delete(temp);
            if(File.Exists(AppExe) && Safety.Same(Hashes.FileHash(AppExe),sha)) return;
            Point("exe:copy"); CopyFresh(source,temp); Point("exe:copied");
            if(!Safety.Same(Hashes.FileHash(temp),sha)) { Atomic.Delete(temp); throw new InvalidOperationException("The launcher file failed its hash check."); }
            Point("exe:replace");
            Atomic.Publish(temp,AppExe); // Antivirus may still hold a file just written, or a process that just exited: Publish waits a moment for both.
            Point("exe:replaced");
        }
        void FinishForward(SwapJournal journal) {
            string keep=Path.Combine(Releases,journal.From);
            if(Directory.Exists(Releases)) foreach(string old in Directory.GetDirectories(Releases)) if(!Safety.Same(old,keep)) FsOps.DeleteTree(old);
            Point("cleanup:releases");
            TryDelete(Path.Combine(Updates,journal.To),"cleanup:staged"); // The launcher doing this runs from there on the way in. Resume clears it later.
            var held=Held();
            if(held!=null && held.Version==journal.To) { journal.Retries=held.Retries; Save(journal); Point("cleanup:retries"); }
            if(held!=null && SemVer.Valid(held.Version) && !SemVer.Newer(held.Version,journal.To)) { Atomic.Delete(HeldPath); Point("cleanup:held"); }
            Atomic.Delete(AttemptsPath);
            journal.Phase=SwapJournal.Swapped; journal.Health="pending"; Save(journal); Point("phase:"+journal.Phase);
        }
        // Everything back, then the record and hold, then the atomic exe replace. Until that replace the new launcher is in place, and its Resume hands
        // this to a helper again; after it the old one is in place, and only tidying is left, which needs no helper.
        void Undo(SwapJournal journal) {
            if(journal.Phase!=SwapJournal.Undoing) { journal.Phase=SwapJournal.Undoing; Save(journal); Point("phase:"+journal.Phase); }
            // Walk back from the last move begun. Saving the index each time keeps a re-run from touching a restored file.
            for(int i=journal.Started;i>=0;i--) {
                Backward(journal.Moves[i]);
                journal.Started=i-1; Save(journal); Point("undone:"+i);
            }
            Point("undo:moved");
            Atomic.Save(RecordPath,journal.Before); Point("undo:record");
            if(journal.HoldReason!=null) { Hold(journal.To,journal.HoldReason,journal.HoldRetryAfter,journal.Retries); Point("undo:held"); }
            var keep=journal.Moves.FirstOrDefault(move=>move.Op=="keep");
            if(keep!=null && !ExeIs(journal.ExeOld)) {
                if(!File.Exists(keep.Dst)) throw new InvalidOperationException("The previous launcher file is gone, so this update can't be undone. "+InstallRecord.StartOver);
                ReplaceExe(keep.Dst,journal.ExeOld);
            }
            string temp=AppExe+".solostone-new";
            Atomic.Delete(temp);
            Point("undo:exe");
            // Whichever process runs this may be in one of these folders, and Windows will not delete a running exe. Stage and Resume clear leftovers.
            // A version that may be tried again keeps its verified zip, so a retry downloads nothing. Stage unpacks it again, because the helper can't delete all of the folder it runs from.
            TryDelete(journal.HoldReason!=null?Path.Combine(Updates,journal.To):StagedApp(journal.To),"undo:staged"); Point("undo:staged-done");
            TryDelete(Path.Combine(Releases,journal.From),"undo:release"); Point("undo:release-done");
            Atomic.Delete(JournalPath);
        }
        // Restores from a release that may be running right now: the launcher files go to a helper folder, and that copy does the work.
        bool BeginUndo(SwapJournal journal) {
            if(!host.RunningFrom(App)) { Undo(journal); return false; }
            string partial=HelperFolder+".partial";
            FsOps.DeleteTree(partial); FsOps.DeleteTree(HelperFolder);
            string prefix=App+Path.DirectorySeparatorChar;
            foreach(string file in Safety.Tree(App)) {
                string relative=file.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar,'/');
                if(relative.StartsWith("payload/",StringComparison.Ordinal) || Path.GetFileName(relative).StartsWith("unins",StringComparison.OrdinalIgnoreCase) || relative==".solostone-app") continue;
                string target=Safety.Child(partial,relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                string hash=FsOps.CopyHashed(file,target,CancellationToken.None);
                if(!Safety.Same(Hashes.FileHash(target),hash)) throw new InvalidOperationException("The restore helper could not be prepared.");
            }
            Point("helper:copied");
            Directory.Move(partial,HelperFolder);
            journal.Helper=HelperFolder; journal.HelperSha=journal.ExeNew; journal.Phase=SwapJournal.Undoing; Save(journal); Point("helper:ready");
            host.HandOffUndo(HelperFolder);
            return true;
        }
        bool ExeIs(string sha) => File.Exists(AppExe) && Safety.Same(Hashes.FileHash(AppExe),sha);
        // A launcher able to finish a restore: the helper made for it, or the staged launcher of a swap that was cut short. Same code as the journal's writer.
        string HelperFor(SwapJournal journal) {
            Func<string,string,bool> holds=(folder,sha)=>!string.IsNullOrEmpty(folder) && File.Exists(Path.Combine(folder,"SoloStone.exe")) && Safety.Same(Hashes.FileHash(Path.Combine(folder,"SoloStone.exe")),sha);
            if(holds(journal.Helper,journal.HelperSha)) return journal.Helper;
            return holds(StagedApp(journal.To),journal.ExeNew)?StagedApp(journal.To):null;
        }
        void TryDelete(string folder,string label) {
            try { Point(label); FsOps.DeleteTree(folder); }
            catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
        SwapJournal LoadJournal() {
            var journal=Atomic.TryRead<SwapJournal>(JournalPath);
            if(!ValidJournal(journal)) throw new InvalidOperationException("The update journal is damaged. "+InstallRecord.StartOver);
            return journal;
        }
        // A journal only ever names files in places SoloStone owns.
        bool ValidJournal(SwapJournal journal) {
            if(journal==null || journal.Schema!=1 || journal.Moves==null || journal.Before==null || journal.After==null || !SemVer.Valid(journal.From) || !SemVer.Valid(journal.To)
                || string.IsNullOrEmpty(journal.Before.ClientPath) || journal.Started>=journal.Moves.Count) return false;
            Func<string,bool> owned=path=>!string.IsNullOrEmpty(path) && new[]{App,Releases,Updates,journal.Before.ClientPath}.Any(folder=>Safety.Within(Path.GetFullPath(path),folder));
            return (string.IsNullOrEmpty(journal.Helper) || Safety.Within(Path.GetFullPath(journal.Helper),Updates)) && journal.Moves.All(move=>owned(move.Src) && owned(move.Dst));
        }
        // Committed means the new launcher is in place. From then on the swap only goes forward. An identical exe has no replace, so there the phase decides.
        bool Committed(SwapJournal journal) => journal.Phase==SwapJournal.Recorded || journal.Phase==SwapJournal.Swapped
            || (journal.Phase==SwapJournal.Moved && !Safety.Same(journal.ExeOld,journal.ExeNew) && ExeIs(journal.ExeNew));
        // Runs on every launcher start, and from any other launcher (setup's, uninstall's). Whatever a kill left, one whole version results.
        // A launcher in the app folder never moves files itself: it hands that to one outside it and exits.
        public ResumeOutcome Resume() {
            FinishRestore();
            string temp=JournalPath+".tmp";
            if(File.Exists(temp)) File.Delete(temp);
            if(!File.Exists(JournalPath)) { TryDelete(HelperFolder,"resume:helper"); return ResumeOutcome.Nothing; }
            var journal=LoadJournal();
            // Only setup's launcher or a helper can get here with the exe gone, which takes a power cut or something outside SoloStone.
            if(!File.Exists(AppExe) && (journal.Phase==SwapJournal.Recorded || journal.Phase==SwapJournal.Swapped)) {
                string staged=Path.Combine(StagedApp(journal.To),"SoloStone.exe");
                if(File.Exists(staged) && Safety.Same(Hashes.FileHash(staged),journal.ExeNew)) ReplaceExe(staged,journal.ExeNew);
                else { journal.HoldReason=null; Undo(journal); return ResumeOutcome.Undone; }
            }
            if(journal.Phase==SwapJournal.Swapped) { TryDelete(Path.Combine(Updates,journal.To),"resume:staged"); return ResumeOutcome.HealthPending; }
            if(Committed(journal)) {
                if(journal.Phase!=SwapJournal.Recorded) { journal.Phase=SwapJournal.Recorded; Save(journal); Point("phase:"+journal.Phase); }
                FinishForward(journal); return ResumeOutcome.HealthPending;
            }
            bool tidyOnly=journal.Started<0 && ExeIs(journal.ExeOld);
            if(host.RunningFrom(App) && !tidyOnly) {
                string helper=HelperFor(journal);
                if(helper!=null) { host.HandOffUndo(helper); return ResumeOutcome.HandedOff; }
                if(!ExeIs(journal.ExeOld)) throw new InvalidOperationException("An update was interrupted and its restore files are gone. "+InstallRecord.StartOver);
            }
            Undo(journal);
            return ResumeOutcome.Undone;
        }
        // Runs in the helper process once the launcher that started it has exited.
        public void FinishUndo() {
            if(!File.Exists(JournalPath)) return;
            var journal=LoadJournal();
            if(journal.Phase==SwapJournal.Swapped || Committed(journal) && journal.Phase!=SwapJournal.Undoing) return;
            Undo(journal);
            ClearNote();
        }
        // The helper's whole job. A swap or restore still open would hand off again from the launcher it reopened, and again, so a failure here
        // does not reopen it. The caller's window shows the message; the note and log keep it.
        public void RunHelper(string command,string version) {
            try {
                if(command=="finish-undo") FinishUndo();
                else Say(FinishSwap(version).Message);
            }
            catch(Exception e) when(e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException) {
                LogFailure("The helper's "+command+" failed",Journal,e);
                string message="SoloStone could not finish its update. "+FriendlyError(e)+" Close this window, then start SoloStone again to retry. Your saves are safe.";
                Note(version,message); Say(message);
                throw new InvalidOperationException(message,e);
            }
        }
        public void RequireReadableJournal() { if(File.Exists(JournalPath)) LoadJournal(); }
        // ---- Health ----
        public int MarkerSeconds() { var open=File.Exists(JournalPath)?Journal:null; return open!=null && open.Phase==SwapJournal.Swapped && open.Retries>=1?Health.RetryStartSeconds:Health.FirstStartSeconds; }
        public HealthResult ReportHealth(HealthOutcome outcome,string reason) {
            var none=new HealthResult();
            if(outcome==HealthOutcome.NoEvidence || !File.Exists(JournalPath)) return none;
            var journal=LoadJournal();
            if(journal.Phase!=SwapJournal.Swapped) return none;
            if(outcome==HealthOutcome.Confirmed) {
                string keep=Path.Combine(Releases,journal.From);
                journal.Health="confirmed"; journal.HoldReason=null; journal.HoldRetryAfter=null;
                Atomic.Save(Path.Combine(keep,"rollback.json"),journal); Point("health:kept");
                File.Delete(JournalPath);
                ClearNote();
                return none;
            }
            string why=reason??"The game did not start with this version.";
            journal.HoldReason=why;
            // A slow or unlucky machine gets one more try a day later. Without the wait the restart would apply the same version straight away, for ever.
            journal.HoldRetryAfter=null;
            if(outcome==HealthOutcome.RetryLater && ++journal.Retries<2) journal.HoldRetryAfter=DateTime.UtcNow.AddHours(24).ToString("o");
            // Only in memory so far. The restore saves this as it starts, so a failed helper copy or a kill before then spends no try.
            Point("health:counted");
            BeginUndo(journal);
            return new HealthResult { Restored=true, Message="SoloStone "+journal.To+" failed its first start, so "+journal.From+" is restored." };
        }
        // ---- Rollback ----
        SwapJournal PreviousRelease() {
            if(File.Exists(JournalPath)) { var active=LoadJournal(); return active.Phase==SwapJournal.Swapped?active:null; }
            if(!Directory.Exists(Releases)) return null;
            foreach(string folder in Directory.GetDirectories(Releases)) {
                var journal=Atomic.TryRead<SwapJournal>(Path.Combine(folder,"rollback.json"));
                if(ValidJournal(journal)) return journal;
            }
            return null;
        }
        public string PreviousVersion() { try { return PreviousRelease()?.From; } catch(InvalidOperationException) { return null; } }
        public RollbackResult Rollback() {
            var journal=PreviousRelease();
            if(journal==null) return new RollbackResult { Outcome=RollbackOutcome.NoPrevious, Message="There is no previous version to roll back to." };
            string busy=host.IdleProblem();
            var scan=Saves.Scan(host.SaveRoot);
            if(busy!=null || scan.Problem!=null) return new RollbackResult { Outcome=RollbackOutcome.NotIdle, Version=journal.From, Message=busy??scan.Problem };
            if(journal.FromSaveSchema<scan.MaxVersion) {
                // Only a backup the previous version can read helps.
                var latest=new Backups(root).List().FirstOrDefault(b=>b.SaveSchema<=journal.FromSaveSchema);
                return new RollbackResult { Outcome=RollbackOutcome.SaveSchemaTooNew, Version=journal.From, OffersBackup=latest!=null, BackupId=latest?.Id,
                    Message=latest!=null?"SoloStone "+journal.From+" cannot read your current saves. Restoring the pre-update backup makes this possible, but progress since the update is lost."
                        :"SoloStone "+journal.From+" cannot read your current saves, and no backup it can read remains." };
            }
            if(!ExeIs(journal.ExeNew)) return new RollbackResult { Outcome=RollbackOutcome.NoPrevious, Message="The current files changed since the update, so a rollback is not safe. "+InstallRecord.StartOver };
            foreach(var move in journal.Moves) {
                bool intact=File.Exists(move.Dst) && Safety.Same(Hashes.FileHash(move.Dst),move.Sha);
                if(!intact) return new RollbackResult { Outcome=RollbackOutcome.NoPrevious, Message="The current files changed since the update, so a rollback is not safe. "+InstallRecord.StartOver };
            }
            journal.HoldReason="You rolled back from this version."; journal.HoldRetryAfter=null;
            BeginUndo(journal);
            return new RollbackResult { Outcome=RollbackOutcome.Done, Version=journal.From, Message="SoloStone "+journal.From+" is restored. The launcher opens again when it finishes." };
        }
        // ---- Restore ----
        public string BackupNow(InstallRecord record) => new Backups(root).Create(host.SaveRoot,ClientConfig(record),record.ReleaseVersion);
        void FinishRestore() {
            string stage=RestoreStage, verified=stage+".verified";
            // The side file appears only once the stage is whole. Without it an absent save tree stays absent: a partial stage is never promoted.
            if(Directory.Exists(stage)) {
                if(File.Exists(verified) && !Directory.Exists(host.SaveRoot)) Directory.Move(stage,host.SaveRoot);
                else FsOps.DeleteTree(stage);
            }
            if(File.Exists(verified)) File.Delete(verified);
        }
        void CopyVerified(string backup,BackupManifest manifest,string prefix,string target) {
            foreach(var row in manifest.Files.Where(r=>r.Key.StartsWith(prefix+"/",StringComparison.Ordinal))) {
                Point("restore:file");
                string destination=Safety.Child(target,row.Key.Substring(prefix.Length+1));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                string hash=FsOps.CopyHashed(Safety.Child(backup,row.Key),destination,CancellationToken.None);
                if(!Safety.Same(hash,row.Value) || !Safety.Same(Hashes.FileHash(destination),row.Value)) throw new InvalidOperationException("The backup failed its hash check. Nothing was changed.");
            }
        }
        // Moves the current tree aside, never deletes it, then puts the verified backup in place.
        public RestoreResult RestoreBackup(string id=null) {
            string busy=host.IdleProblem();
            if(busy!=null) throw new InvalidOperationException(busy);
            var scan=Saves.Scan(host.SaveRoot);
            if(scan.Pending!=null) throw new InvalidOperationException(scan.Pending); // An unreadable save is exactly what a restore may fix.
            var backups=new Backups(root);
            id=id??backups.List().FirstOrDefault()?.Id;
            if(id==null) throw new InvalidOperationException("There is no backup to restore.");
            var manifest=backups.Verified(id);
            var record=InstallRecord.Load(root);
            string saveRoot=host.SaveRoot, stage=RestoreStage, utc=DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'",System.Globalization.CultureInfo.InvariantCulture);
            string aside=Path.Combine(backups.Folder,"replaced-"+utc);
            Directory.CreateDirectory(backups.Folder);
            if(Directory.Exists(saveRoot) && !SameVolume(saveRoot,backups.Folder)) throw new InvalidOperationException("Backups and saves must be on the same drive to restore. Nothing was changed.");
            Safety.NoLinks(saveRoot); Safety.NoLinks(stage);
            FsOps.DeleteTree(stage); Directory.CreateDirectory(stage);
            CopyVerified(backups.PathOf(id),manifest,"saves",stage);
            Point("restore:staged");
            File.WriteAllText(stage+".verified",id);
            Point("restore:verified");
            Directory.CreateDirectory(aside);
            if(Directory.Exists(saveRoot)) Directory.Move(saveRoot,Path.Combine(aside,"saves"));
            Point("restore:aside");
            Directory.Move(stage,saveRoot);
            Point("restore:placed");
            File.Delete(stage+".verified");
            // Settings follow the saves. The copy is kept first because the client may sit on another drive.
            string config=ClientConfig(record), keepConfig=Path.Combine(aside,"config");
            if(Directory.Exists(config)) {
                string prefix=config+Path.DirectorySeparatorChar;
                foreach(string file in Safety.Tree(config)) {
                    string target=Safety.Child(keepConfig,file.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar,'/'));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    string hash=FsOps.CopyHashed(file,target,CancellationToken.None);
                    if(!Safety.Same(Hashes.FileHash(target),hash)) throw new InvalidOperationException("Your settings could not be set aside. Your saves were restored.");
                }
                FsOps.DeleteTree(config);
            }
            CopyVerified(backups.PathOf(id),manifest,"config",config);
            return new RestoreResult { BackupId=id, MovedAsideTo=aside, Message="Your saves are back to backup "+id+". Progress since the update is lost. The replaced saves are kept in "+aside+"." };
        }
    }
}

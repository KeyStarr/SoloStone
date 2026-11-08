using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace SoloStone
{
    public sealed class TargetFacts
    {
        public string Target, Root, Source, SaveRoot, LinkProblem;
        public IEnumerable<string> Registered=new string[0];
        public Func<string,string> Resolve=PhysicalPaths.Resolve;
        public bool TargetIsFile, TargetHasContent, StageExists, StageOwned;
        public long FreeBytes, RequiredBytes; public int LongestRelative;
    }
    public static class TargetCheck
    {
        public const long Headroom=512L*1024*1024;
        public const int MaxPath=259;
        // Pure: wizard step 3 shows this sentence live. Null means the folder is usable.
        public static string Problem(TargetFacts f) {
            string spelled=Safety.ClientPathProblem(f.Target,f.Root,f.Source,f.SaveRoot);
            if(spelled!=null) return spelled;
            if(f.LinkProblem!=null) return f.LinkProblem;
            string target=Safety.Canonical(f.Target);
            try {
                // Resolved paths look like \\?\Volume{...}\x, which the spelling rules rightly refuse, so these compare by containment only.
                string physical=Safety.OverlapProblem(f.Resolve(target),f.Resolve(Safety.Canonical(f.Root)),string.IsNullOrEmpty(f.Source)?null:f.Resolve(Safety.Canonical(f.Source)),string.IsNullOrEmpty(f.SaveRoot)?null:f.Resolve(Safety.Canonical(f.SaveRoot)));
                if(physical!=null) return physical;
                Safety.DestinationLocations(target,f.Registered,f.Resolve);
            } catch(InvalidOperationException e) { return e.Message; }
            if(f.TargetIsFile) return "That path is a file. Choose a folder.";
            if(f.TargetHasContent) return "That folder already contains files. Choose an empty folder or a new one.";
            if(f.StageExists && !f.StageOwned) return "A folder named "+Path.GetFileName(InstallRecord.StageOf(target))+" already sits next to that location and SoloStone does not own it. Move it away or choose another folder.";
            if(f.LongestRelative>0 && target.Length+".solostone-staging".Length+1+f.LongestRelative>MaxPath) return "That folder's path is too long for the game's deepest file. Choose a shorter path.";
            if(f.FreeBytes<f.RequiredBytes) return "Not enough available space: "+Gb(f.RequiredBytes)+" needed, "+Gb(Math.Max(0,f.FreeBytes))+" available on that drive.";
            return null;
        }
        static string Gb(long bytes) => (bytes/(1024.0*1024*1024)).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" GB";
        public static TargetFacts Gather(string target,string root,string source,IEnumerable<string> registered,string saveRoot,string ownerId=null) {
            var f=new TargetFacts { Target=target, Root=root, Source=source, SaveRoot=saveRoot, Registered=registered };
            if(Safety.ClientPathProblem(target,root,source,saveRoot)!=null) return f;
            string full=Safety.Canonical(target);
            if(!string.IsNullOrEmpty(source)) source=Safety.Canonical(source);
            try {
                Safety.NoLinks(full);
                f.TargetIsFile=File.Exists(full);
                f.TargetHasContent=Directory.Exists(full) && Directory.GetFileSystemEntries(full).Length!=0;
                string stage=InstallRecord.StageOf(full);
                Safety.NoLinks(stage);
                f.StageExists=File.Exists(stage) || Directory.Exists(stage);
                f.StageOwned=ownerId!=null && Directory.Exists(stage) && InstallRecord.OwnsCopy(stage,ownerId);
                if(!string.IsNullOrEmpty(source) && Directory.Exists(source)) {
                    var files=Installer.SourceFiles(source);
                    f.RequiredBytes=files.Sum(p=>new FileInfo(p).Length)+Headroom;
                    f.LongestRelative=files.Select(p=>p.Length-source.Length-1).DefaultIfEmpty(0).Max();
                }
                f.FreeBytes=new DriveInfo(Path.GetPathRoot(full)).AvailableFreeSpace;
            }
            catch(InvalidOperationException e) { f.LinkProblem=e.Message; }
            catch(IOException) { f.LinkProblem="That folder could not be read. Choose another folder."; }
            catch(UnauthorizedAccessException) { f.LinkProblem="Windows denied access to that folder. Choose a folder your account owns."; }
            catch(ArgumentException) { f.LinkProblem="That drive could not be read. Choose a local drive folder."; }
            return f;
        }
        public static string SuggestedFolder(string target,string ownerId=null) {
            if(string.IsNullOrWhiteSpace(target)) return target;
            string full=Safety.Canonical(target);
            Safety.NoLinks(full);
            if(!Directory.Exists(full) || string.Equals(Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)),"SoloStone",StringComparison.OrdinalIgnoreCase)) return target;
            if(ownerId!=null && InstallRecord.OwnsCopy(full,ownerId)) return target;
            return Directory.GetFileSystemEntries(full).Length==0 ? target : Path.Combine(target,"SoloStone");
        }
        public static string Run(string target,string root,string source,IEnumerable<string> registered,string saveRoot,string ownerId=null)
            => Problem(Gather(target,root,source,registered,saveRoot,ownerId));
    }
    public enum SetupStep { CheckClient, CheckTarget, CopyClient, VerifyCopy, InstallMods, Finish }
    public sealed class SetupProgress
    {
        public SetupStep Step; public string Message; public long Done, Total;
    }
    public sealed class SetupOptions
    {
        public string Source, Root, ClientPath, Bundle, SaveRoot;
        public IEnumerable<string> Registered=new string[0];
        public Func<ClientFacts,Verdict> Check;
        public bool AcceptedWarning;
        public Action<SetupProgress> Progress=_=>{};
        public CancellationToken Cancel;
    }
    // A live update holds the lease. Setup shows its own sentence for this exit code.
    public sealed class SetupBusyException : InvalidOperationException { public SetupBusyException(string message) : base(message) { } }
    public sealed class SetupOccupiedException : InvalidOperationException { public SetupOccupiedException(string message) : base(message) { } }
    public static class Installer
    {
        public static string DefaultClient(string root) => Path.Combine(root,"client");
        public static void Preflight(string root,string app,IEnumerable<string> registered,bool uninstall) {
            root=Safety.Canonical(root); app=Safety.Canonical(app);
            Safety.Destination(root,registered); Safety.NoLinks(app);
            if(!Safety.Same(app,Path.Combine(root,"app")))
                throw new InvalidOperationException("The actual app destination must be the SoloStone app folder.");
            // Check every descendant before Inno extracts or deletes even its first file.
            string[] files=Directory.Exists(app) ? Safety.Tree(app).ToArray() : new string[0];
            if(!uninstall) {
                Safety.NoLinks(Path.Combine(root,"install.json"));
                Safety.NoLinks(Path.Combine(root,"install.pending.json"));
                Safety.NoLinks(Path.Combine(root,"install.pending.json.tmp"));
                if(!File.Exists(Path.Combine(root,"install.json")) && !File.Exists(Path.Combine(root,"install.pending.json")) && !File.Exists(Path.Combine(root,"install.pending.json.tmp")) && Directory.Exists(app) && files.Length!=0) {
                    try { AppOnlyOwned(root,app); return; }
                    catch(InvalidOperationException e) { throw new SetupOccupiedException("The app destination already contains files or a previous installation. Existing content will not be overwritten. " + e.Message); }
                }
                if(files.Length!=0 || (Directory.Exists(app) && Directory.GetFileSystemEntries(app).Length!=0)
                    || File.Exists(app) || File.Exists(Path.Combine(root,"install.json")) || File.Exists(Path.Combine(root,"install.pending.json")) || File.Exists(Path.Combine(root,"install.pending.json.tmp")))
                    throw new SetupOccupiedException("The app destination already contains files or a previous installation. Existing content will not be overwritten. " + RecoveryStep(root));
                return;
            }
            if(!File.Exists(Path.Combine(root,"install.json"))) { AppOnlyOwned(root,app); return; }
            var record=InstallRecord.Load(root); Safety.Destinations(record,registered);
            string marker=Path.Combine(app,".solostone-app"); Safety.NoLinks(marker);
            if(!File.Exists(marker) || new FileInfo(marker).Length>64 || File.ReadAllText(marker).Trim()!=record.InstallId)
                throw new InvalidOperationException("App ownership could not be verified. Uninstall stopped; existing content was preserved. " + RecoveryStep(root));
        }
        // No install record: the player closed the wizard, or removed a partial copy. The app is still ours if it holds a verified bundle.
        static void AppOnlyOwned(string root,string app) {
            string incomplete="Installation is incomplete. " + RecoveryStep(root);
            if(!Directory.Exists(app) || !File.Exists(Path.Combine(app,"SoloStone.exe")) || !File.Exists(Path.Combine(app,"payload.json"))) throw new InvalidOperationException(incomplete);
            Payload payload;
            try { payload=Json.Read<Payload>(Path.Combine(app,"payload.json")); payload.Verify(Path.Combine(app,"payload")); }
            catch(InvalidOperationException) { throw new InvalidOperationException(incomplete); }
            string pending=Path.Combine(root,"install.pending.json"), marker=Path.Combine(app,".solostone-app");
            Safety.NoLinks(marker); Safety.NoLinks(pending);
            if(File.Exists(marker) && File.Exists(pending) && File.ReadAllText(marker).Trim()!=InstallRecord.ReadRecord(root,"install.pending.json").InstallId)
                throw new InvalidOperationException("App ownership could not be verified. Existing content was preserved. " + RecoveryStep(root));
        }
        // Uninstall with no install record. A partial copy without our marker stays where it is; it was never ours to delete.
        public static void UninstallAppOnly(string root,IEnumerable<string> registered) {
            root=Safety.Canonical(root); var folders=registered.ToArray();
            string app=Path.Combine(root,"app");
            if(File.Exists(Path.Combine(root,"install.json"))) throw new InvalidOperationException("SoloStone has a full installation here. Use the normal uninstall.");
            Preflight(root,app,folders,true);
            if(File.Exists(Path.Combine(root,"install.pending.json"))) {
                try { RemovePartial(root,folders); }
                catch(InvalidOperationException) { File.Delete(Path.Combine(root,"install.pending.json")); }
            }
            foreach(string name in new[]{"updates","releases","state"}) FsOps.DeleteTree(Path.Combine(root,name));
        }
        static bool Excluded(string relative) {
            string first=relative.Split('/')[0]; string name=Path.GetFileName(relative);
            return new[]{"BepInEx","Logs","reports"}.Any(v=>Safety.Same(first,v))
                || name.EndsWith(".log",StringComparison.OrdinalIgnoreCase)
                || new[]{"client.config","winhttp.dll","doorstop_config.ini",".doorstop_version",InstallRecord.Marker,".solostone-app"}.Any(v=>Safety.Same(name,v));
        }
        public static string[] SourceFiles(string source) {
            string prefix=source+Path.DirectorySeparatorChar;
            return Safety.Tree(source).Where(p=>!Excluded(p.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar,'/'))).ToArray();
        }
        public static string RecoveryStep(string root) => InstallRecord.RecoveryStep(root);
        static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
        static void Say(Action<SetupProgress> progress,SetupStep step,string message,long done=0,long total=0)
            => progress(new SetupProgress { Step=step, Message=message, Done=done, Total=total });
        static Payload BundlePayload(string bundle) {
            var payload=Json.Read<Payload>(Path.Combine(bundle,"payload.json")); payload.Verify(Path.Combine(bundle,"payload"));
            string problem=PayloadRules.Problem(payload.Files.Keys,null);
            if(problem!=null) throw new InvalidOperationException(problem);
            return payload;
        }
        // Everything before the first write refuses with the destination untouched, so wizard Cancel leaves nothing behind.
        public static InstallRecord Setup(SetupOptions o) {
            string source=Safety.Canonical(o.Source), root=Safety.Canonical(o.Root), client=Safety.Canonical(o.ClientPath??DefaultClient(root));
            var registered=o.Registered.ToArray();
            Safety.Pair(source,root,registered);
            if(new[]{"install.json","install.pending.json","install.pending.json.tmp"}.Any(name=>Exists(Path.Combine(root,name))))
                throw new InvalidOperationException("SoloStone is already installed or a previous copy is incomplete. " + RecoveryStep(root));
            if(Exists(Path.Combine(root,"app",".solostone-app"))) throw new InvalidOperationException("An app ownership marker already exists. " + RecoveryStep(root));
            Say(o.Progress,SetupStep.CheckClient,"Checking your client…");
            var facts=ClientFacts.Read(source);
            var verdict=o.Check(facts);
            if(verdict.Kind==VerdictKind.Block) throw new InvalidOperationException(verdict.Message);
            if(verdict.Kind==VerdictKind.Warning && !o.AcceptedWarning) throw new InvalidOperationException(verdict.Message+" Confirm the warning to continue.");
            var payload=BundlePayload(o.Bundle);
            // The copy gets its own company name in two data files. A source that doesn't hold exactly one old name in each is refused with nothing written.
            string identity=ClientIdentity.SourceProblem(source);
            if(identity!=null) throw new InvalidOperationException(identity+" No files were changed.");
            Say(o.Progress,SetupStep.CheckTarget,"Checking the copy location…");
            string problem=TargetCheck.Run(client,root,source,registered,o.SaveRoot);
            if(problem!=null) throw new InvalidOperationException(problem);
            o.Cancel.ThrowIfCancellationRequested();
            var record=new InstallRecord { Schema=InstallRecord.CurrentSchema, InstallId=Guid.NewGuid().ToString("N"), Root=root, Source=source, ClientPath=client,
                ClientVersion=facts.Version, ReleaseVersion=ReleaseInfo.TryRead(o.Bundle)?.Version, Files=new Dictionary<string,string>(facts.Files), Payload=payload };
            // Publish recovery ownership before creating a stage or copying any client bytes.
            Directory.CreateDirectory(root);
            try {
                Json.Save(Path.Combine(root,"install.pending.json"),record);
                return Complete(record,o.Bundle,registered,o.SaveRoot,o.Progress,o.Cancel);
            } catch(Exception error) when(error is IOException || error is UnauthorizedAccessException || error is InvalidOperationException) {
                throw new InvalidOperationException("Setup stopped; files and saves were preserved. " + RecoveryStep(root),error);
            }
        }
        static void AppOwnership(InstallRecord record) {
            string app=Path.Combine(record.Root,"app");
            if(!Directory.Exists(app)) return; // Unpacked launcher.
            string marker=Path.Combine(app,".solostone-app"); Safety.NoLinks(marker);
            if(File.Exists(marker)) {
                if(new FileInfo(marker).Length>64 || File.ReadAllText(marker).Trim()!=record.InstallId)
                    throw new InvalidOperationException("App ownership marker does not match. Preserve this root and rerun the installer at a fresh root.");
                return;
            }
            // Interrupted installs publish app ownership last. Adopt only their matching verified bundle.
            Safety.Tree(app).ToArray();
            record.Payload.Verify(Path.Combine(app,"payload"));
            File.WriteAllText(marker,record.InstallId);
        }
        static InstallRecord Complete(InstallRecord record,string bundle,string[] registered,string saveRoot,Action<SetupProgress> progress,CancellationToken cancel) {
            Safety.Pair(record.Source,record.Root,registered); // Only a recopy needs the source. A finished copy works without it.
            BundlePayload(bundle); record.Payload.Verify(Path.Combine(bundle,"payload"));
            Hashes.Verify(record.Source,record.Files);
            // A recopy never saw setup's check. A source that changed since is refused here, before any stage exists; recovery has already removed an old one, so no claim about it.
            string identity=ClientIdentity.SourceProblem(record.Source);
            if(identity!=null) throw new InvalidOperationException(identity+" Your own Hearthstone was not changed and no new copy was started.");
            string prefix=record.Source+Path.DirectorySeparatorChar, stage=record.Stage;
            Safety.NoLinks(stage);
            if(Exists(stage)) throw new InvalidOperationException("A client copy already exists. " + RecoveryStep(record.Root));
            string problem=TargetCheck.Run(record.Client,record.Root,record.Source,registered,saveRoot);
            if(problem!=null) throw new InvalidOperationException(problem+" Then " + RecoveryStep(record.Root));
            AppOwnership(record);
            var files=SourceFiles(record.Source);
            long total=files.Sum(p=>new FileInfo(p).Length), done=0;
            Directory.CreateDirectory(stage); File.WriteAllText(Path.Combine(stage,InstallRecord.Marker),record.InstallId);
            for(int i=0;i<files.Length;i++) {
                cancel.ThrowIfCancellationRequested();
                string relative=files[i].Substring(prefix.Length).Replace(Path.DirectorySeparatorChar,'/');
                Say(progress,SetupStep.CopyClient,"Copying and verifying client files " + (i+1) + " of " + files.Length + "…",done,total);
                string target=Safety.Child(stage,relative); Directory.CreateDirectory(Path.GetDirectoryName(target));
                var before=new FileInfo(files[i]); long length=before.Length; DateTime written=before.LastWriteTimeUtc;
                string hash=FsOps.CopyHashed(files[i],target,cancel,n=>done+=n);
                before.Refresh();
                if(before.Length!=length || before.LastWriteTimeUtc!=written || Hashes.FileHash(target)!=hash) throw new InvalidOperationException("Client changed during copying. Quit Battle.net. " + RecoveryStep(record.Root));
                if(record.Files.ContainsKey(relative) && !Safety.Same(record.Files[relative],hash)) throw new InvalidOperationException("Client changed during copying. Quit Battle.net. " + RecoveryStep(record.Root));
                if(relative.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)) record.Files[relative]=hash;
            }
            Say(progress,SetupStep.VerifyCopy,"Verifying the copy…",total,total);
            Hashes.Verify(record.Source,record.Files); Hashes.Verify(stage,record.Files);
            cancel.ThrowIfCancellationRequested();
            // Before the payload: recovery calls a stage with a good payload complete, so the patch must already be in it.
            record.IdentityFiles=ClientIdentity.Apply(stage);
            Say(progress,SetupStep.InstallMods,"Installing verified offline loader and plugin…");
            foreach(var row in record.Payload.Files) {
                string target=Safety.Child(stage,row.Key); Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Safety.Child(Path.Combine(bundle,"payload"),row.Key),target,false);
            }
            record.Payload.Verify(stage);
            Say(progress,SetupStep.Finish,"Finishing…");
            Json.Save(Path.Combine(record.Root,"install.pending.json"),record);
            if(Directory.Exists(record.Client)) Directory.Delete(record.Client); // The target check proved it empty.
            Directory.Move(stage,record.Client);
            File.Move(Path.Combine(record.Root,"install.pending.json"),Path.Combine(record.Root,"install.json"));
            return record;
        }
        public static InstallRecord Recover(string root,string bundle,IEnumerable<string> registered,string saveRoot,Action<SetupProgress> progress,CancellationToken cancel=default(CancellationToken)) {
            root=Safety.Canonical(root); var folders=registered.ToArray(); Safety.Destination(root,folders);
            bool committed=File.Exists(Path.Combine(root,"install.json"));
            string pending=Path.Combine(root,"install.pending.json");
            if(committed && File.Exists(pending)) throw new InvalidOperationException("Conflicting installation records. Preserve this root and rerun the installer at a fresh root.");
            if(!committed && !File.Exists(pending)) throw new InvalidOperationException("No ownership record exists. Close SoloStone, move the entire root (your saves are in its saves folder: copy that folder out first, and back afterwards) to an unused sibling folder, then rerun the installer at the original root (see INSTALL-prototype.md). No files were changed.");
            var record=InstallRecord.ReadRecord(root,committed?"install.json":"install.pending.json");
            Safety.Destinations(record,folders);
            Safety.Tree(root).ToArray();
            string stage=record.Stage;
            bool hasClient=Exists(record.Client) && (committed || File.Exists(record.Client) || Directory.GetFileSystemEntries(record.Client).Length!=0);
            if(Exists(stage) && hasClient) throw new InvalidOperationException("Both the client copy and its staging folder exist. Preserve this root and rerun the installer at a fresh root.");
            string copy=hasClient?record.Client:stage;
            if((committed && !Directory.Exists(record.Client)) || (Exists(copy) && !Directory.Exists(copy)))
                throw new InvalidOperationException("Client directory ownership could not be verified. Close SoloStone, move the entire root (your saves are in its saves folder: copy that folder out first, and back afterwards) to an unused sibling folder, then rerun the installer at the original root. No files were changed.");
            string done="Recovery complete. Run SoloStone.exe firewall --apply --root " + FirewallPlan.Quote(root) + ", then launch; or run uninstall with the same --root.";
            if(Directory.Exists(copy)) {
                Safety.Tree(copy).ToArray();
                if(!InstallRecord.OwnsCopy(copy,record.InstallId))
                    throw new InvalidOperationException("Copy ownership could not be verified for "+copy+". Close SoloStone, move the entire root (your saves are in its saves folder: copy that folder out first, and back afterwards) to an unused sibling folder, then rerun the installer at the original root. No files were changed.");
                bool complete=true;
                try { Hashes.Verify(copy,record.Files); record.Payload.Verify(copy); ClientIdentity.Verify(copy,record.IdentityFiles); }
                catch(InvalidOperationException) { complete=false; }
                if(complete) {
                    AppOwnership(record);
                    // Every copied EXE needs a recorded firewall rule, including ones a pending record has not listed yet.
                    foreach(string exe in Safety.Tree(copy).Where(path=>path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)))
                        record.Files[exe.Substring(copy.Length+1).Replace(Path.DirectorySeparatorChar,'/')]=Hashes.FileHash(exe);
                    Json.Save(committed?Path.Combine(root,"install.json"):pending,record);
                    if(copy==stage) { if(Directory.Exists(record.Client)) Directory.Delete(record.Client); Directory.Move(stage,record.Client); }
                    if(!committed) File.Move(pending,Path.Combine(root,"install.json"));
                    Say(progress,SetupStep.Finish,done);
                    return record;
                }
                if(copy==record.Client || committed) throw new InvalidOperationException("Committed client integrity failed. No files were changed. Run SoloStone.exe uninstall --root " + FirewallPlan.Quote(root) + "; saves stay. Then rerun the installer.");
            }
            // An owned incomplete stage holds only recopyable bytes. Keeping it would strand 12 GB.
            if(Directory.Exists(stage)) FsOps.DeleteTree(stage);
            try {
                var result=Complete(record,bundle,folders,saveRoot,progress,cancel);
                Say(progress,SetupStep.Finish,done);
                return result;
            } catch(Exception error) when(error is IOException || error is UnauthorizedAccessException || error is InvalidOperationException) {
                throw new InvalidOperationException("Recovery stopped; files and saves were preserved. " + RecoveryStep(root),error);
            }
        }
        // Wizard's "Remove partial copy": only an uncommitted record and a stage carrying its marker.
        public static void RemovePartial(string root,IEnumerable<string> registered) {
            root=Safety.Canonical(root); Safety.Destination(root,registered);
            string pending=Path.Combine(root,"install.pending.json");
            if(File.Exists(Path.Combine(root,"install.json"))) throw new InvalidOperationException("SoloStone is fully installed. Use uninstall instead.");
            if(!File.Exists(pending)) return;
            var record=InstallRecord.ReadRecord(root,"install.pending.json");
            string stage=record.Stage; Safety.NoLinks(stage);
            if(Exists(stage)) {
                if(!Directory.Exists(stage) || !InstallRecord.OwnsCopy(stage,record.InstallId))
                    throw new InvalidOperationException("The partial copy at "+stage+" does not carry SoloStone's marker. It was left in place.");
                FsOps.DeleteTree(stage);
            }
            // Setup writes this before copying. Left behind, it blocks the next setup, recovery and Inno's checks.
            string marker=Path.Combine(root,"app",".solostone-app"); Safety.NoLinks(marker);
            if(File.Exists(marker) && File.ReadAllText(marker).Trim()==record.InstallId) File.Delete(marker);
            File.Delete(pending);
        }
        // A launcher can't delete the folder it runs from, and by then the journal may be gone. Null when this place is safe.
        public static string UninstallProblem(string root,string ownFolder) {
            foreach(string name in new[]{"updates","releases"})
                if(Safety.Within(ownFolder,Path.Combine(root,name))) return "SoloStone can't uninstall itself from "+name+". Use Apps & Features in Windows Settings.";
            return null;
        }
        // Setup's own launcher finishing an update a kill cut short. A live helper holds the lease, and undoing beside it would mix two versions.
        public static void ResumeInterrupted(string root,Func<UpdateEngine> engine) {
            if(!File.Exists(Path.Combine(Safety.Canonical(root),"updates","journal.json"))) return;
            IDisposable lease;
            try { lease=Lease.Open(root); }
            catch(InvalidOperationException) { throw new SetupBusyException("SoloStone is finishing an update. Wait for its window to close, then run setup again."); }
            using(lease) engine().Resume();
        }
        // The options folder of our -uid and nothing else: exactly this path, never its parent or another uid, which belong to the player's own Hearthstone or the lab.
        public static void RemoveGameOptions(string localData) {
            string folder=Path.Combine(localData,"Blizzard","Hearthstone",ClientIdentity.GameUid);
            Safety.NoLinks(folder); FsOps.DeleteTree(folder);
        }
        // Everything the account-level removal would refuse (a link in either folder, or inside), found before the caller removes firewall rules or anything else.
        public static void PrecheckAccountTraces(string localData,IUserRegistry registry,string localLow) {
            if(localData==null) return;
            string options=Path.Combine(localData,"Blizzard","Hearthstone",ClientIdentity.GameUid);
            Safety.NoLinks(options); if(Directory.Exists(options)) Safety.Tree(options).ToArray();
            if(registry!=null && localLow!=null) ClientIdentity.Precheck(localLow);
        }
        // Removes what SoloStone owns; the marker goes last so an interrupted uninstall can run again.
        // registry and localLow (the account's own HKCU and LocalLow) are used only when localData is: the three belong to the account running this, never one named in a record.
        public static void Uninstall(string root,IEnumerable<string> registered,string localData=null,IUserRegistry registry=null,string localLow=null) {
            root=Safety.Canonical(root); var folders=registered.ToArray();
            if(!File.Exists(Path.Combine(root,"install.json"))) throw new InvalidOperationException("SoloStone is not installed here. " + RecoveryStep(root));
            var record=InstallRecord.Load(root); Safety.Destinations(record,folders);
            Safety.Tree(record.Client).ToArray();
            PrecheckAccountTraces(localData,registry,localLow);
            if(localData!=null) {
                RemoveGameOptions(localData);
                if(registry!=null && localLow!=null) ClientIdentity.RemoveTraces(registry,localLow);
            }
            foreach(string name in new[]{"updates","releases","state"}) FsOps.DeleteTree(Path.Combine(root,name));
            string stage=record.Stage; Safety.NoLinks(stage);
            if(Directory.Exists(stage) && InstallRecord.OwnsCopy(stage,record.InstallId)) FsOps.DeleteTree(stage);
            string marker=Path.Combine(record.Client,InstallRecord.Marker);
            foreach(string entry in Directory.GetFileSystemEntries(record.Client))
                if(!Safety.Same(entry,marker)) FsOps.DeleteTree(entry);
            File.Delete(marker); Directory.Delete(record.Client);
            File.Delete(Path.Combine(root,"install.json"));
        }
        // Explicit "delete saves" only. Backups hold the same progress, so they go with it.
        public static void DeleteSaves(string root,string saveRoot) {
            string saves=Safety.Canonical(saveRoot); Safety.NoLinks(saves);
            FsOps.DeleteTree(saves);
            FsOps.DeleteTree(Path.Combine(Safety.Canonical(root),"backups"));
        }
    }
}

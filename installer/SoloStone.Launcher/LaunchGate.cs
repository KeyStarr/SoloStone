using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace SoloStone
{
    [DataContract] public sealed class AcceptedWarnings
    {
        [DataMember(Name="keys")] public List<string> Keys;
    }
    public static class LaunchGate
    {
        static string PathOf(string root) => Path.Combine(root,"state","accepted-warnings.json");
        public static bool Accepted(string root,ClientFacts facts,string release) => (Atomic.TryRead<AcceptedWarnings>(PathOf(root))?.Keys??new List<string>()).Contains(Key(facts,release));
        static string Key(ClientFacts facts,string release) => facts.HashSetId()+"|"+(release??"unknown");
        // Returns the grant's verdict word. A Warning needs explicit acceptance. Any client that is not exactly a tested one backs up saves once per client and release,
        // before the game starts: a block in the plugin comes later and leaves the save tree untouched, and only live play finds changed behaviour.
        public static string Admit(string root,Verdict verdict,ClientFacts facts,string release,bool acceptedWarning,Action backup) {
            if(verdict.Kind==VerdictKind.Block) throw new InvalidOperationException(verdict.Message);
            if(verdict.Kind==VerdictKind.Allow && verdict.Exact) return "allow";
            if(verdict.Kind==VerdictKind.Warning && !acceptedWarning) throw new InvalidOperationException(verdict.Message+" Confirm the warning to play.");
            if(!Accepted(root,facts,release)) {
                backup();
                var state=Atomic.TryRead<AcceptedWarnings>(PathOf(root))??new AcceptedWarnings();
                state.Keys=state.Keys??new List<string>(); state.Keys.Add(Key(facts,release));
                Atomic.Save(PathOf(root),state);
            }
            return verdict.Kind==VerdictKind.Allow?"allow":"warning";
        }
    }
    // What the plugin's own checks said, from reports\client-check.txt. Only this launch's file counts.
    public static class ClientCheckFile
    {
        public const string FileName="client-check.txt";
        static string[] Lines(string reports,string run) {
            try {
                string path=Path.Combine(reports,FileName);
                Safety.NoLinks(path);
                if(!File.Exists(path) || new FileInfo(path).Length>8192) return null;
                var lines=File.ReadAllLines(path);
                return lines.Contains("runId="+run)?lines:null;
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException) { return null; }
        }
        public static string BlockDetail(string reports,string run) {
            var lines=Lines(reports,run);
            if(lines==null || !lines.Contains("state=refused")) return null;
            string detail=lines.FirstOrDefault(l=>l.StartsWith("detail=",StringComparison.Ordinal))?.Substring(7);
            return string.IsNullOrWhiteSpace(detail)?null:detail;
        }
        public static bool Holding(string reports,string run) => Lines(reports,run)?.Contains("state=held")==true;
    }
    public static class CheckClient
    {
        // check-client with --source reads a folder for the wizard. Without it, the installed copy, which is what a staged launcher checks.
        public static Verdict Run(string root,string source,TestedClient compiled,UpdateConfig config,IBindingCheck binding,IEnumerable<string> safetyFailures) {
            var safety=(safetyFailures??new string[0]).ToList();
            string folder=source;
            if(folder==null) {
                try { folder=InstallRecord.Load(root).Client; }
                catch(Exception e) when(e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException) { return ClientPolicy.Blocked(e is InvalidOperationException?e.Message:"The SoloStone installation could not be read.",compiled.Version); }
            }
            try { folder=Safety.Canonical(folder); Safety.Tree(folder).Count(); }
            catch(Exception e) when(e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException) { return ClientPolicy.Blocked(e is InvalidOperationException?e.Message:"That folder could not be read.",compiled.Version); }
            var facts=ClientFacts.Read(folder);
            var verdict=ClientGate.Evaluate(facts,compiled,new ManifestStore(root,config).Current(),binding,safety);
            // The player's own folder, before the Install step: setup would refuse it there, so say so now. Not for the installed copy, which is patched on purpose.
            if(source!=null && verdict.Kind!=VerdictKind.Block) {
                string identity=ClientIdentity.SourceProblem(folder);
                if(identity!=null) return ClientPolicy.BlockedWith(identity,compiled.Version,facts.Version);
            }
            return verdict;
        }
    }
    [DataContract] public sealed class StatusReport
    {
        [DataMember(Name="schema")] public int Schema=1;
        [DataMember(Name="installed")] public bool Installed;
        [DataMember(Name="installProblem")] public string InstallProblem;
        [DataMember(Name="clientPath")] public string ClientPath;
        [DataMember(Name="clientVersion")] public string ClientVersion;
        [DataMember(Name="testedVersion")] public string TestedVersion;
        [DataMember(Name="releaseVersion")] public string ReleaseVersion;
        [DataMember(Name="previousVersion")] public string PreviousVersion;
        [DataMember(Name="verdict")] public Verdict Verdict;
        [DataMember(Name="updatesConfigured")] public bool UpdatesConfigured;
        [DataMember(Name="checkForUpdates")] public bool CheckForUpdates;
        [DataMember(Name="manifestSeq")] public long ManifestSeq;
        [DataMember(Name="latestVersion")] public string LatestVersion;
        [DataMember(Name="heldVersion")] public string HeldVersion;
        [DataMember(Name="heldReason")] public string HeldReason;
        [DataMember(Name="journalPhase")] public string JournalPhase;
        [DataMember(Name="backups")] public string[] Backups;
        [DataMember(Name="updateNote")] public string UpdateNote;
        // Local read only: the launcher window and agents use the same facts. No network.
        public static StatusReport Build(string root,UpdateEngine engine,TestedClient compiled,UpdateConfig config,IBindingCheck binding,IEnumerable<string> safetyFailures) {
            var report=new StatusReport { TestedVersion=compiled.Version, UpdatesConfigured=config.Configured, CheckForUpdates=Settings.Load(root).CheckForUpdates };
            try {
                var record=InstallRecord.Load(root);
                report.Installed=true; report.ClientPath=record.Client; report.ClientVersion=record.ClientVersion; report.ReleaseVersion=record.ReleaseVersion;
                report.Verdict=CheckClient.Run(root,null,compiled,config,binding,safetyFailures);
                report.TestedVersion=report.Verdict.TestedVersion??compiled.Version;
            } catch(Exception e) when(e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException) { report.InstallProblem=ReadError(e); }
            var manifest=new ManifestStore(root,config).Current();
            report.ManifestSeq=manifest?.Seq??0; report.LatestVersion=manifest?.Latest.Version;
            report.UpdateNote=engine.LastNote()?.Message;
            var held=engine.Held();
            report.HeldVersion=held?.Version; report.HeldReason=held?.Reason;
            report.JournalPhase=engine.Journal?.Phase; report.PreviousVersion=engine.PreviousVersion();
            report.Backups=new Backups(root).List().Select(b=>b.Id).ToArray();
            return report;
        }
        static string ReadError(Exception e) => e is InvalidOperationException?e.Message:"The SoloStone installation could not be read.";
        public string ToJson() => System.Text.Encoding.UTF8.GetString(JsonBytes.ToBytes(this));
    }
    public enum UpdateState { NotConfigured, Off, UpToDate, Available, Held, Busy, Failed }
    public sealed class UpdateStatus
    {
        public UpdateState State; public string Installed, Message; public LatestRelease Latest;
    }
    // All network use happens here, before play. A failure never blocks the game.
    public sealed class UpdateService
    {
        readonly string root; readonly UpdateConfig config; readonly IHttp http; readonly UpdateEngine engine; readonly bool afterHelper;
        public UpdateService(string root,UpdateConfig config,IHttp http,UpdateEngine engine,bool afterHelper=false) { this.root=root; this.config=config; this.http=http; this.engine=engine; this.afterHelper=afterHelper; }
        // Null means offer it. Otherwise, why not.
        public static string OfferProblem(LatestRelease latest,string installed,HeldUpdate held) {
            if(!SemVer.Valid(installed)) return "The installed version is unknown.";
            if(!SemVer.Newer(latest.Version,installed)) return "SoloStone is up to date.";
            if(held!=null && SemVer.Valid(held.Version) && !SemVer.Newer(latest.Version,held.Version) && held.BlocksAt(DateTime.UtcNow)) return "SoloStone "+held.Version+" is on hold.";
            return null;
        }
        UpdateClient NewClient() => new UpdateClient(http,config.Repo);
        public UpdateStatus Check(CancellationToken cancel) {
            string installed=null;
            try { installed=InstallRecord.Load(root).ReleaseVersion; }
            catch(Exception e) when(e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException) { }
            if(!config.Configured) return new UpdateStatus { State=UpdateState.NotConfigured, Installed=installed, Message=UpdateConfig.NotConfigured };
            if(!Settings.Load(root).CheckForUpdates) return new UpdateStatus { State=UpdateState.Off, Installed=installed, Message="Update checks are off." };
            // A release still waiting for its first start must not block a newer, possibly fixed one. Only a swap cut short does.
            if(engine.JournalActive && engine.Journal?.Phase!=SwapJournal.Swapped) return new UpdateStatus { State=UpdateState.Busy, Installed=installed, Message="The last update is still finishing." };
            var store=new ManifestStore(root,config);
            ReleaseManifest manifest;
            try {
                var client=NewClient();
                // Optional: a repo with no rotation has no such file, and a failure here must not stop the check.
                try { store.AcceptStatement(client.FetchSigned(SigningStatement.FileName,SigningStatement.MaxBytes,cancel)); }
                catch(Exception e) when(!(e is OperationCanceledException)) { }
                var fetched=client.FetchSigned(ReleaseManifest.FileName,ReleaseManifest.MaxBytes,cancel);
                manifest=store.Accept(fetched.File,fetched.Signature,()=>client.FetchSigned(SigningStatement.FileName,SigningStatement.MaxBytes,cancel));
            }
            catch(OperationCanceledException) { throw; }
            catch(Exception e) {
                InstallerLog.Write(root,"Update check failed: "+Tls.Describe(e));
                // A forged, replayed or revoked file must not read like being offline. A WebException is an InvalidOperationException too, but it is the network.
                bool refused=e is InvalidOperationException && !(e is System.Net.WebException);
                return new UpdateStatus { State=UpdateState.Failed, Installed=installed, Message=refused?"Update refused: "+e.Message:"Couldn't check for updates." };
            }
            var held=engine.Held();
            string problem=OfferProblem(manifest.Latest,installed,held);
            // The swap ran in a helper and failed, and the helper opened this launcher. Offering it again here would loop. A start by the player is a new attempt, so a note never stops updates.
            var note=afterHelper?engine.LastNote():null;
            if(problem==null && note!=null && note.Version==manifest.Latest.Version) return new UpdateStatus { State=UpdateState.Failed, Installed=installed, Latest=manifest.Latest, Message=note.Message };
            // Failed twice: wait, and say until when. The update line shows this on every start, so updates never stop silently, and the day ends by itself.
            var attempts=engine.Attempts();
            var until=problem==null && attempts!=null && attempts.Version==manifest.Latest.Version && attempts.From==installed?attempts.WaitsUntil(DateTime.UtcNow):null;
            if(until!=null) return new UpdateStatus { State=UpdateState.Held, Installed=installed, Latest=manifest.Latest, Message="SoloStone "+manifest.Latest.Version+" could not be installed twice, so SoloStone tries again after "+until.Value.ToLocalTime().ToString("g",System.Globalization.CultureInfo.CurrentCulture)+". Your version keeps working." };
            if(problem==null) return new UpdateStatus { State=UpdateState.Available, Installed=installed, Latest=manifest.Latest, Message="SoloStone "+manifest.Latest.Version+" is available." };
            bool isHeld=held!=null && SemVer.Valid(installed) && SemVer.Newer(manifest.Latest.Version,installed);
            return new UpdateStatus { State=isHeld?UpdateState.Held:UpdateState.UpToDate, Installed=installed, Latest=manifest.Latest, Message=problem };
        }
        public UpdateResult Apply(bool acceptWarning,CancellationToken cancel) {
            var status=Check(cancel);
            if(status.State!=UpdateState.Available) return new UpdateResult { Outcome=status.State==UpdateState.Held?UpdateOutcome.Held:UpdateOutcome.Nothing, Message=status.Message };
            var latest=status.Latest;
            try {
                string zip=engine.AssetPath(latest.Version);
                if(!File.Exists(zip) || new FileInfo(zip).Length!=latest.Asset.Size || !Safety.Same(Hashes.FileHash(zip),latest.Asset.Sha256))
                    NewClient().Download(latest.Version,latest.Asset,zip,cancel);
                engine.Stage(zip,latest,cancel);
                return engine.Apply(latest,acceptWarning);
            }
            catch(OperationCanceledException) { throw; }
            catch(Exception e) when(e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException || e is System.Net.WebException) {
                return new UpdateResult { Outcome=UpdateOutcome.Failed, Version=latest.Version, Message="The update did not install. The current version keeps working. "+Tls.Describe(e) };
            }
        }
    }
}

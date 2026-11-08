using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace SoloStone
{
    public static class SemVer
    {
        static readonly Regex Shape=new Regex(@"^(0|[1-9]\d{0,5})\.(0|[1-9]\d{0,5})\.(0|[1-9]\d{0,5})(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$",RegexOptions.CultureInvariant);
        public static bool Valid(string value) => value!=null && value.Length<=64 && Shape.IsMatch(value);
        public static bool Newer(string candidate,string than) => Compare(candidate,than)>0;
        public static int Compare(string a,string b) {
            if(!Valid(a) || !Valid(b)) throw new InvalidOperationException("Invalid version number.");
            var left=Shape.Match(a); var right=Shape.Match(b);
            for(int i=1;i<=3;i++) {
                int order=int.Parse(left.Groups[i].Value).CompareTo(int.Parse(right.Groups[i].Value));
                if(order!=0) return order;
            }
            string[] l=a.Contains("-")?a.Substring(a.IndexOf('-')+1).Split('.'):null, r=b.Contains("-")?b.Substring(b.IndexOf('-')+1).Split('.'):null;
            if(l==null || r==null) return l==null?(r==null?0:1):-1; // A prerelease sorts before its release.
            for(int i=0;i<l.Length && i<r.Length;i++) {
                bool ln=l[i].All(char.IsDigit), rn=r[i].All(char.IsDigit);
                int order=ln&&rn ? (l[i].Length!=r[i].Length ? l[i].Length.CompareTo(r[i].Length) : string.CompareOrdinal(l[i],r[i]))
                    : ln?-1 : rn?1 : string.CompareOrdinal(l[i],r[i]);
                if(order!=0) return Math.Sign(order);
            }
            return l.Length.CompareTo(r.Length);
        }
    }
    // Keys and repo are build inputs. Without them, updates stay off and everything else works.
    public sealed class UpdateConfig
    {
        public static readonly Regex RepoShape=new Regex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,99}/[A-Za-z0-9][A-Za-z0-9._-]{0,99}$",RegexOptions.CultureInvariant);
        public MinisignPublicKey Root, Signing; public string Repo; public long SeqFloor; public bool Configured;
        public string Problem;
        public const string NotConfigured="Updates are not configured in this build. Everything else works.";
        public static UpdateConfig Compiled() => From(BuildInputs.RootPublicKey,BuildInputs.SigningPublicKey,BuildInputs.Repo,BuildInputs.SeqFloor);
        public static UpdateConfig From(string root,string signing,string repo,long floor) {
            var config=new UpdateConfig { SeqFloor=floor };
            if(string.IsNullOrEmpty(root) || string.IsNullOrEmpty(signing) || string.IsNullOrEmpty(repo)) { config.Problem=NotConfigured; return config; }
            try {
                config.Root=MinisignPublicKey.Parse(root); config.Signing=MinisignPublicKey.Parse(signing); config.Repo=repo;
                if(!RepoShape.IsMatch(repo) || repo.Contains("..") || config.Root.KeyIdHex==config.Signing.KeyIdHex) { config.Problem=NotConfigured; return config; }
                config.Configured=true;
            } catch(InvalidOperationException) { config.Problem=NotConfigured; }
            return config;
        }
    }
    [DataContract] public sealed class ReleaseAsset
    {
        [DataMember(Name="name")] public string Name;
        [DataMember(Name="size")] public long Size;
        [DataMember(Name="sha256")] public string Sha256;
    }
    [DataContract] public sealed class LatestRelease
    {
        [DataMember(Name="version")] public string Version;
        [DataMember(Name="saveSchema")] public int SaveSchema;
        [DataMember(Name="asset")] public ReleaseAsset Asset;
    }
    [DataContract] public sealed class ReleaseManifest
    {
        public const int MaxBytes=256*1024; public const long MaxAssetBytes=1024L*1024*1024; public const string FileName="release-manifest.json";
        static readonly Regex AssetName=new Regex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,99}\.zip$",RegexOptions.CultureInvariant);
        public static bool ValidAssetName(string name) => name!=null && AssetName.IsMatch(name) && !name.Contains("..");
        [DataMember(Name="schema")] public int Schema;
        [DataMember(Name="seq")] public long Seq;
        [DataMember(Name="latest")] public LatestRelease Latest;
        [DataMember(Name="testedClients")] public TestedClient[] TestedClients;
        [DataMember(Name="knownBad")] public KnownBadClient[] KnownBad;
        // Everything read from here is typed and bounded. It carries no URLs, code or text that players see.
        public void Validate() {
            var latest=Latest; var asset=latest?.Asset;
            if(Schema!=1 || Seq<0 || latest==null || asset==null || !SemVer.Valid(latest.Version) || latest.SaveSchema<=0
                || !ValidAssetName(asset.Name) || asset.Size<=0 || asset.Size>MaxAssetBytes || asset.Sha256==null || asset.Sha256.Length!=64 || !asset.Sha256.All(Uri.IsHexDigit))
                throw new InvalidOperationException("Release manifest is invalid.");
            TestedClients=TestedClients??new TestedClient[0]; KnownBad=KnownBad??new KnownBadClient[0];
            if(TestedClients.Length>256 || KnownBad.Length>256
                || TestedClients.Any(t=>t==null || !ClientVersion.IsVersion(t.Version) || !ClientPolicy.ValidFiles(t.Files))
                || KnownBad.Any(b=>b==null || !ClientPolicy.ValidFiles(b.Files)))
                throw new InvalidOperationException("Release manifest is invalid.");
        }
    }
    [DataContract] public sealed class SigningStatement
    {
        public const int MaxBytes=16*1024; public const string FileName="signing-key.json";
        [DataMember(Name="schema")] public int Schema;
        [DataMember(Name="seq")] public long Seq;
        [DataMember(Name="signingKey")] public string SigningKey;
        [DataMember(Name="revoked")] public string[] Revoked;
    }
    [DataContract] public sealed class SignedFile
    {
        [DataMember(Name="file")] public byte[] File;
        [DataMember(Name="signature")] public byte[] Signature;
    }
    [DataContract] public sealed class SeqFloors
    {
        [DataMember(Name="manifest")] public long Manifest;
        [DataMember(Name="signingKey")] public long SigningKey;
    }
    // Everything verified lives in Root\state. A failure at any step keeps the last verified or compiled values.
    public sealed class ManifestStore
    {
        public const long MaxSeqJump=1000;
        readonly string state; readonly UpdateConfig config;
        public ManifestStore(string root,UpdateConfig config) { state=Path.Combine(root,"state"); this.config=config; }
        string ManifestCache => Path.Combine(state,"manifest-cache.json");
        string SigningCache => Path.Combine(state,"signing-cache.json");
        string FloorFile => Path.Combine(state,"seq.json");
        public long ManifestFloor => Math.Max(config.SeqFloor,Atomic.TryRead<SeqFloors>(FloorFile)?.Manifest??0);
        long SigningFloor => Atomic.TryRead<SeqFloors>(FloorFile)?.SigningKey??0;
        void SaveFloors(long manifest,long signing) => Atomic.Save(FloorFile,new SeqFloors { Manifest=Math.Max(ManifestFloor,manifest), SigningKey=Math.Max(SigningFloor,signing) });
        static long CheckedSeq(string comment,string file) {
            var match=Regex.Match(comment,@"^file:"+Regex.Escape(file)+@" seq:(0|[1-9]\d{0,17})$",RegexOptions.CultureInvariant);
            if(!match.Success) throw new InvalidOperationException("Signed comment does not name this file.");
            return long.Parse(match.Groups[1].Value);
        }
        static void CheckFloor(long seq,long floor) {
            if(seq<floor) throw new InvalidOperationException("Refused an older signed file (replay).");
            if(seq>floor+MaxSeqJump) throw new InvalidOperationException("Refused a signed file whose sequence jumped too far.");
        }
        SigningStatement VerifyStatement(byte[] file,byte[] signature) {
            if(file==null || signature==null || file.Length>SigningStatement.MaxBytes) throw new InvalidOperationException("Signing key statement has an invalid size.");
            // Only the root may speak here, so a leaked signing key cannot promote itself.
            long seq=CheckedSeq(Minisign.Verify(file,signature,config.Root),SigningStatement.FileName);
            var statement=JsonBytes.Parse<SigningStatement>(file);
            if(statement==null || statement.Schema!=1 || statement.Seq!=seq) throw new InvalidOperationException("Signing key statement is invalid.");
            var key=MinisignPublicKey.Parse(statement.SigningKey);
            statement.Revoked=statement.Revoked??new string[0];
            if(key.KeyIdHex==config.Root.KeyIdHex || statement.Revoked.Length>256 || statement.Revoked.Any(id=>id==null || id.Length!=16 || !id.All(Uri.IsHexDigit)))
                throw new InvalidOperationException("Signing key statement is invalid.");
            CheckFloor(seq,SigningFloor);
            return statement;
        }
        SigningStatement CachedStatement() {
            var cached=Atomic.TryRead<SignedFile>(SigningCache);
            if(cached==null) return null;
            try { return VerifyStatement(cached.File,cached.Signature); } catch(InvalidOperationException) { return null; }
        }
        // Compiled signing key, cached verified key, else a fresh root-signed statement. Revocations always apply.
        MinisignPublicKey SelectKey(string keyId,Func<SignedFile> fetchStatement) {
            var cached=CachedStatement();
            Func<SigningStatement,MinisignPublicKey> pick=statement=>{
                bool revoked=statement!=null && statement.Revoked.Any(id=>Safety.Same(id,keyId));
                if(revoked) return null;
                var active=statement==null?null:MinisignPublicKey.Parse(statement.SigningKey);
                if(active!=null && active.KeyIdHex==keyId) return active;
                return config.Signing.KeyIdHex==keyId?config.Signing:null;
            };
            var key=pick(cached);
            if(key!=null || fetchStatement==null) return key;
            var fetched=fetchStatement();
            if(fetched==null) return null;
            var fresh=VerifyStatement(fetched.File,fetched.Signature);
            Atomic.Save(SigningCache,fetched);
            SaveFloors(0,fresh.Seq);
            return pick(fresh);
        }
        ReleaseManifest VerifyManifest(byte[] manifest,byte[] signature,Func<SignedFile> fetchStatement) {
            if(!config.Configured) throw new InvalidOperationException(UpdateConfig.NotConfigured);
            if(manifest==null || manifest.Length>ReleaseManifest.MaxBytes) throw new InvalidOperationException("Release manifest has an invalid size.");
            var parsed=MinisignSignature.Parse(signature);
            var key=SelectKey(parsed.KeyIdHex,fetchStatement);
            if(key==null) throw new InvalidOperationException("Release manifest was signed by an unknown or revoked key.");
            long seq=CheckedSeq(Minisign.Verify(manifest,parsed,key),ReleaseManifest.FileName);
            // Parsing starts only after both signatures pass.
            var result=JsonBytes.Parse<ReleaseManifest>(manifest);
            if(result==null) throw new InvalidOperationException("Release manifest is invalid.");
            result.Validate();
            if(result.Seq!=seq) throw new InvalidOperationException("Release manifest sequence does not match its signature.");
            CheckFloor(seq,ManifestFloor);
            return result;
        }
        // Learn revocations even when the current key still looks fine. Refusals change nothing.
        public void AcceptStatement(SignedFile fetched) {
            if(!config.Configured || fetched==null) return;
            var statement=VerifyStatement(fetched.File,fetched.Signature);
            Atomic.Save(SigningCache,fetched);
            SaveFloors(0,statement.Seq);
        }
        public ReleaseManifest Accept(byte[] manifest,byte[] signature,Func<SignedFile> fetchStatement) {
            var result=VerifyManifest(manifest,signature,fetchStatement);
            Atomic.Save(ManifestCache,new SignedFile { File=manifest, Signature=signature });
            SaveFloors(result.Seq,0);
            return result;
        }
        // The last verified manifest, re-verified on every load. Null means use compiled values.
        public ReleaseManifest Current() {
            var cached=config.Configured?Atomic.TryRead<SignedFile>(ManifestCache):null;
            if(cached==null) return null;
            try { return VerifyManifest(cached.File,cached.Signature,null); }
            catch(InvalidOperationException) { return null; }
        }
    }
}

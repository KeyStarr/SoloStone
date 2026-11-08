using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SoloStone;

static partial class Program
{
    static string ManifestJson(long seq,string version="2.0.0",string assetName="SoloStone-2.0.0.zip",long size=1234,string sha=null,int saveSchema=15,string tested=null,string knownBad=null,int schema=1) =>
        "{\"schema\":"+schema+",\"seq\":"+seq+",\"latest\":{\"version\":\""+version+"\",\"saveSchema\":"+saveSchema+",\"asset\":{\"name\":\""+assetName+"\",\"size\":"+size+",\"sha256\":\""+(sha??new string('A',64))+"\"}},\"testedClients\":["+(tested??"")+"],\"knownBad\":["+(knownBad??"")+"]}";
    static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
    static byte[] SignManifest(TestKey key,string json,long seq) => key.Sign(Utf8(json),"file:release-manifest.json seq:"+seq);
    static SignedFile Statement(TestKey root,TestKey signing,long seq,params string[] revoked) {
        byte[] file=Utf8("{\"schema\":1,\"seq\":"+seq+",\"signingKey\":\""+signing.Public+"\",\"revoked\":["+string.Join(",",revoked.Select(r=>"\""+r+"\""))+"]}");
        return new SignedFile { File=file, Signature=root.Sign(file,"file:signing-key.json seq:"+seq) };
    }
    static void ManifestTests(string temp) {
        // SemVer.
        Check(SemVer.Newer("1.0.1","1.0.0") && SemVer.Newer("1.10.0","1.9.0") && !SemVer.Newer("1.0.0","1.0.0") && !SemVer.Newer("0.9.9","1.0.0"),"semver numeric order");
        Check(SemVer.Newer("1.0.0","1.0.0-rc.1") && SemVer.Newer("1.0.0-rc.2","1.0.0-rc.1") && SemVer.Newer("1.0.0-rc.10","1.0.0-rc.2") && SemVer.Newer("1.0.0-rc","1.0.0-1") && SemVer.Newer("1.0.0-a.b","1.0.0-a"),"semver prerelease order");
        foreach(string bad in new[]{null,"","1","1.0","01.0.0","1.0.0+build","1.0.0-","v1.0.0","1.0.0.0"," 1.0.0"}) { Check(!SemVer.Valid(bad),"invalid semver: "+bad); }
        Refuses(()=>SemVer.Compare("x","1.0.0"),"compare refuses junk");
        // Config: empty inputs disable updates only.
        var empty=UpdateConfig.From("","","",0); Check(!empty.Configured && empty.Problem==UpdateConfig.NotConfigured,"empty build inputs mean not configured");
        Check(!UpdateConfig.Compiled().Configured,"default build has no keys");
        var rootKey=new TestKey(); var signing=new TestKey();
        var config=UpdateConfig.From(rootKey.Public,signing.Public,"owner/repo",10);
        Check(config.Configured,"keys and repo configure updates");
        Check(!UpdateConfig.From(rootKey.Public,rootKey.Public,"owner/repo",0).Configured,"root and signing keys must differ");
        foreach(string repo in new[]{"owner","owner/repo/extra","../evil/repo","owner/..","own er/repo"}) Check(!UpdateConfig.From(rootKey.Public,signing.Public,repo,0).Configured,"bad repo name: "+repo);
        Check(!UpdateConfig.From("junk",signing.Public,"owner/repo",0).Configured,"bad key disables updates");
        Refuses(()=>new ManifestStore(Path.Combine(temp,"m-off"),empty).Accept(Utf8(ManifestJson(1)),signing.Sign(Utf8("x"),"c"),null),"unconfigured store accepts nothing");
        // Accept and persist.
        string root=Path.Combine(temp,"m-root"); var store=new ManifestStore(root,config);
        Check(store.Current()==null && store.ManifestFloor==10,"floor starts at the compiled value");
        string json=ManifestJson(11);
        var accepted=store.Accept(Utf8(json),SignManifest(signing,json,11),null);
        Check(accepted.Seq==11 && accepted.Latest.Version=="2.0.0" && store.ManifestFloor==11,"verified manifest accepted and floor raised");
        var reloaded=new ManifestStore(root,config);
        Check(reloaded.Current()?.Seq==11 && reloaded.ManifestFloor==11,"cached manifest and floor survive restart");
        // Replay, jump.
        string older=ManifestJson(10);
        RejectsBecause(()=>store.Accept(Utf8(older),SignManifest(signing,older,10),null),"replay","lower seq refused");
        Check(store.Accept(Utf8(json),SignManifest(signing,json,11),null).Seq==11,"same seq re-fetch accepted");
        long far=11+ManifestStore.MaxSeqJump+1; string jump=ManifestJson(far);
        RejectsBecause(()=>store.Accept(Utf8(jump),SignManifest(signing,jump,far),null),"jumped","seq jump over the cap refused");
        Check(store.ManifestFloor==11 && store.Current().Seq==11,"refusals keep the last verified values");
        long edge=11+ManifestStore.MaxSeqJump; string atCap=ManifestJson(edge);
        Check(new ManifestStore(Path.Combine(temp,"m-cap"),UpdateConfig.From(rootKey.Public,signing.Public,"owner/repo",11)).Accept(Utf8(atCap),SignManifest(signing,atCap,edge),null).Seq==edge,"jump at the cap accepted");
        // Comment binding.
        string next=ManifestJson(12);
        foreach(string comment in new[]{"","seq:12","file:release-manifest.json","file:signing-key.json seq:12","file:other.json seq:12","file:release-manifest.json seq:12 extra","file:release-manifest.json seq:012","file:release-manifest.json seq:-1"})
            Refuses(()=>store.Accept(Utf8(next),signing.Sign(Utf8(next),comment),null),"bad trusted comment: "+comment);
        RejectsBecause(()=>store.Accept(Utf8(next),signing.Sign(Utf8(next),"file:release-manifest.json seq:13"),null),"does not match","comment seq must equal manifest seq");
        // Signature before parsing: garbage with a bad signature is a signature failure, never a parse.
        RejectsBecause(()=>store.Accept(Utf8("{not json"),signing.Sign(Utf8("other"),"file:release-manifest.json seq:12"),null),"Signature check failed","nothing parses before the signature");
        Refuses(()=>store.Accept(Utf8(next+" "),SignManifest(signing,next,12),null),"tampered manifest");
        var stranger=new TestKey();
        RejectsBecause(()=>store.Accept(Utf8(next),SignManifest(stranger,next,12),null),"unknown or revoked","unknown key without a statement refused");
        RejectsBecause(()=>store.Accept(Utf8(next),SignManifest(rootKey,next,12),null),"unknown or revoked","root key never signs a manifest");
        // Schema: everything typed and bounded.
        foreach(string bad in new[]{ManifestJson(12,schema:2),ManifestJson(12,version:"nope"),ManifestJson(12,assetName:"../evil.zip"),ManifestJson(12,assetName:"a/b.zip"),ManifestJson(12,assetName:"a.exe"),ManifestJson(12,assetName:"a..b.zip"),ManifestJson(12,assetName:"https:evil.zip"),
            ManifestJson(12,size:0),ManifestJson(12,size:ReleaseManifest.MaxAssetBytes+1),ManifestJson(12,sha:"abc"),ManifestJson(12,saveSchema:0),
            ManifestJson(12,tested:"{\"version\":\"junk\",\"files\":{\"a\":\""+new string('A',64)+"\"}}"),ManifestJson(12,tested:"{\"version\":\"37.0.1\",\"files\":{\"../a\":\""+new string('A',64)+"\"}}"),
            ManifestJson(12,tested:"{\"version\":\"37.0.1\",\"files\":{\"a\":\"short\"}}"),ManifestJson(12,knownBad:"{\"files\":{}}"),"{\"schema\":1,\"seq\":12}","[1,2]","\"text\""})
            Refuses(()=>store.Accept(Utf8(bad),SignManifest(signing,bad,12),null),"invalid signed manifest: "+bad.Substring(0,Math.Min(40,bad.Length)));
        Refuses(()=>store.Accept(new byte[ReleaseManifest.MaxBytes+1],SignManifest(signing,"x",12),null),"oversized manifest");
        Check(store.Current().Seq==11 && store.ManifestFloor==11,"failed manifests never move state");
        string withLists=ManifestJson(12,tested:"{\"version\":\"37.0.2\",\"build\":\"256589\",\"files\":{\"Hearthstone.exe\":\""+new string('B',64)+"\"}}",knownBad:"{\"files\":{\"Hearthstone.exe\":\""+new string('C',64)+"\"}}");
        var lists=store.Accept(Utf8(withLists),SignManifest(signing,withLists,12),null);
        Check(lists.TestedClients.Single().Version=="37.0.2" && lists.KnownBad.Single().Files.Count==1,"tested and known-bad lists parsed");
        // Tampered cache: re-verified on load, falls back to compiled values.
        string cache=Path.Combine(root,"state","manifest-cache.json"), backup=File.ReadAllText(cache);
        var cached=JsonBytes.Parse<SignedFile>(File.ReadAllBytes(cache)); cached.File[20]^=1; File.WriteAllBytes(cache,JsonBytes.ToBytes(cached));
        Check(new ManifestStore(root,config).Current()==null,"tampered cache ignored");
        File.WriteAllText(cache,"garbage"); Check(new ManifestStore(root,config).Current()==null,"damaged cache ignored");
        File.WriteAllText(cache,backup); Check(new ManifestStore(root,config).Current()?.Seq==12,"intact cache verified again");
        // Rotation: unknown id fetches a root-signed statement.
        string rotateRoot=Path.Combine(temp,"m-rotate"); var rotate=new ManifestStore(rotateRoot,config);
        var newKey=new TestKey(); string signedByNew=ManifestJson(20);
        int fetches=0;
        Func<SignedFile,Func<SignedFile>> fetch=file=>()=>{ fetches++; return file; };
        RejectsBecause(()=>rotate.Accept(Utf8(signedByNew),SignManifest(newKey,signedByNew,20),fetch(Statement(stranger,newKey,1))),"Signature was made with a different key","statement signed by a non-root key refused");
        RejectsBecause(()=>rotate.Accept(Utf8(signedByNew),SignManifest(newKey,signedByNew,20),fetch(Statement(signing,newKey,1))),"different key","old signing key cannot promote a new one");
        Check(rotate.Current()==null,"failed rotation changes nothing");
        var statement=Statement(rootKey,newKey,5,signing.IdHex);
        Check(rotate.Accept(Utf8(signedByNew),SignManifest(newKey,signedByNew,20),fetch(statement)).Seq==20,"new signing key accepted through the root");
        int before=fetches;
        string after=ManifestJson(21);
        Check(rotate.Accept(Utf8(after),SignManifest(newKey,after,21),null).Seq==21 && fetches==before,"cached verified key needs no fetch");
        Check(new ManifestStore(rotateRoot,config).Current()?.Seq==21,"cached manifest under the rotated key re-verifies");
        // Revoked ids hold even for the compiled key and even with a fresh statement offered.
        string revokedManifest=ManifestJson(22);
        RejectsBecause(()=>rotate.Accept(Utf8(revokedManifest),SignManifest(signing,revokedManifest,22),null),"unknown or revoked","revoked compiled key refused");
        RejectsBecause(()=>rotate.Accept(Utf8(revokedManifest),SignManifest(signing,revokedManifest,22),fetch(Statement(rootKey,newKey,6,signing.IdHex))),"unknown or revoked","revoked key stays refused after a fresh statement");
        // Statement replay: an older statement cannot bring a revoked key back.
        var oldStatement=Statement(rootKey,signing,1);
        var third=new TestKey(); string thirdManifest=ManifestJson(23);
        RejectsBecause(()=>rotate.Accept(Utf8(thirdManifest),SignManifest(third,thirdManifest,23),fetch(oldStatement)),"older signed file","old statement replay refused");
        RejectsBecause(()=>rotate.Accept(Utf8(thirdManifest),SignManifest(third,thirdManifest,23),fetch(Statement(rootKey,third,6+ManifestStore.MaxSeqJump+1))),"jumped","statement seq jump refused");
        var promote=Statement(rootKey,rootKey,7);
        Refuses(()=>rotate.Accept(Utf8(thirdManifest),SignManifest(third,thirdManifest,23),fetch(promote)),"statement cannot make the root a signing key");
        Refuses(()=>rotate.Accept(Utf8(thirdManifest),SignManifest(third,thirdManifest,23),fetch(new SignedFile { File=statement.File, Signature=rootKey.Sign(statement.File,"file:release-manifest.json seq:5") })),"statement comment must name its own file");
        // Offer: never not-newer, never a held version until a newer one appears.
        var latest=new LatestRelease { Version="2.0.0" };
        Check(UpdateService.OfferProblem(latest,"1.0.0",null)==null,"newer version offered");
        Check(UpdateService.OfferProblem(latest,"2.0.0",null)!=null,"same version not offered");
        Check(UpdateService.OfferProblem(latest,"3.0.0",null)!=null,"downgrade not offered");
        Check(UpdateService.OfferProblem(latest,null,null)!=null,"unknown install version not offered");
        Check(UpdateService.OfferProblem(latest,"1.0.0",new HeldUpdate { Version="2.0.0", Reason="The plugin rejected this installation." })=="SoloStone 2.0.0 is on hold.","held version not retried or shown as a current rejection");
        Check(UpdateService.OfferProblem(new LatestRelease { Version="2.0.1" },"1.0.0",new HeldUpdate { Version="2.0.0", Reason="r" })==null,"newer manifest version lifts the hold");
    }
}

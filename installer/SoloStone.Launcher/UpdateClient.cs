using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

namespace SoloStone
{
    public sealed class HttpReply
    {
        public int Status; public string Location; public byte[] Body;
    }
    // Never follows redirects itself. sink==null buffers into Body. Must stop at maxBytes.
    public interface IHttp { HttpReply Get(Uri url,long maxBytes,Stream sink,CancellationToken cancel); }
#pragma warning disable SYSLIB0014 // net48 launcher; HttpClient adds nothing here.
    // Plain requests: no cookies, credentials, proxy or telemetry, and no connection stays open.
    public static class Tls
    {
        // Windows picks: TLS 1.2 everywhere supported, 1.3 where this Windows has it. Asking for 1.3 by name can fail where it has not.
        public static void Allow(Action<SecurityProtocolType> set) {
            try { set(SecurityProtocolType.SystemDefault); }
            catch(NotSupportedException) { set(SecurityProtocolType.Tls12); }
        }
        // For the update line in the launcher window. Play is never affected.
        public static string Describe(Exception error) {
            var web=error as WebException;
            if(web!=null) // A WebException is an InvalidOperationException too, and its own text is .NET's, not ours.
                return web.Status==WebExceptionStatus.SecureChannelFailure || web.Status==WebExceptionStatus.TrustFailure
                    ?"Windows could not make a secure connection to GitHub. Check the system clock and any security software."
                    :"The network or GitHub is not reachable.";
            if(error is InvalidOperationException) return error.Message;
            if(error is IOException || error is UnauthorizedAccessException) return "A file could not be written. Check available space on this drive.";
            return "The network or GitHub is not reachable.";
        }
    }
    public sealed class WebHttp : IHttp
    {
        public HttpReply Get(Uri url,long maxBytes,Stream sink,CancellationToken cancel) {
            Tls.Allow(value=>ServicePointManager.SecurityProtocol=value);
            var request=(HttpWebRequest)WebRequest.Create(url);
            request.Method="GET"; request.AllowAutoRedirect=false; request.UserAgent=UpdateClient.UserAgent; request.CookieContainer=null;
            request.Credentials=null; request.UseDefaultCredentials=false; request.PreAuthenticate=false; request.Proxy=null; request.KeepAlive=false;
            request.Timeout=15000; request.ReadWriteTimeout=30000; request.AutomaticDecompression=DecompressionMethods.None;
            using(cancel.Register(request.Abort)) {
                HttpWebResponse response;
                try { response=(HttpWebResponse)request.GetResponse(); }
                catch(WebException e) when(e.Response is HttpWebResponse) { response=(HttpWebResponse)e.Response; }
                using(response) {
                    var reply=new HttpReply { Status=(int)response.StatusCode, Location=response.Headers[HttpResponseHeader.Location] };
                    if(reply.Status!=200) return reply;
                    if(response.ContentLength>maxBytes) throw new InvalidOperationException("Download is larger than expected.");
                    var memory=sink==null?new MemoryStream():null; Stream target=sink??memory;
                    using(var input=response.GetResponseStream()) {
                        var buffer=new byte[64*1024]; long total=0; int read;
                        for(;;) {
                            // A read failure is the connection. A write failure below is this machine's disk.
                            try { read=input.Read(buffer,0,buffer.Length); }
                            catch(IOException e) { throw new WebException("The connection dropped during the download.",e,WebExceptionStatus.ReceiveFailure,null); }
                            if(read<=0) break;
                            cancel.ThrowIfCancellationRequested();
                            total+=read; if(total>maxBytes) throw new InvalidOperationException("Download is larger than expected.");
                            target.Write(buffer,0,read);
                        }
                    }
                    reply.Body=memory?.ToArray();
                    return reply;
                }
            }
        }
    }
#pragma warning restore SYSLIB0014
    public sealed class UpdateClient
    {
        public const string UserAgent="SoloStone-Updater";
        public const int MaxRedirects=5;
        public static readonly string[] PinnedHosts={"raw.githubusercontent.com","github.com","objects.githubusercontent.com","release-assets.githubusercontent.com"};
        readonly IHttp http; readonly string repo;
        public UpdateClient(IHttp http,string repo) {
            if(repo==null || !UpdateConfig.RepoShape.IsMatch(repo) || repo.Contains("..")) throw new InvalidOperationException(UpdateConfig.NotConfigured);
            this.http=http; this.repo=repo;
        }
#if SOLOSTONE_TEST_HOOKS
        // Live-test builds only: one local mock GitHub, because the lab account's outbound deny blocks the real one.
        public Uri TestBaseUrl=FromEnvironment();
        static Uri FromEnvironment() {
            Uri.TryCreate(Environment.GetEnvironmentVariable("SOLOSTONE_TEST_UPDATE_BASE"),UriKind.Absolute,out Uri url);
            return url!=null && url.Scheme=="http" && url.IsLoopback?url:null;
        }
        bool AllowedTestUrl(Uri url) => TestBaseUrl!=null && url.Scheme=="http" && url.IsLoopback && url.Authority==TestBaseUrl.Authority && url.UserInfo.Length==0;
#else
        bool AllowedTestUrl(Uri url) => false;
#endif
        public bool Allowed(Uri url) => AllowedTestUrl(url) || (url.IsAbsoluteUri && url.Scheme=="https" && url.IsDefaultPort && url.UserInfo.Length==0
            && PinnedHosts.Contains(url.Host,StringComparer.OrdinalIgnoreCase));
        Uri Compose(string url) {
#if SOLOSTONE_TEST_HOOKS
            if(TestBaseUrl!=null) { var parsed=new Uri(url); return new Uri(TestBaseUrl,parsed.Host+parsed.PathAndQuery); }
#endif
            return new Uri(url);
        }
        Uri RawUrl(string file) => Compose("https://raw.githubusercontent.com/"+repo+"/main/"+file);
        public Uri AssetUrl(string version,string asset) {
            if(!SemVer.Valid(version) || !ReleaseManifest.ValidAssetName(asset)) throw new InvalidOperationException("Update asset name is invalid.");
            return Compose("https://github.com/"+repo+"/releases/download/v"+version+"/"+asset);
        }
        HttpReply Follow(Uri url,long max,Stream sink,CancellationToken cancel) {
            for(int hops=0;;hops++) {
                cancel.ThrowIfCancellationRequested();
                if(!Allowed(url)) throw new InvalidOperationException("Update download tried to use a host SoloStone does not trust.");
                var reply=http.Get(url,max,sink,cancel);
                if(reply.Status==200) return reply;
                if(reply.Status!=301 && reply.Status!=302 && reply.Status!=303 && reply.Status!=307 && reply.Status!=308)
                    throw new InvalidOperationException("Update server answered "+reply.Status+".");
                if(hops==MaxRedirects || string.IsNullOrEmpty(reply.Location) || !Uri.TryCreate(url,reply.Location,out Uri next))
                    throw new InvalidOperationException("Update server sent too many or invalid redirects.");
                url=next;
            }
        }
        public byte[] FetchText(string file,int cap,CancellationToken cancel) {
            var reply=Follow(RawUrl(file),cap,null,cancel);
            if(reply.Body==null || reply.Body.Length>cap) throw new InvalidOperationException("Update file is larger than expected.");
            return reply.Body;
        }
        public SignedFile FetchSigned(string file,int cap,CancellationToken cancel)
            => new SignedFile { File=FetchText(file,cap,cancel), Signature=FetchText(file+".minisig",Minisign.MaxSignatureFile,cancel) };
        // Streams to .part. The final name appears only after the size and SHA-256 match the signed manifest.
        public void Download(string version,ReleaseAsset asset,string destination,CancellationToken cancel) {
            string part=destination+".part"; Safety.NoLinks(destination); Safety.NoLinks(part);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if(File.Exists(part)) File.Delete(part);
            try {
                using(var stream=new FileStream(part,FileMode.CreateNew,FileAccess.Write,FileShare.None)) Follow(AssetUrl(version,asset.Name),asset.Size,stream,cancel);
                if(new FileInfo(part).Length!=asset.Size || !Safety.Same(Hashes.FileHash(part),asset.Sha256))
                    throw new InvalidOperationException("The downloaded update failed its size or hash check.");
                if(File.Exists(destination)) File.Delete(destination);
                File.Move(part,destination);
            } catch { if(File.Exists(part)) File.Delete(part); throw; }
        }
    }
    [DataContract] public sealed class Settings
    {
        [DataMember(Name="checkForUpdates")] public bool CheckForUpdates=true;
        static string PathOf(string root) => Path.Combine(root,"state","settings.json");
        // Absent means on. A damaged file means off, so a player's "off" is never overridden by accident.
        public static Settings Load(string root) {
            string path=PathOf(root);
            Safety.NoLinks(path);
            if(!File.Exists(path)) return new Settings();
            return Atomic.TryRead<Settings>(path)??new Settings { CheckForUpdates=false };
        }
        public static void Save(string root,Settings value) => Atomic.Save(PathOf(root),value);
    }
}

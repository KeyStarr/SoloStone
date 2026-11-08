using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using SoloStone;

sealed class FakeHttp : IHttp
{
    public readonly Dictionary<string,Func<HttpReply>> Routes=new Dictionary<string,Func<HttpReply>>();
    public readonly List<string> Requests=new List<string>();
    public void Add(string url,byte[] body) => Routes[url]=()=>new HttpReply { Status=200, Body=body };
    public void Redirect(string url,string to,int status=302) => Routes[url]=()=>new HttpReply { Status=status, Location=to };
    public HttpReply Get(Uri url,long maxBytes,Stream sink,CancellationToken cancel) {
        Requests.Add(url.AbsoluteUri);
        if(!Routes.TryGetValue(url.AbsoluteUri,out var route)) return new HttpReply { Status=404 };
        var reply=route();
        if(reply.Status==200) {
            if(reply.Body.Length>maxBytes) throw new InvalidOperationException("Download is larger than expected.");
            if(sink!=null) { sink.Write(reply.Body,0,reply.Body.Length); reply.Body=null; }
        }
        return reply;
    }
}
static partial class Program
{
    // One-shot HTTP/1.1 on loopback, enough to prove what WebHttp really sends and refuses to do.
    static (Uri url,System.Threading.Tasks.Task<string> request) Serve(string response) {
        var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0); listener.Start();
        var url=new Uri("http://127.0.0.1:"+((System.Net.IPEndPoint)listener.LocalEndpoint).Port+"/f");
        var task=System.Threading.Tasks.Task.Run(()=>{
            using(var socket=listener.AcceptTcpClient()) using(var stream=socket.GetStream()) {
                var buffer=new byte[8192]; var seen=new StringBuilder();
                while(!seen.ToString().Contains("\r\n\r\n")) { int read=stream.Read(buffer,0,buffer.Length); if(read==0) break; seen.Append(Encoding.ASCII.GetString(buffer,0,read)); }
                byte[] bytes=Encoding.ASCII.GetBytes(response); stream.Write(bytes,0,bytes.Length); stream.Flush();
                listener.Stop();
                return seen.ToString();
            }
        });
        return (url,task);
    }
    static void WebHttpTests() {
        var web=new WebHttp(); var none=CancellationToken.None;
        var ok=Serve("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nSet-Cookie: a=b\r\nConnection: close\r\n\r\nhello");
        var reply=web.Get(ok.url,100,null,none);
        string sent=ok.request.Result;
        Check(reply.Status==200 && Encoding.ASCII.GetString(reply.Body)=="hello","real request reads the body");
        Check(sent.Contains("User-Agent: "+UpdateClient.UserAgent) && !sent.Contains("Cookie") && !sent.Contains("Authorization"),"fixed user agent, no cookies, no credentials");
        var redirect=Serve("HTTP/1.1 302 Found\r\nLocation: https://evil.example/x\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        reply=web.Get(redirect.url,100,null,none);
        Check(reply.Status==302 && reply.Location=="https://evil.example/x" && reply.Body==null,"redirect is reported, never followed by the transport");
        var error=Serve("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        Check(web.Get(error.url,100,null,none).Status==404,"error status is a reply, not an exception");
        var big=Serve("HTTP/1.1 200 OK\r\nContent-Length: 500\r\nConnection: close\r\n\r\n"+new string('x',500));
        Refuses(()=>web.Get(big.url,100,null,none),"declared size over the cap refused");
        var lying=Serve("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\n"+new string('x',500));
        Refuses(()=>web.Get(lying.url,100,null,none),"streamed size over the cap refused");
        var stream=Serve("HTTP/1.1 200 OK\r\nContent-Length: 4\r\nConnection: close\r\n\r\ndata");
        var sink=new MemoryStream();
        Check(web.Get(stream.url,100,sink,none).Body==null && Encoding.ASCII.GetString(sink.ToArray())=="data","downloads stream to the sink");
    }
    const string RawBase="https://raw.githubusercontent.com/owner/repo/main/";
    static void NetworkTests(string temp) {
        WebHttpTests();
        var http=new FakeHttp(); var client=new UpdateClient(http,"owner/repo");
        var none=CancellationToken.None;
        // Host pinning.
        foreach(string good in new[]{"https://raw.githubusercontent.com/a","https://github.com/a","https://objects.githubusercontent.com/a","https://release-assets.githubusercontent.com/a","https://GITHUB.com/a"})
            Check(client.Allowed(new Uri(good)),"pinned host allowed: "+good);
        foreach(string bad in new[]{"http://github.com/a","https://evil.com/a","https://github.com.evil.com/a","https://evilgithub.com/a","https://github.com:8443/a","https://user:pw@github.com/a","ftp://github.com/a","http://127.0.0.1/a","http://localhost/a","https://api.github.com/a"})
            Check(!client.Allowed(new Uri(bad)),"host refused: "+bad);
        foreach(string bad in new[]{null,"owner","../x/y","a/b/c","a/.."}) Refuses(()=>new UpdateClient(http,bad),"repo name refused: "+bad);
        // Composition: only compiled repo, validated version and asset name.
        Check(client.AssetUrl("2.0.0","SoloStone-2.0.0.zip").AbsoluteUri=="https://github.com/owner/repo/releases/download/v2.0.0/SoloStone-2.0.0.zip","asset URL");
        Refuses(()=>client.AssetUrl("2.0.0/../../evil","a.zip"),"version cannot steer the URL");
        Refuses(()=>client.AssetUrl("2.0.0","../a.zip"),"asset name cannot steer the URL");
        // Fetch, redirects.
        var body=Utf8("hello");
        http.Add(RawBase+"f.json",body);
        Check(client.FetchText("f.json",100,none).SequenceEqual(body),"plain fetch");
        Refuses(()=>client.FetchText("f.json",2,none),"size cap enforced");
        http.Redirect(RawBase+"r1","https://objects.githubusercontent.com/r2",301); http.Add("https://objects.githubusercontent.com/r2",body);
        Check(client.FetchText("r1",100,none).SequenceEqual(body),"redirect to a pinned host followed");
        foreach(int status in new[]{302,303,307,308}) { http.Redirect(RawBase+"rs"+status,"https://github.com/ok",status); http.Add("https://github.com/ok",body); Check(client.FetchText("rs"+status,100,none).Length==5,"redirect status "+status); }
        foreach(var bad in new[]{"https://evil.com/x","http://github.com/x","https://github.com:444/x","//evil.com/x","","ftp://github.com/x"}) {
            http.Redirect(RawBase+"bad",bad);
            int before=http.Requests.Count;
            Refuses(()=>client.FetchText("bad",100,none),"redirect refused: "+bad);
            Check(http.Requests.Count==before+1 || bad=="","no request reaches a refused host: "+bad);
        }
        http.Routes[RawBase+"bad"]=()=>new HttpReply { Status=302 };
        Refuses(()=>client.FetchText("bad",100,none),"redirect without a location");
        for(int i=0;i<=UpdateClient.MaxRedirects;i++) http.Redirect("https://github.com/hop"+i,"https://github.com/hop"+(i+1));
        http.Add("https://github.com/hop"+(UpdateClient.MaxRedirects+1),body);
        http.Redirect(RawBase+"long","https://github.com/hop0");
        Refuses(()=>client.FetchText("long",100,none),"more than five redirects refused");
        http.Redirect(RawBase+"short","https://github.com/hop"+(UpdateClient.MaxRedirects-1));
        Check(client.FetchText("short",100,none).Length==5,"five redirects followed");
        http.Redirect(RawBase+"loop",RawBase+"loop");
        Refuses(()=>client.FetchText("loop",100,none),"redirect loop refused");
        http.Routes[RawBase+"e"]=()=>new HttpReply { Status=500 };
        Refuses(()=>client.FetchText("e",100,none),"server error refuses");
        Refuses(()=>client.FetchText("absent",100,none),"404 refuses");
        var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        Throws<OperationCanceledException>(()=>client.FetchText("f.json",100,cancelled.Token),"cancel respected");
        // Download.
        string folder=Path.Combine(temp,"dl"); Directory.CreateDirectory(folder); string zip=Path.Combine(folder,"asset.zip");
        byte[] asset=Utf8("zip bytes");
        string sha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(asset));
        http.Add("https://github.com/owner/repo/releases/download/v2.0.0/a.zip",asset);
        client.Download("2.0.0",new ReleaseAsset { Name="a.zip", Size=asset.Length, Sha256=sha },zip,none);
        Check(File.ReadAllBytes(zip).SequenceEqual(asset) && !File.Exists(zip+".part"),"verified download gets its final name");
        File.Delete(zip);
        Refuses(()=>client.Download("2.0.0",new ReleaseAsset { Name="a.zip", Size=asset.Length, Sha256=new string('A',64) },zip,none),"wrong hash refused");
        Check(!File.Exists(zip) && !File.Exists(zip+".part"),"failed download leaves nothing");
        Refuses(()=>client.Download("2.0.0",new ReleaseAsset { Name="a.zip", Size=asset.Length-1, Sha256=sha },zip,none),"oversized download refused");
        Check(!File.Exists(zip) && !File.Exists(zip+".part"),"oversized download leaves nothing");
        http.Add("https://github.com/owner/repo/releases/download/v2.0.0/b.zip",asset.Take(4).ToArray());
        Refuses(()=>client.Download("2.0.0",new ReleaseAsset { Name="b.zip", Size=asset.Length, Sha256=sha },zip,none),"short download refused");
        Check(!File.Exists(zip+".part"),"short download leaves nothing");
        // Test hook: loopback only with an explicit base, and only that base.
        Check(!client.Allowed(new Uri("http://127.0.0.1:9000/x")),"no loopback without the hook base");
        client.TestBaseUrl=new Uri("http://127.0.0.1:9000/");
        Check(client.Allowed(new Uri("http://127.0.0.1:9000/x")) && !client.Allowed(new Uri("http://127.0.0.1:9001/x")) && !client.Allowed(new Uri("http://203.0.113.1:9000/x")),"hook allows its one base");
        Check(client.AssetUrl("2.0.0","a.zip").AbsoluteUri.StartsWith("http://127.0.0.1:9000/github.com/owner/repo/",StringComparison.Ordinal),"hook routes composed URLs to the mock");
        // Settings: absent on, damaged off.
        string root=Path.Combine(temp,"settings"); Directory.CreateDirectory(root);
        Check(Settings.Load(root).CheckForUpdates,"update checks default on");
        Settings.Save(root,new Settings { CheckForUpdates=false }); Check(!Settings.Load(root).CheckForUpdates,"off switch persists");
        Settings.Save(root,new Settings { CheckForUpdates=true }); Check(Settings.Load(root).CheckForUpdates,"on switch persists");
        File.WriteAllText(Path.Combine(root,"state","settings.json"),"garbage"); Check(!Settings.Load(root).CheckForUpdates,"damaged settings never turn checks on");
    }
}

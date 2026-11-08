using System;
using System.IO;
using System.Linq;
using System.Text;
using SoloStone;

static partial class Program
{
    static void MinisignTests(string temp) {
        var message=Encoding.UTF8.GetBytes("signed message");
        var key=new TestKey(); var pub=MinisignPublicKey.Parse(key.Public);
        Check(pub.KeyIdHex==key.IdHex,"public key parse");
        foreach(bool prehashed in new[]{true,false}) {
            byte[] signature=key.Sign(message,"file:x seq:1",prehashed);
            Check(Minisign.Verify(message,signature,pub)=="file:x seq:1",(prehashed?"ED":"Ed")+" verifies and returns the trusted comment");
            Refuses(()=>Minisign.Verify(Encoding.UTF8.GetBytes("signed messagf"),signature,pub),"tampered input refused");
            Refuses(()=>Minisign.Verify(message,signature,MinisignPublicKey.Parse(new TestKey().Public)),"wrong key refused");
            Check(Encoding.UTF8.GetString(signature).Contains("untrusted comment: test"),"fixture shape");
            // Swap the trusted comment, keep every signature: only the global signature can catch it.
            string swapped=Encoding.UTF8.GetString(signature).Replace("trusted comment: file:x seq:1","trusted comment: file:x seq:9").Replace("untrusted comment: file:x seq:9","untrusted comment: file:x seq:1");
            Refuses(()=>Minisign.Verify(message,Encoding.UTF8.GetBytes(swapped),pub),"swapped trusted comment refused");
            // Same key material under another id: refuse before any crypto.
            var otherId=new byte[8]; otherId[0]=(byte)(key.Id[0]^1); for(int i=1;i<8;i++) otherId[i]=key.Id[i];
            Refuses(()=>Minisign.Verify(message,key.Sign(message,"file:x seq:1",prehashed,idOverride:otherId),pub),"wrong key id refused");
        }
        // An ED signature cannot pass as Ed, and the other way round.
        string ed=Encoding.UTF8.GetString(key.Sign(message,"c",true));
        var lines=ed.Split('\n'); var raw=Convert.FromBase64String(lines[1]); raw[1]=(byte)'d'; lines[1]=Convert.ToBase64String(raw);
        Refuses(()=>Minisign.Verify(message,Encoding.UTF8.GetBytes(string.Join("\n",lines)),pub),"algorithm downgrade refused");
        // Strict format.
        byte[] good=key.Sign(message,"c");
        Func<byte[],Action> verifying=bytes=>()=>Minisign.Verify(message,bytes,pub);
        Refuses(verifying(new byte[0]),"empty signature");
        Refuses(verifying(null),"null signature");
        Refuses(verifying(new byte[Minisign.MaxSignatureFile+1]),"oversized signature");
        Refuses(verifying(key.Sign(message,new string('c',Minisign.MaxSignatureFile))),"oversized comment");
        Refuses(verifying(Encoding.UTF8.GetBytes("garbage")),"garbage signature");
        string text=Encoding.UTF8.GetString(good);
        Refuses(verifying(Encoding.UTF8.GetBytes(text+"extra line\n")),"extra line refused");
        Refuses(verifying(Encoding.UTF8.GetBytes(text+"\n\n")),"trailing blank lines refused");
        Refuses(verifying(Encoding.UTF8.GetBytes(text.Replace("untrusted comment: ","comment: "))),"missing untrusted prefix");
        Refuses(verifying(Encoding.UTF8.GetBytes(text.Replace("trusted comment: c","trusted: c"))),"missing trusted prefix");
        lines=text.Split('\n');
        Refuses(verifying(Encoding.UTF8.GetBytes(string.Join("\n",lines[0],lines[1]+"=",lines[2],lines[3],""))),"wrong signature length");
        Refuses(verifying(Encoding.UTF8.GetBytes(string.Join("\n",lines[0],lines[1].Replace(lines[1][5],'!'),lines[2],lines[3],""))),"non-base64 signature");
        Refuses(verifying(Encoding.UTF8.GetBytes(string.Join("\n",lines[0],lines[1],lines[2],lines[3].Substring(4),""))),"short global signature");
        Refuses(verifying(Encoding.UTF8.GetBytes(string.Join("\n",lines[0],lines[1],lines[2]+"\t",lines[3],""))),"control character in comment");
        Check(Minisign.Verify(message,Encoding.UTF8.GetBytes(text.Replace("\n","\r\n")),pub)=="c","CRLF line endings accepted (Windows editors)");
        Check(Minisign.Verify(message,Encoding.UTF8.GetBytes(text.TrimEnd('\n')),pub)=="c","missing final newline accepted");
        foreach(string bad in new[]{"","RW","RWabc","xx"+key.Public.Substring(2),key.Public+"AA",null}) Refuses(()=>MinisignPublicKey.Parse(bad),"malformed public key");
        var wrongAlg=Convert.FromBase64String(key.Public); wrongAlg[1]=(byte)'D';
        Refuses(()=>MinisignPublicKey.Parse(Convert.ToBase64String(wrongAlg)),"public key algorithm");
        // Real minisign output: fixture made with a throwaway key whose secret was discarded.
        string fixtures=Path.Combine(AppContext.BaseDirectory,"fixtures","minisign");
        byte[] interop=File.ReadAllBytes(Path.Combine(fixtures,"message.txt"));
        var interopKey=MinisignPublicKey.Parse(File.ReadAllLines(Path.Combine(fixtures,"interop-test-only.pub"))[1]);
        Check(File.ReadAllLines(Path.Combine(fixtures,"interop-test-only.pub"))[0].EndsWith(interopKey.KeyIdHex,StringComparison.Ordinal),"key id matches minisign's own spelling");
        foreach(string file in new[]{"message.prehashed.minisig","message.legacy.minisig"}) {
            byte[] signature=File.ReadAllBytes(Path.Combine(fixtures,file));
            Check(Minisign.Verify(interop,signature,interopKey)=="file:message.txt seq:7","real minisign signature verifies: "+file);
            Refuses(()=>Minisign.Verify(interop.Concat(new byte[]{1}).ToArray(),signature,interopKey),"real minisign signature refuses tampered input: "+file);
            Refuses(()=>Minisign.Verify(interop,signature,pub),"real minisign signature refuses another key: "+file);
        }
        Check(MinisignSignature.Parse(File.ReadAllBytes(Path.Combine(fixtures,"message.prehashed.minisig"))).Prehashed,"minisign's default is the prehashed format");
        Check(!MinisignSignature.Parse(File.ReadAllBytes(Path.Combine(fixtures,"message.legacy.minisig"))).Prehashed,"legacy format parsed");
    }
}

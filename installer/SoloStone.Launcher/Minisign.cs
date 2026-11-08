using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Math.EC.Rfc8032;

namespace SoloStone
{
    public sealed class MinisignPublicKey
    {
        public byte[] KeyId, Key;
        public string KeyIdHex => Minisign.KeyHex(KeyId);
        public static MinisignPublicKey Parse(string text) {
            string line=(text??"").Trim();
            if(!line.StartsWith("RW",StringComparison.Ordinal)) throw new InvalidOperationException("Not a minisign public key.");
            byte[] raw=Minisign.Strict64(line,42);
            if(raw[0]!=(byte)'E' || raw[1]!=(byte)'d') throw new InvalidOperationException("Not a minisign public key.");
            return new MinisignPublicKey { KeyId=raw.Skip(2).Take(8).ToArray(), Key=raw.Skip(10).ToArray() };
        }
    }
    public sealed class MinisignSignature
    {
        public bool Prehashed; public byte[] KeyId, Signature, TrustedBytes, GlobalSignature; public string TrustedComment;
        public string KeyIdHex => Minisign.KeyHex(KeyId);
        const string Untrusted="untrusted comment: ", Trusted="trusted comment: ";
        public static MinisignSignature Parse(byte[] file) {
            if(file==null || file.Length==0 || file.Length>Minisign.MaxSignatureFile) throw new InvalidOperationException("Signature file has an invalid size.");
            // Exactly four lines. Comments stay raw bytes because the global signature covers the raw trusted comment.
            var lines=new byte[5][]; int count=0, start=0;
            for(int i=0;i<=file.Length;i++) {
                if(i!=file.Length && file[i]!=(byte)'\n') continue;
                if(count==5) throw new InvalidOperationException("Signature file is malformed.");
                int end=i; if(end>start && file[end-1]==(byte)'\r') end--;
                lines[count++]=file.Skip(start).Take(end-start).ToArray(); start=i+1;
            }
            if(count==5 && lines[4].Length!=0 || count<4) throw new InvalidOperationException("Signature file is malformed.");
            if(!Prefixed(lines[0],Untrusted) || !Prefixed(lines[2],Trusted)) throw new InvalidOperationException("Signature file is malformed.");
            byte[] main=Minisign.Strict64(Encoding.ASCII.GetString(lines[1]),74);
            bool prehashed=main[0]==(byte)'E' && main[1]==(byte)'D';
            if(!prehashed && !(main[0]==(byte)'E' && main[1]==(byte)'d')) throw new InvalidOperationException("Signature uses an unsupported algorithm.");
            var trusted=lines[2].Skip(Trusted.Length).ToArray();
            string comment;
            try { comment=new UTF8Encoding(false,true).GetString(trusted); }
            catch(ArgumentException) { throw new InvalidOperationException("Signature file is malformed."); }
            if(comment.Any(char.IsControl)) throw new InvalidOperationException("Signature file is malformed.");
            return new MinisignSignature { Prehashed=prehashed, KeyId=main.Skip(2).Take(8).ToArray(), Signature=main.Skip(10).ToArray(),
                TrustedBytes=trusted, TrustedComment=comment, GlobalSignature=Minisign.Strict64(Encoding.ASCII.GetString(lines[3]),64) };
        }
        static bool Prefixed(byte[] line,string prefix) {
            var bytes=Encoding.ASCII.GetBytes(prefix);
            return line.Length>=bytes.Length && line.Take(bytes.Length).SequenceEqual(bytes);
        }
    }
    public static class Minisign
    {
        public const int MaxSignatureFile=4096;
        static readonly Regex Base64=new Regex(@"^[A-Za-z0-9+/]+={0,2}$",RegexOptions.CultureInvariant);
        internal static byte[] Strict64(string text,int length) {
            if(text.Length!=(length+2)/3*4 || !Base64.IsMatch(text)) throw new InvalidOperationException("Signature data is malformed.");
            byte[] raw;
            try { raw=Convert.FromBase64String(text); } catch(FormatException) { throw new InvalidOperationException("Signature data is malformed."); }
            if(raw.Length!=length || Convert.ToBase64String(raw)!=text) throw new InvalidOperationException("Signature data is malformed.");
            return raw;
        }
        // Same spelling as the "minisign public key <id>" comment in the owner's .pub file.
        public static string KeyHex(byte[] id) => string.Concat(Enumerable.Reverse(id).Select(b=>b.ToString("X2")));
        public static byte[] Blake2b512(byte[] message) {
            var digest=new Blake2bDigest(512); digest.BlockUpdate(message,0,message.Length);
            var output=new byte[64]; digest.DoFinal(output,0); return output;
        }
        // Returns the authenticated trusted comment, or refuses. Callers parse the message only afterwards.
        public static string Verify(byte[] message,byte[] signatureFile,MinisignPublicKey key) => Verify(message,MinisignSignature.Parse(signatureFile),key);
        public static string Verify(byte[] message,MinisignSignature signature,MinisignPublicKey key) {
            if(!signature.KeyId.SequenceEqual(key.KeyId)) throw new InvalidOperationException("Signature was made with a different key.");
            byte[] signed=signature.Prehashed?Blake2b512(message):message;
            if(!Ed25519.Verify(signature.Signature,0,key.Key,0,signed,0,signed.Length)) throw new InvalidOperationException("Signature check failed.");
            byte[] global=signature.Signature.Concat(signature.TrustedBytes).ToArray();
            if(!Ed25519.Verify(signature.GlobalSignature,0,key.Key,0,global,0,global.Length)) throw new InvalidOperationException("Signature comment check failed.");
            return signature.TrustedComment;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Math.EC.Rfc8032;
using SoloStone;

static partial class Program
{
    // Real metadata, written without any client bytes, so ClientVersion runs its real path on the fake client.
    static void CraftAssembly(string path,string cosmetic,string[] branch,string[] otherStrings=null,string typeName="Network",string cosmeticField="CosmeticVersion",string programVersion=null) {
        var builder=new PersistedAssemblyBuilder(new AssemblyName("Assembly-CSharp"),typeof(object).Assembly);
        var module=builder.DefineDynamicModule("Assembly-CSharp");
        var type=module.DefineType(typeName,TypeAttributes.Public|TypeAttributes.Class);
        if(cosmetic!=null) {
            var field=type.DefineField(cosmeticField,typeof(string),FieldAttributes.Public|FieldAttributes.Static|FieldAttributes.Literal|FieldAttributes.HasDefault);
            field.SetConstant(cosmetic);
        }
        if(branch!=null) {
            var il=type.DefineMethod("get_BranchName",MethodAttributes.Public|MethodAttributes.Static|MethodAttributes.SpecialName,typeof(string),Type.EmptyTypes).GetILGenerator();
            foreach(string value in branch) { il.Emit(OpCodes.Ldstr,value); il.Emit(OpCodes.Pop); }
            il.Emit(OpCodes.Ldstr,branch.LastOrDefault()??""); il.Emit(OpCodes.Ret);
        }
        if(otherStrings!=null) {
            var il=type.DefineMethod("Other",MethodAttributes.Public|MethodAttributes.Static,typeof(string),Type.EmptyTypes).GetILGenerator();
            foreach(string value in otherStrings) { il.Emit(OpCodes.Ldstr,value); il.Emit(OpCodes.Pop); }
            il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Ret);
        }
        type.CreateType();
        if(programVersion!=null) {
            var telemetry=module.DefineType("TelemetryManager",TypeAttributes.Public|TypeAttributes.Class);
            var il=telemetry.DefineMethod("get_ProgramVersion",MethodAttributes.Public|MethodAttributes.Static|MethodAttributes.SpecialName,typeof(string),Type.EmptyTypes).GetILGenerator();
            il.Emit(OpCodes.Ldstr,programVersion); il.Emit(OpCodes.Ret); telemetry.CreateType();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        builder.Save(path);
    }
    static TestedClient FakeClient(string folder,string version) {
        Directory.CreateDirectory(folder);
        string[] parts=version.Split('.');
        CraftAssembly(Path.Combine(folder,"Hearthstone_Data","Managed","Assembly-CSharp.dll"),parts[0]+"."+parts[1],new[]{version});
        File.WriteAllText(Path.Combine(folder,"Hearthstone.exe"),"fake client "+version);
        File.WriteAllText(Path.Combine(folder,"UnityPlayer.dll"),"fake player");
        File.WriteAllText(Path.Combine(folder,"Hearthstone_Data","Managed","blizzard.bgsclient.dll"),"fake bgs");
        // The two files that carry the company name, as the real client's do: once each, one deep in binary data and one at offset 0.
        File.WriteAllBytes(Path.Combine(folder,"Hearthstone_Data","globalgamemanagers"),IdentityBytes("fake player data\0\u0001\u0002",ClientIdentity.OldName,"\0Hearthstone\0tail"));
        File.WriteAllBytes(Path.Combine(folder,"Hearthstone_Data","app.info"),IdentityBytes("",ClientIdentity.OldName,"\nHearthstone\n"));
        var facts=ClientFacts.Read(folder);
        return new TestedClient { Schema=1, Version=version, Build="254477", Files=facts.Files };
    }
    static byte[] IdentityBytes(string before,string name,string after) => System.Text.Encoding.Latin1.GetBytes(before+name+after);
    static string TreeHash(string folder) {
        if(!Directory.Exists(folder)) return "absent";
        var lines=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal)
            .Select(p=>p.Substring(folder.Length)+"="+Hashes.FileHash(p));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",lines))));
    }
    static void Throws<T>(Action action,string name) where T:Exception {
        try { action(); } catch(T) { count++; return; }
        throw new Exception("Did not throw "+typeof(T).Name+": "+name);
    }
    sealed class TestKey
    {
        readonly byte[] seed=new byte[32]; readonly byte[] pub=new byte[32]; public readonly byte[] Id=new byte[8];
        public TestKey() {
            RandomNumberGenerator.Fill(seed); RandomNumberGenerator.Fill(Id);
            Ed25519.GeneratePublicKey(seed,0,pub,0);
        }
        // Throwaway keys written by `tool keygen` are read back here, so a live run can sign releases without a second crypto library.
        public TestKey(byte[] seedBytes,byte[] id) {
            Array.Copy(seedBytes,seed,32); Array.Copy(id,Id,8);
            Ed25519.GeneratePublicKey(seed,0,pub,0);
        }
        public string SeedText => Convert.ToBase64String(seed);
        public string Public => Convert.ToBase64String(new byte[]{(byte)'E',(byte)'d'}.Concat(Id).Concat(pub).ToArray());
        public string IdHex => Minisign.KeyHex(Id);
        byte[] SignRaw(byte[] message) { var signature=new byte[64]; Ed25519.Sign(seed,0,pub,0,message,0,message.Length,signature,0); return signature; }
        public byte[] Sign(byte[] message,string comment,bool prehashed=true,string untrusted="test",byte[] signWith=null,byte[] idOverride=null) {
            byte[] signature=SignRaw(prehashed?Minisign.Blake2b512(message):message);
            byte[] global=SignRaw(signature.Concat(Encoding.UTF8.GetBytes(comment)).ToArray());
            byte[] main=new byte[]{(byte)'E',prehashed?(byte)'D':(byte)'d'}.Concat(idOverride??Id).Concat(signature).ToArray();
            return Encoding.UTF8.GetBytes("untrusted comment: "+untrusted+"\n"+Convert.ToBase64String(main)+"\ntrusted comment: "+comment+"\n"+Convert.ToBase64String(global)+"\n");
        }
    }
}

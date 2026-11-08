using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Xml;

namespace SoloStone
{
    public static class JsonBytes
    {
        static DataContractJsonSerializer Serializer<T>() => new DataContractJsonSerializer(typeof(T),
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        public static T Parse<T>(byte[] bytes) {
            if(PlainJson.HasBom(bytes)) throw new InvalidOperationException("A SoloStone data file starts with a byte order mark, which .NET Framework's reader refuses.");
            try { using(var stream=new MemoryStream(bytes)) return (T)Serializer<T>().ReadObject(stream); }
            catch(SerializationException e) { throw new InvalidOperationException("A SoloStone data file is malformed.",e); }
            catch(XmlException e) { throw new InvalidOperationException("A SoloStone data file is malformed.",e); }
            catch(ArgumentException e) { throw new InvalidOperationException("A SoloStone data file is malformed.",e); }
        }
        public static byte[] ToBytes<T>(T value) {
            using(var stream=new MemoryStream()) { Serializer<T>().WriteObject(stream,value); return stream.ToArray(); }
        }
    }
    // One lifecycle operation per root at a time, across processes.
    public static class Lease
    {
        static string PathOf(string root) {
            using(var sha=SHA256.Create()) return Path.Combine(Path.GetTempPath(),"SoloStone-"+FsOps.Hex(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(root.ToLowerInvariant())))+".lock");
        }
        public static IDisposable Open(string root,string busy=null) {
            string name=PathOf(root);
            if(Path.DirectorySeparatorChar=='\\') Safety.NoLinks(name); // Elsewhere the temp folder itself may be a link: macOS's is.
            try { return new FileStream(name,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
            catch(IOException) { throw new InvalidOperationException(busy??"Another SoloStone operation is running. Wait for it to finish."); }
        }
        public static IDisposable Wait(string root,TimeSpan wait) {
            DateTime until=DateTime.UtcNow+wait;
            for(;;) {
                try { return Open(root); }
                catch(InvalidOperationException) when(DateTime.UtcNow<until) { Thread.Sleep(200); }
            }
        }
    }
    public static class InstallerLog
    {
        public static string PathOf(string root) => Path.Combine(root,"reports","installer.log");
        public static void Write(string root,string line) {
            try {
                string path=PathOf(root); Safety.NoLinks(path);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if(File.Exists(path) && new FileInfo(path).Length>512*1024) File.Delete(path);
                File.AppendAllText(path,DateTime.UtcNow.ToString("u")+"  "+line+"\r\n",new System.Text.UTF8Encoding(false));
            } catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException) { }
        }
    }
    public static class Atomic
    {
        public static bool FlushToDisk=true; // Tests simulate crashes by exception, so they skip the (slow) disk flush.
        [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true,EntryPoint="MoveFileExW")]
        static extern bool MoveFileEx(string from,string to,uint flags);
        // A same-folder rename over the target, written through: a process kill leaves the old or new file. Windows documents no such promise for a
        // power cut, and File.Replace is not documented as atomic. The source is flushed first so a rename can't publish bytes still in cache.
        public static Action<string> PublishFault; // Test hook: a target another process holds. Never set in production.
        public static int RetryDelayMs=50;
        // Antivirus, an indexer or a backup can hold a file for a moment, and Windows then refuses to replace or move it. That must not end a swap. Bounded: about 4 s, then the error stands.
        public static void Transient(Action work) {
            for(int attempt=0;;attempt++) {
                try { work(); return; }
                catch(FileNotFoundException) { throw; }
                catch(DirectoryNotFoundException) { throw; }
                catch(IOException) when(attempt<11) { }
                catch(UnauthorizedAccessException) when(attempt<11) { }
                Thread.Sleep(Math.Min(RetryDelayMs<<attempt,500));
            }
        }
        public static void Delete(string path) => Transient(()=>{ if(File.Exists(path)) File.Delete(path); });
        public static void Publish(string temp,string target) {
            if(FlushToDisk) Transient(()=>{ using(var stream=new FileStream(temp,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) stream.Flush(true); });
            Transient(()=>{
                PublishFault?.Invoke(target);
                if(Path.DirectorySeparatorChar=='\\') {
                    if(MoveFileEx(temp,target,1|8)) return;
                    int error=System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    if(error==2 || error==3) throw new FileNotFoundException("Could not put "+Path.GetFileName(target)+" in place: its new copy is gone.",temp);
                    throw new IOException("Could not put "+Path.GetFileName(target)+" in place (Windows error "+error+").",System.Runtime.InteropServices.Marshal.GetHRForLastWin32Error());
                }
#if NET
                File.Move(temp,target,true);
#else
                if(File.Exists(target)) File.Replace(temp,target,null); else File.Move(temp,target);
#endif
            });
        }
        // Durable replace: the old bytes or the new bytes survive a kill, never a mix.
        public static void Write(string path,byte[] bytes) {
            string temp=path+".tmp"; Safety.NoLinks(path); Safety.NoLinks(temp);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Transient(()=>{ using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) { stream.Write(bytes,0,bytes.Length); stream.Flush(FlushToDisk); } });
            Publish(temp,path);
        }
        public static void Save<T>(string path,T value) => Write(path,JsonBytes.ToBytes(value));
        // State files are optional and rebuilt on demand. Anything unreadable counts as absent.
        public static T TryRead<T>(string path) where T:class {
            try {
                Safety.NoLinks(path);
                if(!File.Exists(path) || new FileInfo(path).Length>2*1024*1024) return null;
                return JsonBytes.Parse<T>(File.ReadAllBytes(path));
            }
            catch(InvalidOperationException) { return null; } catch(IOException) { return null; } catch(UnauthorizedAccessException) { return null; }
        }
    }
    public static class FsOps
    {
        public static void DeleteTree(string path) {
            if(File.Exists(path)) { Safety.NoLinks(path); File.Delete(path); return; }
            if(!Directory.Exists(path)) return;
            Safety.NoLinks(path);
            foreach(string file in Safety.Tree(path)) { File.SetAttributes(file,FileAttributes.Normal); File.Delete(file); }
            Directory.Delete(path,true);
        }
        public static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-","");
        // Copies through one SHA-256 and returns the source hash. Cancelling leaves a partial target for the caller's stage to remove.
        public static string CopyHashed(string source,string target,CancellationToken cancel,Action<long> copied=null) {
            Safety.NoLinks(source); Safety.NoLinks(target);
            using(var sha=SHA256.Create())
            using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                var buffer=new byte[1024*1024]; int read;
                while((read=input.Read(buffer,0,buffer.Length))>0) {
                    cancel.ThrowIfCancellationRequested();
                    sha.TransformBlock(buffer,0,read,null,0); output.Write(buffer,0,read); copied?.Invoke(read);
                }
                output.Flush(Atomic.FlushToDisk);
                sha.TransformFinalBlock(new byte[0],0,0);
                return Hex(sha.Hash);
            }
        }
    }
}

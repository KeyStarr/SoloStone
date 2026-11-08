using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SoloStone
{
    // The three registry calls uninstall needs, below HKCU\Software and only for SoloStone's own company key. WindowsRegistry is the thin Windows side.
    public interface IUserRegistry
    {
        void DeleteSoftwareTree(string path); // With everything below it. A key that is not there is fine.
        bool SoftwareKeyEmpty(string path); // It exists and holds no values and no subkeys.
        void DeleteEmptySoftwareKey(string path); // Refuses a key that holds anything.
    }
    // Unity writes HKCU\Software\<company>\<product> and LocalLow\<company>\<product>, and the company name sits in two of the client's data files. The copy gets its own name
    // there, so its settings stay out of the key a player's retail Hearthstone uses. Bytes, not encodings; the new name has the old one's length, so no offset moves.
    // The offsets are facts about one client and are never stored: each file must hold exactly one old name, found at run time.
    public static class ClientIdentity
    {
        public const string OldName="Blizzard Entertainment", NewName="SoloStone Offline Mods", Product="Hearthstone";
        public const string GameUid="SoloStone"; // The game's -uid, so its options folder (%LOCALAPPDATA%\Blizzard\Hearthstone\SoloStone) is its own.
        public static readonly string[] Paths={"Hearthstone_Data/globalgamemanagers","Hearthstone_Data/app.info"};
        static byte[] Bytes(string name) => Encoding.ASCII.GetBytes(name);
        public static List<int> Matches(byte[] data,string name) {
            byte[] token=Bytes(name); var found=new List<int>();
            for(int i=0;i+token.Length<=data.Length;i++) {
                if(data[i]!=token[0]) continue;
                int j=1; while(j<token.Length && data[i+j]==token[j]) j++;
                if(j==token.Length) found.Add(i);
            }
            return found;
        }
        // No claim about what was or wasn't changed: each caller adds the part that is true where it fires.
        static string Refusal(string path,string why) => "This client's files aren't what SoloStone expects ("+path+": "+why+").";
        // The source before any copy: one old name, no new one. Null when the file is fit to patch.
        public static string Problem(string path,byte[] data) {
            int old=Matches(data,OldName).Count, current=Matches(data,NewName).Count;
            if(old!=1) return Refusal(path,old+" matches of the company name, expected 1");
            if(current!=0) return Refusal(path,"it already holds SoloStone's name");
            return null;
        }
        // Read-only: the first problem in a client folder, or null. Setup runs this on the player's own folder before copying.
        public static string SourceProblem(string folder) {
            foreach(string path in Paths) {
                string file=Safety.Child(folder,path);
                if(!File.Exists(file)) return Refusal(path,"missing");
                Safety.NoLinks(file);
                string problem=Problem(path,File.ReadAllBytes(file));
                if(problem!=null) return problem;
            }
            return null;
        }
        // Same length, so nothing else in the file moves.
        public static byte[] Patch(string path,byte[] data) {
            string problem=Problem(path,data);
            if(problem!=null) throw new InvalidOperationException(problem);
            var result=(byte[])data.Clone();
            Array.Copy(Bytes(NewName),0,result,Matches(data,OldName)[0],NewName.Length);
            return result;
        }
        // In the stage only, after the copy is verified. Returns the patched files' hashes for the install record.
        public static Dictionary<string,string> Apply(string stage) {
            var hashes=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(string path in Paths) {
                string file=Safety.Child(stage,path); Safety.NoLinks(file);
                Atomic.Write(file,Patch(path,File.ReadAllBytes(file))); // Durable and retried: a virus scanner holding the file for a moment must not cost a recopy.
                hashes[path]=Hashes.FileHash(file);
            }
            return hashes;
        }
        public const string NotRecorded="This copy was made before SoloStone kept its settings apart from Hearthstone's. ";
        // Before a launch and when recovery decides a copy is whole: the recorded hashes, and the names themselves, so an edited record can't bless an unpatched file.
        public static void Verify(string copy,IDictionary<string,string> recorded) {
            if(recorded==null || recorded.Count!=Paths.Length || Paths.Any(p=>!recorded.ContainsKey(p)))
                throw new InvalidOperationException(NotRecorded+"It was not started. "+InstallRecord.StartOver);
            const string Lost="The copy's settings files no longer carry SoloStone's own name, so starting it would write into your own Hearthstone's Windows settings. It was not started. ";
            foreach(string path in Paths) {
                string file=Safety.Child(copy,path);
                if(!File.Exists(file)) throw new InvalidOperationException(Lost+InstallRecord.StartOver);
                byte[] data=File.ReadAllBytes(file);
                string hash=recorded[path];
                if(hash==null || hash.Length!=64 || !Safety.Same(Hashes.FileHash(file),hash)
                    || Matches(data,NewName).Count!=1 || Matches(data,OldName).Count!=0)
                    throw new InvalidOperationException(Lost+InstallRecord.StartOver);
            }
        }
        // Everything RemoveTraces would refuse, found before anything is deleted: a link on the way, or a link anywhere inside the folder.
        public static void Precheck(string localLow) {
            string folder=Path.Combine(localLow,NewName,Product);
            Safety.NoLinks(folder);
            if(Directory.Exists(folder)) Safety.Tree(folder).ToArray();
        }
        // Uninstall, for the account running it: exactly these two registry keys and folders, then each shared parent only if it is empty. Never the old name.
        public static void RemoveTraces(IUserRegistry registry,string localLow) {
            string parent=Path.Combine(localLow,NewName), folder=Path.Combine(parent,Product);
            Precheck(localLow); // Before the first deletion, so a link stops the whole removal.
            registry.DeleteSoftwareTree(NewName+"\\"+Product);
            if(registry.SoftwareKeyEmpty(NewName)) registry.DeleteEmptySoftwareKey(NewName);
            FsOps.DeleteTree(folder);
            if(Directory.Exists(parent) && Directory.GetFileSystemEntries(parent).Length==0) Directory.Delete(parent);
        }
    }
}

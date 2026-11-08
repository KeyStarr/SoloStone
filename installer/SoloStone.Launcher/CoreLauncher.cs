using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

// Launcher-side companions to Core.cs. Not compiled into the plugin.
namespace SoloStone
{
    public static class Json
    {
        public static T Read<T>(string path) {
            Safety.NoLinks(path);
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidOperationException("Installation metadata is too large. "+InstallRecord.DamagedStep);
            return JsonBytes.Parse<T>(File.ReadAllBytes(path));
        }
        public static void Save<T>(string path, T value) {
            Safety.NoLinks(path); Safety.NoLinks(path + ".tmp");
            using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None)) { byte[] bytes = JsonBytes.ToBytes(value); stream.Write(bytes,0,bytes.Length); }
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null); else File.Move(path + ".tmp",path);
        }
    }
    public static class Discovery
    {
        public static string[] UninstallEntries(IEnumerable<(string Key, string Name, string Location)> entries) => Find(entries
            .Where(e=>string.Equals(e.Key,"Hearthstone",StringComparison.OrdinalIgnoreCase) || string.Equals(e.Name,"Hearthstone",StringComparison.OrdinalIgnoreCase))
            .Select(e=>e.Location));
        public static string Prefill(string current, string[] found) => current.Length==0 && found.Length>0 ? found[0] : current;
        public static string[] Find(IEnumerable<string> candidates) {
            var found=new List<string>();
            foreach(string value in candidates.Where(p=>!string.IsNullOrWhiteSpace(p))) {
                try { string path=Safety.Canonical(value); Safety.NoLinks(path);
                    if (File.Exists(Path.Combine(path,"Hearthstone.exe")) && !found.Any(p=>Safety.Same(path,p))) found.Add(path);
                } catch(InvalidOperationException) {} catch(IOException) {} catch(UnauthorizedAccessException) {}
            }
            return found.ToArray();
        }
    }
    public static partial class FirewallPlan
    {
        public static ProcessStartInfo Elevation(InstallRecord record,bool apply,string systemDirectory) {
            CmdQuote(systemDirectory);
            systemDirectory=Safety.Canonical(systemDirectory);
            if(!string.Equals(Path.GetFileName(systemDirectory),"System32",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Windows System32 directory is required for firewall changes.");
            string netsh=CmdQuote(Path.Combine(systemDirectory,"netsh.exe"));
            // Missing rules make netsh delete return 1. Attempt every fixed deletion; read-back is authoritative.
            string commands=string.Join(" & ",Create(record,false).Select(command=>netsh+" "+command));
            commands+=apply?" & "+string.Join(" && ",Create(record,true).Select(command=>netsh+" "+command)):" & exit /b 0";
            var start=new ProcessStartInfo(Path.Combine(systemDirectory,"cmd.exe"),"/d /v:off /s /c \""+commands+"\"") {
                UseShellExecute=true, Verb="runas", WorkingDirectory=systemDirectory };
            // cmd has an 8191-character limit. Never execute a truncated security plan.
            if(CommandLine(start).Length>8191) throw new InvalidOperationException("Firewall command exceeds the Windows command-line limit. Choose a shorter installation path or fewer copied executables.");
            return start;
        }
        public static string CommandLine(ProcessStartInfo start) => CmdQuote(start.FileName)+" "+start.Arguments;
    }
    public static partial class LaunchGrant
    {
        public static string Text(string runId,string verdict,string clientVersion) {
            if(verdict!="allow" && verdict!="warning") throw new InvalidOperationException("Only an allowed or accepted client can start.");
            return "schema=1\nrunId="+runId+"\nverdict="+verdict+"\nclientVersion="+clientVersion+"\n";
        }
        public static void Write(string reports,string runId,string verdict,string clientVersion) {
            string path=PathIn(reports); Safety.NoLinks(path); Safety.NoLinks(path+".tmp");
            File.WriteAllText(path+".tmp",Text(runId,verdict,clientVersion));
            if(File.Exists(path)) File.Replace(path+".tmp",path,null); else File.Move(path+".tmp",path);
        }
        public static void Remove(string reports) { string path=PathIn(reports); Safety.NoLinks(path); if(File.Exists(path)) File.Delete(path); }
    }
    public static class Relaunch
    {
        public static string Read(string path,string previousRun,DateTime launchedUtc) => Read(path,previousRun,launchedUtc,out _);
        public static string Read(string path,string previousRun,DateTime launchedUtc,out string target) {
            target=null;
            Safety.NoLinks(path);
            if(!File.Exists(path) || new FileInfo(path).Length>4096 || File.GetLastWriteTimeUtc(path)<launchedUtc.AddSeconds(-2))
                throw new InvalidOperationException("Save-switch request is missing or stale. Start SoloStone again.");
            var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=4096 };
            var doc=new XmlDocument { XmlResolver=null };
            using(var reader=XmlReader.Create(path,settings)) doc.Load(reader);
            string token=doc.SelectSingleNode("/ProfileSwitchRequest/Token")?.InnerText;
            target=doc.SelectSingleNode("/ProfileSwitchRequest/TargetId")?.InnerText;
            Guid id;
            if(doc.DocumentElement.Name!="ProfileSwitchRequest" || doc.SelectSingleNode("/ProfileSwitchRequest/Version")?.InnerText!="1"
                || doc.SelectSingleNode("/ProfileSwitchRequest/RunId")?.InnerText!=previousRun
                || token==null || token.Length!=32 || !token.All(Uri.IsHexDigit)
                || (target!="legacy" && !Guid.TryParseExact(target,"N",out id)))
                throw new InvalidOperationException("Save-switch request does not match this game. Start SoloStone again.");
            return token;
        }
    }
}

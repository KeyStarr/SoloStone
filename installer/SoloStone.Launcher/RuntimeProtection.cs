using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace SoloStone
{
    public static class WindowsInstallPaths
    {
        public static string[] RegisteredFolders() {
            var paths=new List<string>();
            foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})
                foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64})
                    using(var baseKey=RegistryKey.OpenBaseKey(hive,view))
                    using(var key=baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")) {
                        if(key==null) continue;
                        foreach(string subkey in key.GetSubKeyNames())
                            using(var sub=key.OpenSubKey(subkey)) {
                                string publisher=sub?.GetValue("Publisher") as string;
                                string name=sub?.GetValue("DisplayName") as string;
                                if((publisher??"").IndexOf("Blizzard",StringComparison.OrdinalIgnoreCase)<0
                                    && (name??"").IndexOf("Hearthstone",StringComparison.OrdinalIgnoreCase)<0) continue;
                                string path=sub.GetValue("InstallLocation") as string;
                                if(!string.IsNullOrWhiteSpace(path)) paths.Add(path.Trim().Trim('"'));
                            }
                    }
            // Older Battle.net installs store the game location here instead of in Uninstall.
            foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})
                foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64})
                    using(var key=RegistryKey.OpenBaseKey(hive,view))
                    using(var game=key.OpenSubKey(@"SOFTWARE\Blizzard Entertainment\Hearthstone")) {
                        string path=game?.GetValue("InstallPath") as string;
                        if(!string.IsNullOrWhiteSpace(path)) paths.Add(path);
                    }
            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
    public static partial class FirewallProtection
    {
        public static string[] Executables(InstallRecord record) => Safety.Tree(record.Client)
            .Where(p=>p.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    // What Windows stores for one firewall rule: "v2.33|Action=Block|Active=TRUE|Dir=Out|App=...|Name=...|".
    public sealed class FirewallRuleText
    {
        public readonly Dictionary<string,List<string>> Fields=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);
        public static FirewallRuleText Parse(string text) {
            if(string.IsNullOrEmpty(text) || text[0]!='v') return null;
            var rule=new FirewallRuleText();
            string[] parts=text.Split('|');
            for(int i=1;i<parts.Length;i++) {
                if(parts[i].Length==0) continue;
                int equals=parts[i].IndexOf('=');
                if(equals<=0) return null;
                string key=parts[i].Substring(0,equals);
                List<string> values;
                if(!rule.Fields.TryGetValue(key,out values)) rule.Fields[key]=values=new List<string>();
                values.Add(parts[i].Substring(equals+1));
            }
            return rule;
        }
        public string One(string key) { List<string> values; return Fields.TryGetValue(key,out values) && values.Count==1?values[0]:null; }
    }
    public sealed class FirewallSnapshot
    {
        public List<string> Rules=new List<string>();
        public bool ProfileOff, PolicyOff, PolicyNoLocalRules, ExcludedInterface, ServiceStopped;
    }
    // The firewall check the plugin runs inside Unity's Mono, which has no COM binder. The launcher runs this too, beside its COM check.
    public static class FirewallRegistry
    {
        const string Policy=@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy";
        const string GroupPolicy=@"SOFTWARE\Policies\Microsoft\WindowsFirewall";
        static readonly string[] Profiles={"DomainProfile","StandardProfile","PublicProfile"};
        // Group Policy's keys. The Windows Defender Firewall with Advanced Security policy writes the Private profile as PrivateProfile;
        // the older Windows Firewall policy (Administrative Templates) writes it as StandardProfile, like the local settings above.
        internal static readonly string[] PolicyProfiles={"DomainProfile","PrivateProfile","StandardProfile","PublicProfile"};
        const string AllProfiles="Domain|Private|Public";
        public static FirewallSnapshot Read() {
            var snapshot=new FirewallSnapshot();
            snapshot.ServiceStopped=FirewallServiceRunning()==false;
            snapshot.Rules.AddRange(RuleStrings(Policy+@"\FirewallRules"));
            foreach(string profile in Profiles)
                using(var key=Registry.LocalMachine.OpenSubKey(Policy+@"\"+profile)) {
                    if(key==null) continue;
                    if(Off(key,"EnableFirewall")) snapshot.ProfileOff=true;
                    object excluded=key.GetValue("DisabledInterfaces");
                    string[] many=excluded as string[]; string one=excluded as string;
                    if((many!=null && many.Any(v=>!string.IsNullOrWhiteSpace(v))) || !string.IsNullOrWhiteSpace(one)) snapshot.ExcludedInterface=true;
                }
            foreach(string profile in PolicyProfiles)
                using(var key=Registry.LocalMachine.OpenSubKey(GroupPolicy+@"\"+profile))
                    if(key!=null && Off(key,"EnableFirewall")) snapshot.PolicyOff=true;
            snapshot.PolicyNoLocalRules=PolicyDropsLocalRules();
            return snapshot;
        }
        // "Apply local firewall rules: No" in any profile's Group Policy: Windows then ignores SoloStone's rules on that profile.
        public static bool PolicyDropsLocalRules() => PolicyProfiles.Any(profile=>PolicyValue(profile,"AllowLocalPolicyMerge")==0);
        // Group Policy's "Block all incoming connections" (DoNotAllowExceptions=1) on every profile in `active`, COM's profile bits
        // (1 Domain, 2 Private, 4 Public). COM's BlockAllInboundTraffic shows only the local setting, so this reads the policy itself.
        // A policy that can't be read counts as not blocking, so state 1 keeps its refusal.
        public static bool PolicyBlocksAllInbound(int active) {
            try {
                foreach(int bit in new[]{1,2,4})
                    if((active & bit)!=0 && !(bit==1?new[]{"DomainProfile"}:bit==2?new[]{"PrivateProfile","StandardProfile"}:new[]{"PublicProfile"})
                        .Any(profile=>PolicyValue(profile,"DoNotAllowExceptions")==1)) return false;
                return true;
            } catch(Exception) { return false; }
        }
        static int? PolicyValue(string profile,string name) {
            using(var key=Registry.LocalMachine.OpenSubKey(GroupPolicy+@"\"+profile)) {
                object value=key==null?null:key.GetValue(name);
                return value is int?(int)value:(int?)null;
            }
        }
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode)] static extern int RegOpenKeyExW(IntPtr key,string subKey,int options,int access,out IntPtr result);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode)] static extern int RegEnumValueW(IntPtr key,uint index,StringBuilder name,ref uint nameLength,IntPtr reserved,out uint type,byte[] data,ref uint dataLength);
        [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr key);
        // The client's stripped mscorlib has no RegistryKey.GetValueNames, so the value list comes from Win32 directly.
        static List<string> RuleStrings(string path) {
            const int KeyRead=0x20019, Wow64Key64=0x0100, NoMoreItems=259, MoreData=234, RegSz=1, MaxName=16384;
            var found=new List<string>();
            IntPtr key;
            int opened=RegOpenKeyExW(new IntPtr(unchecked((int)0x80000002)),path,0,KeyRead|Wow64Key64,out key);
            if(opened==2) return found;
            if(opened!=0) throw new InvalidOperationException("The firewall rules could not be read.");
            try {
                byte[] data=new byte[65536];
                for(uint index=0;;) {
                    var name=new StringBuilder(MaxName); uint nameLength=MaxName, dataLength=(uint)data.Length, type;
                    int result=RegEnumValueW(key,index,name,ref nameLength,IntPtr.Zero,out type,data,ref dataLength);
                    if(result==NoMoreItems) return found;
                    if(result==MoreData) { data=new byte[Math.Max(data.Length*2,(int)dataLength)]; continue; }
                    if(result!=0) throw new InvalidOperationException("The firewall rules could not be read.");
                    if(type==RegSz) found.Add(Encoding.Unicode.GetString(data,0,(int)dataLength).TrimEnd('\0'));
                    index++;
                }
            } finally { RegCloseKey(key); }
        }
        // The one thing the rules can't say: whether anything enforces them. Null when Windows won't tell us, and only "stopped" refuses.
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="OpenSCManagerW")] static extern IntPtr OpenSCManager(string machine,string database,uint access);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="OpenServiceW")] static extern IntPtr OpenService(IntPtr manager,string name,uint access);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool QueryServiceStatus(IntPtr service,uint[] status);
        [DllImport("advapi32.dll")] static extern bool CloseServiceHandle(IntPtr handle);
        static bool? FirewallServiceRunning() {
            IntPtr manager=OpenSCManager(null,null,1);
            if(manager==IntPtr.Zero) return null;
            try {
                IntPtr service=OpenService(manager,"mpssvc",4);
                if(service==IntPtr.Zero) return null;
                try { var status=new uint[7]; return QueryServiceStatus(service,status)?status[1]==4:(bool?)null; }
                finally { CloseServiceHandle(service); }
            } finally { CloseServiceHandle(manager); }
        }
        static bool Off(RegistryKey key,string name) { object value=key.GetValue(name); return value is int && (int)value==0; }
        public static void Verify(InstallRecord record,string[] executables) {
            try { Check(record,executables,Read()); }
            catch(InvalidOperationException) { throw; }
            catch(Exception) { throw new InvalidOperationException("Windows Firewall protection could not be verified. SoloStone cannot start. Run SoloStone firewall --apply."); }
        }
        // Pure, and strict: an unknown field may narrow the rule, so only fields known to leave it covering everything pass.
        public static void Check(InstallRecord record,string[] executables,FirewallSnapshot snapshot) {
            if(snapshot.ServiceStopped) throw new InvalidOperationException("The Windows Firewall service is stopped, so firewall rules do nothing. Start the Windows Firewall service, then try again.");
            if(snapshot.PolicyNoLocalRules) throw new InvalidOperationException("Windows policy prevents SoloStone from verifying its local firewall rules. Contact the computer administrator.");
            if(snapshot.ProfileOff || snapshot.PolicyOff) throw new InvalidOperationException("Windows Firewall must be enabled for all network profiles before SoloStone can start.");
            if(snapshot.ExcludedInterface) throw new InvalidOperationException("Windows Firewall excludes a network interface. SoloStone cannot start.");
            var parsed=snapshot.Rules.Select(FirewallRuleText.Parse).Where(rule=>rule!=null).ToList();
            foreach(string exe in executables) {
                string name=FirewallPlan.Name(record,exe);
                var matches=parsed.Where(rule=>rule.One("Name")==name).ToList();
                if(matches.Count!=1) throw new InvalidOperationException("SoloStone firewall rules are missing or duplicated. Run SoloStone firewall --apply.");
                if(!Blocks(matches[0],exe)) throw new InvalidOperationException("SoloStone firewall protection is incomplete. Run SoloStone firewall --apply.");
            }
        }
        static bool Blocks(FirewallRuleText rule,string exe) {
            foreach(var field in rule.Fields) {
                string key=field.Key, value=field.Value.Count==1?field.Value[0]:null;
                bool good;
                switch(key.ToLowerInvariant()) {
                    case "action": good=value!=null && value.Equals("Block",StringComparison.OrdinalIgnoreCase); break;
                    case "active": good=value!=null && value.Equals("TRUE",StringComparison.OrdinalIgnoreCase); break;
                    case "dir": good=value!=null && value.Equals("Out",StringComparison.OrdinalIgnoreCase); break;
                    case "app": good=value!=null && Safety.Same(value,exe); break;
                    case "name": case "desc": good=true; break;
                    case "profile": good=string.Join("|",field.Value.OrderBy(v=>v,StringComparer.OrdinalIgnoreCase))==AllProfiles; break;
                    case "protocol": good=value=="256"; break;
                    case "iftype": good=value!=null && value.Equals("All",StringComparison.OrdinalIgnoreCase); break;
                    case "embedctxt": good=value=="SoloStone"; break;
                    default: good=false; break;
                }
                if(!good) return false;
            }
            return rule.One("Action")!=null && rule.One("Active")!=null && rule.One("Dir")!=null && rule.One("App")!=null;
        }
    }
}

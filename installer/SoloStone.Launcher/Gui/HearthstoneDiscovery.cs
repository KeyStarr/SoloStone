using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace SoloStone
{
    internal static class HearthstoneDiscovery
    {
        public static string[] HearthstoneUninstallFolders() {
            var entries=new List<(string Key, string Name, string Location)>();
            foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})
                foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) {
                    try {
                        using(var baseKey=RegistryKey.OpenBaseKey(hive,view))
                        using(var key=baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")) {
                            if(key==null) continue;
                            foreach(string name in key.GetSubKeyNames())
                                using(var sub=key.OpenSubKey(name)) {
                                    string display=sub?.GetValue("DisplayName") as string;
                                    if(!string.Equals(name,"Hearthstone",StringComparison.OrdinalIgnoreCase) && !string.Equals(display,"Hearthstone",StringComparison.OrdinalIgnoreCase)) continue;
                                    entries.Add((name,display,(sub?.GetValue("InstallLocation") as string)?.Trim().Trim('"')));
                                }
                        }
                    } catch(UnauthorizedAccessException) { } catch(System.Security.SecurityException) { } catch(IOException) { }
                }
            return Discovery.UninstallEntries(entries);
        }
    }
}

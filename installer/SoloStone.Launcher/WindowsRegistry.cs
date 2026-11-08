using System;
using Microsoft.Win32;

namespace SoloStone
{
    // The Windows side of IUserRegistry. HKCU only, so no elevation, and only SoloStone's own company key: a path for any other key is refused here as well as by the caller.
    internal sealed class WindowsRegistry : IUserRegistry
    {
        static string Own(string path) {
            if(string.IsNullOrEmpty(path) || path.Split('\\')[0]!=ClientIdentity.NewName) throw new InvalidOperationException("Not SoloStone's own registry key.");
            return @"Software\"+path;
        }
        public void DeleteSoftwareTree(string path) { Registry.CurrentUser.DeleteSubKeyTree(Own(path),false); }
        public bool SoftwareKeyEmpty(string path) {
            using(var key=Registry.CurrentUser.OpenSubKey(Own(path),false)) return key!=null && key.ValueCount==0 && key.SubKeyCount==0;
        }
        public void DeleteEmptySoftwareKey(string path) {
            if(!SoftwareKeyEmpty(path)) throw new InvalidOperationException("SoloStone's registry key is not empty.");
            Registry.CurrentUser.DeleteSubKey(Own(path),false);
        }
    }
}

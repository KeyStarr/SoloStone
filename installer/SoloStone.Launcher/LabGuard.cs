#if SOLOSTONE_TEST_HOOKS
using System.Collections.Generic;
using System.Linq;

namespace SoloStone
{
    // The disposable lab's real install and accepted account. Only hook builds run there, and only they carry this.
    internal static class LabGuard
    {
        internal static readonly string[] Roots={@"D:\Battle"+@"Net\Hearthstone",@"C:\Us"+@"ers\HSOffline"+@"Probe"};
        // Text only, so it works before any disk access and for Windows paths on any machine: separators, "." and "..".
        internal static string Normalize(string path) {
            var parts=new List<string>();
            foreach(string part in (path??"").Replace('/','\\').Split('\\')) {
                if(part=="." || part.Length==0) continue;
                if(part==".." && parts.Count>1) { parts.RemoveAt(parts.Count-1); continue; }
                parts.Add(part);
            }
            return string.Join("\\",parts);
        }
        internal static bool IsProtected(string path) {
            string normal=Normalize(path);
            string longName=normal.IndexOf('~')>=0?Normalize(PhysicalPaths.LongName(normal)):normal;
            return Roots.Any(root=>Safety.Within(normal,root) || Safety.Within(longName,root));
        }
    }
}
#endif

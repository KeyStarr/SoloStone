using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SoloStone;

static partial class Program
{
    const string NetDevHeader="Inter-|   Receive                                                |  Transmit\n"
        +" face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed\n";
    static void LinuxTests(string temp) {
        // The isolation the Linux launcher gives the game: /proc/net/dev lists loopback only. (The no-route canary is the plugin's, in OfflineProbe.)
        string loopback=NetDevHeader+"    lo:       0       0    0    0    0     0          0         0        0       0    0    0    0     0       0          0\n";
        string networked=loopback+"enp13s0: 21758956135 15679490    0  208    0     0          0     45313 5235610294 1811851    0    0    0     0       0          0\n";
        NetworkIsolation.Check(loopback); NetworkIsolation.Check(loopback.Replace("\n","\r\n"));
        Check(string.Join(",",NetworkIsolation.Interfaces(networked))=="lo,enp13s0","interface names read from /proc/net/dev");
        RejectsBecause(()=>NetworkIsolation.Check(networked),"enp13s0","an interface besides loopback is refused and named");
        foreach(string junk in new[]{null,"","lo: 0 0\n",NetDevHeader,NetDevHeader+"  eth0: 1 2\n",NetDevHeader+"no colon here\n"})
            RejectsBecause(()=>NetworkIsolation.Check(junk),"could not read","unreadable or loopback-less /proc/net/dev refused: "+(junk??"null").Replace("\n","|"));
        Check(!NetworkIsolation.UnderWine,"the test host is not Wine");
        // Off Wine there is no /proc to read through Wine's paths, so the Wine protection refuses: it can never pass by accident.
        RejectsBecause(NetworkIsolation.Verify,"could not read","isolation check off Wine refuses");

        // install.json keeps the game's spelling; the Linux launcher maps it to the prefix's folders and back.
        var prefix=new WinePrefix("/x/prefix","/usr/bin/wine");
        Check(prefix.ToWine("/x/prefix/drive_c/users/a/AppData/Local/SoloStone")==@"C:\users\a\AppData\Local\SoloStone","prefix path is the C: drive");
        Check(prefix.ToWine("/x/prefix/drive_c")==@"C:\","drive_c itself is C:\\");
        Check(prefix.ToWine("/home/a/games/Hearthstone")==@"Z:\home\a\games\Hearthstone","any other path is Z:");
        Check(prefix.ToWine("/x/prefix/drive_cx/y")==@"Z:\x\prefix\drive_cx\y","a sibling of drive_c is not C:");
        Check(prefix.FromWine(@"C:\users\a\AppData\Local\SoloStone")=="/x/prefix/drive_c/users/a/AppData/Local/SoloStone","C: maps back into the prefix");
        Check(prefix.FromWine(@"c:\users")=="/x/prefix/drive_c/users","drive letters in any case");
        Check(prefix.FromWine(@"Z:\home\a\games\Hearthstone")=="/home/a/games/Hearthstone","Z: maps back to /");
        foreach(string bad in new[]{null,"",@"D:\games",@"C:\users\..\..\..\etc",@"C:\.\x","relative",@"\\server\share",@"C:/users/a",@"C:"})
            Refuses(()=>prefix.FromWine(bad),"record path the Linux launcher can't map: "+(bad??"null"));

        string root=Path.Combine(temp,"linux-record","drive_c","users","a","AppData","Local","SoloStone");
        var mapped=new WinePrefix(Path.Combine(temp,"linux-record"),"/usr/bin/wine");
        Directory.CreateDirectory(root);
        var record=new InstallRecord { Schema=InstallRecord.CurrentSchema, InstallId=Guid.NewGuid().ToString("N"), Root=root, Source="/home/a/games/Hearthstone",
            ClientPath=Path.Combine(root,"client"), ClientVersion="37.0.0", Files=new Dictionary<string,string>{{"Hearthstone.exe",new string('A',64)}} };
        try {
            RecordPaths.ToDisk=mapped.ToWine; RecordPaths.FromDisk=mapped.FromWine;
            string file=Path.Combine(root,"install.json");
            Json.Save(file,record);
            string text=Encoding.UTF8.GetString(File.ReadAllBytes(file));
            Check(text.Contains(@"C:\\users\\a\\AppData\\Local\\SoloStone\\client") && text.Contains(@"Z:\\home\\a\\games\\Hearthstone") && !text.Contains(temp),"install.json holds the game's spelling");
            Check(record.Root==root && record.Source=="/home/a/games/Hearthstone","saving leaves the launcher's own record in host paths");
            var read=InstallRecord.FromBytes(File.ReadAllBytes(file));
            Check(read.Root==root && read.ClientPath==Path.Combine(root,"client") && read.Source=="/home/a/games/Hearthstone","the plain reader maps back");
            var parsed=Json.Read<InstallRecord>(file);
            Check(parsed.Root==root && parsed.ClientPath==Path.Combine(root,"client"),"the data contract reader maps back (update journal)");
        } finally { RecordPaths.ToDisk=path=>path; RecordPaths.FromDisk=path=>path; }
        // The applications-menu entry starts this launcher wherever the player unpacked it, quoted as the Desktop Entry spec asks.
        Check(DesktopEntry.Quote("/home/a b/SoloStone/solostone")=="\"/home/a b/SoloStone/solostone\"","a path with a space is quoted");
        Check(DesktopEntry.Quote("/x/$HOME`\"\\y")=="\"/x/\\$HOME\\`\\\"\\\\y\"","$ ` \" and \\ are escaped");
        string entry=DesktopEntry.Text("/opt/solo stone/solostone","/opt/solo stone/solostone.png");
        Check(entry.StartsWith("[Desktop Entry]\n") && entry.Contains("\nExec=\"/opt/solo stone/solostone\" play\n") && entry.Contains("\nIcon=/opt/solo stone/solostone.png\n") && entry.Contains("\nTerminal=true\n"),"desktop entry runs play in a terminal");
        Check(!DesktopEntry.Text("/x/solostone",null).Contains("Icon="),"no icon line without an icon");
        Check(InstallRecord.FromBytes(Encoding.UTF8.GetBytes("{\"schema\":2,\"root\":\"/r\",\"clientPath\":\"/r/client\"}")).Root=="/r","unmapped by default");
    }
}

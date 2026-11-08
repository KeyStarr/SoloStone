using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using SoloStone;

static partial class Program
{
    sealed class FullDisk : MemoryStream { public override void Write(byte[] b,int o,int c) { throw new IOException("There is not enough space on the disk."); } }
    static void LastRoundTests(string temp) {
#if SOLOSTONE_INSTALLER
        // N1: only a disk, access or data problem is a setup failure. Anything else in startup is a real mod failure, and stays 42.
        Func<Action,int> exit=step=>{ try { InstallerRuntime.Prepare(step); return 0; } catch(InstallerRefusal r) { return r.ExitCode; } };
        foreach(var failure in new Exception[]{new MissingMethodException("gone"),new TypeLoadException("gone"),new NullReferenceException(),new ArgumentException("bad"),new MissingFieldException("gone")}) {
            bool escaped=false;
            try { InstallerRuntime.Prepare(()=>throw failure); } catch(InstallerRefusal) { } catch(Exception e) when(ReferenceEquals(e,failure)) { escaped=true; }
            Check(escaped,"a mod failure stays a hook failure: "+failure.GetType().Name);
        }
        foreach(var failure in new Exception[]{new IOException("full"),new UnauthorizedAccessException("no"),new System.Xml.XmlException("bad save"),new InvalidDataException("bad save"),new FormatException("bad save"),new InvalidOperationException("guard")})
            Check(exit(()=>throw failure)==Health.ExitSetupFailed,"a setup problem is not one: "+failure.GetType().Name);
#endif
        // N3: a dropped connection is the network; only our own disk write is the disk.
        var drop=Serve("HTTP/1.1 200 OK\r\nContent-Length: 500\r\nConnection: close\r\n\r\nonly a little");
        string dropped=null;
        try { new WebHttp().Get(drop.url,1000,new MemoryStream(),CancellationToken.None); } catch(Exception e) { dropped=Tls.Describe(e); }
        Check(dropped!=null && dropped.Contains("not reachable"),"a connection dropped mid-download reads as the network: "+dropped);
        var disk=Serve("HTTP/1.1 200 OK\r\nContent-Length: 4\r\nConnection: close\r\n\r\ndata");
        string full=null;
        try { new WebHttp().Get(disk.url,1000,new FullDisk(),CancellationToken.None); } catch(Exception e) { full=Tls.Describe(e); }
        Check(full!=null && full.Contains("available space"),"a failed disk write reads as the disk: "+full);
        // N2: uninstall must not run from a folder it is about to delete.
        string root=Path.Combine(temp,"n2-root");
        foreach(string folder in new[]{"updates","releases"}) {
            Check(Installer.UninstallProblem(root,Path.Combine(root,folder,"2.0.0","app"))!=null,"uninstall refuses to run from "+folder);
            Check(Installer.UninstallProblem(root,Path.Combine(root,folder,"2.0.0","app")+Path.DirectorySeparatorChar)!=null,"with a trailing separator too");
        }
        Check(Installer.UninstallProblem(root,Path.Combine(root,"app"))==null && Installer.UninstallProblem(root,Path.Combine(temp,"is-tmp"))==null,"the app folder and a temp copy may");
        string iss=Source("SoloStone.iss");
        Check(iss.Contains("{tmp}\\fallback") && iss.Contains("FileCopy("),"Inno's fallback runs from a temp copy");
        // N4 and N5: a helper's words, and setup's.
        var f=NewFixture(temp); Check(Update(f,"2.0.0").Outcome==UpdateOutcome.Swapped,"swap");
        var j=f.Engine().Journal; j.Phase=SwapJournal.Undoing; File.WriteAllBytes(Path.Combine(f.Root,"updates","journal.json"),JsonBytes.ToBytes(j)); f.Host.InApp=false; f.Host.Fault=null;
        var failing=new UpdateEngine(f.Root,f.Host) { Fault=label=>{ if(label=="undo:moved") throw new IOException("disk gone"); } };
        RejectsBecause(()=>failing.RunHelper("finish-undo",null),"Close this window","a failed helper says to close its window first");
        string host=Source("Gui/GuiHost.cs");
        Check(host.Contains("SingleInstance.RaiseHelper(options.Root)") && host.Contains("AllowSetForegroundWindow") && Source("Program.cs").Contains("internal void Raise()"),"a re-click raises the helper's window");
        var g=NewFixture(temp); Update(g,"2.0.0"); var cut=g.Engine().Journal; cut.Phase=SwapJournal.Moved; File.WriteAllBytes(Path.Combine(g.Root,"updates","journal.json"),JsonBytes.ToBytes(cut));
        using(Lease.Open(g.Root)) Throws<SetupBusyException>(()=>Installer.ResumeInterrupted(g.Root,()=>new UpdateEngine(g.Root,g.Host)),"a live helper is a distinct result");
        Check(iss.Contains("Code = 3") && iss.Contains("Wait for its window to close") && !iss.Contains("has been finished or undone") && iss.Contains("Code = 5") && iss.Contains("Couldn''t read the install folder") && iss.Contains("GetExceptionMessage") && Source("Program.cs").Contains("e is SetupBusyException?3:e is SetupOccupiedException?4:e is IOException || e is UnauthorizedAccessException?5:1"),"setup says what is true for each case");
    }
}

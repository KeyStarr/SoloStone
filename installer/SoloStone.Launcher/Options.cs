using System;
using System.IO;
using System.Linq;

namespace SoloStone
{
    internal sealed class Options
    {
        internal string Command="launch", Source, ClientPath, BackupId, UpdatesSwitch, Uninstaller, App, Version, Root=DefaultRoot();
        internal int WaitPid; internal bool StartAppAfter, HookBuild, NoSetup, AfterHelper; internal string ClientReference, OutFolder;
        internal static string DefaultRoot() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SoloStone");
        internal bool Quiet, Apply, Remove, DryRun, DeleteSaves, Inno, Json, CheckOnly, AcceptWarning;
        // The command line a launcher hands its work to. Parse below is the other half; a test round-trips the two.
        internal static string HelperArguments(string command,string root,string version,int waitPid)
            => command+(version==null?"":" --version "+version)+" --root "+FirewallPlan.Quote(root)+" --wait-pid "+waitPid;
        internal static Options Parse(string[] args) {
            var o=new Options(); int i=0;
            Func<string,string> value=name=>{ if(++i>=args.Length) throw new InvalidOperationException(name+" needs a value."); return args[i]; };
            if(args.Length>0 && !args[0].StartsWith("--")) { o.Command=args[0]; i=1; }
            for(;i<args.Length;i++) switch(args[i]) {
                case "--source": o.Source=value("--source"); break;
                case "--client": o.ClientPath=value("--client"); break;
                case "--root": o.Root=value("--root"); break;
                case "--app": o.App=value("--app"); break;
                case "--id": o.BackupId=value("--id"); break;
                case "--updates": o.UpdatesSwitch=value("--updates"); break;
                case "--version": o.Version=value("--version"); break;
                case "--client-reference": o.ClientReference=value("--client-reference"); break;
                case "--hook-build": o.HookBuild=true; break;
                case "--out": o.OutFolder=value("--out"); break;
                case "--no-setup": o.NoSetup=true; break;
                case "--after-helper": o.AfterHelper=true; break;
                case "--wait-pid": { int pid; if(!int.TryParse(value("--wait-pid"),out pid)) throw new InvalidOperationException("--wait-pid needs a process id."); o.WaitPid=pid; break; }
                case "--quiet": o.Quiet=true; break;
                case "--apply": o.Apply=true; break;
                case "--remove": o.Remove=true; break;
                case "--dry-run": o.DryRun=true; break;
                case "--delete-saves": o.DeleteSaves=true; break;
                case "--inno": o.Inno=true; break;
                case "--json": o.Json=true; o.Quiet=true; break;
                case "--check-only": o.CheckOnly=true; break;
                case "--accept-warning": o.AcceptWarning=true; break;
                default: throw new InvalidOperationException("Unknown option: "+args[i]);
            }
            if(!new[]{"setup","wizard","launch","firewall","uninstall","update","rollback","restore-backup","check-client","status","settings","check-payload","check-release","finish-swap","finish-undo","preflight","preflight-uninstall","recover"}.Contains(o.Command))
                throw new InvalidOperationException("Commands: wizard, launch, setup, recover, firewall, uninstall, update, rollback, restore-backup, check-client, status, settings.");
            if(o.Command=="wizard" && (o.Quiet || o.Json)) throw new InvalidOperationException("The setup wizard is a window. Use setup --quiet --source <folder> from a script.");
            if(o.Source!=null && o.Command!="setup" && o.Command!="check-client") throw new InvalidOperationException("--source is only valid with setup or check-client.");
            if(o.ClientPath!=null && o.Command!="setup") throw new InvalidOperationException("--client is only valid with setup.");
            if(o.App!=null && o.Command!="preflight" && o.Command!="preflight-uninstall" && o.Command!="check-payload") throw new InvalidOperationException("--app requires preflight.");
            if((o.DeleteSaves || o.Inno) && o.Command!="uninstall") throw new InvalidOperationException("Uninstall options require the uninstall command.");
            if((o.Apply || o.Remove || o.DryRun) && o.Command!="firewall") throw new InvalidOperationException("Firewall options require the firewall command.");
            if(o.Json && o.Command!="check-client" && o.Command!="status") throw new InvalidOperationException("--json requires check-client or status.");
            if(o.CheckOnly && o.Command!="update") throw new InvalidOperationException("--check-only requires the update command.");
            if(o.AcceptWarning && o.Command!="setup" && o.Command!="launch" && o.Command!="update") throw new InvalidOperationException("--accept-warning requires setup, launch or update.");
            if(o.BackupId!=null && o.Command!="restore-backup") throw new InvalidOperationException("--id requires restore-backup.");
            if(o.Command=="settings" && o.UpdatesSwitch!="on" && o.UpdatesSwitch!="off") throw new InvalidOperationException("Use settings --updates on or off.");
            if(o.Command=="firewall" && ((o.Apply&&o.Remove) || (!o.Apply&&!o.Remove&&!o.DryRun)))
                throw new InvalidOperationException("Use firewall --apply, --remove or --dry-run.");
            if(o.AfterHelper && o.Command!="launch") throw new InvalidOperationException("--after-helper requires launch.");
            if((o.OutFolder!=null || o.NoSetup) && o.Command!="check-release") throw new InvalidOperationException("--out and --no-setup require check-release.");
            if(o.Command=="check-release" && (o.OutFolder==null || o.Version==null)) throw new InvalidOperationException("Use check-release --out <build folder> --version <version>.");
            o.Root=Safety.Canonical(o.Root); return o;
        }
    }
}

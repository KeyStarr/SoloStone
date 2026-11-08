using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using SoloStone;

static partial class Program
{
    static string Source(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"launcher-source",name));
    static string StripHooks(string text) {
        // Removes every "#if SOLOSTONE_TEST_HOOKS" region, up to its #else or #endif.
        return Regex.Replace(text,@"#if SOLOSTONE_TEST_HOOKS.*?(#else|#endif)","",RegexOptions.Singleline);
    }
    static void BoundaryTests() {
        // CoreLauncher.cs and WindowsFirewall.cs hold what the plugin must never compile.
        string[] launcherOnly={"CoreLauncher.cs","WindowsFirewall.cs","ClientPolicy.cs","Minisign.cs","ReleaseManifest.cs","UpdateClient.cs","UpdateEngine.cs","LaunchGate.cs","Install.cs","Fs.cs"};
        string pluginProject=Source("OfflineProbe.csproj");
        var compiled=Regex.Matches(pluginProject,@"Include=""\.\./installer/[^""]+""").Select(m=>m.Value).ToArray();
        Check(compiled.Length==3 && compiled.Any(c=>c.EndsWith("/Core.cs\"")) && compiled.Any(c=>c.EndsWith("/RuntimeProtection.cs\"")) && compiled.Any(c=>c.EndsWith("/LabGuard.cs\"")),"plugin compiles exactly Core.cs, RuntimeProtection.cs and the hook-only LabGuard.cs from the launcher");
        foreach(string file in launcherOnly) Check(!pluginProject.Contains(file),"plugin does not compile "+file);
        Check(pluginProject.Contains("SOLOSTONE_INSTALLER") && pluginProject.Contains("'$(Installer)' == 'true'") && !pluginProject.Contains("INSTALLER_PROTOTYPE") && !pluginProject.Contains("InstallerPrototype"),"plugin build uses the renamed switch");
        foreach(string file in new[]{"Core.cs","RuntimeProtection.cs","InstallerRuntime.cs"}) {
            string text=Source(file);
            foreach(string banned in new[]{"System.Net","BouncyCastle","System.Reflection.Metadata","HttpWebRequest","HttpClient","WebClient","Socket","UpdateClient","UpdateEngine","Minisign","ClientPolicy"})
                Check(!text.Contains(banned),file+" stays free of "+banned);
        }
        // Unity's Mono: no working serializer, no Microsoft.CSharp. Nothing the plugin compiles may reach for them.
        foreach(string file in new[]{"Core.cs","RuntimeProtection.cs","InstallerRuntime.cs"}) {
            string text=Regex.Replace(Source(file),@"#if !SOLOSTONE_PLUGIN.*?#endif","",RegexOptions.Singleline);
            // Naming the exception a bad save throws is not parsing XML. The default plugin already references this assembly, and the Mono guard checks it.
            text=text.Replace("System.Xml.XmlException","");
            foreach(string banned in new[]{"dynamic ","System.Runtime.Serialization","DataContractJsonSerializer","Microsoft.CSharp","GetTypeFromProgID","Regex","File.Replace","System.Xml","ProcessStartInfo","HNetCfg"})
                Check(!text.Contains(banned),file+" stays free of "+banned);
        }
        Check(!pluginProject.Contains("Microsoft.CSharp") && !pluginProject.Contains("System.Runtime.Serialization") && pluginProject.Contains("SOLOSTONE_PLUGIN"),"the plugin build references neither stub");
        // .NET Framework's reader refuses a byte order mark, and PowerShell's utf8 encoding writes one.
        foreach(string line in Source("build.ps1").Split('\n').Where(l=>l.Contains("Set-Content") || l.Contains("Out-File")))
            Check(!Regex.IsMatch(line,@"-Encoding\s+(utf8|unicode|bigendian|utf32)",RegexOptions.IgnoreCase) && line.Contains("-Encoding ascii"),"build.ps1 writes no byte order mark: "+line.Trim().Substring(0,Math.Min(50,line.Trim().Length)));
        string runtime=Source("InstallerRuntime.cs");
        Check(runtime.Contains("Type.GetType(\"Network, Assembly-CSharp\")") && !Regex.IsMatch(runtime,@"\bNetwork\.CosmeticVersion"),"plugin reads the client version by reflection, never through an inlined constant");
        Check(runtime.Contains("FirewallRegistry.Verify(") && !runtime.Contains("FirewallProtection.Verify("),"the plugin's firewall check is the registry read-back");
        Check(!runtime.Contains("BuildSpec") && !Regex.IsMatch(runtime,@"[0-9A-F]{64}"),"compiled four-file pin is gone");
        // Launcher network boundary.
        string host=Source("WindowsHost.cs"), program=Source("Program.cs");
        foreach(string banned in new[]{"System.Net","HttpWebRequest","WebHttp","UpdateClient","UpdateService","IHttp","Socket"})
            Check(!host.Contains(banned),"the file that starts the game has no network reference: "+banned);
        foreach(string file in new[]{"UpdateClient.cs","UpdateEngine.cs","LaunchGate.cs","ReleaseManifest.cs","Minisign.cs","Install.cs","ClientPolicy.cs","Fs.cs"})
            Check(!Source(file).Contains("Process.Start") && !Source(file).Contains("using System.Diagnostics"),file+" can never start the game");
        foreach(string file in new[]{"UpdateEngine.cs","Install.cs","ClientPolicy.cs","Minisign.cs","Fs.cs"})
            Check(!Regex.IsMatch(Source(file),@"HttpWebRequest|WebRequest|HttpClient|Socket|using System\.Net"),file+" opens no network");
        Check(Regex.Matches(program,@"new WebHttp\(\)").Count==1 && Regex.Matches(program,@"new UpdateService\(").Count==1,"one network entry point in the launcher");
        int runUpdate=program.IndexOf("static int RunUpdate(",StringComparison.Ordinal);
        string executeBody=program.Substring(0,runUpdate);
        Check(!executeBody.Contains("new WebHttp") && !executeBody.Contains("new UpdateService") && Regex.Matches(executeBody,@"RunUpdate\(").Count==1 && executeBody.Contains("if(o.Command==\"update\") return RunUpdate("),"network runs only for the update command, which returns");
        Check(executeBody.IndexOf("if(o.Command==\"update\") return RunUpdate(",StringComparison.Ordinal)<executeBody.IndexOf("WindowsHost.Launch(",StringComparison.Ordinal),"launch path is reached only after the update branch has returned");
        string launch=host.Substring(host.IndexOf("internal static int Launch(",StringComparison.Ordinal));
        launch=launch.Substring(0,launch.IndexOf("static bool WaitMarker(",StringComparison.Ordinal));
        Check(launch.IndexOf("Process.Start(start)",StringComparison.Ordinal)>launch.IndexOf("LaunchGrant.Write(",StringComparison.Ordinal) && launch.IndexOf("LaunchGrant.Write(",StringComparison.Ordinal)>launch.IndexOf("LaunchGate.Admit(",StringComparison.Ordinal),"verdict, then grant, then the game");
        Check(launch.IndexOf("LaunchGrant.Remove(reports)",StringComparison.Ordinal)>launch.IndexOf("WaitMarker(client,Path.Combine(reports,\"plugin-loaded.txt\")",StringComparison.Ordinal),"launcher removes the grant after the plugin marker");
        Check(launch.Contains("client.ExitCode==46") && launch.Contains("Relaunch.Read(request,run,started,out target)"),"exit-46 relaunch intact");
        Check(launch.Contains("engine.ReportHealth(HealthOutcome.Confirmed,null)") && launch.Contains("Health.Classify(false,timedOut"),"launch reports update health");
        // Test hook cannot reach a release build.
        string client=Source("UpdateClient.cs"), clean=StripHooks(client);
        foreach(string token in new[]{"TestBaseUrl","IsLoopback","127.0.0.1","localhost","\"http\"","http://","SOLOSTONE_TEST_UPDATE_BASE","GetEnvironmentVariable"})
            Check(!clean.Contains(token),"loopback override exists only inside the hook #if: "+token);
        Check(client.Contains("#if SOLOSTONE_TEST_HOOKS"),"hook region present");
        // Core.cs's only hook is the lab guard. A release has neither.
        Check(!Regex.IsMatch(StripHooks(Source("Core.cs")),@"LabGuard\.") && string.IsNullOrWhiteSpace(StripHooks(Source("LabGuard.cs")).Replace("#endif","").Replace("#else","")),"the lab guard exists only in hook builds");
        foreach(string file in launcherOnly.Where(f=>f!="UpdateClient.cs").Concat(new[]{"RuntimeProtection.cs","Program.cs"}))
            Check(!Source(file).Contains("SOLOSTONE_TEST_HOOKS") && !Source(file).Contains("TestBaseUrl") && !Source(file).Contains("SOLOSTONE_TEST_UPDATE_BASE"),file+" has no test hook");
        // The file that starts the game has two short hook regions (client search, elevation handoff) that GuiBoundaryTests proves vanish from a release.
        Check(!Source("WindowsHost.cs").Contains("TestBaseUrl") && !Source("WindowsHost.cs").Contains("SOLOSTONE_TEST_UPDATE_BASE") && Regex.Matches(Source("WindowsHost.cs"),"#if SOLOSTONE_TEST_HOOKS").Count==2,"WindowsHost.cs has exactly its two hook regions");
        string project=Source("SoloStone.Launcher.csproj");
        Check(Regex.IsMatch(project,@"<PropertyGroup Condition=""'\$\(TestHooks\)' == 'true'""><DefineConstants>\$\(DefineConstants\);SOLOSTONE_TEST_HOOKS</DefineConstants></PropertyGroup>") && Regex.Matches(project,"SOLOSTONE_TEST_HOOKS").Count==1,"hook symbol only with an explicit TestHooks build");
        foreach(string pin in new[]{"BouncyCastle.Cryptography\" Version=\"[2.6.2]\"","System.Reflection.Metadata\" Version=\"[8.0.1]\""}) Check(project.Contains(pin),"exact package pin: "+pin);
        GuiBoundaryTests();
    }
    // What a release build sees: every hook-only branch removed, anything after #else kept.
    static string ReleaseView(string text) {
        var output=new StringBuilder(); bool skipping=false;
        foreach(string line in text.Replace("\r\n","\n").Split('\n')) {
            string t=line.Trim();
            if(t=="#if SOLOSTONE_TEST_HOOKS") { skipping=true; continue; }
            if(skipping && (t=="#else" || t=="#endif")) { skipping=false; continue; }
            if(!skipping) output.Append(line).Append('\n');
        }
        return output.ToString();
    }
    static string[] LauncherSources() {
        string folder=Path.Combine(AppContext.BaseDirectory,"launcher-source");
        return Directory.GetFiles(folder,"*.cs",SearchOption.AllDirectories).Select(p=>p.Substring(folder.Length+1).Replace(Path.DirectorySeparatorChar,'/')).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
    }
    static string Body(string text,string from,string to) {
        int start=text.IndexOf(from,StringComparison.Ordinal); Check(start>=0,"found "+from);
        int end=to==null?text.Length:text.IndexOf(to,start+from.Length,StringComparison.Ordinal); Check(end>start,"found end of "+from);
        return text.Substring(start,end-start);
    }
    // The windows must keep the same promises as the command line: one network entry, finished before the game starts, and no hook in a release.
    static void GuiBoundaryTests() {
        string[] files=LauncherSources();
        string[] gui=files.Where(f=>f.StartsWith("Gui/",StringComparison.Ordinal)).ToArray();
        foreach(string expected in new[]{"Gui/Texts.cs","Gui/Messages.cs","Gui/WizardModel.cs","Gui/LauncherModel.cs","Gui/WindowsOps.cs","Gui/WizardForm.cs","Gui/LauncherForm.cs","Gui/Controls.cs","Gui/TestHooksCore.cs","Gui/TestControl.cs"})
            Check(gui.Contains(expected),"window source is covered by the boundary tests: "+expected);
        // Pure files: nothing that draws, starts programs or touches the network.
        foreach(string pure in new[]{"Gui/Texts.cs","Gui/Messages.cs","Gui/WizardModel.cs","Gui/LauncherModel.cs"}) {
            string text=Source(pure);
            foreach(string banned in new[]{"System.Windows.Forms","System.Drawing","Process.Start","System.Net","WebHttp","UpdateClient","UpdateService","new Socket","MessageBox","Application."})
                Check(!text.Contains(banned),pure+" stays free of "+banned);
        }
        // Network: one extra entry point, in the file that wires the real calls, and the launch method never reaches it.
        foreach(string file in gui.Where(f=>f!="Gui/WindowsOps.cs")) {
            string text=ReleaseView(Source(file));
            foreach(string banned in new[]{"new WebHttp","new UpdateService","UpdateClient","HttpWebRequest","WebClient","using System.Net","Socket"})
                Check(!text.Contains(banned),file+" has no network reference: "+banned);
        }
        string ops=Source("Gui/WindowsOps.cs");
        Check(Regex.Matches(ops,@"new WebHttp\(\)").Count==1 && Regex.Matches(ops,@"new UpdateService\(").Count==1,"the window code has exactly one network entry");
        string launch=Body(ops,"public int Launch(","public RollbackResult Rollback(");
        foreach(string banned in new[]{"WebHttp","UpdateService","Service(","Check(","Apply(","Http"}) Check(!launch.Contains(banned),"the launcher's Play path never reaches the network: "+banned);
        Check(launch.Contains("WindowsHost.Launch("),"Play uses the same launch path as the command line");
        foreach(string file in files.Where(f=>f!="Gui/WindowsOps.cs" && f!="WindowsHost.cs" && f!="Program.cs"))
            Check(!ReleaseView(Source(file)).Contains("WindowsHost.Launch("),file+" cannot start the game");
        string model=Source("Gui/LauncherModel.cs");
        string play=Body(model,"public int Play(","public void RollBack(");
        Check(!play.Contains("ops.Check(") && !play.Contains("ops.Apply(") && play.Contains("LauncherPhase.Ready"),"Play refuses until the start step finished, and makes no update call");
        string start=Body(model,"public bool Start(","bool CheckAndApply(");
        Check(start.IndexOf("ops.Resume()",StringComparison.Ordinal)<start.IndexOf("CheckAndApply(",StringComparison.Ordinal) && start.IndexOf("using (ops.Lease())",StringComparison.Ordinal)<start.IndexOf("ops.Resume()",StringComparison.Ordinal),"start order: lease, resume, then check and apply");
        Check(start.IndexOf("ops.Lease()",StringComparison.Ordinal)>=0 && start.LastIndexOf("Restart()",StringComparison.Ordinal)>start.IndexOf("post(model.Ready)",StringComparison.Ordinal),"the restart comes after the lease is released");
        string restart=Body(ops,"public void Restart(","public int Launch(");
        Check(restart.IndexOf("Process.Start",StringComparison.Ordinal)>=0 && restart.Contains("\"app\"") && restart.Contains("Environment.Exit"),"a swap restarts from Root\\app and the old process exits");
        // Wizard: the form never writes. Only the step 5 runner does, so Cancel before step 5 leaves nothing.
        string wizard=ReleaseView(Source("Gui/WizardForm.cs"));
        foreach(string write in new[]{"ops.Setup(","ops.Recover(","ops.ApplyFirewall(","ops.SaveSettings(","File.Write","File.Move","Directory.Create","Atomic.","Json.Save","Installer."})
            Check(!wizard.Contains(write),"the wizard form does not write by itself: "+write);
        string runnerSource=Body(Source("Gui/WizardModel.cs"),"public sealed class WizardRunner",null);
        Check(runnerSource.Contains("ops.Setup(") && Regex.Matches(wizard,@"new WizardRunner\(").Count==1,"one runner, started from the Install step");
        Check(Regex.Matches(wizard,@"runner\.Run\(").Count==1 && wizard.IndexOf("StartRun()",StringComparison.Ordinal)>0,"the runner starts from StartRun only");
        // Hooks: every region is flat, whole hook files are empty in a release, and nothing hook-like survives.
        string[] tokens={"SOLOSTONE_TEST","TestDiscovery","TestElevation","TestControl","ControlCommand","ControlFiles","elevate-request","elevate-result","BFFM_","SendInput","TestBaseUrl"};
        foreach(string file in files) {
            string raw=Source(file);
            int ifs=Regex.Matches(raw,@"^\s*#if\b",RegexOptions.Multiline).Count, ends=Regex.Matches(raw,@"^\s*#endif\b",RegexOptions.Multiline).Count;
            Check(ifs==ends,file+" has balanced conditional regions");
            Check(!Regex.IsMatch(raw,@"^\s*#if\s+(!|.*[&|])\s*.*SOLOSTONE_TEST_HOOKS",RegexOptions.Multiline),file+" uses only the plain hook switch");
            string release=ReleaseView(raw);
            foreach(string token in tokens) Check(!release.Contains(token),"a release build of "+file+" has no trace of "+token);
        }
        foreach(string hookFile in new[]{"Gui/TestHooksCore.cs","Gui/TestControl.cs","LabGuard.cs"}) {
            string raw=Source(hookFile).Trim();
            Check(raw.Contains("#if SOLOSTONE_TEST_HOOKS") && string.IsNullOrWhiteSpace(ReleaseView(raw)),hookFile+" is empty in a release build");
        }
        // Hook regions in shared files stay tiny and only call into the hook files.
        foreach(string file in files.Where(f=>f!="Gui/TestHooksCore.cs" && f!="Gui/TestControl.cs" && f!="LabGuard.cs" && f!="UpdateClient.cs")) {
            foreach(Match region in Regex.Matches(Source(file).Replace("\r\n","\n"),@"#if SOLOSTONE_TEST_HOOKS\n(.*?)(#else|#endif)",RegexOptions.Singleline))
                Check(region.Groups[1].Value.Split('\n').Length<=12,file+" keeps its hook region short");
        }
        Check(Regex.Matches(project(),"SOLOSTONE_TEST_HOOKS").Count==1,"the build defines the hook symbol in one place");
    }
    static string project() => Source("SoloStone.Launcher.csproj");
}

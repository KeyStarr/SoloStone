using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SoloStone;

static partial class Program
{
    static void HookTests(string temp) {
        // Discovery: a test build never searches the machine, and never accepts the lab's protected locations.
        Throws<InvalidOperationException>(() => TestDiscovery.Paths(null), "unset discovery override");
        Check(TestDiscovery.Paths("none").Length == 0 && TestDiscovery.Paths("  ").Length == 0 && TestDiscovery.Paths("NONE").Length == 0, "none means no client found");
        string one = Path.Combine(temp, "hook-client-one"), two = Path.Combine(temp, "hook client two");
        Check(TestDiscovery.Paths(one + ";" + two + ";").SequenceEqual(new[] { one, two }), "a list of folders is used as given");
        Refuses(() => TestDiscovery.Paths(@"D:\Battle"+@"Net\Hearthstone"), "the real install is refused by name");
        Refuses(() => TestDiscovery.Paths(@"D:\Battle"+@"Net\Hearthstone\Hearthstone_Data"), "inside the real install is refused");
        Refuses(() => TestDiscovery.Paths(@"C:\Us"+@"ers\HSOffline"+@"Probe\AppData\Local\HearthstoneOffline"), "the accepted save is refused");
        Refuses(() => TestDiscovery.Paths(one + ";relative\\path"), "a relative folder is refused");
        if (!OperatingSystem.IsWindows()) {
            // Proof by construction: a folder nobody can read still resolves, so the override cannot be reading folder contents.
            string sealedFolder = Path.Combine(temp, "sealed"); Directory.CreateDirectory(sealedFolder);
            File.SetUnixFileMode(sealedFolder, UnixFileMode.None);
            try { Check(TestDiscovery.Paths(sealedFolder).Single() == sealedFolder, "discovery does not read the folder it returns"); }
            finally { File.SetUnixFileMode(sealedFolder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        }
        string host = Source("WindowsHost.cs");
        string find = host.Substring(host.IndexOf("internal static string[] FindClients(bool wizard=false)", StringComparison.Ordinal));
        find = find.Substring(0, find.IndexOf("static string[] FindInstalledClients()", StringComparison.Ordinal));
        string hookBranch = find.Substring(0, find.IndexOf("#else", StringComparison.Ordinal));
        Check(hookBranch.Contains("#if SOLOSTONE_TEST_HOOKS") && hookBranch.Contains("return TestDiscovery.Paths();"), "a hook build returns the override first");
        foreach (string read in new[] { "RegisteredFolders", "Discovery.Find", "File.Exists", "Directory.", "Registry", "Environment.GetFolderPath" })
            Check(!hookBranch.Contains(read), "the hook branch of FindClients reads nothing: " + read);
        string installed = host.Substring(host.IndexOf("static string[] FindInstalledClients()", StringComparison.Ordinal));
        installed = installed.Substring(0, installed.IndexOf("internal static string[] IdleFailures(", StringComparison.Ordinal));
        Check(installed.Contains("RegisteredFolders()") && installed.Contains("Discovery.Find"), "the normal search is untouched");
        Check(Matches(host, @"#else\s+return wizard \? HearthstoneDiscovery\.HearthstoneUninstallFolders\(\) : FindInstalledClients\(\);\s+#endif"), "a release build uses the wizard search or the original search");

        // Elevation handoff.
        string control = Path.Combine(temp, "control"); Directory.CreateDirectory(control);
        string line = "\"C:\\Windows\\System32\\cmd.exe\" /d /v:off /s /c \"\"C:\\Windows\\System32\\netsh.exe\" advfirewall firewall delete rule name=\"SoloStone-00000000000000000000000000000000-0123456789ABCDEF\"\"";
        Func<string, Task<int>> ask = answer => Task.Run(() => {
            string request = Path.Combine(control, "elevate-request.txt");
            for (int i = 0; i < 200 && !File.Exists(request); i++) Thread.Sleep(25);
            Check(File.ReadAllText(request) == line, "the exact command line is handed over");
            File.WriteAllText(Path.Combine(control, "elevate-result.txt"), answer + "\n");
            return 0;
        });
        File.WriteAllText(Path.Combine(control, "elevate-result.txt"), "ok");
        Throws<InvalidOperationException>(() => TestElevation.Handoff(control, line, TimeSpan.FromMilliseconds(400), CancellationToken.None), "a stale answer is never accepted");
        Check(!File.Exists(Path.Combine(control, "elevate-request.txt")), "a timed-out request is withdrawn");
        var pending = ask("ok"); Check(TestElevation.Handoff(control, line, TimeSpan.FromSeconds(10), CancellationToken.None) == 0, "ok means exit 0"); pending.Wait();
        pending = ask("exit 5"); Check(TestElevation.Handoff(control, line, TimeSpan.FromSeconds(10), CancellationToken.None) == 5, "exit N is passed on"); pending.Wait();
        pending = ask("cancelled");
        try { TestElevation.Handoff(control, line, TimeSpan.FromSeconds(10), CancellationToken.None); throw new Exception("cancelled answer did not raise"); }
        catch (Win32Exception e) { Check(e.NativeErrorCode == 1223, "cancelled maps to native error 1223"); }
        pending.Wait();
        pending = ask("whatever"); Throws<InvalidOperationException>(() => TestElevation.Handoff(control, line, TimeSpan.FromSeconds(10), CancellationToken.None), "an unreadable answer is refused"); pending.Wait();
        Throws<InvalidOperationException>(() => TestElevation.Handoff(control, "a\nb", TimeSpan.FromSeconds(1), CancellationToken.None), "a two-line request is refused");
        Throws<Win32Exception>(() => TestElevation.ParseAnswer("cancelled"), "cancelled answer");
        Throws<InvalidOperationException>(() => TestElevation.ParseAnswer("exit 0"), "exit 0 must be spelled ok");
        string firewall = Source("WindowsHost.cs");
        Check(firewall.Contains("catch(Win32Exception e) when(e.NativeErrorCode==1223)") && firewall.Contains("throw new FirewallCancelledException(!apply)"), "the real prompt still maps error 1223 to a cancelled-permission error");
        string region = Body(firewall, "#if SOLOSTONE_TEST_HOOKS\n", "using(var child=Process.Start(start))");
        Check(region.Contains("TestElevation.Handoff(FirewallPlan.CommandLine(start))") && region.Contains("else"), "a hook build hands over the same command line instead of starting it");
        Check(firewall.IndexOf("using(var child=Process.Start(start))", StringComparison.Ordinal) > 0 && firewall.IndexOf("if(apply) VerifyFirewall(record,executables)", firewall.IndexOf("using(var child=Process.Start(start))", StringComparison.Ordinal), StringComparison.Ordinal) > 0, "the firewall read-back still follows the elevated step");

        // The command channel.
        var cmd = ControlCommand.Parse("abc-1\nui");
        Check(cmd.Id == "abc-1" && cmd.Verb == "ui", "ui parses");
        cmd = ControlCommand.Parse("7\r\npress wizard.next\r\n"); Check(cmd.Verb == "press" && cmd.Name == "wizard.next", "press parses, CRLF accepted");
        cmd = ControlCommand.Parse("8\ntype target.path C:\\a path\\with spaces"); Check(cmd.Name == "target.path" && cmd.Text == "C:\\a path\\with spaces", "type keeps the whole text");
        cmd = ControlCommand.Parse("9\ntick source.accept true"); Check(cmd.Name == "source.accept" && cmd.Text == "true", "tick parses");
        cmd = ControlCommand.Parse("10\nscreenshot " + Path.Combine(control, "a b.png")); Check(cmd.Text.EndsWith("a b.png"), "screenshot keeps its path");
        foreach (string bad in new[] { "", "1", "1\nfly", "1\nui extra", "1\npress", "1\npress a b", "1\ntick a maybe", "1\ntick a", "../x\nui", "1\ndialog-accept", "1\nui\nextra", "1\npress bad;name", new string('x', 70) + "\nui" })
            Throws<InvalidOperationException>(() => ControlCommand.Parse(bad), "bad command: " + bad.Replace("\n", "|"));
        Check(ControlCommand.InsideChannel(control, Path.Combine(control, "a.png")).EndsWith("a.png"), "a file inside the channel is fine");
        Refuses(() => ControlCommand.InsideChannel(control, Path.Combine(control, "..", "escape.png")), "escape from the channel");
        Refuses(() => ControlCommand.InsideChannel(control, control), "the folder itself");
        Refuses(() => ControlCommand.InsideChannel(control, Path.Combine(temp, "elsewhere.png")), "a path elsewhere");
        File.WriteAllText(Path.Combine(control, "command.txt"), "5\nstate");
        var taken = ControlFiles.TryTake(control, out string problem);
        Check(taken != null && taken.Verb == "state" && problem == null && !File.Exists(Path.Combine(control, "command.txt")), "a command is taken once");
        Check(ControlFiles.TryTake(control, out problem) == null && problem == null, "no command, no error");
        File.WriteAllText(Path.Combine(control, "command.txt"), "5\nexplode");
        Check(ControlFiles.TryTake(control, out problem) == null && problem.Contains("Unknown command"), "a bad command is reported and removed");
        ControlFiles.Write(control, ControlFiles.Reply("5", true, "{\"x\":1}", null));
        using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(control, "result.txt")))) Check(doc.RootElement.GetProperty("ok").GetBoolean() && doc.RootElement.GetProperty("data").GetProperty("x").GetInt32() == 1, "a reply is JSON");
        using (var doc = System.Text.Json.JsonDocument.Parse(ControlFiles.Reply("6", false, null, "quote \" back \\ tab \t newline \n unicode é"))) Check(!doc.RootElement.GetProperty("ok").GetBoolean() && doc.RootElement.GetProperty("error").GetString().Contains("é"), "error text is quoted properly");
    }
    static bool Matches(string text, string pattern) => System.Text.RegularExpressions.Regex.IsMatch(text, pattern);
}

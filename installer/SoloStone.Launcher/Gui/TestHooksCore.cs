#if SOLOSTONE_TEST_HOOKS
// Live-test hooks, pure part. Compiled only into a build made with -p:TestHooks=true; a release build has none of this.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SoloStone
{
    // Replaces the search for the player's own Hearthstone. A test build must never read the lab machine's real install.
    public static class TestDiscovery
    {
        public const string Variable = "SOLOSTONE_TEST_DISCOVERY";
        public static string[] Paths() => Paths(Environment.GetEnvironmentVariable(Variable));
        // "none" or an empty value means no client is found. Several folders are separated by ';'. Nothing is read from disk here.
        public static string[] Paths(string value) {
            if (value == null)
                throw new InvalidOperationException("This test build needs " + Variable + " so it never reads a real Hearthstone install. Set it to a folder, a list separated by ';', or none.");
            string text = value.Trim();
            if (text.Length == 0 || text.Equals("none", StringComparison.OrdinalIgnoreCase)) return new string[0];
            // Canonical refuses the real install and the accepted save by name, before anything touches the disk.
            return text.Split(';').Select(p => p.Trim()).Where(p => p.Length != 0).Select(Safety.Canonical).ToArray();
        }
    }

    // The elevation handoff: the exact command line goes to a file, the driver runs it as administrator and writes the answer back.
    public static class TestElevation
    {
        public const string ControlVariable = "SOLOSTONE_TEST_CONTROL", RequestFile = "elevate-request.txt", ResultFile = "elevate-result.txt";
        public static string Dir => Environment.GetEnvironmentVariable(ControlVariable);
        public static bool Active => !string.IsNullOrWhiteSpace(Dir);
        public static TimeSpan Timeout = TimeSpan.FromMinutes(10);
        public static int Handoff(string commandLine) => Handoff(Dir, commandLine, Timeout, CancellationToken.None);
        // Returns the exit code. A cancelled answer raises Windows error 1223, which the caller already maps to "permission cancelled".
        public static int Handoff(string dir, string commandLine, TimeSpan timeout, CancellationToken cancel) {
            if (string.IsNullOrWhiteSpace(commandLine) || commandLine.Any(c => c == '\r' || c == '\n'))
                throw new InvalidOperationException("The elevation request must be one line.");
            Directory.CreateDirectory(dir);
            string request = Path.Combine(dir, RequestFile), result = Path.Combine(dir, ResultFile);
            if (File.Exists(result)) File.Delete(result);
            File.WriteAllText(request + ".tmp", commandLine, new UTF8Encoding(false));
            if (File.Exists(request)) File.Delete(request);
            File.Move(request + ".tmp", request);
            DateTime deadline = DateTime.UtcNow + timeout;
            while (!File.Exists(result)) {
                cancel.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > deadline) { TryDelete(request); throw new InvalidOperationException("The test driver did not answer the elevation request."); }
                Thread.Sleep(100);
            }
            string answer;
            for (int attempt = 0; ; attempt++) {
                try { answer = File.ReadAllText(result).Trim(); break; }
                catch (IOException) { if (attempt > 20) throw; Thread.Sleep(50); }
            }
            TryDelete(result); TryDelete(request);
            return ParseAnswer(answer);
        }
        public static int ParseAnswer(string answer) {
            if (answer == "ok") return 0;
            if (answer == "cancelled") throw new Win32Exception(1223, "The operation was canceled by the user.");
            if (answer.StartsWith("exit ", StringComparison.Ordinal) && int.TryParse(answer.Substring(5), out int code) && code != 0) return code;
            throw new InvalidOperationException("The test driver sent an unreadable elevation answer.");
        }
        static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    // One command at a time through command.txt, one reply through result.txt. First line is an id, second line is the command.
    public sealed class ControlCommand
    {
        public string Id, Verb, Name, Text;
        public static readonly string[] Verbs = { "ui", "state", "press", "type", "tick", "screenshot", "dialog-accept", "close" };
        public static ControlCommand Parse(string file) {
            var lines = (file ?? "").Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            if (lines.Length != 2 || lines[0].Length == 0 || lines[0].Length > 64 || lines[0].Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                throw new InvalidOperationException("A command is an id line and one command line.");
            string line = lines[1]; int space = line.IndexOf(' ');
            string verb = space < 0 ? line : line.Substring(0, space), rest = space < 0 ? "" : line.Substring(space + 1);
            if (!Verbs.Contains(verb)) throw new InvalidOperationException("Unknown command: " + verb);
            var command = new ControlCommand { Id = lines[0], Verb = verb };
            switch (verb) {
                case "ui": case "state": case "close": if (rest.Length != 0) throw new InvalidOperationException(verb + " takes no arguments."); break;
                case "press": command.Name = CheckName(rest); break;
                case "type": {
                    int cut = rest.IndexOf(' ');
                    command.Name = CheckName(cut < 0 ? rest : rest.Substring(0, cut)); command.Text = cut < 0 ? "" : rest.Substring(cut + 1);
                    break;
                }
                case "tick": {
                    var parts = rest.Split(' ');
                    if (parts.Length != 2 || (parts[1] != "true" && parts[1] != "false")) throw new InvalidOperationException("Use: tick NAME true|false");
                    command.Name = CheckName(parts[0]); command.Text = parts[1]; break;
                }
                default:
                    if (rest.Length == 0) throw new InvalidOperationException(verb + " needs a path.");
                    command.Text = rest; break;
            }
            return command;
        }
        static string CheckName(string value) {
            if (value.Length == 0 || value.Length > 80 || value.Any(c => !char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != '_' && c != ':'))
                throw new InvalidOperationException("A control name uses letters, digits and . - _ : only.");
            return value;
        }
        // Files the hooks write must stay inside the channel folder.
        public static string InsideChannel(string dir, string path) {
            string root = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || full.Length == root.Length)
                throw new InvalidOperationException("Hook files stay inside the control folder.");
            return full;
        }
    }
    public static class ControlFiles
    {
        public const string CommandFile = "command.txt", ResultFile = "result.txt";
        public static ControlCommand TryTake(string dir, out string error) {
            error = null;
            string path = Path.Combine(dir, CommandFile);
            if (!File.Exists(path)) return null;
            string text;
            try { text = File.ReadAllText(path); File.Delete(path); }
            catch (IOException) { return null; } // the driver is still moving the file in
            try { return ControlCommand.Parse(text); }
            catch (InvalidOperationException e) { error = e.Message; return null; }
        }
        public static string Quote(string value) {
            var b = new StringBuilder("\"");
            foreach (char c in value ?? "") {
                if (c == '"') b.Append("\\\""); else if (c == '\\') b.Append("\\\\"); else if (c == '\n') b.Append("\\n"); else if (c == '\r') b.Append("\\r"); else if (c == '\t') b.Append("\\t");
                else if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4")); else b.Append(c);
            }
            return b.Append('"').ToString();
        }
        public static string Reply(string id, bool ok, string dataJson, string error)
            => "{\"id\":" + Quote(id) + ",\"ok\":" + (ok ? "true" : "false") + (error != null ? ",\"error\":" + Quote(error) : "") + ",\"data\":" + (dataJson ?? "null") + "}";
        public static void Write(string dir, string reply) {
            string path = Path.Combine(dir, ResultFile);
            File.WriteAllText(path + ".tmp", reply, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".tmp", path);
        }
    }
}
#endif

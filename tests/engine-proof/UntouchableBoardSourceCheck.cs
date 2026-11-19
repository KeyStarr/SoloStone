using System.Text;
using System.Text.RegularExpressions;

// Source check for the "skip Untouchable minions" rule. Untouchable minions (Dormant ones and permanents such as Nether Portal)
// cannot be targeted or counted by effects, but a raw enumeration of a BoardZone sees them. Every card under
// engine/SabberStoneCore/src/CardSets/Client reads board minions through BoardZone.Interactable (or GetAll), or says why a raw
// read is right with an "untouchable-ok: <reason>" comment on the same line or the line above. Occupancy (IsFull, FreeSpace,
// SlotCount) is not a read of minions and is not flagged.
internal static class UntouchableBoardSourceCheck
{
    internal sealed record Finding(string File, int Line, string Snippet);

    private const string Marker = "untouchable-ok:";

    // Members after "BoardZone." that are not reads of minions: occupancy, the Interactable helpers, location and mutation members.
    // Anything else (LINQ, Count, indexers, Slots, GetSlot, GetSpan, enumeration) is a raw read and needs Interactable or a marker.
    private static readonly HashSet<string> NonReads = new(StringComparer.Ordinal)
    {
        "Interactable", "AnyInteractable", "CountInteractable", "FirstInteractable", "LastInteractable", "GetAll", "CountExceptUntouchables", "HasUntouchables", "Random",
        "IsFull", "FreeSpace", "SlotCount", "LocationCount", "HasLocations", "GetLocations",
        "Add", "Remove", "MoveTo", "Swap", "ChangeEntity", "Replace", "Controller", "Game", "Type", "AdjacentAuras",
    };

    internal static void Run()
    {
        SelfTest();
        string client = Path.Combine(EngineSrc(), "CardSets", "Client");
        if (!Directory.Exists(client)) throw new Exception("UntouchableBoardSourceCheck: folder missing: " + client);
        var files = Directory.GetFiles(client, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        if (files.Length == 0) throw new Exception("UntouchableBoardSourceCheck: no .cs file under " + client);
        var found = new List<Finding>();
        foreach (var f in files) found.AddRange(Scan(File.ReadAllText(f), Rel(f)));
        if (found.Count != 0)
            throw new Exception("UntouchableBoardSourceCheck: " + found.Count + " unmarked raw read(s) of board minions or IsDormant. "
                + "Use BoardZone.Interactable, or add `// untouchable-ok: <reason>` on the line or the line above.\n"
                + string.Join("\n", found.Select(x => $"  {x.File}:{x.Line}: {x.Snippet}")));
        Console.WriteLine($"PASS untouchable board source check: {files.Length} files under CardSets/Client, 0 unmarked raw reads");

        // Informational: the same rule is not enforced outside CardSets/Client yet.
        int outside = 0; var perFile = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var f in Directory.GetFiles(EngineSrc(), "*.cs", SearchOption.AllDirectories))
        {
            if (f.StartsWith(client + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;
            int n = Scan(File.ReadAllText(f), Rel(f), ignoreMarkers: true).Count;
            if (n != 0) { outside += n; perFile[Rel(f)] = n; }
        }
        Console.WriteLine($"INFO raw board reads outside CardSets/Client (not enforced): {outside} in {perFile.Count} files");
        if (Environment.GetEnvironmentVariable("UNTOUCHABLE_LIST_OUTSIDE") == "1")
            foreach (var kv in perFile) Console.WriteLine($"  {kv.Key}: {kv.Value}");
    }

    private static string EngineSrc([System.Runtime.CompilerServices.CallerFilePath] string here = "")
    {
        // The build output may live outside the repository, so also start from this source file and the working directory.
        foreach (string start in new[] { Path.GetDirectoryName(here), Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = string.IsNullOrEmpty(start) ? null : new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "engine", "SabberStoneCore", "src");
                if (Directory.Exists(candidate)) return candidate;
            }
        throw new Exception("UntouchableBoardSourceCheck: cannot find engine/SabberStoneCore/src from " + here + ", the working directory or " + AppContext.BaseDirectory);
    }

    private static string Rel(string path)
    {
        string root = Path.GetFullPath(Path.Combine(EngineSrc(), "..", "..", ".."));
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    internal static List<Finding> Scan(string source, string file, bool ignoreMarkers = false)
    {
        var raw = source.Replace("\r\n", "\n").Split('\n');
        string code = Strip(source.Replace("\r\n", "\n"));
        var findings = new List<Finding>();
        // line start offsets
        var starts = new List<int> { 0 };
        for (int i = 0; i < code.Length; i++) if (code[i] == '\n') starts.Add(i + 1);
        int LineOf(int pos) { int lo = 0, hi = starts.Count - 1; while (lo < hi) { int mid = (lo + hi + 1) / 2; if (starts[mid] <= pos) lo = mid; else hi = mid - 1; } return lo; }
        bool Marked(int line) => !ignoreMarkers && (HasMarker(raw[line]) || (line > 0 && raw[line - 1].TrimStart().StartsWith("//", StringComparison.Ordinal) && HasMarker(raw[line - 1])));

        void Flag(int pos)
        {
            int line = LineOf(pos);
            if (Marked(line) || findings.Any(f => f.Line == line + 1)) return;
            findings.Add(new Finding(file, line + 1, raw[line].Trim()));
        }

        foreach (Match m in Regex.Matches(code, @"\bIsDormant\b")) Flag(m.Index);

        foreach (Match m in Regex.Matches(code, @"\bBoardZone\b"))
        {
            int after = m.Index + m.Length;
            int p = SkipWs(code, after);
            if (p < code.Length && code[p] == '[') { Flag(m.Index); continue; }
            if (p < code.Length && code[p] == '.' )
            {
                int q = SkipWs(code, p + 1);
                int e = q; while (e < code.Length && (char.IsLetterOrDigit(code[e]) || code[e] == '_')) e++;
                string name = code.Substring(q, e - q);
                if (!NonReads.Contains(name)) Flag(m.Index);
                continue;
            }
            // A bare BoardZone is only fine as one side of a zone comparison (x.Zone == c.BoardZone).
            if (PrevToken(code, m.Index) is "==" or "!=") continue;
            // A zone being compared on the right: "<BoardZone> == x".
            if (p + 1 < code.Length && (code[p] == '=' || code[p] == '!') && code[p + 1] == '=') continue;
            Flag(m.Index);
        }
        return findings;
    }

    private static bool HasMarker(string rawLine)
    {
        int c = rawLine.IndexOf("//", StringComparison.Ordinal);
        if (c < 0) return false;
        int i = rawLine.IndexOf(Marker, c, StringComparison.Ordinal);
        return i >= 0 && rawLine.Substring(i + Marker.Length).Trim().Length > 0;
    }

    private static int SkipWs(string s, int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; return i; }

    // The token before a position: the operator or word that comes before the receiver chain "a.b(x).c.BoardZone".
    private static string PrevToken(string s, int pos)
    {
        int i = pos - 1;
        while (i >= 0 && char.IsWhiteSpace(s[i])) i--;
        if (i >= 0 && (s[i] == '.' || s[i] == '?'))
        {
            while (i >= 0)
            {
                if (s[i] == '.' || s[i] == '?') { i--; while (i >= 0 && char.IsWhiteSpace(s[i])) i--; continue; }
                if (char.IsLetterOrDigit(s[i]) || s[i] == '_') { while (i >= 0 && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i--; }
                else if (s[i] == ')' || s[i] == ']')
                {
                    int depth = 0;
                    for (; i >= 0; i--)
                    {
                        if (s[i] == ')' || s[i] == ']') depth++;
                        else if (s[i] == '(' || s[i] == '[') { depth--; if (depth == 0) { i--; break; } }
                    }
                    while (i >= 0 && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i--;
                }
                else break;
                int j = i; while (j >= 0 && char.IsWhiteSpace(s[j])) j--;
                if (j >= 0 && (s[j] == '.' || s[j] == '?')) { i = j; continue; }
                break;
            }
        }
        while (i >= 0 && char.IsWhiteSpace(s[i])) i--;
        if (i < 0) return "";
        if (i >= 1 && s[i] == '=' && (s[i - 1] == '=' || s[i - 1] == '!')) return s.Substring(i - 1, 2);
        int k = i; while (k >= 0 && (char.IsLetterOrDigit(s[k]) || s[k] == '_')) k--;
        return k < i ? s.Substring(k + 1, i - k) : s[i].ToString();
    }

    // Comments, string and char literals become spaces (newlines kept) so identifiers inside them never match.
    internal static string Strip(string s)
    {
        var sb = new StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') { sb.Append(' '); i++; } continue; }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                while (i < s.Length && !(s[i] == '*' && i + 1 < s.Length && s[i + 1] == '/')) { sb.Append(s[i] == '\n' ? '\n' : ' '); i++; }
                if (i < s.Length) { sb.Append("  "); i += 2; }
                continue;
            }
            bool verbatim = (c == '@' && i + 1 < s.Length && s[i + 1] == '"') || (c == '$' && i + 2 < s.Length && s[i + 1] == '@' && s[i + 2] == '"') || (c == '@' && i + 2 < s.Length && s[i + 1] == '$' && s[i + 2] == '"');
            bool plain = c == '"' || (c == '$' && i + 1 < s.Length && s[i + 1] == '"');
            if (verbatim || plain)
            {
                bool interpolated = false;
                while (s[i] != '"') { if (s[i] == '$') interpolated = true; sb.Append(' '); i++; }
                sb.Append(' '); i++;
                while (i < s.Length)
                {
                    // Interpolation holes are code: a raw board read inside {...} must still be seen.
                    if (interpolated && s[i] == '{')
                    {
                        if (i + 1 < s.Length && s[i + 1] == '{') { sb.Append("  "); i += 2; continue; }
                        int depth = 0;
                        while (i < s.Length)
                        {
                            if (s[i] == '{') depth++;
                            else if (s[i] == '}') { depth--; if (depth == 0) { sb.Append(' '); i++; break; } }
                            sb.Append(s[i] == '{' ? ' ' : s[i]); i++;
                        }
                        continue;
                    }
                    if (verbatim && s[i] == '"' && i + 1 < s.Length && s[i + 1] == '"') { sb.Append("  "); i += 2; continue; }
                    if (!verbatim && s[i] == '\\' && i + 1 < s.Length) { sb.Append("  "); i += 2; continue; }
                    if (s[i] == '"') { sb.Append(' '); i++; break; }
                    sb.Append(s[i] == '\n' ? '\n' : ' '); i++;
                }
                continue;
            }
            if (c == '\'' )
            {
                int j = i + 1;
                if (j < s.Length && s[j] == '\\') j += 2; else j += 1;
                while (j < s.Length && s[j] != '\'' && s[j] != '\n') j++;
                if (j < s.Length && s[j] == '\'') { for (; i <= j; i++) sb.Append(' '); continue; }
            }
            sb.Append(c); i++;
        }
        return sb.ToString();
    }

    private static void SelfTest()
    {
        void Expect(string sample, int flagged, string what)
        {
            int n = Scan(sample, "sample.cs").Count;
            if (n != flagged) throw new Exception($"UntouchableBoardSourceCheck self-test '{what}': expected {flagged} finding(s), got {n}");
        }
        Expect("var a = c.Opponent.BoardZone.Where(m => !m.IsDead).ToArray();", 1, "inline Where");
        Expect("var a = c.Opponent\n    .BoardZone\n    .Where(m => m.IsDead)\n    .ToArray();", 1, "multi-line chain");
        Expect("var a = c.Opponent.BoardZone\n    .Where(m => m.IsDead)\n    .ToArray();", 1, "multi-line chain after token");
        Expect("foreach (var m in c.BoardZone) { }", 1, "foreach over the zone");
        Expect("var all = c.HandZone.Concat(c.BoardZone).ToList();", 1, "bare Concat argument");
        Expect("var n = c.BoardZone.Count;", 1, "Count property");
        Expect("var n = c.BoardZone.Count(m => m.IsDead);", 1, "Count predicate");
        Expect("var m = c.BoardZone[0];", 1, "indexer");
        Expect("var s = c.BoardZone.Slots;", 1, "Slots");
        Expect("var d = m.IsDormant;", 1, "IsDormant");
        Expect("// untouchable-ok: occupancy\nvar a = c.BoardZone.Count;", 0, "marker on the line above");
        Expect("var a = c.BoardZone.IsFull; // untouchable-ok: occupancy\nvar b = c.BoardZone.Count;", 1, "a trailing marker does not silence the next line");
        Expect("var a = c.BoardZone.MinBy(m => m.Health);", 1, "MinBy");
        Expect("var a = c.BoardZone.MaxBy(m => m.Health);", 1, "MaxBy");
        Expect("var a = c.BoardZone.AsEnumerable();", 1, "AsEnumerable");
        Expect("var a = c.BoardZone.GetSpan();", 1, "GetSpan");
        Expect("var a = $\"n={c.BoardZone.Count(m => m.IsDead)}\";", 1, "read inside an interpolated hole");
        Expect("var a = $\"BoardZone.Count {{x}} {c.BoardZone.Interactable.Length}\";", 0, "interpolated text and an Interactable hole");
        Expect("var a = c.BoardZone.AnyInteractable(m => m.IsDead) || c.BoardZone.CountInteractable() > 1 || c.BoardZone.CountExceptUntouchables > 1 || c.BoardZone.FirstInteractable == null || c.BoardZone.LastInteractable == null;", 0, "non-allocating helpers");
        Expect("var a = c.BoardZone?.Where(m => m.IsDead);", 1, "null-conditional chain");
        Expect("var a = c.BoardZone.Count; // untouchable-ok: occupancy", 0, "marker on the same line");
        Expect("var a = c.BoardZone.Count; // untouchable-ok:", 1, "marker without a reason");
        Expect("var a = c.BoardZone.GetAll().Where(m => m.IsDead).ToArray();", 0, "GetAll");
        Expect("var a = c.BoardZone.Interactable.Where(m => m.IsDead).ToArray();", 0, "Interactable");
        Expect("if (x.Zone == c.BoardZone) { }", 0, "zone equality");
        Expect("if (x.Zone != c.Opponent.BoardZone) { }", 0, "zone inequality");
        Expect("if (c.BoardZone.IsFull || c.BoardZone.FreeSpace == 0 || c.BoardZone.SlotCount > 3) { }", 0, "occupancy");
        Expect("var s = \"BoardZone.Where IsDormant\"; // BoardZone.Where(x) IsDormant", 0, "string and comment");
        Expect("var a = c.BoardZone.Where(m => m.IsDead)\n    .ToArray(); /* BoardZone.Count */", 1, "block comment ignored, real read found");
    }
}

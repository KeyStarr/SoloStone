// Finds the repository root for tests that read repository files at run time (plugin sources, data/, tools/).
// Never walk up from AppContext.BaseDirectory alone: tests/run.py builds into a temporary folder outside the repository.
internal static class RepoRoot
{
    // The folder that holds `relative` (a file or a folder, forward slashes), or null. $SOLOSTONE_ROOT (tests/run.py sets it) is
    // used as is. Otherwise look from the working directory, then the program's own folder, walking up to the first folder with
    // global.json (the repository root) and no further: a checkout nested in another one must not see the outer one's files.
    internal static string Find(string relative)
    {
        string fixedRoot = Environment.GetEnvironmentVariable("SOLOSTONE_ROOT");
        if (!string.IsNullOrEmpty(fixedRoot)) return Has(fixedRoot, relative) ? fixedRoot : null;
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var at = new DirectoryInfo(start); at != null; at = at.Parent)
            {
                if (Has(at.FullName, relative)) return at.FullName;
                if (File.Exists(Path.Combine(at.FullName, "global.json"))) break;
            }
        return null;
    }

    // A file only the maintainer's checkout has (lab scripts, private docs): at `relative` in the private tree, or under
    // private/ once the private add-on sits inside a public checkout. Null in a public checkout; the check that reads it
    // then skips with a printed reason.
    internal static string FindMaintainerFile(string relative)
    {
        foreach (string candidate in new[] { relative, "private/" + relative })
        {
            string root = Find(candidate);
            if (root != null) return Path.Combine(root, candidate);
        }
        return null;
    }

    private static bool Has(string root, string relative)
    {
        string path = Path.Combine(root, relative);
        return File.Exists(path) || Directory.Exists(path);
    }
}

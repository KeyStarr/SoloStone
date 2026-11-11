// Finds the repository root for tests that read repository files at run time (plugin sources, data/, tools/).
// Never walk up from AppContext.BaseDirectory alone: tests/run.py builds into a temporary folder outside the repository.
internal static class RepoRoot
{
    // The folder that holds `relative` (a file or a folder, forward slashes), or null. Looked for from $SOLOSTONE_ROOT
    // (tests/run.py sets it), then the working directory (run.py starts every suite in the root), then the program's own folder.
    internal static string Find(string relative)
    {
        foreach (string start in new[] { Environment.GetEnvironmentVariable("SOLOSTONE_ROOT"), Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var at = string.IsNullOrEmpty(start) ? null : new DirectoryInfo(start); at != null; at = at.Parent)
            {
                string path = Path.Combine(at.FullName, relative);
                if (File.Exists(path) || Directory.Exists(path)) return at.FullName;
            }
        return null;
    }
}

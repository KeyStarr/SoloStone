using System.Reflection;
using System.Security.Cryptography;

// Release plan R2 and V3. A failed V1 or V2 leaves the whole save tree byte-identical, and that is this suite's acceptance. Every entry point runs for real
// against a tree with everything an open acts on: a tool request, a crashed create, a pending match, an old selected save, a missing root profile.
internal static class SaveHoldChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    // Every file and folder under the save root, lock files and all, with its bytes.
    private static string Tree(string root) => string.Join("\n", Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(path => Path.GetRelativePath(root, path) + (Directory.Exists(path) ? "/" : ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))));

    private static void Refused(Action entry, string what)
    {
        try { entry(); }
        catch (InvalidOperationException error) when (error.Message.Contains("on hold")) { return; }
        throw new Exception("not held: " + what);
    }

    private static readonly ClientBindings.Client ThisRuntime = new ClientBindings.Client
    {
        Assembly = name => name == typeof(object).Assembly.GetName().Name,
        Type = (assembly, name) => typeof(object).Assembly.GetType(name, false),
        Iterator = method => false
    };

    // A client whose records hold everything but what the test leaves out.
    private static IReadOnlyList<string> FailedChecks(string plantedRow, string plantedCard)
    {
        var failed = new List<string>();
        failed.AddRange(ClientBindings.Missing(new[] { "T\t*\tSystem.Object\t\t", plantedRow }, ThisRuntime));
        failed.AddRange(ClientBindings.MissingCards(new[] { "CS2_029", plantedCard }, id => id != plantedCard));
        return failed;
    }

    internal static void Run(string testRoot)
    {
        string root = Path.Combine(testRoot, "save-hold"); Directory.CreateDirectory(root);
        if (Path.DirectorySeparatorChar == '/' && root.StartsWith("/var/", StringComparison.Ordinal)) root = "/private" + root;
        string legacy = Path.Combine(root, "profile.xml");
        var pending = ProfileCatalog.Create(legacy, "Pending", new ProfileOptions());
        var other = ProfileCatalog.Create(legacy, "Other", new ProfileOptions());
        ProfileCatalog.CommitSelection(legacy, pending.Id);
        string pendingPath = ProfileCatalog.ProfilePathForId(legacy, pending.Id), otherPath = ProfileCatalog.ProfilePathForId(legacy, other.Id);
        var options = ProfileCatalog.OptionsForProfile(legacy, pendingPath);
        using (var profile = new LocalProfile(pendingPath, options)) profile.Begin("mage", "warrior", 7);
        File.WriteAllText(ProfileCatalog.Startup.RequestPath(legacy), pending.Id);
        // A slot folder the catalog no longer lists: a delete that crashed between its two steps. Reconcile moves it.
        string crashedDelete = Path.Combine(root, "profiles", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(crashedDelete); File.WriteAllText(Path.Combine(crashedDelete, "profile.xml"), "half a save");
        string fixedProfile = Path.Combine(root, "lanes", "1", "profile.xml"), fixture = Path.Combine(root, "fixture.xml");
        File.WriteAllText(fixture, "fixture");
        string run = Guid.NewGuid().ToString("D");
        // savesPending: what OfflineGuard.ResolveHeldLaunch returns on a picker-capable launch, which only runs where the separator is \.
        var launch = new OfflineLaunchContext(run, null, legacy, 0, false, legacy, false, null, null, null, null, null, true);
        var fixedLaunch = new OfflineLaunchContext(run, null, fixedProfile, 1);
        Check(launch.Startup == null && !launch.ShowProfileSelector && launch.ProfilePath == legacy && launch.AutoOpenTargetId == null,
            "a held launch looks like an unbound one: no choice, no selector, the root path");
        string before = Tree(root);
        Check(before.Contains("startup-save.txt") && before.Contains(Path.GetFileName(crashedDelete)) && File.ReadAllText(pendingPath).Contains("<Pending>"), "the tree has what an open would act on");

        foreach (var failure in new[] { ("a planted missing hook", FailedChecks("M\t*\tSystem.Object\tNoSuchHook\t*", "CS2_029x")), ("a planted missing card", FailedChecks("T\t*\tSystem.String\t\t", "CS2_029x")) })
        {
            Check(failure.Item2.Count > 0, failure.Item1 + " fails its check");
            SaveHold.Engage();
            try
            {
                // Every way into the save tree, as the plugin would call it if a path around the hold ever existed.
                Refused(launch.ResolveSaves, "startup choice");
                Refused(() => ProfileCatalog.Startup.ResolveLaunch(legacy, run, null, null), "startup decision");
                Refused(() => ProfileCatalog.Startup.ConsumeRequest(legacy), "tool request");
                Refused(() => ProfileCatalog.Startup.Arm(legacy, pending.Id, run), "unfinished-start marker");
                Refused(() => ProfileCatalog.Startup.Confirm(legacy, run), "clearing the marker");
                Refused(() => SelectedProfile.OpenStartup(launch, pendingPath, true), "match recovery and migration");
                Refused(() => new LocalProfile(pendingPath, options), "opening a save");
                Refused(() => new LocalProfile(legacy), "welcome grant on a missing root profile");
                Refused(() => OfflineGuard.SeedProfileIfMissing(fixedLaunch, fixture), "seeding");
                Refused(() => ProfileCatalog.Create(legacy, "New", new ProfileOptions()), "creating a save");
                Refused(() => ProfileCatalog.CommitSelection(legacy, other.Id), "selecting a save");
                Refused(() => ProfileCatalog.Reconcile(legacy, _ => { }), "reconciling");
                Refused(() => ProfileCatalog.List(legacy), "listing, whose lease writes a lock file");
                Refused(() => ProfileCatalog.SelectedId(legacy), "reading the selection");
                Refused(() => ProfileCatalog.ResolveActiveProfile(legacy), "resolving the active save");
                Refused(() => ProfileCatalog.OptionsForProfile(legacy, otherPath), "reading options through the catalog");
                Refused(() => ProfileCatalog.RequestRelaunch(legacy, run, other.Id), "Change Profile");
                // The picker's delete reports failures in its result, so the player sees them. It must still change nothing.
                Check(!ProfileCatalog.DeleteFromPicker(legacy, other.Id, _ => { }, true, null, false).SlotRemoved, "deleting a save is refused");
                Refused(() => LocalProfileBackups.Automatic(pendingPath, "startup", 15), "automatic backup");
                Refused(() => LocalProfileBackups.ProcessRestoreRequest(pendingPath, options), "restore");
            }
            finally { SaveHold.Release(); }
            Check(Tree(root) == before, "the whole save tree is byte-identical after " + failure.Item1);
            Check(launch.Startup == null && launch.ProfilePath == legacy, "and the launch still has no startup choice");
        }

        // Both checks pass: the same calls do what they always did, so the hold above is not vacuous.
        SaveHold.Engage();
        SaveHold.Release();
        launch.ResolveSaves();
        Check(launch.Startup != null && launch.Startup.IsAuto && launch.AutoOpenTargetId == pending.Id && launch.ProfilePath == pendingPath && launch.Startup.Reconciled,
            "released: the tool request is obeyed");
        Check(!File.Exists(ProfileCatalog.Startup.RequestPath(legacy)) && ProfileCatalog.Startup.ReadMarker(legacy)?.RunId == run && !Directory.Exists(crashedDelete),
            "released: request consumed, marker armed, the crashed delete finished");
        using (var opened = SelectedProfile.OpenStartup(launch, pendingPath, true))
            Check(!opened.HasPendingMatch && opened.History.Any(match => match.Result == "Abandoned"), "released: the pending match is recovered");
        Check(Tree(root) != before, "released: the tree changed, as an open changes it");
    }
}

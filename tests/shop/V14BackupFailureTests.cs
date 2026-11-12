// EnsureBackup runs when a V13/V14 save upgrades to V15. A refused or failed backup or commit must leave the save
// and any existing backup untouched, and a retry against a matching backup must finish the upgrade.
internal static class V14BackupFailureTests
{
    static string Fixture(string root, string name, out byte[] original)
    {
        string path = Path.Combine(root, name, "profile.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "profile-fixtures", "v14-mono-migrated.xml"), path);
        original = File.ReadAllBytes(path);
        return path;
    }
    static bool Throws(string path)
    {
        try { using var ignored = new LocalProfile(path); return false; }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException) { return true; }
    }

    internal static void Run(Action<bool, string> check, string root)
    {
        // A different existing backup is never overwritten, and the save is not upgraded.
        string path = Fixture(root, "v14-bak-collision", out byte[] original);
        File.WriteAllText(path + ".v14.bak", "different");
        check(Throws(path), "V14 upgrade refuses a conflicting backup");
        check(original.SequenceEqual(File.ReadAllBytes(path)) && File.ReadAllText(path + ".v14.bak") == "different",
            "a conflicting backup changed the save or the backup");

        // Same length, one byte off: the length check alone must not pass it.
        path = Fixture(root, "v14-bak-same-length", out original);
        byte[] near = (byte[])original.Clone(); near[near.Length / 2] ^= 1;
        File.WriteAllBytes(path + ".v14.bak", near);
        check(Throws(path), "V14 upgrade refuses a same-length backup with different bytes");
        check(original.SequenceEqual(File.ReadAllBytes(path)) && near.SequenceEqual(File.ReadAllBytes(path + ".v14.bak")),
            "a same-length mismatched backup changed the save or the backup");

        // A failed backup write (a directory sits where the temporary file goes) leaves the save alone and no backup.
        path = Fixture(root, "v14-bak-write-failure", out original);
        Directory.CreateDirectory(path + ".v14.bak.tmp");
        check(Throws(path), "V14 upgrade fails when the backup cannot be written");
        check(original.SequenceEqual(File.ReadAllBytes(path)) && !File.Exists(path + ".v14.bak"),
            "a failed backup write changed the save or left a backup");

        // A failed commit after the backup keeps both; once the obstacle is gone the retry accepts the matching backup.
        path = Fixture(root, "v14-commit-failure", out original);
        Directory.CreateDirectory(path + ".tmp");
        check(Throws(path), "V14 upgrade fails when the commit cannot be written");
        check(original.SequenceEqual(File.ReadAllBytes(path)) && original.SequenceEqual(File.ReadAllBytes(path + ".v14.bak")),
            "a failed commit damaged the save or its backup");
        Directory.Delete(path + ".tmp");
        using (var retry = new LocalProfile(path)) check(retry.Gold >= 0, "the retry with a matching backup loads");
        check(original.SequenceEqual(File.ReadAllBytes(path + ".v14.bak")) && !original.SequenceEqual(File.ReadAllBytes(path)),
            "the retry kept the backup and upgraded the save");
        Console.WriteLine("PASS V14 backup failures: conflicting and same-length backups refuse, failed backup write and failed commit keep the save");
    }
}

// EnsureBackup runs when an older save upgrades. A failed backup or commit must leave the save and any existing backup
// untouched, and a retry against a matching backup must finish the upgrade. An existing backup of other bytes is never
// overwritten: the current bytes are backed up beside it under a hash-named file and the upgrade goes on (#87 review).
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
        // A different existing backup is never overwritten; the current bytes get their own hash-named backup.
        string Hashed(string profile, byte[] bytes) {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return profile + ".v14." + Convert.ToHexString(sha.ComputeHash(bytes))[..16].ToLowerInvariant() + ".bak";
        }
        string path = Fixture(root, "v14-bak-collision", out byte[] original);
        File.WriteAllText(path + ".v14.bak", "different");
        check(!Throws(path), "V14 upgrade with a conflicting backup goes on");
        check(File.ReadAllText(path + ".v14.bak") == "different" && original.SequenceEqual(File.ReadAllBytes(Hashed(path, original)))
            && !original.SequenceEqual(File.ReadAllBytes(path)), "a conflicting backup stays; the upgraded bytes are backed up beside it");

        // Same length, one byte off: the length check alone must not pass it as the matching backup.
        path = Fixture(root, "v14-bak-same-length", out original);
        byte[] near = (byte[])original.Clone(); near[near.Length / 2] ^= 1;
        File.WriteAllBytes(path + ".v14.bak", near);
        check(!Throws(path) && near.SequenceEqual(File.ReadAllBytes(path + ".v14.bak")) && original.SequenceEqual(File.ReadAllBytes(Hashed(path, original))),
            "a same-length mismatched backup stays; the current bytes get their own backup");
        // A hash-named backup that does not match its bytes (tampered) still refuses, and changes nothing.
        path = Fixture(root, "v14-bak-hashed-tampered", out original);
        File.WriteAllText(path + ".v14.bak", "different");
        File.WriteAllText(Hashed(path, original), "tampered");
        check(Throws(path) && original.SequenceEqual(File.ReadAllBytes(path)), "a tampered hash-named backup refuses and leaves the save");

        // A failed backup write (a directory sits where the temporary file goes) leaves the save alone and no backup.
        path = Fixture(root, "v14-bak-write-failure", out original);
        Directory.CreateDirectory(path + ".v14.bak.tmp");
        check(Throws(path), "V14 upgrade fails when the backup cannot be written");
        check(original.SequenceEqual(File.ReadAllBytes(path)) && !File.Exists(path + ".v14.bak"),
            "a failed backup write changed the save or left a backup");

        // A failed Move (a directory sits at the backup path) also leaves no temporary file behind.
        path = Fixture(root, "v14-bak-move-failure", out original);
        Directory.CreateDirectory(path + ".v14.bak");
        check(Throws(path), "V14 upgrade fails when the backup cannot be moved into place");
        check(original.SequenceEqual(File.ReadAllBytes(path)) && !File.Exists(path + ".v14.bak.tmp"),
            "a failed backup move changed the save or left the temporary file");

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
        Console.WriteLine("PASS V14 backup failures: conflicting backups kept and bytes backed up beside them, tampered hash backup refuses, failed backup write and failed commit keep the save");
    }
}

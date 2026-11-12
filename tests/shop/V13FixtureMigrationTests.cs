using System.Xml.Serialization;

// A real disposable V13 save (lane 2, authored on the Mac by .NET) and the V14 file the game's Mono build wrote when it
// migrated that save on 2026-10-28. Keep progress, normalizing the legacy card-back selection and current grind pace before comparing.
internal static class V13FixtureMigrationTests
{
    private static readonly XmlSerializer Serializer = new(typeof(LocalProfileData));
    private static LocalProfileData Read(string path) { using var stream = File.OpenRead(path); return (LocalProfileData)Serializer.Deserialize(stream); }
    private static byte[] Bytes(LocalProfileData data) { using var stream = new MemoryStream(); Serializer.Serialize(stream, data); return stream.ToArray(); }

    internal static void Run(Action<bool, string> check, string root)
    {
        string fixtures = Path.Combine(AppContext.BaseDirectory, "profile-fixtures");
        string source = Path.Combine(fixtures, "v13-dotnet-authored.xml");
        string path = Path.Combine(root, "v13-fixture", "profile.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.Copy(source, path);
        byte[] original = File.ReadAllBytes(source);
        var before = Read(source);
        check(before.Version == 13 && before.OwnedPets.Count == 0 && before.PetProgress.Count == 0 && before.SelectedPet == null,
            "V13 fixture is a pet-free V13 save");
        using (var profile = new LocalProfile(path))
            check(profile.Gold == before.GoldBalance && profile.DustBalance == before.DustBalance && profile.OwnedPets.Count == 0,
                "V13 fixture loads and migrates");
        var migrated = Read(path);
        check(migrated.Version == 15 && migrated.OwnedPets.Count == 0 && migrated.PetProgress.Count == 0 && migrated.SelectedPet == null,
            "V13 fixture migrates to the current version with an empty pet ledger");
        check(original.SequenceEqual(File.ReadAllBytes(path + ".v13.bak")), "V13 fixture migration keeps a byte-exact backup");
        before.FavoriteCardBacks.Add(before.SelectedCardBack ?? 0); before.SelectedCardBack = null;
        before.GrindTier = GrindTiers.Successor(before.GrindTier).Id;
        migrated.Version = 13;
        check(Bytes(before).SequenceEqual(Bytes(migrated)), "V13 fixture migration changes no existing progress");
        var game = Read(Path.Combine(fixtures, "v14-mono-migrated.xml"));
        game.FavoriteCardBacks.Add(game.SelectedCardBack ?? 0); game.SelectedCardBack = null;
        game.GrindTier = GrindTiers.Successor(game.GrindTier).Id;
        game.Version = 13;
        check(Bytes(game).SequenceEqual(Bytes(migrated)), "V13 fixture migrates to the same values the game wrote");
        // The game's V14 file upgrades version and current pace in place, with a byte-exact .v14.bak.
        string v14path = Path.Combine(root, "v14-fixture", "profile.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(v14path));
        File.Copy(Path.Combine(fixtures, "v14-mono-migrated.xml"), v14path);
        byte[] v14original = File.ReadAllBytes(v14path);
        var v14before = Read(v14path);
        check(v14before.Version == 14, "V14 fixture is a V14 save");
        using (var profile = new LocalProfile(v14path)) check(profile.Gold == v14before.GoldBalance, "V14 fixture loads and upgrades");
        var v14after = Read(v14path);
        check(v14after.Version == 15 && v14original.SequenceEqual(File.ReadAllBytes(v14path + ".v14.bak")), "V14 fixture upgrade is in place with a byte-exact .v14.bak");
        v14before.FavoriteCardBacks.Add(v14before.SelectedCardBack ?? 0); v14before.SelectedCardBack = null;
        v14before.GrindTier = GrindTiers.Successor(v14before.GrindTier).Id;
        v14after.Version = 14;
        check(Bytes(v14before).SequenceEqual(Bytes(v14after)), "V14 fixture upgrade changes no existing value");
        Console.WriteLine("PASS V13 fixture migration: real lane save keeps every value; only Version, current pace and the empty pet ledger change");
    }
}

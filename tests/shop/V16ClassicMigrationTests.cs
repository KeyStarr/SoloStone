using System.Xml.Serialization;

// #87 V15 -> V16: Classic/Basic cards are Wild. A V15 save (the real V14 lane save stamped V15, which is all V15 added)
// with two Mage decks: one mixing reprinted Classic cards (Fireball, Frostbolt with a Golden slot, Sen'jin), a Classic card
// with no Core reprint (Chillwind Yeti) and Core cards; one holding only reprinted Classic cards. The upgrade must keep every
// collection row, grant the owned reprints' Core copies, move both decks onto them, stamp Format, leave the Yeti (so that deck
// cannot be played until edited), keep a byte-exact .v15.bak, change nothing on a second open, and survive a refused backup
// and a failed commit like the V14 upgrade does.
internal static class V16ClassicMigrationTests
{
    private static readonly XmlSerializer Serializer = new(typeof(LocalProfileData));
    private static LocalProfileData Read(string path) { using var stream = File.OpenRead(path); return (LocalProfileData)Serializer.Deserialize(stream); }
    private static void Write(string path, LocalProfileData data) { using var stream = File.Create(path); Serializer.Serialize(stream, data); }
    private static long Mixed, Reprinted, Legend;

    internal static string V15Fixture(string root, string name, out byte[] original, int version = 15)
    {
        string path = Path.Combine(root, name, "profile.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var data = Read(Path.Combine(AppContext.BaseDirectory, "profile-fixtures", "v14-mono-migrated.xml"));
        data.Version = version;
        Mixed = data.NextCustomDeckId; Reprinted = Mixed + 1; Legend = Mixed + 2;
        data.NextCustomDeckId = Legend + 1;
        // A Classic legendary the save owns, and a deck that already mixes a Classic and a Core copy of one card.
        if (!data.Collection.Any(row => row.Id == "EX1_559")) data.Collection.Add(new LocalCardCount { Id = "EX1_559", Count = 1, Acquired = 1 });
        LocalDeckCard Card(string id, int premium = 0) => new LocalDeckCard { Id = id, Premium = premium };
        data.CustomDecks.Add(new LocalCustomDeck { Id = Mixed, SortOrder = Mixed, Name = "Old Mage", Hero = "HERO_08",
            CardEntries = new List<LocalDeckCard> { Card("CS2_029"), Card("CS2_029"), Card("CS2_024", 1), Card("CS2_024"),
                Card("CS2_179"), Card("CS2_182"), Card("CS2_182"), Card("CORE_DRG_107"), Card("CORE_DRG_107") } });
        data.CustomDecks.Add(new LocalCustomDeck { Id = Reprinted, SortOrder = Reprinted, Name = "Fireballs", Hero = "HERO_08",
            CardEntries = new List<LocalDeckCard> { Card("CS2_029"), Card("CS2_029") } });
        data.CustomDecks.Add(new LocalCustomDeck { Id = Legend, SortOrder = Legend, Name = "Legend Mage", Hero = "HERO_08",
            CardEntries = new List<LocalDeckCard> { Card("CS2_029"), Card("CORE_CS2_029"), Card("EX1_559") } });
        Write(path, data);
        original = File.ReadAllBytes(path);
        return path;
    }

    private static bool Throws(string path)
    {
        try { using var ignored = new LocalProfile(path); return false; }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException) { return true; }
    }

    internal static void Run(Action<bool, string> check, string root)
    {
        check(LocalProfile.ClassicReprints.TryGetValue("CS2_029", out string fireball) && fireball == "CORE_CS2_029"
            && LocalProfile.ClassicReprints["CS2_024"] == "CORE_CS2_024" && !LocalProfile.ClassicReprints.ContainsKey("CS2_182")
            && LocalProfile.ClassicReprints.Values.All(LocalCardCatalog.IsStandardId)
            && LocalProfile.ClassicReprints.Keys.All(id => !LocalCardCatalog.IsStandardId(id)),
            "Classic reprint map: Classic id -> Standard Core id, Yeti has none");
        string path = V15Fixture(root, "v15-classic", out byte[] original);
        var before = Read(path);
        check(before.Collection.Any(row => row.Id == "CS2_029" && row.Count > 0) && before.Collection.Any(row => row.Id == "CS2_182" && row.Count > 0),
            "V15 fixture owns the Classic cards its decks use");
        using (var profile = new LocalProfile(path))
        {
            check(profile.WildCards(Mixed).SequenceEqual(new[] { "CS2_182" }), "mixed deck keeps only the unreprinted Classic card as Wild");
            check(!profile.IsDeckComplete(Mixed), "a deck holding a Wild card cannot be played");
            check(profile.WildCards(Reprinted).Count == 0, "a deck of reprinted Classic cards becomes fully Standard");
        }
        var after = Read(path);
        check(after.Version == 16, "V15 save upgrades to V16");
        check(original.SequenceEqual(File.ReadAllBytes(path + ".v15.bak")), "V16 upgrade keeps a byte-exact .v15.bak");
        check(before.Collection.All(row => after.Collection.Any(next => next.Id == row.Id && next.Count == row.Count && next.Acquired == row.Acquired))
            && before.PremiumCollection.All(row => after.PremiumCollection.Any(next => next.Id == row.Id && next.Premium == row.Premium && next.Count == row.Count)),
            "every Classic collection row survives unchanged");
        int Grant(LocalProfileData data, string id) => data.CoreGrants.SingleOrDefault(row => row.Id == id)?.Count ?? 0;
        int Owned(string id) => before.Collection.Where(row => row.Id == id).Sum(row => row.Count);
        check(Grant(after, "CORE_CS2_029") == Math.Min(2, Owned("CS2_029")) && Grant(after, "CORE_CS2_024") == Math.Min(2, Owned("CS2_024"))
            && Grant(after, "CORE_CS2_179") == Math.Min(2, Owned("CS2_179")), "owned reprinted Classic cards grant their Core copies");
        check(before.CoreGrants.All(row => Grant(after, row.Id) >= row.Count), "no Core grant is lowered");
        var mixed = after.CustomDecks.Single(deck => deck.Id == Mixed);
        check(mixed.CardEntries.Select(entry => entry.Id).SequenceEqual(new[] { "CORE_CS2_029", "CORE_CS2_029", "CORE_CS2_024", "CORE_CS2_024",
            "CORE_CS2_179", "CS2_182", "CS2_182", "CORE_DRG_107", "CORE_DRG_107" }), "mixed deck moves to the reprints in place, Yeti stays");
        check(mixed.CardEntries.All(entry => entry.Premium == 0), "an unowned Golden reprint slot falls back to the owned Normal copy");
        check(after.CustomDecks.Where(deck => deck.Id == Mixed || deck.Id == Reprinted).All(deck => deck.Format == LocalProfile.StandardFormat),
            "every migrated deck is stamped Standard");
        check(after.CustomDecks.Single(deck => deck.Id == Legend).CardEntries.Select(entry => entry.Id)
            .SequenceEqual(new[] { "CORE_CS2_029", "CORE_CS2_029", "CORE_EX1_559" }) && Grant(after, "CORE_EX1_559") == 1,
            "a Classic legendary moves to its one-copy reprint; a mixed Classic and Core pair becomes two Core copies");
        byte[] migrated = File.ReadAllBytes(path);
        using (var reopened = new LocalProfile(path)) check(reopened.WildCards(Mixed).Count == 1, "the migrated save reopens");
        check(migrated.SequenceEqual(File.ReadAllBytes(path)), "a second open migrates nothing again (restart is a no-op)");

        // A Classic card that arrives after the upgrade (an old unopened Legacy pack) brings its reprint on the next load.
        var later = Read(path);
        later.CoreGrants.RemoveAll(row => row.Id == "CORE_EX1_559");
        Write(path, later);
        using (var reopened = new LocalProfile(path)) check(reopened.Collection.TryGetValue("CORE_EX1_559", out int legend) && legend == 1,
            "a later-owned Classic card grants its reprint on load");
        // A deck format this build does not know (a later Wild mode) loads, and its deck cannot be played here.
        var unknown = Read(path);
        unknown.CustomDecks.Single(deck => deck.Id == Reprinted).Format = "Wild";
        Write(path, unknown);
        using (var reopened = new LocalProfile(path))
            check(!reopened.IsDeckComplete(Reprinted) && reopened.WildCards(Reprinted).Count == 0
                && reopened.NotPlayableReason(Reprinted).Contains("Wild deck") && reopened.NotPlayableReason(Mixed).Contains("CS2_182"),
                "an unknown deck format loads, is not playable and says why; a Wild-card deck names its cards");
        // V14 saves take the same upgrade, with a byte-exact .v14.bak.
        string v14 = V15Fixture(root, "v14-classic", out byte[] v14original, 14);
        using (var profile = new LocalProfile(v14)) check(profile.WildCards(Mixed).SequenceEqual(new[] { "CS2_182" }), "V14 save with Classic decks migrates");
        check(Read(v14).Version == 16 && v14original.SequenceEqual(File.ReadAllBytes(v14 + ".v14.bak")), "V14 upgrade to V16 keeps a byte-exact .v14.bak");

        // Failure paths, as for V14: a conflicting backup is kept and the bytes are backed up beside it; a failed commit keeps the save
        // and its backup, and the retry with the matching backup finishes the upgrade.
        // The #87 review case: the player copies .v15.bak back over the save, plays on V15 (other bytes now), and upgrades again.
        path = V15Fixture(root, "v15-restored-and-played", out original);
        using (var first = new LocalProfile(path)) { }
        File.Copy(path + ".v15.bak", path, true);
        var played = Read(path); played.GoldBalance += 25; Write(path, played);
        byte[] playedBytes = File.ReadAllBytes(path);
        using (var again = new LocalProfile(path)) check(again.Gold == played.GoldBalance, "a restored-and-played V15 save upgrades again");
        check(original.SequenceEqual(File.ReadAllBytes(path + ".v15.bak")) && Read(path).Version == 16
            && Directory.GetFiles(Path.GetDirectoryName(path)!, "profile.xml.v15.*.bak").Select(File.ReadAllBytes).Any(bytes => bytes.SequenceEqual(playedBytes)),
            "the first .v15.bak stays and the played V15 bytes get their own backup");
        path = V15Fixture(root, "v15-commit-failure", out original);
        Directory.CreateDirectory(path + ".tmp");
        check(Throws(path) && original.SequenceEqual(File.ReadAllBytes(path)) && original.SequenceEqual(File.ReadAllBytes(path + ".v15.bak")),
            "a failed V16 commit keeps the V15 save and its backup");
        Directory.Delete(path + ".tmp");
        using (var retry = new LocalProfile(path)) check(retry.WildCards(Mixed).SequenceEqual(new[] { "CS2_182" }), "the retry migrates");
        check(Read(path).Version == 16 && original.SequenceEqual(File.ReadAllBytes(path + ".v15.bak")), "the retry kept the backup");
        Console.WriteLine("PASS V16 Classic migration: reprints granted and swapped, Wild cards flagged, byte-exact backup, idempotent, failures safe");
    }
}

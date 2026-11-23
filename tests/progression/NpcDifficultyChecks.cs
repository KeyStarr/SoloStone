using System.Xml.Serialization;

// NPC difficulty on the save (owner 2026-11-15, plugin/NpcDifficultyIds.cs): absent = Hard (every save made before the setting),
// the Create save choice (default Medium) is written at creation, any selectable level can be set any time, Extreme is locked,
// and each world match stamps the Easy/Medium level it began on (Hard matches stay byte-identical).
internal static class NpcDifficultyChecks
{
    private static readonly XmlSerializer Xml = new XmlSerializer(typeof(LocalProfileData));
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    private static LocalProfileData Read(string path) { using var input = File.OpenRead(path); return (LocalProfileData)Xml.Deserialize(input)!; }
    private static void Write(string path, LocalProfileData data) { using var output = File.Create(path); Xml.Serialize(output, data); }

    internal static void Run(string root)
    {
        Check(NpcDifficultyIds.All.SequenceEqual(new[] { "easy", "medium", "hard", "extreme" }), "four levels in order");
        Check(NpcDifficultyIds.Resolve(null) == "hard" && NpcDifficultyIds.NewSaveDefault == "medium", "absent = Hard, new saves Medium");
        Check(!NpcDifficultyIds.IsSelectable("extreme") && NpcDifficultyIds.IsLocked("extreme") && NpcDifficultyIds.Summary("extreme") == "Coming later", "Extreme locked");
        Reject<InvalidOperationException>(() => NpcDifficultyIds.Resolve("nightmare"));

        string dir = Path.Combine(root, "npc-difficulty"); Directory.CreateDirectory(dir);
        // An existing save (no field) is Hard and stays byte-identical until the player changes it.
        string old = Path.Combine(dir, "old.xml");
        using (var profile = new LocalProfile(old)) { profile.PersistNewProfile(new ProfileOptions()); }
        var data = Read(old); data.NpcDifficulty = null; Write(old, data);
        byte[] before = File.ReadAllBytes(old);
        using (var profile = new LocalProfile(old))
        {
            Check(profile.NpcDifficulty == "hard", "a save without the field is Hard");
            Check(!profile.SetNpcDifficulty("hard") && File.ReadAllBytes(old).SequenceEqual(before), "picking Hard on an old save writes nothing");
            var hard = profile.Begin("player-core-mage", "priest", 11, "npc.a", "npc-dragon-warrior", 1200);
            Check(hard.NpcDifficulty == null, "a Hard match writes no difficulty");
            profile.Settle(hard.Id, "Won", 8, false); profile.AcknowledgeRewardPresentation(profile.History.Last().Id);
        }
        Check(!File.ReadAllText(old).Contains("NpcDifficulty"), "Hard saves and matches never write the element");

        // A new save takes the Create save choice; without one, Medium.
        string fresh = Path.Combine(dir, "fresh.xml");
        using (var profile = new LocalProfile(fresh)) { profile.PersistNewProfile(new ProfileOptions()); }
        Check(Read(fresh).NpcDifficulty == "medium", "new save defaults to Medium");
        // The picker reads the field and stops at the first other root element (NativeProfileSelector.ReadDifficulty): this order is its contract.
        string freshText = File.ReadAllText(fresh);
        int grind = freshText.IndexOf("<GrindTier>", StringComparison.Ordinal), level = freshText.IndexOf("<NpcDifficulty>", StringComparison.Ordinal),
            version = freshText.IndexOf("<Version>", StringComparison.Ordinal);
        Check(grind >= 0 && grind < level && level < version, "root order: GrindTier, NpcDifficulty, Version");
        // First launch (no Create form): the welcome path makes the save, and it starts at Medium; an existing save never gains the field.
        string launch = Path.Combine(dir, "launch", "profile.xml");
        using (var profile = new LocalProfile(launch)) Check(profile.GrantWelcomeIfNew() && profile.NpcDifficulty == "medium", "a first-launch save starts at Medium");
        Check(Read(launch).NpcDifficulty == "medium", "first-launch Medium is written");
        string oldBytes = File.ReadAllText(old);
        using (var profile = new LocalProfile(old)) Check(!profile.GrantWelcomeIfNew() && profile.NpcDifficulty == "hard", "an existing save is not granted again");
        Check(File.ReadAllText(old) == oldBytes && !oldBytes.Contains("NpcDifficulty"), "an existing save never gains the field");
        string chosen = Path.Combine(dir, "chosen.xml");
        using (var profile = new LocalProfile(chosen)) { profile.PersistNewProfile(new ProfileOptions { NpcDifficulty = "easy" }); }
        using (var profile = new LocalProfile(chosen))
        {
            Check(profile.NpcDifficulty == "easy", "Create save choice survives reopen");
            byte[] bytes = File.ReadAllBytes(chosen);
            Check(!profile.SetNpcDifficulty("easy") && File.ReadAllBytes(chosen).SequenceEqual(bytes), "same level is idempotent");
            Reject<ArgumentException>(() => profile.SetNpcDifficulty("extreme"));
            Reject<ArgumentException>(() => profile.SetNpcDifficulty("nightmare"));
            Check(File.ReadAllBytes(chosen).SequenceEqual(bytes), "refused changes leave bytes untouched");

            // The match stamps the level it began on; a later change never rewrites it.
            profile.ArmNpcDifficulty("easy");
            var easy = profile.Begin("player-core-mage", "npc-core-priest", 12, "npc.b", "npc-core-priest", 980);
            Check(easy.NpcDifficulty == "easy", "Begin stamps the encounter's level");
            Check(profile.SetNpcDifficulty("hard"), "can change any time; the pending match keeps its level");
            var settled = profile.Settle(easy.Id, "Lost", 9, false);
            Check(settled.NpcDifficulty == "easy" && settled.Ladder != null, "settled record keeps Easy and a normal ladder receipt");
            profile.AcknowledgeRewardPresentation(settled.Id);
            profile.ArmNpcDifficulty("hard");
            var hard = profile.Begin("player-core-mage", "priest", 13, "npc.c", "npc-dragon-warrior", 1200);
            Check(hard.NpcDifficulty == null, "a Hard encounter stamps nothing");
            profile.Settle(hard.Id, "Won", 8, false); profile.AcknowledgeRewardPresentation(profile.History.Last().Id);
            profile.ArmNpcDifficulty("medium");
            var legacy = profile.Begin("player-core-mage", "priest", 14);
            Check(legacy.NpcDifficulty == null, "a legacy opponent (no NPC) stamps nothing");
            profile.Settle(legacy.Id, "Won", 8, false); profile.AcknowledgeRewardPresentation(profile.History.Last().Id);
        }
        using (var profile = new LocalProfile(chosen))
        {
            Check(profile.NpcDifficulty == "hard" && profile.History.Count(m => m.NpcDifficulty == "easy") == 1, "level and stamps reopen");
        }

        foreach (int oldVersion in new[] { 15, 16 })
        {
            string migrate = Path.Combine(dir, $"v{oldVersion}-easy.xml");
            var older = Read(chosen);
            older.Version = oldVersion;
            older.NpcDifficulty = NpcDifficultyIds.Easy;
            Write(migrate, older);
            using var profile = new LocalProfile(migrate);
            Check(Read(migrate).Version == LocalProfile.SaveVersion && profile.NpcDifficulty == NpcDifficultyIds.Easy,
                $"V{oldVersion} upgrade preserves save difficulty");
            Check(profile.History.Any(match => match.NpcDifficulty == NpcDifficultyIds.Easy), $"V{oldVersion} upgrade preserves match difficulty");
        }

        // Unknown or locked values in a save are refused, never silently changed.
        foreach (string bad in new[] { "extreme", "nightmare" })
        {
            string path = Path.Combine(dir, "bad-" + bad + ".xml");
            File.Copy(chosen, path);
            var broken = Read(path); broken.NpcDifficulty = bad; Write(path, broken);
            Reject<InvalidOperationException>(() => { using var _ = new LocalProfile(path); });
            string match = Path.Combine(dir, "bad-match-" + bad + ".xml");
            File.Copy(chosen, match);
            var stamped = Read(match); stamped.Matches.First(m => m.NpcDifficulty != null).NpcDifficulty = bad; Write(match, stamped);
            Reject<InvalidOperationException>(() => { using var _ = new LocalProfile(match); });
        }
        Console.WriteLine("PASS NPC difficulty on the save: absent = Hard, new saves Medium, set any time, Extreme locked, match stamps");
    }
}

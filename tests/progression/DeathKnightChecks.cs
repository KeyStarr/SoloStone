using System.Xml.Serialization;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class DeathKnightChecks
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    private static void Reject(Action action, string name)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidOperationException || e is ArgumentException || e is IOException || e is UnauthorizedAccessException) { return; }
        throw new Exception("Expected rejection: " + name);
    }
    private static LocalProfileData Read(string path) { using var stream = File.OpenRead(path); return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(stream); }
    private static void Write(string path, LocalProfileData saved) { using var stream = File.Create(path); new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, saved); }
    private static Card Runes(int blood, int frost, int unholy)
    {
        var card = Cards.FromId("CS2_182").Clone();
        card.Tags[(GameTag)2196] = blood; card.Tags[(GameTag)2197] = frost; card.Tags[(GameTag)2198] = unholy;
        return card;
    }
    internal static void Run(string root)
    {
        string path = Path.Combine(root, "dk-v7.xml");
        using (var profile = new LocalProfile(path))
        {
            profile.CreateDraft("Retained Mage", "HERO_08");
            var match = profile.Begin("mage", "priest", 42);
            profile.Settle(match.Id, "Won", 12, false);
        }
        var v7 = Read(path); v7.Version = 7; v7.CoreScheduleVersion = 2; v7.DustBalance = 0;
        // A real V7 record predates the V10 settlement receipts and the V3 rolled rewards.
        foreach (var match in v7.Matches) {
            var v2 = LocalEconomy.Reward(match.Result, match.Turns, match.Conceded, match.RewardFacts);
            match.RewardVersion = 2; match.Gold = v2.Gold; match.BonusGold = v2.BonusGold; match.PackReward = v2.PackReward; match.DustReward = 0;
            match.Ladder = null; match.ClassXpBefore = null; match.ClassXpAfter = null;
            match.UnlockedCardIds = null; match.ProgressionRuleVersion = 0; match.TierRewards = null;
            match.ClassRewardGrants = null; match.PortraitGrants = null;
        }
        v7.ClassXp.RemoveAll(entry => entry.Hero == "HERO_11");
        v7.ClassXp.Single(entry => entry.Hero == "HERO_10").Xp = 12345;
        v7.CoreGrants = LocalCoreProgression.Owned(v7.ClassXp.ToDictionary(entry => entry.Hero, entry => entry.Xp), false)
            .Where(pair => LocalCoreProgression.ById[pair.Key].Hero != "HERO_11")
            .Select(pair => new LocalCardCount { Id = pair.Key, Count = pair.Value }).ToList();
        v7.CoreGrants.Add(new LocalCardCount { Id = "CORE_UNKNOWN_EARNED", Count = 2 });
        Write(path, v7); byte[] original = File.ReadAllBytes(path);
        Directory.CreateDirectory(path + ".v7.bak.tmp");
        Reject(() => { using var ignored = new LocalProfile(path); }, "V7MigrationBackupFailureLeavesOriginal");
        Check(File.ReadAllBytes(path).SequenceEqual(original) && !File.Exists(path + ".v7.bak"), "V7MigrationBackupFailureLeavesOriginal");
        Directory.Delete(path + ".v7.bak.tmp");
        Directory.CreateDirectory(path + ".tmp");
        Reject(() => { using var ignored = new LocalProfile(path); }, "V7 migration commit failure");
        Check(File.ReadAllBytes(path).SequenceEqual(original) && File.ReadAllBytes(path + ".v7.bak").SequenceEqual(original), "V7 failed replacement changed bytes");
        Directory.Delete(path + ".tmp");
        using (var profile = new LocalProfile(path))
            Check(profile.ClassXp.Count == 11 && profile.ClassXp["HERO_11"] == 0
                && v7.ClassXp.All(old => profile.ClassXp[old.Hero] == old.Xp), "V7ToV8PreservesBytesAndProgress XP");
        var migrated = Read(path);
        var additions = migrated.CoreGrants.Where(grant => v7.CoreGrants.All(old => old.Id != grant.Id)).ToList();
        Check(additions.Select(grant => grant.Id).SequenceEqual(LocalCoreProgression.Unlocks
            .Where(card => card.Hero == "HERO_11" && card.Threshold == 1).Select(card => card.Id)), "schedule 2 to current adds only DK level one");
        v7.Version = 12; v7.SelectedHubTheme = "CLASSIC";
        int folded = v7.Matches.Aggregate(0, (stars, match) => LocalProfile.NextStars(stars, match.Result));
        var seed = LadderRules.Seed(folded, folded, v7.Matches.Select(m => LadderRules.Outcome(m.Result)).ToArray());
        v7.LadderState = new LocalLadderState { StarIndex = folded, BestStarIndex = folded,
            Multiplier = seed.Multiplier, StreakWins = seed.StreakWins, HiddenRating = LadderRules.StartRating };
        v7.LadderRuleVersion = LadderRules.RuleVersion;
        v7.LadderCalibration = new LocalLadderCalibration { Version = 1, MatchCount = v7.Matches.Count };
        v7.CoreScheduleVersion = LocalCoreProgression.ScheduleVersion;
        v7.ClassXp.Add(new LocalClassXp { Hero = "HERO_11" }); v7.CoreGrants.AddRange(additions);
        Check(migrated.ClassRewardGrants.Count == LocalCoreProgression.Level(12345) - 10
            && migrated.ClassRewardGrants.All(row => row.Hero == "HERO_10")
            && migrated.PremiumCollection.Count == migrated.ClassRewardGrants.Count, "V7 adds earned DH premiums");
        v7.ClassRewardGrants = migrated.ClassRewardGrants; v7.PremiumCollection = migrated.PremiumCollection;
        string expected = path + ".expected"; Write(expected, v7);
        Check(File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(expected))
            && File.ReadAllBytes(path + ".v7.bak").SequenceEqual(original), "V7ToV8PreservesBytesAndProgress all fields");
        byte[] current = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path)) { }
        Check(File.ReadAllBytes(path).SequenceEqual(current), "V8 stable reload");
        foreach (var corrupt in new Action<LocalProfileData>[] {
            saved => saved.ClassXp.RemoveAt(10), saved => saved.ClassXp[10].Hero = saved.ClassXp[0].Hero,
            saved => saved.ClassXp[10].Hero = "HERO_99", saved => saved.ClassXp[10].Xp = -1 })
        {
            var bad = Read(path); corrupt(bad); string badPath = path + ".bad"; Write(badPath, bad);
            Reject(() => { using var ignored = new LocalProfile(badPath); }, "V8RequiresElevenUniqueClassXpEntries");
        }
        // Validate the frozen V7 shape before creating a backup.
        var invalidV7 = Read(path); invalidV7.Version = 7; Write(path + ".bad-v7", invalidV7);
        Reject(() => { using var ignored = new LocalProfile(path + ".bad-v7"); }, "frozen V7 rejects eleven heroes");
        Check(!File.Exists(path + ".bad-v7.v7.bak"), "invalid V7 backed up");
        invalidV7.ClassXp.RemoveAll(entry => entry.Hero == "HERO_11");
        invalidV7.CustomDecks[0].Hero = "HERO_11";
        Write(path + ".bad-v7-draft", invalidV7);
        Reject(() => { using var ignored = new LocalProfile(path + ".bad-v7-draft"); }, "frozen V7 draft heroes");
        Check(!File.Exists(path + ".bad-v7-draft.v7.bak"), "invalid V7 draft backed up");
        var previous = new HashSet<string>();
        foreach (int xp in LocalCoreProgression.LevelThresholds)
        {
            var progress = LocalCoreProgression.Heroes.ToDictionary(hero => hero, hero => hero == "HERO_11" ? xp : 0);
            var grants = LocalCoreProgression.Owned(progress, false).Where(pair => LocalCoreProgression.ById[pair.Key].Hero == "HERO_11")
                .Select(pair => pair.Key).ToHashSet();
            Check(previous.IsSubsetOf(grants), "DkCoreUnlocksMonotonic"); previous = grants;
        }
        Check(previous.Count == 34 && LocalCoreProgression.NeutralBaseCombinedLevel == 4, "DkCoreUnlocksMonotonic count/base");
        var ff = Runes(0, 2, 0);
        Check(LocalDecks.RuneCounts(new[] { ff, ff }).SequenceEqual(new[] { 0, 2, 0 }), "RuneMaximaNotSum");
        LocalDecks.ValidateRunes(new[] { Runes(2, 0, 0), Runes(0, 1, 0) }, CardClass.DEATHKNIGHT);
        LocalDecks.ValidateRunes(new[] { Runes(1, 1, 0), Runes(1, 0, 1) }, CardClass.DEATHKNIGHT);
        Reject(() => LocalDecks.ValidateRunes(new[] { Runes(2, 0, 0), ff }, CardClass.DEATHKNIGHT), "RuneThreeSlotBoundary");
        LocalDecks.ValidateRunes(new[] { Runes(2, 0, 0), ff }, CardClass.MAGE);
        var dual = Runes(0, 0, 0); dual.Tags[(GameTag)476] = 65;
        Check(LocalDecks.IsAllowedInDeck(dual, CardClass.DEATHKNIGHT) && LocalDecks.IsAllowedInDeck(dual, CardClass.ROGUE)
            && !LocalDecks.IsAllowedInDeck(dual, CardClass.MAGE), "ModernMulticlassLegality DK/Rogue");
        dual.Tags[(GameTag)476] = 8193;
        Check(LocalDecks.IsAllowedInDeck(dual, CardClass.DEATHKNIGHT) && LocalDecks.IsAllowedInDeck(dual, (CardClass)14)
            && !LocalDecks.IsAllowedInDeck(dual, CardClass.MAGE), "ModernMulticlassLegality DK/DH");
        dual.Tags[(GameTag)476] = 9153;
        Check(LocalDecks.IsAllowedInDeck(dual, CardClass.DEATHKNIGHT) && !LocalDecks.IsAllowedInDeck(dual, CardClass.MAGE), "ModernMulticlassLegality six-class");
        var maskCard = Cards.FromId("CS2_182"); GameTag mask = (GameTag)476;
        bool hadMask = maskCard.Tags.TryGetValue(mask, out int oldMask);
        try
        {
            maskCard.Tags[mask] = 65;
            LocalDecks.Draft("Rogue mask", "HERO_03", new[] { maskCard.Id });
            Reject(() => LocalDecks.Draft("Mage mask", "HERO_08", new[] { maskCard.Id }), "modern mask at draft boundary");
        }
        finally { if (hadMask) maskCard.Tags[mask] = oldMask; else maskCard.Tags.Remove(mask); }
#if ENGINE_SOURCE
        // Authoritative catalog identities with injected printed requirements exercise the real save boundaries.
        var blood = Cards.FromId("CS2_182"); var frost = Cards.FromId("CS2_179");
        GameTag b = (GameTag)2196, f = (GameTag)2197;
        bool hadB = blood.Tags.TryGetValue(b, out int oldB), hadF = frost.Tags.TryGetValue(f, out int oldF);
        try
        {
            blood.Tags[b] = 2; frost.Tags[f] = 2;
            var illegal = new[] { blood.Id, frost.Id };
            LocalDecks.Draft("Non-DK ignores runes", "HERO_08", illegal);
            Reject(() => LocalDecks.Draft("Invalid", "HERO_11", illegal), "IncompleteDraftStillValidatesRunes");
            string atomic = Path.Combine(root, "dk-atomic.xml");
            using var profile = new LocalProfile(atomic);
            var draft = profile.CreateDraft("DK", "HERO_11");
            profile.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { blood.Id }, draft.SortOrder);
            byte[] before = File.ReadAllBytes(atomic); long nextId = profile.NextCustomDeckId;
            var invalidStored = Read(atomic); invalidStored.CustomDecks[0].Cards = illegal.ToList();
            string invalidPath = atomic + ".invalid"; Write(invalidPath, invalidStored);
            byte[] invalidBytes = File.ReadAllBytes(invalidPath);
            Reject(() => { using var ignored = new LocalProfile(invalidPath); }, "stored incomplete draft validates runes");
            Check(File.ReadAllBytes(invalidPath).SequenceEqual(invalidBytes), "invalid stored rune validation rewrote bytes");
            Reject(() => profile.SaveDraft(draft.Id, "Invalid", "HERO_11", illegal, draft.SortOrder), "InvalidRuneSaveLeavesProfileBytesUnchanged");
            var neutrals = profile.Collection.Where(pair => pair.Value >= 2 && Cards.FromId(pair.Key).Class == CardClass.NEUTRAL
                && !LocalCardCatalog.IsCore(pair.Key)).Select(pair => pair.Key).ToList();
            var thirty = illegal.Concat(neutrals.Where(id => !illegal.Contains(id)).Take(14).SelectMany(id => new[] { id, id })).ToArray();
            Check(thirty.Length == 30, "atomic complete fixture");
            Reject(() => profile.SaveDeck("Invalid", "HERO_11", thirty), "InvalidRuneSaveDoesNotConsumeDeckId");
            Check(File.ReadAllBytes(atomic).SequenceEqual(before) && profile.NextCustomDeckId == nextId
                && profile.GetDraft(draft.Id).Cards.SequenceEqual(new[] { blood.Id }), "atomic invalid save changed memory/bytes/allocator");
            string allocatorPath = atomic + ".allocator";
            using var allocator = new LocalProfile(allocatorPath);
            long firstId = allocator.NextCustomDeckId;
            Reject(() => allocator.SaveDeck("Invalid", "HERO_11", thirty), "invalid new deck before allocation");
            Check(allocator.NextCustomDeckId == firstId && !File.Exists(allocatorPath), "invalid new deck created profile/consumed id");
            var next = allocator.CreateDraft("Next DK", "HERO_11");
            Check(next.Id == firstId, "InvalidRuneSaveDoesNotConsumeDeckId");
        }
        finally { if (hadB) blood.Tags[b] = oldB; else blood.Tags.Remove(b); if (hadF) frost.Tags[f] = oldF; else frost.Tags.Remove(f); }
        Check(LocalDecks.IsAllowedInDeck(Cards.FromId("END_002"), CardClass.ROGUE)
            && !LocalDecks.IsAllowedInDeck(Cards.FromId("END_002"), CardClass.MAGE), "export-backed modern multiclass");
        var dk = LocalDecks.TestLoaners.Where(deck => deck.Hero == "HERO_11" && !deck.Id.StartsWith("showcase-", StringComparison.Ordinal)).ToArray();
        Check(dk.Length == 10 && dk.Select(deck => NativeUIProjection.DeckId(deck.Id)).SequenceEqual(new long[] { 900000040, 900000041, 900000042, 900000043, 900000044, 900000045, 900000046, 900000039, 900000047, 900000048 }), "DK loaner unique ids");
        string[] opponents = { "test-standard-batch-2-opp-druid", "test-standard-batch-2-opp-warlock", "test-standard-batch-2-opp-hunter", "test-standard-batch-2-opp-druid", "test-standard-batch-2-opp-rogue", "test-standard-batch-2-opp-rogue", "test-standard-batch-2-opp-rogue", "test-standard-batch-2-opp-rogue", "test-standard-batch-2-opp-rogue", "test-standard-batch-2-opp-hunter" };
        Check(dk.Select(deck => LocalDecks.OpponentFor(deck.Id, Array.Empty<string>()).Id).SequenceEqual(opponents), "DK fixed opponents");
        foreach (var deck in dk)
        {
            Check(deck.Build().All(card => LocalDecks.IsAllowedInDeck(card, CardClass.DEATHKNIGHT)), "DK loaner class legality");
            LocalDecks.ValidateRunes(deck.Build(), CardClass.DEATHKNIGHT);
            Check(deck.Strategy.Contains("Not a starter deck"), "DK test-only label");
            string loanerPath = Path.Combine(root, deck.Id + ".xml");
            using (var profile = new LocalProfile(loanerPath))
                profile.Begin(deck.Id, LocalDecks.OpponentFor(deck.Id, Array.Empty<string>()).Id, 31);
            using (var profile = new LocalProfile(loanerPath))
            {
                Check(profile.Pending.DeckHero == "HERO_11", "DK loaner pending hero binding survives reload");
                profile.Settle(profile.Pending.Id, "Won", 12, false);
                Check(profile.ClassXp["HERO_11"] == LocalCoreProgression.WinXp
                    && profile.ClassXp.Where(pair => pair.Key != "HERO_11").All(pair => pair.Value == 0), "DK loaner settlement targets DK XP");
            }
        }
        Check(dk.Take(5).Select(deck => string.Join(",", LocalDecks.RuneCounts(deck.Build()))).SequenceEqual(new[] { "1,1,1", "0,3,0", "0,0,3", "3,0,0", "1,0,2" }), "DK loaner rune legality");
        // The batch-10 DK seats (900000045..46) grow with coordinator batch-10 bookkeeping; they only have to stay rune-legal.
        Check(dk.Skip(5).Take(2).Select(deck => deck.Id).SequenceEqual(new[] { "test-standard-b10-deathknight", "test-standard-b10-deathknight-2" })
            && dk.Skip(5).Take(2).All(deck => LocalDecks.RuneCounts(deck.Build()).Sum() <= 3), "batch-10 DK seat rune legality");
        Check(dk.Skip(7).Take(2).Select(deck => deck.Id).SequenceEqual(new[] { "test-standard-mecha-deathknight", "test-standard-mecha-deathknight-2" })
            && dk.Skip(7).Take(2).All(deck => LocalDecks.RuneCounts(deck.Build()).Sum() <= 3), "mech families DK seat rune legality");
        Check(string.Join(",", LocalDecks.RuneCounts(dk.Single(deck => deck.Id == LocalDecks.TestShowcaseId).Build())) == "1,0,2", "showcase DK rune legality");
#endif
        Console.WriteLine("PASS DK V7/V8 migration/backup/atomicity, monotonic Core, rune maxima, modern class masks");
        ScheduleThreeToFour(root);
        ScheduleFourToFive(root);
        ScheduleFiveToCurrent(root, 5);
        ScheduleFiveToCurrent(root, 6);
    }

    // Schedule 4 = cost-rules rows: 26 moved thresholds and six new Core cards. A schedule-3 save only gains grants.
    private static void ScheduleThreeToFour(string root)
    {
        Check(LocalCoreProgression.ScheduleVersion >= 4, "schedule 4 or later is current");
        string path = Path.Combine(root, "schedule-3.xml");
        using (var profile = new LocalProfile(path)) profile.CreateDraft("Schedule", "HERO_08");
        var saved = Read(path);
        saved.CoreScheduleVersion = 3;
        // Shaman level 9 now earns Fire Elemental (threshold 10 -> 9); Warlock level 4 earned Hellfire under 3 (4 -> 5).
        saved.ClassXp.Single(entry => entry.Hero == "HERO_02").Xp = LocalCoreProgression.LevelThresholds[8];
        saved.ClassXp.Single(entry => entry.Hero == "HERO_07").Xp = LocalCoreProgression.LevelThresholds[3];
        var owned = LocalCoreProgression.Owned(saved.ClassXp.ToDictionary(entry => entry.Hero, entry => entry.Xp), false);
        Check(owned.ContainsKey("CORE_CS2_042") && !owned.ContainsKey("CORE_CS2_062"), "schedule 4 fixture thresholds");
        saved.CoreGrants = owned.Where(pair => pair.Key != "CORE_CS2_042")
            .Select(pair => new LocalCardCount { Id = pair.Key, Count = pair.Value }).ToList();
        saved.CoreGrants.Add(new LocalCardCount { Id = "CORE_CS2_062", Count = 2 });
        Write(path, saved);
        using (var profile = new LocalProfile(path)) { }
        saved.CoreScheduleVersion = LocalCoreProgression.ScheduleVersion;
        saved.CoreGrants.Add(new LocalCardCount { Id = "CORE_CS2_042", Count = 2 });
        string expected = path + ".expected"; Write(expected, saved);
        byte[] migrated = File.ReadAllBytes(path);
        Check(migrated.SequenceEqual(File.ReadAllBytes(expected)), "ScheduleThreeToFour grants lowered threshold, keeps raised one, changes nothing else");
        using (var profile = new LocalProfile(path)) { }
        Check(File.ReadAllBytes(path).SequenceEqual(migrated), "ScheduleFour stable reload");
        Console.WriteLine("PASS Core schedule 3 -> 4: earlier unlock granted, earned grant kept, other state unchanged");
    }

    // Schedule 5 lowers Plague/Runner and adds Warsong; previously earned Execute stays owned.
    private static void ScheduleFourToFive(string root)
    {
        Check(LocalCoreProgression.ScheduleVersion >= 5, "schedule 5 or later is current");
        string path = Path.Combine(root, "schedule-4.xml");
        using (var profile = new LocalProfile(path)) profile.CreateDraft("Schedule 4", "HERO_08");
        var saved = Read(path);
        saved.CoreScheduleVersion = 4;
        saved.ClassXp.Single(entry => entry.Hero == "HERO_09").Xp = LocalCoreProgression.LevelThresholds[3];
        var owned = LocalCoreProgression.Owned(saved.ClassXp.ToDictionary(entry => entry.Hero, entry => entry.Xp), false);
        string[] additions = { "CORE_BAR_311", "CORE_BT_480", "CORE_WON_350" };
        Check(additions.All(owned.ContainsKey) && !owned.ContainsKey("CORE_EX1_391"), "schedule 5 fixture thresholds");
        saved.CoreGrants = owned.Where(pair => !additions.Contains(pair.Key))
            .Select(pair => new LocalCardCount { Id = pair.Key, Count = pair.Value }).ToList();
        saved.CoreGrants.Add(new LocalCardCount { Id = "CORE_EX1_391", Count = 2 });
        saved.CoreGrants.Add(new LocalCardCount { Id = "CORE_UNKNOWN_EARNED", Count = 2 });
        Write(path, saved);
        using (var profile = new LocalProfile(path)) { }
        saved.CoreScheduleVersion = LocalCoreProgression.ScheduleVersion;
        saved.CoreGrants.AddRange(LocalCoreProgression.Unlocks.Where(card => additions.Contains(card.Id))
            .Select(card => new LocalCardCount { Id = card.Id, Count = 2 }));
        string expected = path + ".expected"; Write(expected, saved);
        byte[] migrated = File.ReadAllBytes(path);
        Check(migrated.SequenceEqual(File.ReadAllBytes(expected)), "ScheduleFourToFive grants only lowered/new, revokes nothing, changes nothing else");
        using (var profile = new LocalProfile(path)) { }
        Check(File.ReadAllBytes(path).SequenceEqual(migrated), "ScheduleFive stable reload");
        Console.WriteLine("PASS Core schedule 4 -> 5: two lowered unlocks and one new grant, earned/unknown grants retained, other state unchanged");
    }

    private static void ScheduleFiveToCurrent(string root, int previous)
    {
        Check(LocalCoreProgression.ScheduleVersion == 8, "schedule 8 is current");
        string[] additions = "CORE_BAR_313 CORE_BT_035 CORE_BT_120 CORE_CATA_002 CORE_CATA_004 CORE_CATA_006 CORE_CATA_007 CORE_EDR_003 CORE_ETC_111 CORE_REV_946 CORE_RLK_086 CORE_RLK_087 CORE_RLK_116 CORE_RLK_118 CORE_RLK_506 CORE_RLK_567 CORE_SCH_717 CORE_TRL_345 CORE_TTN_866 CORE_WON_141 CORE_WW_374 CORE_YOD_026 CS3_020 Core_UNG_072 RLK_048 RLK_060 RLK_061 TTN_851".Split(' ');
        additions = (previous == 5 ? additions : Array.Empty<string>()).Concat(new[] { "CORE_WON_145", "CS3_022", "CS3_035" }).ToArray();
        additions = additions.Concat("CORE_BT_781 CORE_AT_037 Core_LOE_115 CORE_EX1_160 CORE_EX1_154 CORE_OG_047 CORE_TSC_650 CORE_ONY_018 CORE_EX1_250 CORE_BT_187 CORE_ETC_523 CORE_BT_156 CORE_SCH_605".Split(' ')).ToArray();
        string path = Path.Combine(root, "schedule-" + previous + ".xml");
        using (var profile = new LocalProfile(path)) profile.CreateDraft("Schedule " + previous, "HERO_08");
        var saved = Read(path);
        saved.CoreScheduleVersion = previous;
        foreach (var entry in saved.ClassXp) entry.Xp = 2700;
        var owned = LocalCoreProgression.Owned(saved.ClassXp.ToDictionary(entry => entry.Hero, entry => entry.Xp), false);
        saved.CoreGrants = owned.Where(pair => !additions.Contains(pair.Key))
            .Select(pair => new LocalCardCount { Id = pair.Key, Count = pair.Value }).ToList();
        Check(saved.CoreGrants.Count == (previous == 5 ? 245 : 273) && additions.Length == (previous == 5 ? 44 : 16) && additions.All(owned.ContainsKey), "schedule 5 fixture membership");
        saved.CoreGrants.Add(new LocalCardCount { Id = "CORE_UNKNOWN_EARNED", Count = 2 });
        Write(path, saved);
        using (var profile = new LocalProfile(path)) { }
        saved.CoreScheduleVersion = LocalCoreProgression.ScheduleVersion;
        saved.CoreGrants.AddRange(owned.Where(pair => additions.Contains(pair.Key))
            .Select(pair => new LocalCardCount { Id = pair.Key, Count = pair.Value }));
        var reconciled = Read(path);
        Check(reconciled.ClassRewardGrants.Count == 55 && reconciled.ClassRewardGrants.All(row => row.Level >= 11 && row.Level <= 15),
            "Core schedule fixture also reconciles earned premium rewards");
        saved.ClassRewardGrants = reconciled.ClassRewardGrants; saved.PremiumCollection = reconciled.PremiumCollection;
        string expected = path + ".expected"; Write(expected, saved);
        byte[] migrated = File.ReadAllBytes(path);
        Check(migrated.SequenceEqual(File.ReadAllBytes(expected)), "Core migration adds only new grants, retains prior and unknown grants, changes nothing else");
        using (var profile = new LocalProfile(path)) { }
        Check(File.ReadAllBytes(path).SequenceEqual(migrated), "ScheduleEight stable reload");
        Console.WriteLine("PASS Core schedule " + previous + " -> 8: " + additions.Length + " additions, prior/unknown grants retained, exact other state and stable reload");
    }
}

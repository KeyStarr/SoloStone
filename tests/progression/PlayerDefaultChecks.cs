using System.Xml.Serialization;

internal static class PlayerDefaultChecks
{
    private static LocalProfileData Read(string path)
    {
        using var file = File.OpenRead(path);
        return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(file)!;
    }

    private static void Write(string path, LocalProfileData data)
    {
        using var file = File.Create(path);
        new XmlSerializer(typeof(LocalProfileData)).Serialize(file, data);
    }

    private static void SeedLegendRun(string path)
    {
        using var profile = new LocalProfile(path);
        int seed = 0;
        while (profile.Stars < LadderRules.LegendIndex - 1)
        {
            var pending = profile.Begin("mage", "priest", seed++);
            profile.Settle(pending.Id, "Won", 10, false);
            profile.AcknowledgeRewardPresentation(pending.Id);
        }
    }

    private static void CheckLegendSnapshot(string path, string key, LocalDeck expected, Action<bool, string> check)
    {
        SeedLegendRun(path);
        using (var profile = new LocalProfile(path))
        {
            var match = profile.Begin(key, "priest", 501);
            var settled = profile.Settle(match.Id, "Won", 10, false);
            check(profile.Stars == 150 && settled.Ladder.LegendReached, "test reaches Legend with " + key);
        }
        var snapshot = Read(path).LegendDeck;
        check(snapshot != null && !snapshot.Legacy && snapshot.DeckName == expected.Name && snapshot.Hero == expected.Hero
            && snapshot.Cards.SequenceEqual(expected.CardIds) && snapshot.Sideboards.Count == expected.Sideboards.Count
            && snapshot.Sideboards.All(board => expected.Sideboards.TryGetValue(board.Source, out var cards)
                && board.Cards.SequenceEqual(cards)), "Legend snapshot resolves the intended recipe: " + key);
    }

    internal static void Run()
    {
#if ENGINE_SOURCE
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        var catalog = LocalDecks.PlayerCoreStarterDecks.ToArray();
        string[] classes = { "mage", "paladin", "priest", "warrior", "druid", "hunter", "rogue", "shaman", "warlock", "demonhunter", "deathknight" };
        string[] expectedKeys = classes.Select(name => "player-core-" + name).ToArray();
        Check(catalog.Select(deck => deck.Id).SequenceEqual(expectedKeys), "player starter catalog has stable distinct identities");
        Check(catalog.Select(deck => deck.Hero).SequenceEqual(LocalCoreProgression.Heroes),
            "one Core starter per class, in class order, each class exactly once");
        Check(expectedKeys.Select(NativeUIProjection.DeckId).SequenceEqual(Enumerable.Range(0, 11).Select(i => 900000215L + i)),
            "Core starters own native ids 900000215..900000225");
        var allDecks = LocalDecks.All.Concat(LocalDecks.TestLoaners).Concat(LocalDecks.TestOpponents)
            .Concat(LocalDecks.RetiredPlayerStarterDecks).Concat(LocalDecks.NpcDecks).ToArray();
        foreach (var deck in catalog)
        {
            var cards = deck.Build();
            Check(cards.Count == 30 && cards.All(card => LocalCoreProgression.ById.TryGetValue(card.Id, out var core)
                    && (core.Hero == deck.Hero || core.Hero.Length == 0) && card.Implemented && LocalCardCatalog.InActiveMeta(card)),
                "Core-only, own-class or neutral, implemented active 30-card starter: " + deck.Id);
            Check(deck.CardIds.GroupBy(id => id).All(group => group.Count() <= LocalCoreProgression.ById[group.Key].Copies)
                && !deck.CardIds.Contains("CS3_035"), "starter copies respect Core limits and avoid Nozdormu: " + deck.Id);
            Check(!string.IsNullOrWhiteSpace(deck.Strategy) && deck.Strategy.Length <= 160, "starter states one strategy line: " + deck.Id);
            Check(allDecks.All(other => other.Name != deck.Name && other.Id != deck.Id), "starter name never reuses another deck's name: " + deck.Name);
            NativeUIProjection.ValidateSelection(deck.Id, deck.Hero, deck.CardIds.ToArray(), new Dictionary<string, int>());
        }
        Check(catalog.Select(deck => deck.Name).Distinct().Count() == catalog.Length, "starter display names are unique");

        // Retired meta loaners stay resolvable for history and pending records, outside every normal tray.
        var retired = LocalDecks.RetiredPlayerStarterDecks.ToArray();
        string[] retiredKeys = { "player-rogue-imbue", "player-warlock-dragon-shred", "player-warrior-armor-herald" };
        Check(retired.Select(deck => deck.Id).SequenceEqual(retiredKeys)
            && LocalDecks.PlayerStarterDecks.SequenceEqual(catalog.Concat(retired))
            && retiredKeys.Select(key => LocalDecks.Get(key)).SequenceEqual(retired)
            && retiredKeys.Select(NativeUIProjection.DeckId).SequenceEqual(new[] { 900000212L, 900000213L, 900000214L }),
            "retired player loaners keep identity, lookup and native ids");
        string[][] expectedRecipes = {
            new[] { "CORE_EX1_145 EDR_528 EDR_852 END_000 EDR_523 FIR_920 JAIL_225 END_001 FIR_921 TIME_001 EDR_800 EDR_860 TIME_770", "END_036 EDR_856 TIME_EVENT_999 EDR_888" },
            new[] { "TLC_451 CATA_556 TIME_026 CORE_EX1_319 EDR_485 CORE_KAR_062 EDR_889 TIME_027 TIME_025 CATA_111 END_018 TIME_028 TIME_045 TIME_029 END_033", "" },
            new[] { "JAIL_386 CORE_AT_064 CORE_EX1_606 TIME_873 DINO_400 CATA_580 TLC_620 CATA_160 TLC_601 TLC_622 CATA_722 TLC_600 CORE_SW_068", "EDR_979 TIME_714 CATA_150 CATA_190h" }
        };
        for (int i = 0; i < retired.Length; i++)
            Check(retired[i].CardIds.SequenceEqual(expectedRecipes[i][0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(id => new[] { id, id }).Concat(expectedRecipes[i][1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))),
                "retired player recipe unchanged: " + retired[i].Id);
        string[] legacyNames = { "Imbue Rogue", "Shred Warlock", "Armor Warrior" };
        string[] expectedHeroes = { "HERO_03", "HERO_07", "HERO_01" };
        string[] legacyKeys = { "showcase-rogue-imbue", "showcase-warlock-dragon-shred", "showcase-warrior-armor-herald" };
        string[][] legacyRecipes = {
            new[] { "EDR_528 CORE_BT_701 EDR_852 END_000 CORE_DMF_511 JAIL_225 EDR_523 TIME_001 END_001 JAIL_470 TIME_770 EDR_800 TIME_710", "FIR_919 END_036 EDR_856 TIME_712" },
            new[] { "CATA_556 TIME_026 END_020 EDR_485 CORE_WON_096 CORE_NEW1_023 EDR_889 TIME_027 TIME_025 CATA_111 FIR_955 TIME_028 TIME_031 TIME_029 END_033", "" },
            new[] { "CORE_AT_064 CORE_EX1_606 BE_559 BE_572 BE_600 DINO_400 CATA_580 TLC_620 CATA_160 TLC_601 CATA_722 BE_560 CORE_SW_068", "FIR_939 EDR_979 TIME_714 CATA_150" }
        };
        for (int i = 0; i < legacyKeys.Length; i++)
        {
            var legacy = LocalDecks.Get(legacyKeys[i]);
            Check(LocalDecks.TestLoaners.Contains(legacy) && legacy.Name == legacyNames[i]
                && legacy.CardIds.SequenceEqual(legacyRecipes[i][0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .SelectMany(id => new[] { id, id }).Concat(legacyRecipes[i][1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))),
                "historical showcase identity and recipe stay unchanged: " + legacyKeys[i]);
        }
        for (int i = 0; i < legacyNames.Length; i++)
        {
            string aliasPath = Path.Combine(Path.GetTempPath(), "player-default-alias-" + Guid.NewGuid() + ".xml");
            try
            {
                using var profile = new LocalProfile(aliasPath);
                var match = profile.Begin(legacyNames[i], "showcase-ai-hunter-face", 30 + i);
                Check(match.DeckHero == expectedHeroes[i], "legacy player deck name remains a valid Begin alias: " + legacyNames[i]);
            }
            finally
            {
                if (File.Exists(aliasPath)) File.Delete(aliasPath);
                if (File.Exists(aliasPath + ".bak")) File.Delete(aliasPath + ".bak");
            }
        }

        string legendRoot = Path.Combine(Path.GetTempPath(), "player-default-legend-" + Guid.NewGuid());
        Directory.CreateDirectory(legendRoot);
        try
        {
            CheckLegendSnapshot(Path.Combine(legendRoot, "exact-id.xml"), expectedKeys[0], catalog[0], Check);
            CheckLegendSnapshot(Path.Combine(legendRoot, "retired-id.xml"), retiredKeys[0], retired[0], Check);
            CheckLegendSnapshot(Path.Combine(legendRoot, "legacy-name.xml"), legacyNames[0], LocalDecks.Get(legacyKeys[0]), Check);
        }
        finally { Directory.Delete(legendRoot, true); }
        // Loaners sit above the personal cap: every personal count keeps all eleven starters.
        int cap = NativeUIProjection.MaximumPersonalDecks;
        foreach (int personalCount in new[] { 0, 3, 18, cap - 4, cap - 1, cap, cap + 1 })
        {
            var defaults = NativeUIProjection.ProjectedLoanerKeys(personalCount);
            Check(defaults.SequenceEqual(expectedKeys), "player tray offers all eleven Core starters: " + personalCount);
            Check(Math.Min(personalCount, NativeUIProjection.MaximumPersonalDecks) + defaults.Count
                    + (personalCount < NativeUIProjection.MaximumPersonalDecks ? 1 : 0) <= NativeUIProjection.NativeDeckLimit,
                "personal decks, starters and New Deck fit the native tray: " + personalCount);
            Check(!defaults.Intersect(retiredKeys).Any(), "retired loaners never project: " + personalCount);
            foreach (string selector in new[] { "showcase", "11", " ShOwCaSe " })
                Check(NativeUIProjection.ProjectedLoanerKeys(personalCount, selector).SequenceEqual(defaults),
                    "installed showcase selector cannot expose the rest of the catalog");
            foreach (string group in Enumerable.Range(1, 10).Select(value => value.ToString()))
                Check(!NativeUIProjection.ProjectedLoanerKeys(personalCount, group).Intersect(expectedKeys.Concat(retiredKeys)).Any(),
                    "explicit harness group excludes player defaults: " + group);
            foreach (string key in defaults)
            {
                var deck = LocalDecks.Get(key);
                Check(catalog.Contains(deck) && !LocalDecks.TestLoaners.Contains(deck)
                    && !LocalDecks.TestOpponents.Contains(deck) && !LocalDecks.NpcDecks.Contains(deck),
                    "player starter stays outside historical, test and NPC catalogs: " + key);
            }
        }

        string root = Path.Combine(Path.GetTempPath(), "player-defaults-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "profile.xml");
            using (var fresh = new LocalProfile(path)) { fresh.GrantWelcomeIfNew(); fresh.SelectCardBack(0); }
            var serializer = new XmlSerializer(typeof(LocalProfileData));
            LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
            data.GoldBalance = 100000;
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            using (var profile = new LocalProfile(path))
            {
                profile.Purchase("deck-manastorm-mage", 1, profile.PurchasePrice("deck-manastorm-mage"), "bought");
                profile.CreateDraft("Personal deck", "HERO_03");
                for (int i = 0; i < retiredKeys.Length; i++)
                {
                    foreach (string key in new[] { legacyNames[i], retiredKeys[i] })
                    {
                        var match = profile.Begin(key, "showcase-ai-hunter-face", 42 + profile.History.Count);
                        Check(match.DeckHero == expectedHeroes[i], "historical alias and player ID resolve for engine match: " + key);
                        profile.Settle(match.Id, "Won", 12, false);
                        profile.AcknowledgeRewardPresentation(match.Id);
                    }
                }
                var core = profile.Begin(expectedKeys[10], "showcase-ai-hunter-face", 49);
                Check(core.DeckHero == "HERO_11", "Core starter resolves for an engine match");
                profile.Settle(core.Id, "Won", 12, false);
                profile.AcknowledgeRewardPresentation(core.Id);
                // A pending match from an affected historical showcase must survive the new tray.
                profile.Begin(legacyKeys[2], "showcase-ai-hunter-face", 50);
            }
            byte[] before = File.ReadAllBytes(path);
            using (var reopened = new LocalProfile(path))
            {
                var own = reopened.OwnedCards.Select(card => (card.Id, card.Premium, card.Count)).ToArray();
                var keys = NativeUIProjection.ProjectedLoanerKeys(reopened.CustomDecks.Count, "showcase");
                var ranks = NativeUIProjection.ExpectedNativeSortOrders(reopened.CustomDecks.Select(deck =>
                    new KeyValuePair<long, long>(deck.Id, deck.SortOrder)), keys);
                Check(ranks.Count == reopened.CustomDecks.Count + 11 && reopened.CustomDecks.Count == 2
                    && reopened.Purchases.Count == 1 && reopened.History.Count == 7
                    && reopened.History.Select(match => match.Deck).SequenceEqual(legacyNames.Zip(retiredKeys, (name, id) => new[] { name, id })
                        .SelectMany(row => row).Concat(new[] { expectedKeys[10] }))
                    && reopened.Pending?.Deck == legacyKeys[2]
                    && expectedHeroes.Concat(new[] { "HERO_11" }).All(hero => reopened.ClassXp[hero] > 0),
                    "Core starters keep retired aliases and IDs, earned progress, bought/personal decks and pending match");
                Check(reopened.OwnedCards.Select(card => (card.Id, card.Premium, card.Count)).SequenceEqual(own)
                    && File.ReadAllBytes(path).SequenceEqual(before), "projection never changes save bytes or ownership");
                foreach (string key in keys)
                {
                    var deck = LocalDecks.Get(key);
                    Check(deck.CardIds.Any(id => reopened.OwnedCount(id) == 0), "loaner access does not grant its cards: " + key);
                    Check(!NativeUIProjection.IsPersonalId(NativeUIProjection.DeckId(key)), "default keeps immutable loaner identity");
                }
            }
            Check(File.ReadAllBytes(path).SequenceEqual(before), "default selection needs no save migration");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("PASS player defaults: eleven Core starter loaners; retired loaners resolve; durable ownership and pending match preserved");
#endif
    }
}

using System.Xml.Serialization;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class MasteryChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("Mastery: " + message); }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (Exception error) when (error is InvalidOperationException || error is IOException) { return; }
        throw new Exception("Expected rejection: " + message);
    }
    private static LocalProfileData Read(string path) { using var stream = File.OpenRead(path); return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(stream); }
    private static void Write(string path, LocalProfileData data) { using var stream = File.Create(path); new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, data); }
    private static LocalAchievementDeckCard Card(string id)
    {
        var c = Cards.FromId(id);
        return new LocalAchievementDeckCard { BaseCardId = id, Copies = 2, CardType = c.Type.ToString(), CardClass = c.Class.ToString(),
            BaseCost = c.Cost, Rarity = c.Rarity.ToString(), CardSet = c.Set.ToString() };
    }
    private static LocalMatchRecord Win(params string[] ids) => new LocalMatchRecord { Id = Guid.NewGuid().ToString(), Result = "Won", Turns = 4,
        AchievementContext = new LocalAchievementPendingContext { EligibleWorldMatch = true, PersonalDeck = true, MainDeck = ids.Select(Card).ToList() } };
    private static LocalProfileData History(int count, params string[] ids) => new LocalProfileData { Matches = Enumerable.Range(0, count).Select(_ => Win(ids)).ToList() };
    private static int Owned(LocalProfileData data, string id, int premium) => data.PremiumCollection.SingleOrDefault(row => row.Id == id && row.Premium == premium)?.Count ?? 0;
    private static void Own(LocalProfileData data, string id, int premium, int count, bool progression = true)
    {
        data.PremiumCollection.Add(new LocalCardCount { Id = id, Premium = premium, Count = count, Acquired = count });
        if (progression) data.ClassRewardGrants.Add(new LocalClassRewardGrant { CardId = id, Premium = premium, Copies = count });
    }

    internal static void Run()
    {
        using var cosmetics = new CosmeticsFixture();
        PaceThresholds(); Thresholds(); Eligibility(); Ownership(); Decks(); Results(); Validation();
        string root = Path.Combine(Path.GetTempPath(), "hs-mastery-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try { PremiumRewardChecks.Run(root); MixedPace(root); PacePersistence(root); FeatsPool(root); Persistence(root); Settlement(root); MetaSwitch(root); Achievements(root); CraftUpgrade(root); } finally { Directory.Delete(root, true); }
        Console.WriteLine("PASS card mastery: thresholds, exclusions, ownership/next tiers, deck upgrades, results, receipts, abandon, meta catch-up, class reward collision, Core achievements, replay/reload, atomic failures, legacy bytes");
    }

    private static void FeatsPool(string root)
    {
        using var profile = new LocalProfile(Path.Combine(root, "feats-pool.xml"));
        var data = profile.FeatsSnapshot();
        data.Collection.Clear(); data.PremiumCollection.Clear();
        var pool = LocalCardCatalog.Supported.Concat(LocalShop.StandardCollectionCards)
            .Where(card => card.Collectible && LocalCardCatalog.InActiveMeta(card) && LocalCardMastery.Tiers(card.Id, 0).Any())
            .GroupBy(card => card.Id, StringComparer.Ordinal).Select(group => group.First()).ToArray();
        var view = LocalAchievementFeats.Create(data.Achievements, data);
        var tracks = view.Categories.Single(category => category.Id == "collection").Subcategories.Where(sub => sub.SectionName == "Card Mastery").ToArray();
        Check(LocalCardMastery.WinCounts(data.Matches).Count == 0 && tracks.Length == 12, "fresh mastery fixture/tabs");
        Check(tracks.Sum(sub => sub.Feats.Count) == pool.Length, "fresh mastery count differs from tiered active pool: "
            + tracks.Sum(sub => sub.Feats.Count) + "/" + pool.Length + "; missing "
            + string.Join(",", pool.Where(card => view.Find("mastery:" + card.Id) == null).Select(card => card.Id + ":" + card.Class)));
        for (int i = 0; i <= LocalCoreProgression.Heroes.Length; i++) {
            string cardClass = i == LocalCoreProgression.Heroes.Length ? "NEUTRAL" : Cards.FromId(LocalCoreProgression.Heroes[i]).Class.ToString();
            var rows = tracks.Single(sub => sub.Id == "mastery:" + i).Feats;
            var expected = pool.Where(card => card.Class.ToString() == cardClass || card.Class == CardClass.INVALID
                && i < LocalCoreProgression.Heroes.Length && LocalDecks.IsAllowedInDeck(card, Cards.FromId(LocalCoreProgression.Heroes[i]).Class)
                && !LocalCoreProgression.Heroes.Take(i).Any(hero => LocalDecks.IsAllowedInDeck(card, Cards.FromId(hero).Class)))
                .Select(card => card.Id).OrderBy(id => id, StringComparer.Ordinal);
            Check(rows.Count > 0 && rows.Select(row => row.Cards.Single().CardId).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(expected),
                "fresh mastery missing unowned cards " + cardClass);
            Check(rows.All(row => row.Progress == 0 && !row.Earned && row.Generated && row.Points == 0
                && row.Quota == LocalCardMastery.Tiers(row.Cards.Single().CardId, 0).First().RequiredWins
                && row.NativeId == LocalAchievementFeats.MasteryIdBase + Cards.FromId(row.Cards.Single().CardId).AssetId * 4 + 1),
                "fresh mastery progress/threshold/id " + cardClass);
            Console.WriteLine("MASTERY " + cardClass + ": " + rows.Count);
        }
        Check(view.All.Select(row => row.NativeId).Distinct().Count() == view.All.Count
            && view.All.All(row => LocalAchievementFeats.IsNativeAchievementId(row.NativeId)) && view.RecentlyCompleted.Count == 0,
            "fresh mastery ids/ranges/notifications");
        data.Matches = History(150, "CORE_CS2_029").Matches;
        LocalAchievementFeats.Create(data.Achievements, data); // Warm the projection before timing it.
        var timer = System.Diagnostics.Stopwatch.StartNew();
        view = LocalAchievementFeats.Create(data.Achievements, data);
        timer.Stop();
        Console.WriteLine($"PERF Feats full mastery pool ({pool.Length} cards, 150 matches): {timer.Elapsed.TotalMilliseconds:F2} ms (budget 500 ms)");
        Check(timer.Elapsed.TotalMilliseconds < 500 && view.All.Count(row => row.Id.StartsWith("mastery:", StringComparison.Ordinal)) == pool.Length,
            "full-pool Feats exceeds performance budget or lost rows");
    }

    private static void PaceThresholds()
    {
        var requirements = new[] {
            (GrindTiers.Relaxed2xId, new[] { 4, 4, 5, 6, 8, 15, 25 }),
            (GrindTiers.Standard2xId, new[] { 6, 6, 8, 9, 12, 23, 38 }),
            (GrindTiers.GrinderId, new[] { 8, 8, 10, 12, 16, 30, 50 }),
            (GrindTiers.RetailId, new[] { 12, 12, 15, 18, 24, 45, 75 })
        };
        var rarities = new[] { Rarity.FREE, Rarity.COMMON, Rarity.RARE, Rarity.EPIC, Rarity.LEGENDARY };
        foreach (var pace in requirements)
            for (int i = 0; i < pace.Item2.Length; i++) {
                string id = i < rarities.Length ? Cards.All.First(c => c.Collectible && c.Rarity == rarities[i]).Id : "CATA_213";
                int premium = i < rarities.Length ? 1 : i == 5 ? 3 : 2;
                int needed = pace.Item2[i];
                var data = History(needed - 1, id);
                foreach (var match in data.Matches) match.GrindTier = pace.Item1;
                LocalCardMastery.Grant(data, null);
                Check(!data.MasteryGrants.Any(g => g.CardId == id && g.Premium == premium), "pace unlock early: " + pace.Item1 + ":" + i);
                var final = Win(id, id); final.GrindTier = pace.Item1;
                LocalCardMastery.Grant(data, final);
                Check(data.MasteryGrants.Single(g => g.CardId == id && g.Premium == premium).Wins == needed,
                    "pace unlock missing: " + pace.Item1 + ":" + i);
            }
    }

    private static void MixedPace(string root)
    {
        const string id = "CORE_CS2_029";
        var data = History(5, id);
        for (int i = 0; i < 5; i++) data.Matches[i].GrindTier = i < 2 ? GrindTiers.Relaxed2xId : GrindTiers.Standard2xId;
        var before = LocalCardMastery.PointCounts(data.Matches.Take(4));
        Check(before[id] == 20 && !LocalCardMastery.Grant(new LocalProfileData { Matches = data.Matches.Take(4).ToList() }, null), "mixed pace unlocked below 24 points");
        Check(LocalCardMastery.Grant(data, data.Matches.Last()), "mixed pace missing unlock");
        var grant = data.MasteryGrants.Single();
        Check(grant.Points == 24 && grant.Wins == 5 && grant.ThresholdVersion == 2, "mixed points/actual wins receipt");
        LocalCardMastery.Validate(data);
        foreach (var pace in GrindTiers.Selectable) {
            data.GrindTier = pace.Id;
            LocalCardMastery.Validate(data);
            Check(!LocalCardMastery.Grant(data, null), "pace switch regranted mastery");
        }
        var progressMatch = data.Matches[3];
        using var profile = new LocalProfile(Path.Combine(root, "mixed-projection.xml"));
        foreach (var row in new[] { (GrindTiers.Standard2x, 5, 6), (GrindTiers.Relaxed2x, 3, 4), (GrindTiers.Grinder, 6, 8), (GrindTiers.Retail, 10, 12) }) {
            var next = LocalCardMastery.Next(data.Matches.Take(4), progressMatch, Array.Empty<LocalCardCount>(), row.Item1).Single();
            Check(next.Wins == row.Item2 && next.RequiredWins == row.Item3, "mixed progress display " + row.Item1.Id);
            var receipt = LocalResultsReceipt.FromHistory(data.Matches.Take(4).ToArray(), progressMatch, masteryPace: row.Item1);
            Check(receipt.ProgressionText.Contains(row.Item2 + " / " + row.Item3 + " wins"), "results do not use current pace");
            data.GrindTier = row.Item1.Id;
            var projection = profile.FeatsSnapshot(); projection.GrindTier = data.GrindTier; projection.Matches = data.Matches.Take(4).ToList();
            var view = LocalAchievementFeats.Create(projection.Achievements, projection);
            var feat = view.Find("mastery:" + id);
            Check(feat.Progress == row.Item2 && feat.Quota == row.Item3 && view.Detail(feat).Contains(row.Item2 + " / " + row.Item3 + " wins"), "Feats do not use current pace");
        }
        foreach (string pace in new[] { null, "", GrindTiers.StandardId, GrindTiers.RelaxedId }) {
            var match = Win(id); match.GrindTier = pace;
            Check(LocalCardMastery.PointCounts(new[] { match })[id] == (pace == GrindTiers.RelaxedId ? 6 : 4), "legacy mastery points");
        }
    }

    private static void PacePersistence(string root)
    {
        string path = Path.Combine(root, "pace-history.xml");
        using (var p = new LocalProfile(path)) {
            for (int i = 0; i < 6; i++) { var m = p.Begin("mage", "priest", i); p.Settle(m.Id, "Won", 4, false); p.AcknowledgeRewardPresentation(m.Id); }
        }
        var data = Read(path);
        for (int i = 0; i < data.Matches.Count; i++) {
            var match = data.Matches[i];
            match.Deck = "custom:910000010"; match.NpcId = "npc-mastery"; match.NpcRating = 2500;
            match.AchievementContext = new LocalAchievementPendingContext { MatchId = match.Id, EligibleWorldMatch = true, PersonalDeck = true,
                StartingHero = match.DeckHero, StartingHeroClass = "MAGE", QueuedNpcId = match.NpcId, QueuedNpcRating = 2500, QueuedNpcBand = "Diamond",
                MainDeck = new() { Card("CORE_CS2_029"), Card("CS2_182") } };
            match.AchievementEvidenceFingerprint = new string('A', 64);
        }
        var historical = new LocalMasteryGrant { CardId = "CORE_CS2_029", Premium = 1, Copies = 2, Wins = 3, ThresholdVersion = 1,
            MatchId = data.Matches[2].Id };
        data.MasteryGrants.Add(historical);
        data.Matches[2].MasteryGrants = new() { LocalCardMastery.Clone(historical) };
        LocalProfile.AddPremium(data, historical.CardId, 1, 2);
        Write(path, data);
        using (var p = new LocalProfile(path)) {
            var loaded = Read(path);
            Check(loaded.MasteryGrants.Count == 2 && loaded.MasteryGrants.Single(g => g.CardId == historical.CardId).ThresholdVersion == 1
                && loaded.MasteryGrants.Single(g => g.CardId == "CS2_182").Points == 24, "v1 preservation or v2 catch-up");
            foreach (var pace in GrindTiers.Selectable) {
                p.SetGrindTier(pace.Id);
                LocalCardMastery.Validate(Read(path));
            }
        }
        byte[] stable = File.ReadAllBytes(path);
        using (var p = new LocalProfile(path)) {
            Check(p.GrindTier == GrindTiers.Retail && p.OwnedCount(historical.CardId, 1) == 2, "pace reload lost historical grant");
            Check(stable.SequenceEqual(File.ReadAllBytes(path)) && Read(path).MasteryGrants.Count == 2, "pace reload paid or rewrote grants");
        }
        data = Read(path);
        var current = data.MasteryGrants.Single(g => g.ThresholdVersion == 2);
        current.Points = 23; Reject(() => LocalCardMastery.Validate(data), "v2 grant below points threshold");
        current.Points = 24; current.ThresholdVersion = 3;
        Reject(() => LocalCardMastery.Validate(data), "future mastery version");
        current.ThresholdVersion = 2;
        data.Matches[2].MasteryGrants[0].Wins++;
        Reject(() => LocalCardMastery.Validate(data), "historical receipt still structurally checked");
    }

    private static void Thresholds()
    {
        foreach (var row in new[] { (Rarity.FREE, 6), (Rarity.COMMON, 6), (Rarity.RARE, 8), (Rarity.EPIC, 9), (Rarity.LEGENDARY, 12) }) {
            var card = Cards.All.First(c => c.Collectible && c.Rarity == row.Item1);
            var data = History(row.Item2 - 1, card.Id);
            Check(!LocalCardMastery.Grant(data, null) && data.MasteryGrants.Count == 0, "early Golden " + row.Item1);
            var final = Win(card.Id, card.Id);
            Check(LocalCardMastery.Grant(data, final), "missing Golden " + row.Item1);
            var grant = data.MasteryGrants.Single();
            Check(grant.Premium == 1 && grant.Wins == row.Item2 && grant.Copies == (row.Item1 == Rarity.LEGENDARY ? 1 : 2)
                && grant.MatchId == final.Id && grant.ThresholdVersion == 2 && grant.Points == row.Item2 * 4, "wrong Golden grant " + row.Item1);
            Check(LocalCardMastery.WinCounts(data.Matches.Append(final))[card.Id] == row.Item2, "copies counted as wins");
            Check(!LocalCardMastery.Grant(data, final) && final.MasteryGrants.Single().Copies == grant.Copies, "pure grant replay changed receipt");
        }
        foreach (var row in new[] { ("TLC_460", 3, 23), ("CATA_485", 3, 23), ("TLC_452", 2, 38) }) {
            var data = History(row.Item3 - 1, row.Item1); LocalCardMastery.Grant(data, null);
            Check(data.MasteryGrants.All(g => g.Premium != row.Item2), "early premium tier");
            LocalCardMastery.Grant(data, Win(row.Item1));
            int copies = row.Item1 == "CATA_485" ? 2 : 1;
            Check(data.MasteryGrants.Single(g => g.Premium == row.Item2).Copies == copies && Owned(data, row.Item1, row.Item2) == copies, "missing premium tier");
        }
        var incapable = History(35, "CORE_CS2_029"); LocalCardMastery.Grant(incapable, null);
        Check(incapable.MasteryGrants.Count == 1 && incapable.MasteryGrants[0].Premium == 1, "uncapable card got Signature/Diamond");
        var both = History(38, "CATA_213"); LocalCardMastery.Grant(both, null);
        Check(both.MasteryGrants.Select(g => g.Premium).SequenceEqual(new[] { 1, 3, 2 }), "premium quality order");
        Check(!LocalCardMastery.Grant(both, null), "history replay granted twice");
    }

    private static void Eligibility()
    {
        foreach (var change in new Action<LocalMatchRecord>[] {
            m => m.Result = "Lost", m => m.Result = "Tied", m => m.Conceded = true, m => m.Turns = 3, m => m.Turns = 0,
            m => m.AchievementContext.PersonalDeck = false, m => m.AchievementContext.EligibleWorldMatch = false,
            m => m.AchievementContext = null, m => m.Mode = "Test"
        }) {
            var match = Win("CORE_CS2_029"); change(match);
            Check(LocalCardMastery.WinCounts(new[] { match }).Count == 0, "excluded match counted");
            var data = new LocalProfileData { Matches = Enumerable.Repeat(match, 10).ToList() };
            Check(!LocalCardMastery.Grant(data, null), "excluded match granted mastery");
        }
        var casual = Win("CORE_CS2_029"); casual.Mode = "Casual";
        casual.AchievementContext.Sideboards.Add(Card("TLC_460"));
        var counts = LocalCardMastery.WinCounts(new[] { casual });
        Check(counts["CORE_CS2_029"] == 1 && !counts.ContainsKey("TLC_460"), "Casual/sideboard eligibility");
        var separate = LocalCardMastery.WinCounts(new[] { Win("CORE_CS2_029"), Win("CS2_029") });
        Check(separate.Count == 2 && separate.Values.All(w => w == 1), "Core/original tracks collapsed");
    }

    private static void Ownership()
    {
        foreach (var row in new[] { ("CORE_CS2_029", 1, 6, 1, 2), ("CORE_CS2_029", 1, 6, 3, 2),
            ("CORE_CS2_029", 1, 6, 2, 2), ("CATA_213", 3, 23, 2, 1), ("CATA_213", 2, 38, 2, 1) }) {
            var data = History(row.Item3, row.Item1); Own(data, row.Item1, row.Item4, row.Item5);
            Check(LocalCardMastery.Grant(data, null), "zero-copy claim not reported as change");
            Check(data.MasteryGrants.Single(g => g.Premium == row.Item2).Copies == 0, "owned same/better version granted extra copies");
            Check(!LocalCardMastery.Grant(data, null), "zero-copy claim replayed");
        }
        var partial = History(6, "CORE_CS2_029"); Own(partial, "CORE_CS2_029", 1, 1); LocalCardMastery.Grant(partial, null);
        Check(partial.MasteryGrants.Single().Copies == 1 && Owned(partial, "CORE_CS2_029", 1) == 2, "partial top-up");
        Check(LocalProfile.ProgressionGrantedCount(partial, "CORE_CS2_029", 1) == 2
            && LocalProfile.DisenchantablePremiumCount(partial, "CORE_CS2_029", 1) == 0, "progression top-up loses protection");
        foreach (int premium in new[] { 1, 3, 2 }) {
            var purchased = History(6, "CORE_CS2_029"); Own(purchased, "CORE_CS2_029", premium, 2, false);
            LocalCardMastery.Grant(purchased, null);
            Check(purchased.MasteryGrants.Single().Copies == 2, "sellable same/better playset reduces mastery");
        }
        var betterPartial = History(6, "CORE_CS2_029"); Own(betterPartial, "CORE_CS2_029", 3, 1); LocalCardMastery.Grant(betterPartial, null);
        Check(Owned(betterPartial, "CORE_CS2_029", 1) == 1, "better-version partial ownership ignored");
    }

    private static void Decks()
    {
        var data = History(6, "CORE_CS2_029");
        var deck = new LocalCustomDeck { CardEntries = new() { new LocalDeckCard { Id = "CORE_CS2_029" } },
            Sideboards = new() { new LocalSideboard { Source = "JAIL_397", CardEntries = new() { new LocalDeckCard { Id = "CORE_CS2_029" } } } } };
        data.CustomDecks.Add(deck);
        data.CustomDeck = new LocalCustomDeck { CardEntries = new() { new LocalDeckCard { Id = "CORE_CS2_029" } } };
        LocalCardMastery.Grant(data, null);
        Check(deck.CardEntries.Single().Premium == 1 && deck.Sideboards.Single().CardEntries.Single().Premium == 1
            && data.CustomDeck.CardEntries.Single().Premium == 1, "saved deck/sideboard switch");
        var both = History(38, "CATA_213");
        both.CustomDecks.Add(new LocalCustomDeck { CardEntries = new() { new LocalDeckCard { Id = "CATA_213" } } });
        LocalCardMastery.Grant(both, null);
        Check(both.CustomDecks[0].CardEntries[0].Premium == 2, "Diamond did not replace Signature");
        var limited = new LocalCustomDeck { CardEntries = new() { new LocalDeckCard { Id = "CORE_CS2_029" }, new LocalDeckCard { Id = "CORE_CS2_029" }, new LocalDeckCard { Id = "CORE_CS2_029", Premium = 2 } } };
        LocalCrafting.ReconcileUpgradeDeck(limited, "CORE_CS2_029", 0, 1, 3);
        Check(limited.CardEntries.Select(c => c.Premium).SequenceEqual(new[] { 3, 0, 2 }), "switch exceeds ownership or downgrades Diamond");
        var upgrade = new LocalCustomDeck { CardEntries = new() { new LocalDeckCard { Id = "CORE_CS2_029" }, new LocalDeckCard { Id = "CORE_CS2_029" } } };
        LocalCrafting.ReconcileUpgradeDeck(upgrade, "CORE_CS2_029", 1, 1);
        Check(upgrade.CardEntries.Select(c => c.Premium).SequenceEqual(new[] { 1, 0 }), "craft upgrade no longer leaves remaining Normal copy");
        LocalCrafting.ReconcileUpgradeDeck(upgrade, "CORE_CS2_029", 1, 1);
        Check(upgrade.CardEntries.Count(c => c.Premium == 1) == 1, "craft upgrade converted twice");
#if ENGINE_SOURCE
        var fabled = Cards.All.First(c => c.Collectible && SabberStoneCore.Actions.FabledRules.Bundle(c.Id).Count > 0);
        var bundle = SabberStoneCore.Actions.FabledRules.Bundle(fabled.Id);
        var fabledDeck = new LocalCustomDeck { CardEntries = new[] { fabled.Id }.Concat(bundle).Select(id => new LocalDeckCard { Id = id }).ToList() };
        LocalCrafting.ReconcileUpgradeDeck(fabledDeck, fabled.Id, 0, 1);
        Check(fabledDeck.CardEntries.All(c => c.Premium == 1), "craft Fabled companions no longer upgrade");
        LocalCrafting.ReconcileUpgradeDeck(fabledDeck, fabled.Id, 0, 1, 3);
        Check(fabledDeck.CardEntries.All(c => c.Premium == 3), "mastery Fabled companions did not upgrade");
#endif
    }

    private static void Results()
    {
        var history = History(5, "CORE_CS2_029", "EX1_298").Matches;
        var settled = Win("CORE_CS2_029", "EX1_298");
        var data = new LocalProfileData { Matches = history };
        LocalCardMastery.Grant(data, settled); history.Add(settled);
        var receipt = LocalResultsReceipt.FromHistory(history, settled, LocalProfile.ProgressionCardCounts(data));
        Check(receipt.ProgressionText.Contains("Card Mastery: Golden Fireball x2") && receipt.ProgressionText.Contains("Next: Golden Ragnaros the Firelord 6 / 12 wins"), "results missing grants/progress");
        settled.MasteryGrants[0].Copies = 99;
        Check(receipt.MasteryGrants[0].Copies == 2, "receipt aliases mastery grant");
        var closest = History(10, "EX1_298").Matches.Concat(History(20, "TLC_460").Matches).ToList();
        var played = Win("EX1_298", "TLC_460", "CORE_CS2_029"); played.Result = "Lost"; closest.Add(played);
        var next = LocalCardMastery.Next(closest, played, Array.Empty<LocalCardCount>());
        Check(next.Select(p => p.CardId).SequenceEqual(new[] { "TLC_460", "EX1_298" }) && next[0].Premium == 3 && next[0].Wins == 20
            && next[0].RequiredWins == 23 && next[1].Wins == 10 && next[1].RequiredWins == 12, "closeness/next tier ordering");
        var tied = Win("CAP_003", "CAP_000", "CORE_CS2_029");
        Check(LocalCardMastery.Next(new[] { tied }, tied, Array.Empty<LocalCardCount>()).Select(p => p.CardId).SequenceEqual(new[] { "CAP_000", "CAP_003" }), "progress tie not sorted by id");
        foreach (var row in new[] { (1, 3), (3, 2), (2, 0) }) {
            var owned = new LocalProfileData(); Own(owned, "CATA_213", row.Item1, 1);
            var match = Win("CATA_213");
            var progress = LocalResultsReceipt.FromHistory(new[] { match }, match, LocalProfile.ProgressionCardCounts(owned)).MasteryProgress;
            Check(row.Item2 == 0 ? progress.Count == 0 : progress.Single().Premium == row.Item2, "Next suggests an owned tier " + row.Item1);
        }
        var purchased = new LocalProfileData(); Own(purchased, "CATA_213", 2, 1, false);
        var purchaseMatch = Win("CATA_213");
        Check(LocalResultsReceipt.FromHistory(new[] { purchaseMatch }, purchaseMatch, LocalProfile.ProgressionCardCounts(purchased))
            .MasteryProgress.Single().Premium == 1, "sellable premium skips Next tier");
        var partial = new LocalProfileData(); Own(partial, "CATA_485", 3, 1);
        var partialMatch = Win("CATA_485");
        Check(LocalCardMastery.Next(new[] { partialMatch }, partialMatch, LocalProfile.ProgressionCardCounts(partial)).Single().Premium == 1, "Next skips partial playset");
    }

    private static void Validation()
    {
        var data = History(6, "CORE_CS2_029");
        LocalCardMastery.Grant(data, data.Matches.Last()); LocalCardMastery.Validate(data);
        var grant = data.MasteryGrants.Single(); string matchId = grant.MatchId;
        grant.MatchId = Guid.NewGuid().ToString();
        Reject(() => LocalCardMastery.Validate(data), "ledger references unknown match"); grant.MatchId = matchId;
        data.Pending = Win("CORE_CS2_029"); data.Pending.Result = null;
        data.Pending.MasteryGrants = new() { LocalCardMastery.Clone(grant) };
        Reject(() => LocalCardMastery.Validate(data), "pending carries mastery receipt"); data.Pending = null;
        data.Matches.Last().MasteryGrants.Single().MatchId = data.Matches.First().Id;
        Reject(() => LocalCardMastery.Validate(data), "receipt references different match");
    }

    private static void Persistence(string root)
    {
        string path = Path.Combine(root, "history.xml");
        using (var p = new LocalProfile(path)) {
            for (int i = 0; i < 6; i++) { var m = p.Begin("mage", "priest", i); p.Settle(m.Id, "Won", 4, false); p.AcknowledgeRewardPresentation(m.Id); }
        }
        using (var p = new LocalProfile(path)) {
            var prints = p.AchievementContextFor(new LocalDeck("Prints", "HERO_08", new[] { "CORE_CS2_029", "CS2_029" }), true, "npc-mastery", 2500, "Diamond");
            Check(prints.MainDeck.Select(c => c.BaseCardId).OrderBy(id => id).SequenceEqual(new[] { "CORE_CS2_029", "CS2_029" }), "new contexts collapse print identity");
        }
        byte[] older = File.ReadAllBytes(path);
        Check(!System.Text.Encoding.UTF8.GetString(older).Contains("MasteryGrants"), "empty ledger changed legacy XML");
        Write(path, Read(path));
        Check(older.SequenceEqual(File.ReadAllBytes(path)), "legacy save did not round-trip byte-identical");
        using (var p = new LocalProfile(path)) p.CommitChange(next => true);
        Check(older.SequenceEqual(File.ReadAllBytes(path)), "ordinary commit rewrote legacy save");
        var save = Read(path);
        foreach (var m in save.Matches) {
            m.Deck = "custom:910000010"; m.NpcId = "npc-mastery"; m.NpcRating = 2500;
            m.AchievementContext = new LocalAchievementPendingContext { MatchId = m.Id, EligibleWorldMatch = true, PersonalDeck = true,
                StartingHero = m.DeckHero, StartingHeroClass = "MAGE", QueuedNpcId = m.NpcId, QueuedNpcRating = 2500, QueuedNpcBand = "Diamond", MainDeck = new() { Card("CORE_CS2_029") } };
            m.AchievementEvidenceFingerprint = new string('A', 64);
        }
        Write(path, save);
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            byte[] before = File.ReadAllBytes(path);
            Reject(() => { using var p = new LocalProfile(path, point => { if (point == stage) throw new IOException("mastery reload injected"); }); }, stage);
            Check(before.SequenceEqual(File.ReadAllBytes(path)), "reload failure changed save");
        }
        using (var p = new LocalProfile(path)) Check(p.OwnedCount("CORE_CS2_029", 1) == 2 && p.DisenchantablePremiumCount("CORE_CS2_029", 1) == 0, "past wins reconciliation/protection");
        var reconciled = Read(path);
        Check(reconciled.MasteryGrants.Single().Copies == 2 && reconciled.Matches.All(m => m.MasteryGrants?.Count != 1), "load reconciliation rewrote historical receipts");
        byte[] stable = File.ReadAllBytes(path);
        using (var p = new LocalProfile(path)) { }
        Check(stable.SequenceEqual(File.ReadAllBytes(path)), "reload replay paid again");
        foreach (var corrupt in new Action<LocalProfileData>[] { d => d.MasteryGrants[0].Premium = 4, d => d.MasteryGrants[0].Copies = 3,
            d => d.MasteryGrants[0].Copies = -1, d => d.MasteryGrants[0].Wins = 0, d => d.MasteryGrants[0].ThresholdVersion = 0,
            d => d.MasteryGrants.Add(LocalCardMastery.Clone(d.MasteryGrants[0])) }) {
            var bad = Read(path); corrupt(bad); string invalid = Path.Combine(root, "corrupt.xml"); Write(invalid, bad);
            Reject(() => { using var p = new LocalProfile(invalid); }, "invalid mastery ledger");
        }
    }

    private static void CraftUpgrade(string root)
    {
        string path = Path.Combine(root, "craft-upgrade.xml");
        string id = "CATA_485";
        using var p = new LocalProfile(path);
        p.CommitChange(next => {
            next.DustBalance = 1000;
            next.Collection.Add(new LocalCardCount { Id = id, Count = 2, Acquired = 2 });
            return true;
        });
        var deck = p.CreateDraft("Upgrade", "HERO_08"); p.SaveDraft(deck.Id, deck.Name, deck.Hero, new[] { id, id }, 0);
        var upgraded = LocalCrafting.CommitUpgrade(p, id, 1, 2, 0);
        Check(upgraded.Ok && p.OwnedCount(id) == 1 && p.OwnedCount(id, 1) == 1
            && p.GetDraft(deck.Id).CardEntries.Select(c => c.Premium).SequenceEqual(new[] { 1, 0 }), "existing upgrade-crafting transaction/deck behavior");
    }

    private static void MetaSwitch(string root)
    {
        string path = Path.Combine(root, "meta.xml");
        using (var p = new LocalProfile(path))
            for (int i = 0; i < 23; i++) { var m = p.Begin("mage", "priest", i); p.Settle(m.Id, "Won", 4, false); p.AcknowledgeRewardPresentation(m.Id); }
        var save = Read(path);
        foreach (var match in save.Matches) {
            match.Deck = "custom:910000010"; match.NpcId = "npc-mastery"; match.NpcRating = 2500;
            match.AchievementContext = new LocalAchievementPendingContext { MatchId = match.Id, EligibleWorldMatch = true, PersonalDeck = true,
                StartingHero = match.DeckHero, StartingHeroClass = "MAGE", QueuedNpcId = match.NpcId, QueuedNpcRating = 2500, QueuedNpcBand = "Diamond",
                MainDeck = new() { Card("BE_009") } };
            match.AchievementEvidenceFingerprint = new string('A', 64);
        }
        Write(path, save);
        string previous = LocalClassRewards.ActiveMetaOverride;
        try {
            LocalClassRewards.ActiveMetaOverride = "violet-hold";
            using (var p = new LocalProfile(path)) { }
            Check(Read(path).MasteryGrants.All(g => g.Premium == 1 && g.MatchId == null), "inactive meta granted Signature");
            using (var p = new LocalProfile(path)) {
                var data = p.FeatsSnapshot();
                var row = LocalAchievementFeats.Create(data.Achievements, data).Find("mastery:BE_009");
                Check(row != null && row.Progress > 0, "inactive-meta mastery row with wins/grants hidden");
            }
            LocalClassRewards.ActiveMetaOverride = "black-empire";
            using (var p = new LocalProfile(path)) { }
            var caughtUp = Read(path);
            var signature = caughtUp.MasteryGrants.Single(g => g.CardId == "BE_009" && g.Premium == 3);
            Check(signature.Copies == LocalCrafting.Playset("BE_009") && signature.MatchId == null
                && Owned(caughtUp, "BE_009", 3) == signature.Copies && caughtUp.Matches.All(m => m.MasteryGrants == null || m.MasteryGrants.Count == 0),
                "meta catch-up has wrong provenance or rewrites receipts");
            byte[] bytes = File.ReadAllBytes(path);
            foreach (string meta in new[] { "black-empire", "violet-hold", "black-empire" }) {
                LocalClassRewards.ActiveMetaOverride = meta;
                using var p = new LocalProfile(path);
                Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "meta catch-up paid twice");
            }
        } finally { LocalClassRewards.ActiveMetaOverride = previous; }
    }

    private static void Achievements(string root)
    {
#if ENGINE_SOURCE
        using var profile = new LocalProfile(Path.Combine(root, "core-achievements.xml"));
        var game = new Game(new SabberStoneCore.Config.GameConfig { RandomSeed = 73, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame(); Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.ConcedeTask.Any(game.Player2)), "achievement fixture did not finish");
        LocalAchievementEvaluation Evaluate(LocalAchievementPendingContext context)
        {
            context.MatchId = "core-achievements";
            var evidence = new LocalAchievementMatchEvidence(context.MatchId, "Won", context.StartingHero, context.QueuedNpcId, context.QueuedNpcBand,
                false, true, false, LocalAchievementContextBinding.Create(context), new string('A', 64), true,
                game.Player1.Id, game.Player2.Id, game.Player1.Hero.Id, game.Player2.Hero.Id, game.HistoryFacts.Query.Events<HistoryFact>());
            return LocalAchievementEvaluator.Evaluate(context, evidence, new LocalAchievementCareer(), Array.Empty<string>());
        }
        var singleton = profile.AchievementContextFor(new LocalDeck("Core singleton", "HERO_08", new[] { "CORE_CS2_029" }), true, "npc-mastery", 2500, "Diamond");
        Check(Evaluate(singleton).Earned.Any(row => row.Id == "no-copies-no-problem"), "singleton positive control");
        var duplicate = profile.AchievementContextFor(new LocalDeck("Core duplicate", "HERO_08", new[] { "CORE_CS2_029", "CS2_029" }), true, "npc-mastery", 2500, "Diamond");
        Check(duplicate.MainDeck.All(row => row.Copies == 1) && Evaluate(duplicate).Earned.All(row => row.Id != "no-copies-no-problem"),
            "Core/original shared identity passed singleton");
        var ids = LocalShop.StandardCollectionCards.Where(card => LocalDecks.IsAllowedInDeck(card, CardClass.MAGE)
            && LocalCardCatalog.CopyGroup(card.Id) != LocalCardCatalog.CopyGroup("CORE_CS2_029"))
            .GroupBy(card => LocalCardCatalog.CopyGroup(card.Id), StringComparer.Ordinal).Take(9).Select(group => group.First().Id).Append("CORE_CS2_029").ToArray();
        profile.CommitChange(next => {
            next.Achievements.Career.CraftedBaseCardIds = ids.Select(LocalCardCatalog.CopyGroup).OrderBy(id => id, StringComparer.Ordinal).ToList();
            foreach (string id in ids) {
                var row = next.Collection.SingleOrDefault(card => card.Id == id);
                if (row == null) { row = new LocalCardCount { Id = id }; next.Collection.Add(row); }
                row.Count = row.Acquired = Math.Max(1, row.Count);
            }
            return true;
        });
        var crafted = profile.AchievementContextFor(new LocalDeck("Core crafted", "HERO_08", ids), true, "npc-mastery", 2500, "Diamond");
        Check(crafted.CraftedAndOwnedBaseCardIds.Count == 10 && Evaluate(crafted).Earned.Any(row => row.Id == "made-to-measure"), "Core print missed crafted-deck count");
        crafted.CraftedAndOwnedBaseCardIds.Remove(LocalCardCatalog.CopyGroup("CORE_CS2_029"));
        Check(Evaluate(crafted).Earned.All(row => row.Id != "made-to-measure"), "crafted-deck negative control");
#endif
    }

    private static void Settlement(string root)
    {
        string path = Path.Combine(root, "settle.xml");
        var collision = LocalClassRewards.ActiveRewards.First(row => row.Hero == "HERO_08" && row.Premium == 1 && row.Level >= 15
            && LocalCardMastery.Tiers(row.CardId, 0).Any());
        int threshold = LocalCardMastery.Tiers(collision.CardId, 0).First().RequiredWins;
        string cardId;
        using (var p = new LocalProfile(path)) {
            var cards = LocalShop.StandardCollectionCards.Where(c => c.Rarity == Rarity.COMMON && (c.Class == CardClass.MAGE || c.Class == CardClass.NEUTRAL)
                && c.Id != collision.CardId && !LocalCardCatalog.IsCore(c.Id) && c.Type != CardType.HERO && LocalDecks.IsAllowedInDeck(c, CardClass.MAGE))
                .GroupBy(c => c.Id).Select(group => group.First()).OrderBy(c => c.Id, StringComparer.Ordinal).Take(14).Select(c => c.Id).Append(collision.CardId).ToArray();
            cardId = cards[0];
            p.CommitChange(next => {
                foreach (var id in cards) {
                    var c = next.Collection.SingleOrDefault(row => row.Id == id);
                    if (c == null) { c = new LocalCardCount { Id = id }; next.Collection.Add(c); }
                    c.Count = c.Acquired = 2;
                }
                return true;
            });
            var deck = p.CreateDraft("Mastery", "HERO_08");
            var ids = cards.SelectMany(id => new[] { id, id }).ToArray(); p.SaveDraft(deck.Id, deck.Name, deck.Hero, ids, 0);
            var played = new LocalDeck(deck.Name, deck.Hero, ids);
            for (int i = 0; i < threshold; i++) {
                if (i == threshold - 1) p.CommitChange(next => { next.ClassXp.Single(row => row.Hero == "HERO_08").Xp = LocalCoreProgression.LevelThresholds[collision.Level - 1] - LocalCoreProgression.Xp("Won", false); return true; });
                var context = p.AchievementContextFor(played, true, "npc-mastery", 2500, "Diamond");
                var pending = p.BeginLaunch("custom:" + deck.Id, "priest", i, "npc-mastery", "priest", 2500, context, _ => { });
                var frozen = pending.AchievementContext;
                var evidence = new LocalAchievementMatchEvidence(pending.Id, "Won", frozen.StartingHero, frozen.QueuedNpcId, frozen.QueuedNpcBand,
                    false, false, false, LocalAchievementContextBinding.Create(frozen), new string('A', 64), true, 1, 2, 3, 4
#if ENGINE_SOURCE
                    , null
#endif
                    ).Invalidated("No engine facts in mastery fixture.");
                if (i == 5 || i == threshold - 1) foreach (string stage in new[] { "write", "flush", "replace" }) {
                    byte[] bytes = File.ReadAllBytes(path); int before = p.History.Count;
                    p.CommitFailure = point => { if (point == stage) throw new IOException("mastery settlement injected"); };
                    Reject(() => p.Settle(pending.Id, "Won", 4, false, null, null, null, evidence), stage);
                    Check(bytes.SequenceEqual(File.ReadAllBytes(path)) && p.History.Count == before && p.Pending.Id == pending.Id
                        && p.OwnedCount(cardId, 1) == (i == 5 ? 0 : 2) && p.OwnedCount(collision.CardId, 1) == 0
                        && p.GetDraft(deck.Id).CardEntries.All(c => c.Premium == (c.Id == collision.CardId || i == 5 ? 0 : 1)), "failed settlement changed file/memory/decks i=" + i + " collision=" + collision.CardId + " owned=" + p.OwnedCount(collision.CardId, 1) + " first=" + p.OwnedCount(cardId, 1));
                }
                p.CommitFailure = null;
                var match = p.Settle(pending.Id, "Won", 4, false, null, null, null, evidence);
                if (i == 5) {
                    Check(match.MasteryGrants.Count == 14 && match.MasteryGrants.All(g => g.Copies == 2 && g.Wins == 6)
                        && p.GetDraft(deck.Id).CardEntries.Where(c => c.Id != collision.CardId).All(c => c.Premium == 1), "settlement grant or automatic deck switch: "
                        + string.Join(",", match.MasteryGrants.Select(g => g.CardId + ":" + g.Copies + ":" + g.Wins)));
                    Check(!LocalCrafting.CommitPremiumBatch(p, cardId, 1, CraftDirection.Disenchant, 1, 2).Ok
                        && p.ProgressionGrantedCount(cardId, 1) == 2, "mastery copies disenchantable");
                    match.MasteryGrants[0].Copies = 99;
                    byte[] bytes = File.ReadAllBytes(path);
                    Check(p.Settle(pending.Id, "Won", 4, false, null, null, null, evidence).MasteryGrants.All(g => g.Copies == 2)
                        && bytes.SequenceEqual(File.ReadAllBytes(path)), "settlement retry paid again or aliased receipt");
                }
                if (i == threshold - 1) {
                    Check(match.MasteryGrants.Single().CardId == collision.CardId && match.MasteryGrants[0].Copies == 0
                        && match.ClassRewardGrants.Single(g => g.CardId == collision.CardId).Copies == 2
                        && p.OwnedCount(collision.CardId, 1) == 2 && p.ProgressionGrantedCount(collision.CardId, 1) == 2
                        && p.DisenchantablePremiumCount(collision.CardId, 1) == 0, "class/mastery collision overpaid or lost protection");
                    byte[] bytes = File.ReadAllBytes(path);
                    Check(p.Settle(pending.Id, "Won", 4, false, null, null, null, evidence).MasteryGrants.Single().Copies == 0
                        && bytes.SequenceEqual(File.ReadAllBytes(path)), "zero-copy collision replay paid again");
                }
                p.AcknowledgeRewardPresentation(match.Id);
            }
        }
        var saved = Read(path);
        Check(saved.MasteryGrants.Count == 15 && saved.Matches.Last().MasteryGrants.Count == 1, "ledger/receipt not persisted");
        byte[] after = File.ReadAllBytes(path);
        using (var p = new LocalProfile(path)) Check(p.OwnedCount(cardId, 1) == 2, "restart lost mastery");
        Check(after.SequenceEqual(File.ReadAllBytes(path)), "restart duplicated settlement grants");
        string abandon = Path.Combine(root, "abandon.xml"); Write(abandon, saved);
        using (var p = new LocalProfile(abandon)) {
            var deck = p.CustomDecks.Single();
            var context = p.AchievementContextFor(new LocalDeck(deck.Name, deck.Hero, deck.Cards.ToArray()), true, "npc-mastery", 2500, "Diamond");
            var pending = p.BeginLaunch("custom:" + deck.Id, "priest", 4, "npc-mastery", "priest", 2500, context, _ => { });
            p.MarkStarted(pending.Id);
        }
        using (var p = new LocalProfile(abandon)) {
            p.RecoverInterrupted();
            var recovered = p.History.Last();
            Check(p.Pending == null && recovered.Result == "Abandoned" && recovered.AchievementContext.EligibleWorldMatch
                && (recovered.MasteryGrants == null || recovered.MasteryGrants.Count == 0), "abandon created mastery receipt");
            LocalCardMastery.Validate(Read(abandon));
        }
        var badReceipt = Read(path); badReceipt.Matches.Last().MasteryGrants[0].Copies = 1; Write(path, badReceipt);
        Reject(() => { using var p = new LocalProfile(path); }, "receipt differs from ledger");
    }
}

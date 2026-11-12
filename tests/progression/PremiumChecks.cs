using System.Xml.Linq;
using System.Xml.Serialization;

internal static class PremiumChecks
{
    static readonly XmlSerializer Serializer = new(typeof(LocalProfileData));
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Reject(Action action) {
        try { action(); } catch (InvalidOperationException) { return; } catch (IOException) { return; } catch (UnauthorizedAccessException) { return; }
        throw new Exception("Expected premium validation or atomic failure");
    }
    static LocalProfileData Read(string path) { using var stream = File.OpenRead(path); return (LocalProfileData)Serializer.Deserialize(stream); }
    static void Write(string path, LocalProfileData data) { using var stream = File.Create(path); Serializer.Serialize(stream, data); }
    static string Fresh(string root, string name) {
        string path = Path.Combine(root, name + ".xml");
        using var p = new LocalProfile(path); p.CreateDraft("Premium", "HERO_08"); return path;
    }
    static LocalMatchRecord Win(string hero = "HERO_08") {
        // A V4 rolled win without a ladder receipt (a receipt-less match may not carry dust, so pick a seed whose roll pays none);
        // the profile recomputes the reward from the match id and seed on load.
        string id = Guid.NewGuid().ToString(); var tier = GrindTiers.Find(null); int seed = 1; LocalReward reward;
        while ((reward = LocalEconomy.RewardV4("Won", 10, false, LocalEconomy.RewardRollSeed(id, seed))).Dust != 0) seed++;
        return new LocalMatchRecord { Id = id, Deck = "mage", DeckHero = hero, Opponent = "priest",
            Seed = seed, StartedUtc = "2026-10-25T00:00:00Z", Result = "Won", Turns = 10,
            RewardVersion = 4, Gold = tier.Gold(reward.Gold), BonusGold = reward.BonusGold, PackReward = reward.PackReward,
            RewardFacts = new LocalRewardFacts() };
    }
    internal static void GrantOnlyReplacementChecks(string root)
    {
        var grantOnly = LocalWelcomeGift.Cards.Select(card => card.CardId).ToHashSet(StringComparer.Ordinal);
        // The gift fills Normal/Golden; Signature event rewards stay class rewards (owner 2026-10-29).
        Check(LocalClassRewards.Rewards.Where(row => row.Meta == "violet-hold" && grantOnly.Contains(row.CardId))
            .Select(row => (row.Hero, row.Level, row.CardId, row.Premium)).OrderBy(row => row.Hero).SequenceEqual(new[] {
                ("HERO_01", 47, "CATA_EVENT_002", 3), ("HERO_04", 53, "TIME_EVENT_998", 3), ("HERO_11", 30, "TLC_EVENT_402", 3) }),
            "Violet Hold class rewards keep only the three Signature event rows");
        var slots = new[] { ("HERO_04", 53), ("HERO_01", 47), ("HERO_06", 37), ("HERO_03", 33),
            ("HERO_02", 29), ("HERO_07", 34), ("HERO_10", 34), ("HERO_11", 30) };
        try {
            LocalClassRewards.ActiveMetaOverride = "violet-hold";
            foreach (var (hero, level) in slots) {
                string slot = Path.Combine(root, "class-replacement-" + hero); Directory.CreateDirectory(slot);
                string path = Path.Combine(slot, "profile.xml");
                using (var profile = new LocalProfile(path)) Check(profile.GrantWelcomeIfNew(), "replacement fixture has welcome playsets");
                var setup = Read(path);
                setup.ClassXp.Single(row => row.Hero == hero).Xp = LocalCoreProgression.LevelThresholds[level - 1];
                Write(path, setup);
                var reward = LocalClassRewards.ActiveRewards.Single(row => row.Hero == hero && row.Level == level);
                byte[] before = File.ReadAllBytes(path);
                foreach (string stage in new[] { "write", "flush", "replace" }) {
                    Reject(() => { using var profile = new LocalProfile(path, point => { if (point == stage) throw new IOException("injected"); }); });
                    var failed = Read(path);
                    Check(before.SequenceEqual(File.ReadAllBytes(path)) && failed.ClassRewardGrants.Count == 0
                        && !failed.PremiumCollection.Any(row => row.Id == reward.CardId && row.Premium == reward.Premium), "replacement failure preserves receipt and ownership: " + hero + "/" + stage);
                }
                using (var profile = new LocalProfile(path)) {
                    var receipt = profile.ClassRewardGrants.Single(row => row.Hero == hero && row.Level == level);
                    Check(receipt.CardId == reward.CardId && receipt.Premium == reward.Premium && receipt.Copies == reward.Copies
                        && profile.OwnedCount(reward.CardId, reward.Premium) == reward.Copies, "replacement recovered with exact receipt: " + hero);
                    Check(LocalWelcomeGift.Cards.All(card => profile.OwnedCount(card.CardId, card.Premium) == (LocalProfile.KnownLedgerCard(card.CardId) ? card.Count : 0)), "welcome playsets preserved");
                }
                var saved = Read(path);
                Check(saved.ClassRewardGrants.Count(row => row.Hero == hero && row.Level == level) == 1
                    && saved.PremiumCollection.Single(row => row.Id == reward.CardId && row.Premium == reward.Premium).Count == reward.Copies,
                    "replacement receipt and cards persisted together: " + hero);
                byte[] committed = File.ReadAllBytes(path);
                using (var profile = new LocalProfile(path)) Check(profile.OwnedCount(reward.CardId, reward.Premium) == reward.Copies, "restart does not duplicate replacement");
                Check(committed.SequenceEqual(File.ReadAllBytes(path)), "restart does not rewrite replacement");
            }
        } finally { LocalClassRewards.ActiveMetaOverride = null; }
        Console.WriteLine("PASS eight class reward slots (five Golden replacements, three Signature event rows beside welcome playsets): atomic failures, persistence, restart exactly once");
    }
    internal static void Run(string root)
    {
        using var cosmetics = new CosmeticsFixture();
        string settle = Fresh(root, "premium-settle");
        var setup = Read(settle); setup.ClassXp.Single(x => x.Hero == "HERO_08").Xp = 1200; Write(settle, setup);
        string matchId;
        using (var p = new LocalProfile(settle)) {
            matchId = p.Begin("mage", "priest", 1).Id;
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                byte[] bytes = File.ReadAllBytes(settle);
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected"); };
                Reject(() => p.Settle(matchId, "Won", 10, false));
                Check(bytes.SequenceEqual(File.ReadAllBytes(settle)) && p.ClassXp["HERO_08"] == 1200
                    && p.ClassRewardGrants.Count == 0 && p.OwnedCount("CORE_SW_108", 1) == 0, "settlement leaked premium reward");
            }
            p.CommitFailure = null;
            var match = p.Settle(matchId, "Won", 10, false);
            Check(match.ClassRewardGrants.Single().CardId == "CORE_SW_108" && match.ClassRewardGrants[0].Copies == 2
                && match.ClassRewardGrants[0].Premium == 1 && match.PortraitGrants.Count == 0, "missing class reward receipt");
            string summary = p.ProgressionSummary(match);
            Check(summary.Contains("Class rewards: Golden ") && summary.Contains(" x2") && !summary.Contains("CORE_SW_108")
                && summary.Contains(SabberStoneCore.Model.Cards.FromId("CORE_SW_108").Name), "summary names the class reward card");
            int normal = p.OwnedCount("CORE_SW_108");
            Check(normal == p.Collection["CORE_SW_108"] && p.TotalOwnedCount("CORE_SW_108") == normal + 2, "ownership API mixes premiums");
            match.ClassRewardGrants[0].Copies = 99;
            byte[] bytesAfter = File.ReadAllBytes(settle);
            Check(p.Settle(matchId, "Won", 10, false).ClassRewardGrants[0].Copies == 2
                && bytesAfter.SequenceEqual(File.ReadAllBytes(settle)), "settlement replay or receipt alias");
            p.AcknowledgeRewardPresentation(matchId); p.AcknowledgeRewardPresentation(matchId);
            Check(p.OwnedCount("CORE_SW_108", 1) == 2, "ack granted twice");
            long id = p.CustomDecks[0].Id;
            p.SavePremiumDraft(id, "Mixed premium", "HERO_08", new[] {
                new LocalDeckCard { Id = "CORE_SW_108" }, new LocalDeckCard { Id = "CORE_SW_108", Premium = 1 } }, 0);
            Reject(() => p.SavePremiumDraft(id, "Unowned", "HERO_08", new[] { new LocalDeckCard { Id = "CORE_SW_108", Premium = 3 } }, 0));
            Reject(() => p.SavePremiumDraft(id, "Three copies", "HERO_08", new[] {
                new LocalDeckCard { Id = "CORE_SW_108" }, new LocalDeckCard { Id = "CORE_SW_108", Premium = 1 },
                new LocalDeckCard { Id = "CORE_SW_108", Premium = 1 } }, 0));
        }
        using (var p = new LocalProfile(settle)) {
            var deck = p.CustomDecks.Single();
            Check(deck.CardEntries.Select(card => card.Premium).SequenceEqual(new[] { 0, 1 }), "premium deck failed round trip");
            deck.CardEntries[1].Premium = 3;
            Check(p.GetDraft(deck.Id).CardEntries[1].Premium == 1, "deck aliases profile state");
            p.ReorderDrafts(new[] { deck.Id });
            Check(p.GetDraft(deck.Id).CardEntries[1].Premium == 1, "snapshot lost premium");
            p.SaveDraft(deck.Id, "Normal", deck.Hero, new[] { "CS2_029" }, 0);
            Check(p.GetDraft(deck.Id).CardEntries.Single().Premium == 0, "legacy SaveDraft changed semantics");
        }
        var good = Read(settle);
        foreach (var corrupt in new Action<LocalProfileData>[] {
            p => p.PremiumCollection[0].Premium = 4,
            p => p.PremiumCollection.Add(LocalProfile.Clone(p.PremiumCollection[0])),
            p => p.ClassRewardGrants[0].Level = 10,
            p => p.Matches[0].ClassRewardGrants[0].Copies = 3,
            p => p.Matches[0].PortraitGrants = null,
            p => p.CustomDecks[0].CardEntries[0].Premium = -1,
            p => p.Pending = new LocalMatchRecord { ClassRewardGrants = new() }
        }) {
            var bad = LocalProfile.Clone(good); corrupt(bad); string path = Path.Combine(root, "bad-premium.xml"); Write(path, bad);
            Reject(() => { using var p = new LocalProfile(path); });
        }

        // Missed/unknown historical grants remain owned even after schedule removal or a higher threshold.
        string reconcile = Fresh(root, "premium-reconcile");
        setup = Read(reconcile); setup.ClassXp.Single(x => x.Hero == "HERO_08").Xp = 1930;
        setup.ClassRewardGrants.Add(new LocalClassRewardGrant { Hero = "HERO_08", Level = 12, CardId = "UNKNOWN_OLD", Premium = 3, Copies = 2, ScheduleVersion = 99 });
        setup.PremiumCollection.Add(new LocalCardCount { Id = "UNKNOWN_OLD", Premium = 3, Count = 2, Acquired = 2 });
        Write(reconcile, setup);
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            byte[] bytes = File.ReadAllBytes(reconcile);
            Reject(() => { using var p = new LocalProfile(reconcile, point => { if (point == stage) throw new IOException("injected"); }); });
            Check(bytes.SequenceEqual(File.ReadAllBytes(reconcile)), "reconciliation failure changed profile");
        }
        using (var p = new LocalProfile(reconcile))
            Check(p.ClassRewardGrants.Count == 3 && p.OwnedCount("UNKNOWN_OLD", 3) == 0, "reconciliation or unknown exposure");
        Check(Read(reconcile).PremiumCollection.Any(row => row.Id == "UNKNOWN_OLD" && row.Count == 2), "unknown grant lost");
        int index = Array.FindIndex(LocalClassRewards.Rewards, row => row.Meta == LocalClassRewards.ActiveMeta && row.Hero == "HERO_08" && row.Level == 11);
        var original = LocalClassRewards.Rewards[index];
        LocalClassRewards.Rewards[index] = new ClassReward(original.Meta, original.Hero, 60, original.CardId, original.Name, original.Premium, original.Copies);
        try {
            using var p = new LocalProfile(reconcile);
            Check(p.OwnedCount("CORE_SW_108", 1) == 2 && p.ClassRewardGrants.Count == 3, "schedule revoked earned ownership");
        } finally { LocalClassRewards.Rewards[index] = original; }

        string variants = Fresh(root, "premium-variants");
        setup = Read(variants); setup.ClassXp.ForEach(row => row.Xp = 25190); Write(variants, setup);
        using (var p = new LocalProfile(variants)) {
            Check(p.ClassRewardGrants.Count == 550 && p.ClassRewardGrants.GroupBy(row => row.Hero).All(group => group.Count() == 50), "level 60 schedule incomplete");
            Check(p.OwnedCount("TLC_460", 3) == 1 && p.OwnedCount("TLC_452", 2) == 1, "Signature/Diamond ownership missing");
            long id = p.CustomDecks.Single().Id;
            p.SavePremiumDraft(id, "Variants", "HERO_08", new[] {
                new LocalDeckCard { Id = "TLC_460", Premium = 3 }, new LocalDeckCard { Id = "TLC_452", Premium = 2 } }, 0);
            var candidate = p.OwnedCards.First(row => row.Premium > 0 && SabberStoneCore.Actions.DeckbuildingRules.Eligible("JAIL_397",
                SabberStoneCore.Model.Cards.FromId(row.Id), SabberStoneCore.Enums.FormatType.FT_STANDARD));
            var side = p.CreateDraft("Premium sideboard", "HERO_04");
            p.SavePremiumDraft(side.Id, side.Name, side.Hero, new[] { new LocalDeckCard { Id = "JAIL_397" } }, 1,
                new[] { new LocalSideboard { Source = "JAIL_397", CardEntries = new() { new LocalDeckCard { Id = candidate.Id, Premium = candidate.Premium } } } });
            p.ReorderDrafts(new[] { side.Id, id });
            Check(p.GetDraft(side.Id).Sideboards.Single().CardEntries.Single().Premium == candidate.Premium, "sideboard premium lost on snapshot");
        }
        using (var p = new LocalProfile(variants)) {
            Check(p.CustomDecks.Single(row => row.Hero == "HERO_08").CardEntries.Select(row => row.Premium).SequenceEqual(new[] { 3, 2 }), "premium variants lost on reload");
            Check(p.CustomDecks.Single(row => row.Hero == "HERO_04").Sideboards.Single().CardEntries.Single().Premium != 0, "sideboard premium lost on reload");
        }
        // A later disenchant may spend the award; the grant ledger must prevent replenishment.
        var spent = Read(variants); spent.PremiumCollection.Single(row => row.Id == "TLC_460" && row.Premium == 3).Count = 0; Write(variants, spent);
        using (var p = new LocalProfile(variants)) Check(p.OwnedCount("TLC_460", 3) == 0 && p.ClassRewardGrants.Count == 550, "reconciliation replenished a spent grant");

        // A (class, level) is claimed once: switching meta never re-grants claimed levels, only unclaimed ones use the new schedule.
        string metaPath = Fresh(root, "premium-meta-switch");
        setup = Read(metaPath); setup.ClassXp.Single(x => x.Hero == "HERO_08").Xp = LocalCoreProgression.LevelThresholds[14]; Write(metaPath, setup);
        try {
            LocalClassRewards.ActiveMetaOverride = "violet-hold";
            using (var p = new LocalProfile(metaPath))
                Check(p.ClassRewardGrants.Select(row => row.Level).OrderBy(level => level).SequenceEqual(Enumerable.Range(11, 5)), "violet-hold levels 11-15 claimed");
            LocalClassRewards.ActiveMetaOverride = "black-empire";
            byte[] claimedBytes = File.ReadAllBytes(metaPath);
            using (var p = new LocalProfile(metaPath))
                Check(p.ClassRewardGrants.Count == 5, "meta switch granted claimed levels again");
            Check(claimedBytes.SequenceEqual(File.ReadAllBytes(metaPath)), "meta switch rewrote the profile");
            setup = Read(metaPath); setup.ClassXp.Single(x => x.Hero == "HERO_08").Xp = LocalCoreProgression.LevelThresholds[15]; Write(metaPath, setup);
            var expected16 = LocalClassRewards.Rewards.Single(row => row.Meta == "black-empire" && row.Hero == "HERO_08" && row.Level == 16);
            using (var p = new LocalProfile(metaPath)) {
                // Level 16's card was already paid in full by violet-hold at level 15, so the claim substitutes the next scheduled
                // card that is not yet owned (black-empire level 17), and records the card it stood in for (substitution is the
                // rule since the premium-track work: see PremiumTrackChecks "better progression must substitute").
                var substitute = LocalClassRewards.Rewards.Single(row => row.Meta == "black-empire" && row.Hero == "HERO_08" && row.Level == 17);
                var added = p.ClassRewardGrants.Where(row => row.Level >= 16).ToArray();
                Check(p.ClassRewardGrants.Count == 6 && added.Length == 1 && added[0].CardId == substitute.CardId
                    && added[0].Premium == substitute.Premium && added[0].Copies == substitute.Copies
                    && added[0].ScheduledCardId == expected16.CardId, "level 16 did not substitute the next unowned card of the active meta");
            }
        } finally { LocalClassRewards.ActiveMetaOverride = null; }

        // A schedule revision moves TIME_000 from level 13 to 14: the owned playset is never paid twice, and level 14 substitutes the next unowned card.
        string moved = Fresh(root, "premium-moved-card");
        try {
            setup = Read(moved); setup.ClassXp.Single(x => x.Hero == "HERO_08").Xp = LocalCoreProgression.LevelThresholds[12]; Write(moved, setup);
            LocalClassRewards.ActiveMetaOverride = "violet-hold";
            using (var p = new LocalProfile(moved))
                Check(p.OwnedCount("TIME_000", 1) == 2 && p.ClassRewardGrants.Count == 3, "violet-hold paid TIME_000 at level 13");
            LocalClassRewards.ActiveMetaOverride = "black-empire";
            setup = Read(moved); setup.ClassXp.Single(x => x.Hero == "HERO_08").Xp = LocalCoreProgression.LevelThresholds[14]; Write(moved, setup);
            var level15 = LocalClassRewards.Rewards.Single(row => row.Meta == "black-empire" && row.Hero == "HERO_08" && row.Level == 15);
            using (var p = new LocalProfile(moved)) {
                var again = p.ClassRewardGrants.Single(row => row.Level == 14);
                Check(again.CardId == level15.CardId && again.Copies == level15.Copies && again.ScheduledCardId == "TIME_000" && p.OwnedCount("TIME_000", 1) == 2 && p.ProgressionGrantedCount("TIME_000", 1) == 2
                    && p.OwnedCount(level15.CardId, level15.Premium) == level15.Copies,
                    "moved card is not paid twice, other levels still pay");
            }
            byte[] movedBytes = File.ReadAllBytes(moved);
            using (var p = new LocalProfile(moved)) { }
            Check(movedBytes.SequenceEqual(File.ReadAllBytes(moved)), "substituted claim is stable on reload");
        } finally { LocalClassRewards.ActiveMetaOverride = null; }

        foreach (int threshold in new[] { 200, 400 }) {
            string path = Fresh(root, "portrait-" + threshold);
            if (threshold == 400) {
                var funded = Read(path); funded.GoldBalance = 100000; Write(path, funded);
                // Honored portraits are earn-only now; model a purchase made before that change.
                funded = Read(path); funded.GoldBalance -= 1000; funded.PurchaseSequence = 1; funded.OwnedHeroSkins.Add("HERO_08c");
                funded.Purchases.Add(new LocalPurchaseReceipt { Sequence = 1, RequestId = "prior-honored", CatalogVersion = 1,
                    ProductId = "heroskin-HERO_08c", Kind = "HeroSkin", Quantity = 1, GoldPaid = 1000, PriceBeforeKit = 1000,
                    Granted = new LocalPurchaseGrants { HeroSkin = "HERO_08c" } });
                Write(path, funded);
            }
            var save = Read(path); save.Matches = Enumerable.Range(0, threshold - 1).Select(_ => Win()).ToList();
            // These wins have no class identity/XP and cannot advance the portrait counter.
            save.Matches.AddRange(Enumerable.Range(0, 5).Select(_ => Win(null)));
            // Receipt-less history is the pre-ladder shape: it needs no ladder state (the catalog catch-up checks do the same).
            save.LadderRuleVersion = 0; save.LadderState = null;
            Write(path, save);
            using (var p = new LocalProfile(path)) {
                Check(p.PortraitGrants.Count == (threshold == 200 ? 0 : 1), "portrait granted early or counted excluded wins");
                string id = p.Begin("mage", "priest", 1).Id;
                var bytes = File.ReadAllBytes(path); int before = p.PortraitGrants.Count;
                p.CommitFailure = point => { if (point == "replace") throw new IOException("portrait injected"); };
                Reject(() => p.Settle(id, "Won", 10, false));
                Check(bytes.SequenceEqual(File.ReadAllBytes(path)) && p.PortraitGrants.Count == before, "portrait was not atomic");
                p.CommitFailure = null;
                var receipt = p.Settle(id, "Won", 10, false);
                Check(receipt.PortraitGrants.Single().Kind == (threshold == 200 ? "golden" : "honored"), "portrait receipt missing");
                string portraitText = p.ProgressionSummary(receipt);
                Check(portraitText.Contains("Portraits unlocked: " + (threshold == 200 ? "Golden " : "Honored "))
                    && !portraitText.Contains("HERO_08") && portraitText.Contains(SabberStoneCore.Model.Cards.FromId(receipt.PortraitGrants.Single().CardId).Name),
                    "summary names the portrait card");
                Check(p.OwnedCount("HERO_08", 1) == 1, "golden hero absent");
                if (threshold == 400) Check(p.OwnedHeroSkins.Contains("HERO_08c"), "honored hero absent");
                p.Settle(id, "Won", 10, false); p.AcknowledgeRewardPresentation(id); p.AcknowledgeRewardPresentation(id);
                Check(p.PortraitGrants.Count == before + 1 && p.OwnedCount("HERO_08", 1) == 1, "portrait replay duplicates");
            }
            using (var p = new LocalProfile(path)) Check(p.OwnedCount("HERO_08", 1) == 1, "portrait restart lost ownership");
        }
        // A corrected honored mapping must not fail load: stored rows with a retired honored id stay valid.
        string retiredPath = Fresh(root, "retired-honored");
        var retiredSave = Read(retiredPath);
        retiredSave.PortraitGrants.Add(new LocalPortraitGrant { Hero = "HERO_08", Kind = "honored", CardId = "HERO_08_RETIRED" });
        retiredSave.OwnedHeroSkins.Add("HERO_08_RETIRED"); Write(retiredPath, retiredSave);
        Reject(() => { using var p = new LocalProfile(retiredPath); });
        LocalClassRewards.RetiredHonoredIds["HERO_08"] = new[] { "HERO_08_RETIRED" };
        try { using (var p = new LocalProfile(retiredPath)) Check(p.OwnedHeroSkins.Contains("HERO_08_RETIRED"), "retired honored id lost"); }
        finally { LocalClassRewards.RetiredHonoredIds.Remove("HERO_08"); }
        retiredSave.PortraitGrants[0] = new LocalPortraitGrant { Hero = "HERO_08", Kind = "golden", CardId = "HERO_01" }; Write(retiredPath, retiredSave);
        Reject(() => { using var p = new LocalProfile(retiredPath); });

        string missed = Fresh(root, "missed-portraits");
        setup = Read(missed); setup.Matches = Enumerable.Range(0, 400).Select(_ => Win("HERO_11")).ToList(); setup.LadderRuleVersion = 0; setup.LadderState = null; Write(missed, setup);
        using (var p = new LocalProfile(missed))
            Check(p.PortraitGrants.Count == 2 && p.OwnedCount("HERO_11", 1) == 1 && p.OwnedHeroSkins.Contains("HERO_11a"), "load missed portrait grants");
        byte[] stable = File.ReadAllBytes(missed);
        using (var p = new LocalProfile(missed)) { }
        Check(stable.SequenceEqual(File.ReadAllBytes(missed)), "portrait reconciliation replay changed bytes");
        Console.WriteLine("PASS premium: premium rewards/decks, receipts/replay, reconciliation, portraits");
    }
}

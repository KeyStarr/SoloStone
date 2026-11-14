using System.Xml.Serialization;
using SabberStoneCore.Enums;

internal static class CraftingLedgerTests
{
    const string Common = "CATA_452", Legendary = "CATA_458";
    static readonly XmlSerializer Serializer = new(typeof(LocalProfileData));
    static int assertions;
    static LocalProfileData Read(string path) { using var file = File.OpenRead(path); return (LocalProfileData)Serializer.Deserialize(file)!; }
    static void Write(string path, LocalProfileData data) { using var file = File.Create(path); Serializer.Serialize(file, data); }
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static void Refused(CraftPlan plan) => Check(!plan.Ok && !string.IsNullOrEmpty(plan.Error)
        && plan.DustDelta == 0 && plan.Cards.Count == 0, "refusal exposes no partial changes");
    static string Xml<T>(T value) { using var text = new StringWriter(); new XmlSerializer(typeof(T)).Serialize(text, value); return text.ToString(); }
    static string Memory(LocalProfile profile) => Xml((LocalProfileData)typeof(LocalProfile)
        .GetField("data", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(profile)!);

    // Live PC 2026-10-30: CanUpgradeCardToGolden answered NotUpgradable because GetAllCollectibleCardIds (our pool) held
    // only the Classic/Core catalogue; Standard cards existed natively only once owned, and only in the owned version.
    static void NativeCollectiblePool(string root)
    {
        var pool = NativeUIProjection.NativeCollectiblePool(LocalCrafting.CraftableCardIds);
        var set = new HashSet<string>(pool, StringComparer.Ordinal);
        Check(set.Count == pool.Count && set.IsSupersetOf(NativeUIProjection.Supported) && NativeUIProjection.CosmeticHeroPool.All(set.Contains),
            "native collectible pool keeps the Classic/Core catalogue and hero skins, without duplicates");
        Check(new[] { "TLC_824", "TLC_451", "CATA_452", "EDR_001" }.All(set.Contains) && !NativeUIProjection.Supported.Contains("TLC_824"),
            "every craftable Standard card is natively collectible, so Normal and Golden register as missing");
        Check(LocalCrafting.CraftableCardIds.All(id => LocalCrafting.TryGetPrice(id, out _, out _)),
            "the pool adds only cards the ledger crafts");
        foreach (string id in LocalWelcomeGift.HistoricalCardIds.Where(id => !NativeUIProjection.Supported.Contains(id)))
            Check(!set.Contains(id), "unowned grant-only card stays out of the native pool: " + id);
        string giftDirectory = Path.Combine(root, "native-collection-welcome");
        Directory.CreateDirectory(giftDirectory);
        string giftPath = Path.Combine(giftDirectory, "profile.xml");
        using (var profile = new LocalProfile(giftPath))
        {
            var unownedPool = NativeUIProjection.NativeCollectiblePool(LocalCrafting.CraftableCardIds, profile.OwnedCards);
            Check(!unownedPool.Contains("BE_036"), "unowned M.O.T.H.E.R. stays out of the native collection pool");
            // Deck-slot collectibility keeps its historical predicate: native EvaluateSlot checks it before ownership, so a
            // missing card must stay a blue MISSING ghost, not a red NOT_VALID slot.
            Check(LocalDecks.TestStandardCardIds.Where(id => profile.TotalOwnedCount(id) == 0).Take(50)
                .All(id => NativeUIProjection.IsNativeCollectible(id, profile)), "unowned deck-slot cards stay natively collectible");
            Check(profile.GrantWelcomeIfNew() && profile.OwnedCount("BE_036") == 1, "fresh welcome owns admitted M.O.T.H.E.R.");
        }
        using (var profile = new LocalProfile(giftPath))
        {
            var ownedPool = NativeUIProjection.NativeCollectiblePool(LocalCrafting.CraftableCardIds, profile.OwnedCards);
            var grants = LocalWelcomeGift.HistoricalCardIds.Distinct().Where(LocalProfile.KnownLedgerCard).ToArray();
            Check(grants.All(id => ownedPool.Contains(id) && NativeUIProjection.IsNativeCollectible(id, profile)),
                "every owned admitted welcome grant registers and accepts native collection counts after restart");
            Check(grants.All(id => LocalCrafting.GetIneligibility(id) == CraftIneligibility.GrantOnly
                && !LocalCrafting.CraftableCardIds.Contains(id) && !LocalCrafting.TryGetPrice(id, out _, out _)),
                "visible grants remain Uncraftable without craft, disenchant or upgrade prices");
            int motherSet = (int)SabberStoneCore.Model.Cards.FromId("BE_036").Set;
            int eventSet = (int)SabberStoneCore.Model.Cards.FromId("BE_EVENT_100").Set;
            var visible = NativeUIProjection.DisplayableSetIds(new[] { 2, eventSet, motherSet }, profile.OwnedCards).ToArray();
            Check(motherSet != eventSet && !LocalCardCatalog.InActiveMetaSet(motherSet) && visible.Contains(motherSet)
                && visible.Contains(eventSet) && visible.Distinct().Count() == visible.Length,
                "owned window grant exposes its real BE set beside EVENT without admitting the expansion");
            var empty = new[] { new LocalCardCount { Id = "BE_036", Count = 0 } };
            Check(!NativeUIProjection.NativeCollectiblePool(Array.Empty<string>(), empty).Contains("BE_036")
                && !NativeUIProjection.DisplayableSetIds(new[] { motherSet }, empty).Any(), "zero-owned M.O.T.H.E.R. exposes neither card nor set");
            var outside = new[] { new LocalCardCount { Id = "BE_EVENT_103", Count = 2 }, new LocalCardCount { Id = "BE_152", Count = 1 } };
            var outsidePool = NativeUIProjection.NativeCollectiblePool(Array.Empty<string>(), outside);
            Check(outside.All(card => !outsidePool.Contains(card.Id))
                && !NativeUIProjection.DisplayableSetIds(new[] { motherSet }, outside).Any(),
                "non-admitted grants and other BE cards cannot expose cards or BE set even with supplied ownership");
            Check(ownedPool.Except(pool).OrderBy(id => id).SequenceEqual(grants.Except(pool).OrderBy(id => id)),
                "owned pool adds exactly admitted grants, with no other unowned cards");
        }
        foreach (var card in LocalDecks.KnownBundleAndSideboardCards.Where(LocalCardCatalog.InActiveMeta))
            Check(set.Contains(card.Id) == card.Collectible, card.Collectible ? "Fabled primary/sideboard owner is natively collectible: " + card.Id
                : "Fabled companion stays out of the native pool: " + card.Id);
        Check(!LocalCrafting.CraftableCardIds.Any(LocalCardCatalog.IsCore), "Core cards are added only through the existing catalogue");
        Check(pool.Count < 4000, "native pool stays bounded: " + pool.Count);
        Console.WriteLine("PASS native collectible pool " + pool.Count + " ids (" + LocalCrafting.CraftableCardIds.Count + " craftable)");
    }

    internal static void Run(string root)
    {
        if (Path.DirectorySeparatorChar == '/' && root.StartsWith("/var/", StringComparison.Ordinal)) root = "/private" + root;
        assertions = 0;
        NativeCollectiblePool(root);
        string Fresh(string name, long dust = 1000, Action<LocalProfileData> setup = null) {
            string path = Path.Combine(root, "craft-" + name + ".xml");
            using (var p = new LocalProfile(path)) p.SelectCardBack(0);
            var data = Read(path); data.DustBalance = dust; setup?.Invoke(data); Write(path, data); return path;
        }
        void Unchanged(LocalProfile p, string path, Func<CraftPlan> action) {
            byte[] bytes = File.ReadAllBytes(path); string memory = Memory(p);
            Refused(action());
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)) && memory == Memory(p), "refusal preserves file and memory");
        }
        void Receipt(string path, long sequence, LocalCraftOp op, long dust, params (string Id, int Delta, int Before)[] cards) {
            var data = Read(path); var receipt = data.LastCraft;
            Check(data.CraftSequence == sequence && receipt.Sequence == sequence && receipt.Op == op
                && receipt.DustDelta == dust && !receipt.Undoable && receipt.TokenDelta == 0 && receipt.TokenRarity == null,
                "receipt sequence, operation, dust and reserved fields");
            Check(receipt.Cards.Count == cards.Length && cards.All(c => receipt.Cards.Any(r => r.CardId == c.Id
                && r.CountDelta == c.Delta && r.AcquiredBefore == c.Before)), "receipt card deltas and acquisition before");
        }

        var giftIds = LocalWelcomeGift.Cards.Select(card => card.CardId).Distinct().ToArray();
        string gifts = Fresh("grant-only", 100000, data => {
            foreach (string id in giftIds.Where(LocalProfile.KnownLedgerCard)) {
                data.Collection.Add(new LocalCardCount { Id = id, Count = 2, Acquired = 2 });
                foreach (int premium in new[] { 1, 2, 3 })
                    data.PremiumCollection.Add(new LocalCardCount { Id = id, Premium = premium, Count = 5, Acquired = 2 });
            }
        });
        using (var p = new LocalProfile(gifts)) {
            var ledger = new LocalCraftingLedger(p);
            foreach (string id in giftIds) {
                Check(LocalCrafting.GetIneligibility(id) == CraftIneligibility.GrantOnly, "grant-only: " + id);
                Check(!LocalCrafting.TryGetUpgradePrice(id, out int upgradeCost) && upgradeCost == 0, "grant-only upgrade price refused: " + id);
                var view = new CraftLedgerView { Dust = p.DustBalance, Counts = p.Collection, Acquired = p.Acquired };
                Refused(LocalCrafting.PlanUpgrade(view, id, 1, 0, 0, 2, 0));
                Unchanged(p, gifts, () => LocalCrafting.CommitUpgrade(p, id, 1, 2, 5));
                foreach (int count in new[] { 1, 3 }) {
                    Unchanged(p, gifts, () => ledger.CommitBatch(id, CraftDirection.Craft, count, 2));
                    Unchanged(p, gifts, () => ledger.CommitBatch(id, CraftDirection.Disenchant, count, 2));
                    foreach (int premium in new[] { 1, 2, 3 }) {
                        Unchanged(p, gifts, () => LocalCrafting.CommitPremiumBatch(p, id, premium, CraftDirection.Craft, count, 5));
                        Unchanged(p, gifts, () => LocalCrafting.CommitPremiumBatch(p, id, premium, CraftDirection.Disenchant, count, 5));
                    }
                }
            }
            Unchanged(p, gifts, ledger.CommitMassDisenchant);
        }
        using (var p = new LocalProfile(gifts))
            Check(p.DustBalance == 100000 && Read(gifts).CraftSequence == 0 && giftIds.All(id => Enumerable.Range(0, 4).All(premium => p.OwnedCount(id, premium) == (LocalProfile.KnownLedgerCard(id) ? premium == 0 ? 2 : 5 : 0))),
                "refused gift transactions preserve every quality and wallet after restart");
        string giftUpgrade = Fresh("grant-only-upgrade", 100000, data => {
            foreach (string id in giftIds.Where(LocalProfile.KnownLedgerCard)) data.Collection.Add(new LocalCardCount { Id = id, Count = 2, Acquired = 2 });
        });
        using (var p = new LocalProfile(giftUpgrade))
            foreach (string id in giftIds)
                Unchanged(p, giftUpgrade, () => LocalCrafting.CommitUpgrade(p, id, 1, 2, 0));
        using (var p = new LocalProfile(giftUpgrade))
            Check(p.DustBalance == 100000 && Read(giftUpgrade).CraftSequence == 0
                && giftIds.All(id => p.OwnedCount(id) == (LocalProfile.KnownLedgerCard(id) ? 2 : 0) && p.OwnedCount(id, 1) == 0),
                "grant-only upgrade refusals preserve ownership, wallet and receipt after restart");
        var withOrdinary = Read(gifts);
        foreach (var card in FairPackRules.Eligible("CATACLYSM", 1).Where(card => card.Rarity == Rarity.COMMON))
            withOrdinary.Collection.Add(new LocalCardCount { Id = card.Id, Count = card.Id == Common ? 3 : 0, Acquired = Cap(card) });
        Write(gifts, withOrdinary);
        using (var p = new LocalProfile(gifts)) {
            var ledger = new LocalCraftingLedger(p);
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
                Unchanged(p, gifts, ledger.CommitMassDisenchant);
            }
            p.CommitFailure = null;
            var plan = ledger.CommitMassDisenchant();
            Check(plan.Ok && plan.DustDelta == 20 && plan.Cards.Single().CardId == Common, "mixed mass sale pays ordinary extras only");
        }
        using (var p = new LocalProfile(gifts))
            Check(p.DustBalance == 100020 && Read(gifts).CraftSequence == 1 && p.OwnedCount(Common) == 2
                && giftIds.All(id => Enumerable.Range(0, 4).All(premium => p.OwnedCount(id, premium) == (LocalProfile.KnownLedgerCard(id) ? premium == 0 ? 2 : 5 : 0))), "mixed mass sale survives restart exactly once");

        var upgradeView = new CraftLedgerView { Dust = 720, Counts = new Dictionary<string, int> { [Common] = 2 },
            Acquired = new Dictionary<string, int> { [Common] = 2 } };
        var pureUpgrade = LocalCrafting.PlanUpgrade(upgradeView, Common, 2, 0, 0, 2, 0);
        Check(pureUpgrade.Ok && pureUpgrade.DustDelta == -720 && upgradeView.Dust == 720
            && upgradeView.Counts[Common] == 2 && upgradeView.Acquired[Common] == 2, "upgrade planner leaves input untouched");
        Refused(LocalCrafting.PlanUpgrade(null, Common, 1, 0, 0, 2, 0));
        Refused(LocalCrafting.PlanUpgrade(upgradeView, Common, -1, 0, 0, 2, 0));
        Refused(LocalCrafting.PlanUpgrade(upgradeView, Common, 1, -1, 0, 2, 0));

        // Catches upgrade pricing, partial writes, replay and treating upgrades as new crafts.
        // TLC_824 Odd Map / TLC_451 Cursed Catacombs: the owned Normal pairs from the 2026-10-29 live PC check.
        foreach (var price in new[] { (Common, 360), ("EDR_001", 700), ("CATA_493", 1200), (Legendary, 1600), ("TLC_824", 360), ("TLC_451", 1200) }) {
            int copies = price.Item1 == Legendary ? 1 : 2;
            string upgradePath = Fresh("upgrade-" + price.Item1, 4000, d =>
                d.Collection.Add(new LocalCardCount { Id = price.Item1, Count = copies, Acquired = copies }));
            string committedMemory;
            using (var p = new LocalProfile(upgradePath)) {
                Check(LocalCrafting.TryGetUpgradePrice(price.Item1, out int tariff) && tariff == price.Item2, "upgrade tariff by rarity");
                Unchanged(p, upgradePath, () => LocalCrafting.CommitUpgrade(p, price.Item1, 0, copies, 0));
                Unchanged(p, upgradePath, () => LocalCrafting.CommitUpgrade(p, price.Item1, copies + 1, copies, 0));
                Unchanged(p, upgradePath, () => LocalCrafting.CommitUpgrade(p, price.Item1, 1, copies - 1, 0));
                Unchanged(p, upgradePath, () => LocalCrafting.CommitUpgrade(p, price.Item1, 1, copies, 1));
                int replaces = 0;
                p.CommitFailure = point => { if (point == "replace") replaces++; };
                var beforeAchievements = Xml(p.Achievements.Career);
                var plan = LocalCrafting.CommitUpgrade(p, price.Item1, copies, copies, 0);
                Check(plan.Ok && plan.DustDelta == -copies * price.Item2 && replaces == 1
                    && p.OwnedCount(price.Item1) == 0 && p.OwnedCount(price.Item1, 1) == copies
                    && p.Acquired[price.Item1] == copies && p.DustBalance == 4000 - copies * price.Item2
                    && Xml(p.Achievements.Career) == beforeAchievements, "upgrade moves copies and dust once without craft achievements");
                Check(plan.Cards.Count == 2 && plan.Cards.Any(c => c.Premium == 0 && c.CountDelta == -copies)
                    && plan.Cards.Any(c => c.Premium == 1 && c.CountDelta == copies && c.AcquiredAfter == copies), "upgrade plan carries both versions");
                var receipt = Read(upgradePath).LastCraft;
                Check(receipt.Op == LocalCraftOp.Upgrade && receipt.Cards.Count == 2 && receipt.DustDelta == -copies * price.Item2
                    && !receipt.Undoable && receipt.TokenDelta == 0 && receipt.TokenRarity == null
                    && receipt.Cards.Any(c => c.CardId == price.Item1 && c.Premium == 0 && c.CountDelta == -copies && c.AcquiredBefore == copies)
                    && receipt.Cards.Any(c => c.CardId == price.Item1 && c.Premium == 1 && c.CountDelta == copies && c.AcquiredBefore == 0),
                    "upgrade receipt contains equal opposite deltas");
                Check(Read(upgradePath).PremiumCollection.Single(c => c.Id == price.Item1).Acquired == copies, "new golden row acquired");
                committedMemory = Memory(p);
            }
            using (var p = new LocalProfile(upgradePath)) {
                Check(Memory(p) == committedMemory, "upgrade restart equals committed memory");
                Unchanged(p, upgradePath, () => LocalCrafting.CommitUpgrade(p, price.Item1, copies, copies, 0));
                var sale = LocalCrafting.CommitPremiumBatch(p, price.Item1, 1, CraftDirection.Disenchant, 1, copies);
                Check(sale.Ok && sale.DustDelta == (price.Item2 == 360 ? 200 : price.Item2 == 700 ? 400 : price.Item2 == 1200 ? 800 : 1600),
                    "upgraded golden sells at unchanged premium price");
            }
        }
        foreach (var sample in new[] { (Common, 2, 1), (Legendary, 1, 1), (Common, 1, 2) }) {
            string capPath = Fresh("upgrade-cap-" + sample.Item1 + sample.Item2 + sample.Item3, 10000, d => {
                d.Collection.Add(new LocalCardCount { Id = sample.Item1, Count = 1, Acquired = 1 });
                d.PremiumCollection.Add(new LocalCardCount { Id = sample.Item1, Premium = 1, Count = sample.Item2, Acquired = Math.Min(2, sample.Item2) });
            });
            using var p = new LocalProfile(capPath);
            Unchanged(p, capPath, () => LocalCrafting.CommitUpgrade(p, sample.Item1, sample.Item3, 1, sample.Item2));
        }
        string acquiredUpgrade = Fresh("upgrade-acquired", 1000, d => {
            d.Collection.Add(new LocalCardCount { Id = Common, Count = 1, Acquired = 2 });
            d.PremiumCollection.Add(new LocalCardCount { Id = Common, Premium = 1, Count = 1, Acquired = 2 });
        });
        using (var p = new LocalProfile(acquiredUpgrade)) {
            Check(LocalCrafting.CommitUpgrade(p, Common, 1, 1, 1).Ok && p.Acquired[Common] == 2
                && Read(acquiredUpgrade).PremiumCollection.Single().Acquired == 2, "upgrade retains and caps lifetime acquisition");
        }
        string poorUpgrade = Fresh("upgrade-poor", 359, d => d.Collection.Add(new LocalCardCount { Id = Common, Count = 1, Acquired = 1 }));
        using (var p = new LocalProfile(poorUpgrade)) Unchanged(p, poorUpgrade, () => LocalCrafting.CommitUpgrade(p, Common, 1, 1, 0));
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            string failed = Fresh("upgrade-failure-" + stage, 1000, d => d.Collection.Add(new LocalCardCount { Id = Common, Count = 2, Acquired = 2 }));
            string original;
            using (var p = new LocalProfile(failed)) {
                original = Memory(p);
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
                Unchanged(p, failed, () => LocalCrafting.CommitUpgrade(p, Common, 2, 2, 0));
            }
            using (var p = new LocalProfile(failed)) Check(Memory(p) == original && p.OwnedCount(Common, 1) == 0, "failed upgrade reload has no partial changes");
        }
        foreach (string invalid in new[] { "CORE_CS2_029", "EX1_008", "does-not-exist" }) {
            string invalidPath = Fresh("upgrade-invalid-" + invalid);
            using var p = new LocalProfile(invalidPath);
            Check(!LocalCrafting.TryGetUpgradePrice(invalid, out _), "Core, free and unknown cannot upgrade");
            Unchanged(p, invalidPath, () => LocalCrafting.CommitUpgrade(p, invalid, 1, p.OwnedCount(invalid), 0));
        }
        string unavailablePath = Fresh("upgrade-unavailable", setup: d => d.Collection.Add(new LocalCardCount { Id = Common, Count = 1, Acquired = 1 }));
        // Today's entire craftable pool is available; model a later availability withdrawal in the cached catalog.
        var available = ((Lazy<HashSet<string>>)typeof(LocalClassRewards).GetField("Available",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!).Value;
        Check(available.Remove(Common), "availability fixture starts available");
        try {
            Check(!LocalCrafting.TryGetUpgradePrice(Common, out _), "non-available priced card cannot upgrade");
            using var p = new LocalProfile(unavailablePath);
            Unchanged(p, unavailablePath, () => LocalCrafting.CommitUpgrade(p, Common, 1, 1, 0));
        }
        finally { available.Add(Common); }
        string upgradePending = Fresh("upgrade-pending", setup: d => {
            d.GoldBalance = 100; d.Collection.Add(new LocalCardCount { Id = Common, Count = 1, Acquired = 1 });
        });
        using (var p = new LocalProfile(upgradePending)) {
            p.BuyPack(); p.BeginNativePack(FairPackRules.Standard);
            Unchanged(p, upgradePending, () => LocalCrafting.CommitUpgrade(p, Common, 1, p.OwnedCount(Common), 0));
        }
        string infiniteRoot = Path.Combine(root, "upgrade-infinite"); Directory.CreateDirectory(infiniteRoot);
        string legacy = Path.Combine(infiniteRoot, "profile.xml");
        var slot = ProfileCatalog.Create(legacy, "Infinite", new ProfileOptions { InfiniteDust = true });
        string infinitePath = ProfileCatalog.ProfilePathForId(legacy, slot.Id);
        using (var p = new LocalProfile(infinitePath)) {
            Check(new LocalCraftingLedger(p).CommitBatch(Common, CraftDirection.Craft, 1, 0).Ok, "infinite upgrade setup");
            long stored = Read(infinitePath).DustBalance;
            Check(LocalCrafting.CommitUpgrade(p, Common, 1, 1, 0).Ok && p.DustBalance == ProfileCatalog.UnlimitedDust
                && Read(infinitePath).DustBalance == stored && Read(infinitePath).LastCraft.DustDelta == -360, "infinite upgrade keeps stored wallet and nominal receipt");
        }
        string corruptUpgrade = Fresh("upgrade-corrupt", setup: d => d.Collection.Add(new LocalCardCount { Id = Common, Count = 1, Acquired = 1 }));
        using (var p = new LocalProfile(corruptUpgrade)) Check(LocalCrafting.CommitUpgrade(p, Common, 1, 1, 0).Ok, "corrupt receipt setup");
        foreach (var corruption in new Action<LocalCraftReceipt>[] {
            r => r.Cards[1].Premium = 2, r => r.Cards[1].CountDelta = 2, r => r.DustDelta = 360,
            r => r.Cards.Add(new LocalCraftCard { CardId = Legendary, CountDelta = 1 }), r => r.Undoable = true,
            r => r.Cards[1].CardId = Legendary, r => r.Cards[0].CountDelta = 1,
            r => r.TokenDelta = -1, r => r.TokenRarity = LocalTokenRarity.Common }) {
            var data = Read(corruptUpgrade); corruption(data.LastCraft);
            string badPath = corruptUpgrade + ".bad.xml"; Write(badPath, data);
            bool refused = false;
            try { using var p = new LocalProfile(badPath); } catch (InvalidOperationException) { refused = true; }
            Check(refused, "malformed upgrade receipt refused on load");
        }
        Console.WriteLine("PASS golden upgrades: four tariffs, deltas, acquired, caps, dust, stale counts, availability, failures, restart, receipts, premium sales");

        var upgradeReward = LocalClassRewards.ActiveRewards.First(r => r.Premium == 1 && r.Copies == 2
            && LocalCrafting.TryGetUpgradePrice(r.CardId, out _));
        string grantUpgrade = Fresh("upgrade-then-grant", 5000, d => d.Collection.Add(
            new LocalCardCount { Id = upgradeReward.CardId, Count = 1, Acquired = 1 }));
        using (var p = new LocalProfile(grantUpgrade)) {
            Check(LocalCrafting.CommitUpgrade(p, upgradeReward.CardId, 1, 1, 0).Ok, "upgrade before progression reward");
            p.CommitChange(next => { next.ClassXp.Single(x => x.Hero == upgradeReward.Hero).Xp = LocalCoreProgression.LevelThresholds[upgradeReward.Level - 1]; return true; });
            Check(p.OwnedCount(upgradeReward.CardId, 1) == 3 && p.ProgressionGrantedCount(upgradeReward.CardId, 1) == 2
                && p.DisenchantablePremiumCount(upgradeReward.CardId, 1) == 1, "grant plus upgraded ownership preserves sale provenance");
            Unchanged(p, grantUpgrade, () => LocalCrafting.CommitPremiumBatch(p, upgradeReward.CardId, 1, CraftDirection.Disenchant, 2, 3));
            Check(LocalCrafting.CommitPremiumBatch(p, upgradeReward.CardId, 1, CraftDirection.Disenchant, 1, 3).Ok
                && p.OwnedCount(upgradeReward.CardId, 1) == 2 && p.DisenchantablePremiumCount(upgradeReward.CardId, 1) == 0,
                "only upgraded copy sells after progression grants arrive");
        }
        string upgradeDeck = Fresh("upgrade-deck", 10000, d => {
            WelcomeGiftTests.OwnGrantOnly(d); d.GoldBalance = 100000; d.ClassXp.ForEach(entry => entry.Xp = LocalCoreProgression.LevelThresholds.Last());
        });
        long upgradeDeckId, secondUpgradeDeckId; string upgradeDeckRecipe, secondUpgradeDeckRecipe, upgradeCard;
        using (var p = new LocalProfile(upgradeDeck)) {
            var purchase = p.Purchase("deck-manastorm-mage", 1, p.PurchasePrice("deck-manastorm-mage"), "deck");
            upgradeDeckId = purchase.Granted.DeckId.Value;
            var draft = p.GetDraft(upgradeDeckId);
            upgradeCard = draft.Cards.GroupBy(c => c).First(g => g.Count() == 2 && LocalCrafting.TryGetUpgradePrice(g.Key, out _)
                && p.OwnedCount(g.Key) == 2 && p.OwnedCount(g.Key, 1) == 0).Key;
            var second = p.CreateDraft("Same ownership, independent deck", draft.Hero);
            secondUpgradeDeckId = second.Id;
            p.SavePremiumDraft(second.Id, second.Name, second.Hero, draft.CardEntries, second.SortOrder, draft.Sideboards);
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
                Unchanged(p, upgradeDeck, () => LocalCrafting.CommitUpgrade(p, upgradeCard, 1, 2, 0));
            }
            p.CommitFailure = null;
            Check(LocalCrafting.CommitUpgrade(p, upgradeCard, 1, 2, 0).Ok, "first deck upgrade commits");
            draft.CardEntries.First(card => card.Id == upgradeCard).Premium = 1;
            second = p.GetDraft(second.Id);
            upgradeDeckRecipe = Xml(draft); secondUpgradeDeckRecipe = Xml(second);
            Check(p.IsDeckComplete(upgradeDeckId) && p.IsDeckComplete(second.Id)
                && Xml(p.GetDraft(upgradeDeckId)) == upgradeDeckRecipe
                && second.CardEntries.Count(card => card.Id == upgradeCard && card.Premium == 1) == 1,
                "first upgrade converts one copy in each independent deck; recipe otherwise identical");
            string match = p.Begin("custom:" + upgradeDeckId, "priest", 1).Id;
            Check(!string.IsNullOrEmpty(match), "one Golden and one Normal deck starts");
            p.Settle(match, "Lost", 10, false);
            p.AcknowledgeRewardPresentation(match);
        }
        using (var p = new LocalProfile(upgradeDeck)) {
            Check(p.IsDeckComplete(upgradeDeckId) && Xml(p.GetDraft(upgradeDeckId)) == upgradeDeckRecipe
                && Xml(p.GetDraft(secondUpgradeDeckId)) == secondUpgradeDeckRecipe, "first upgrade deck premiums survive reload");
            var expected = p.GetDraft(upgradeDeckId);
            expected.CardEntries.First(card => card.Id == upgradeCard && card.Premium == 0).Premium = 1;
            upgradeDeckRecipe = Xml(expected);
            expected = p.GetDraft(secondUpgradeDeckId);
            expected.CardEntries.First(card => card.Id == upgradeCard && card.Premium == 0).Premium = 1;
            secondUpgradeDeckRecipe = Xml(expected);
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
                Unchanged(p, upgradeDeck, () => LocalCrafting.CommitUpgrade(p, upgradeCard, 1, 1, 1));
            }
            p.CommitFailure = null;
            Check(LocalCrafting.CommitUpgrade(p, upgradeCard, 1, 1, 1).Ok && p.IsDeckComplete(upgradeDeckId)
                && p.IsDeckComplete(secondUpgradeDeckId) && Xml(p.GetDraft(upgradeDeckId)) == upgradeDeckRecipe
                && Xml(p.GetDraft(secondUpgradeDeckId)) == secondUpgradeDeckRecipe,
                "second upgrade respects existing Golden demand and converts remaining copy in both decks");
            Check(!string.IsNullOrEmpty(p.Begin("custom:" + upgradeDeckId, "priest", 1).Id), "two Golden copies deck starts");
        }
        using (var p = new LocalProfile(upgradeDeck)) Check(p.IsDeckComplete(upgradeDeckId)
            && Xml(p.GetDraft(upgradeDeckId)) == upgradeDeckRecipe && Xml(p.GetDraft(secondUpgradeDeckId)) == secondUpgradeDeckRecipe,
            "two Golden entries in both decks survive reload with recipe otherwise identical");

        string upgradeSides = Fresh("upgrade-sideboard", 10000);
        string sideCard = LocalShop.StandardCollectionCards.First(card => LocalCrafting.TryGetUpgradePrice(card.Id, out _)
            && SabberStoneCore.Actions.DeckbuildingRules.Eligible("JAIL_831", card, SabberStoneCore.Enums.FormatType.FT_WILD)
            && card.Rarity != Rarity.LEGENDARY && LocalProfile.CardPackType(card.Id) != null).Id;
        var sideData = Read(upgradeSides);
        foreach (var card in FairPackRules.Eligible(LocalProfile.CardPackType(sideCard), 1)
            .Where(card => card.Rarity == SabberStoneCore.Model.Cards.FromId(sideCard).Rarity))
            sideData.Collection.Add(new LocalCardCount { Id = card.Id, Count = 0, Acquired = Cap(card) });
        sideData.Collection.Single(card => card.Id == sideCard).Count = 3;
        Write(upgradeSides, sideData);
        long pooledDeck, onlySideDeck, coveredDeck;
        string pooledRecipe, onlySideRecipe, coveredRecipe;
        using (var p = new LocalProfile(upgradeSides)) {
            string hero = LocalDecks.PlayerHeroes.First(id => SabberStoneCore.Model.Cards.FromId(id).Class == SabberStoneCore.Model.Cards.FromId(sideCard).Class);
            var pooled = p.CreateDraft("Two main copies", hero); pooledDeck = pooled.Id;
            p.SaveDraft(pooled.Id, pooled.Name, pooled.Hero, new[] { sideCard, sideCard }, pooled.SortOrder);
            var onlySide = p.CreateDraft("Sideboard only", "HERO_05"); onlySideDeck = onlySide.Id;
            p.SaveDraft(onlySide.Id, onlySide.Name, onlySide.Hero, new[] { "JAIL_831" }, onlySide.SortOrder,
                new[] { new LocalSideboard { Source = "JAIL_831", Cards = new() { sideCard, sideCard } } });
            var covered = p.CreateDraft("Still owned Normal", hero); coveredDeck = covered.Id;
            p.SaveDraft(covered.Id, covered.Name, covered.Hero, new[] { sideCard }, covered.SortOrder);
            pooledRecipe = Xml(p.GetDraft(pooledDeck)); onlySideRecipe = Xml(p.GetDraft(onlySideDeck)); coveredRecipe = Xml(p.GetDraft(coveredDeck));
            Check(LocalCrafting.CommitUpgrade(p, sideCard, 1, 3, 0).Ok && Xml(p.GetDraft(pooledDeck)) == pooledRecipe
                && Xml(p.GetDraft(onlySideDeck)) == onlySideRecipe && Xml(p.GetDraft(coveredDeck)) == coveredRecipe,
                "owned Normal demand needs no conversion even with spare Golden copies");
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
                Unchanged(p, upgradeSides, () => LocalCrafting.CommitUpgrade(p, sideCard, 1, 2, 1));
            }
            p.CommitFailure = null;
            pooled = p.GetDraft(pooledDeck); pooled.CardEntries.First(card => card.Id == sideCard).Premium = 1; pooledRecipe = Xml(pooled);
            onlySide = p.GetDraft(onlySideDeck); onlySide.Sideboards.Single().CardEntries[0].Premium = 1; onlySideRecipe = Xml(onlySide);
            Check(LocalCrafting.CommitUpgrade(p, sideCard, 1, 2, 1).Ok && Xml(p.GetDraft(pooledDeck)) == pooledRecipe
                && Xml(p.GetDraft(onlySideDeck)) == onlySideRecipe && Xml(p.GetDraft(coveredDeck)) == coveredRecipe,
                "sideboard entries convert in order; covered Normal stays unchanged");
        }
        using (var p = new LocalProfile(upgradeSides)) Check(Xml(p.GetDraft(pooledDeck)) == pooledRecipe
            && Xml(p.GetDraft(onlySideDeck)) == onlySideRecipe && Xml(p.GetDraft(coveredDeck)) == coveredRecipe,
            "sideboard conversion survives reload without other recipe changes");
        var pool = new LocalCustomDeck { Cards = new() { Common, Legendary, Common }, Sideboards = new() {
            new() { Source = "first", Cards = new() { Common, Common } },
            new() { Source = "second", Cards = new() { Common } } } };
        pool.Sideboards[0].CardEntries[0].Premium = 1;
        pool.Sideboards[0].CardEntries[1].Premium = 2;
        var expectedPool = Xml(pool);
        LocalCrafting.ReconcileUpgradeDeck(pool, Common, 1, 2);
        Check(pool.CardEntries[0].Premium == 1 && pool.CardEntries[2].Premium == 0
            && pool.Sideboards[0].CardEntries[0].Premium == 1 && pool.Sideboards[0].CardEntries[1].Premium == 2
            && pool.Sideboards[1].CardEntries[0].Premium == 0, "pooled demand spends existing Golden once; main copies convert first");
        pool.CardEntries[0].Premium = 0;
        Check(Xml(pool) == expectedPool, "pool rewrite changes only selected premium");
        LocalCrafting.ReconcileUpgradeDeck(pool, Common, 0, 4);
        Check(pool.CardEntries.Where(card => card.Id == Common).All(card => card.Premium == 1)
            && pool.Sideboards[1].CardEntries[0].Premium == 1 && pool.Sideboards[0].CardEntries[1].Premium == 2,
            "pool rewrite continues through sideboards without changing other premiums");
        Console.WriteLine("PASS golden upgrades: atomic deck premiums, independent decks, pooled sideboards, failures, reload and playability");

#if ENGINE_SOURCE
        var fabledPrimaries = SabberStoneCore.Model.Cards.All.Where(card => SabberStoneCore.Actions.FabledRules.IsFabled(card.Id)).ToArray();
        Check(fabledPrimaries.Length == 11, "upgrade fixture checks all Fabled primaries");
        var addedAvailable = new List<string>();
        try {
            // Owner 2026-10-30: Fabled primaries are ordinary legendaries (no virtual ownership): craftable, and
            // upgradeable once available; their companions are never craftable.
            foreach (var primary in fabledPrimaries) {
                Check(LocalCrafting.TryGetPrice(primary.Id, out int buy, out int sell) && buy == 1600 && sell == 800,
                    "Fabled primary crafts at the legendary price: " + primary.Id);
                if (available.Add(primary.Id)) addedAvailable.Add(primary.Id);
                Check(LocalCrafting.TryGetUpgradePrice(primary.Id, out int upgrade) && upgrade == 1600,
                    "available Fabled primary upgrades like any legendary: " + primary.Id);
                foreach (string companion in SabberStoneCore.Actions.FabledRules.Bundle(primary.Id))
                    Check(!LocalCrafting.TryGetPrice(companion, out _, out _) && !LocalCrafting.TryGetUpgradePrice(companion, out _),
                        "Fabled companion is never craftable: " + companion);
            }
        } finally { foreach (string id in addedAvailable) available.Remove(id); }
        // Exercise the shared Fabled deck rewrite directly.
        foreach (string primary in new[] { "TIME_005", "TIME_009" }) {
            var companions = SabberStoneCore.Actions.FabledRules.Bundle(primary);
            Check(companions.Count == (primary == "TIME_005" ? 9 : 2), "Fabled fixture bundle size");
            foreach (int copies in new[] { 1, 2 }) {
                LocalCustomDeck BundleDeck(int converted) => new() {
                    Name = "Fabled upgrade", Hero = "HERO_07", SortOrder = 7, SourceRecipeId = "fixture",
                    CardEntries = new[] {
                        new LocalDeckCard { Id = primary, Premium = converted > 0 ? 1 : 0 },
                        new LocalDeckCard { Id = Common }, new LocalDeckCard { Id = primary, Premium = 1 }
                    }.Concat(companions.Where((_, index) => index % 2 == 0)
                        .Select(id => new LocalDeckCard { Id = id, Premium = converted > 0 ? 1 : 0 })).ToList(),
                    Sideboards = new() {
                        new() { Source = "first", CardEntries = new[] { new LocalDeckCard { Id = primary, Premium = converted > 1 ? 1 : 0 } }
                            .Concat(companions.Where((_, index) => index % 2 == 1)
                                .Select(id => new LocalDeckCard { Id = id, Premium = converted > 0 ? 1 : 0 })).ToList() },
                        new() { Source = "second", CardEntries = companions
                            .Select(id => new LocalDeckCard { Id = id, Premium = converted > 1 ? 1 : 0 }).ToList() },
                        new() { Source = "untouched", CardEntries = companions.SelectMany(id => new[] {
                            new LocalDeckCard { Id = id }, new LocalDeckCard { Id = id, Premium = 1 },
                            new LocalDeckCard { Id = id, Premium = 2 }, new LocalDeckCard { Id = id, Premium = 3 }
                        }).ToList() }
                    }
                };
                var deck = BundleDeck(0);
                string before = Xml(deck);
                LocalCrafting.ReconcileUpgradeDeck(deck, primary, 2, 3);
                Check(Xml(deck) == before, "Normal Fabled primaries covered by ownership leave companions untouched");
                LocalCrafting.ReconcileUpgradeDeck(deck, primary, 2 - copies, 1 + copies);
                string expected = Xml(BundleDeck(copies));
                Check(Xml(deck) == expected, "each converted primary upgrades exactly one Normal companion, main then sideboards: " + (primary, copies));
                LocalCrafting.ReconcileUpgradeDeck(deck, primary, 2 - copies, 1 + copies);
                Check(Xml(deck) == expected, "Fabled reconciliation is idempotent; existing Golden primaries do not upgrade companions");
            }
            var alreadyGolden = new LocalCustomDeck { CardEntries = new[] {
                new LocalDeckCard { Id = primary, Premium = 1 }, new LocalDeckCard { Id = Common }
            }.Concat(companions.Select(id => new LocalDeckCard { Id = id })).ToList() };
            string unchanged = Xml(alreadyGolden);
            LocalCrafting.ReconcileUpgradeDeck(alreadyGolden, Common, 0, 1);
            Check(alreadyGolden.CardEntries[1].Premium == 1, "unrelated card upgrade converts its own entry");
            alreadyGolden.CardEntries[1].Premium = 0;
            Check(Xml(alreadyGolden) == unchanged, "unrelated upgrade leaves already-Golden primary and Normal companions byte-identical");
        }
        Console.WriteLine("PASS Fabled upgrades: 11/11 craftable, upgradeable when available, companions never; synthetic 9/2-companion bundles, exact copies/order, unchanged entries and Golden primaries");
#endif

        // Catches premium sales hitting Normal ownership, wrong rarity prices, or replay on load.
        foreach (var price in new[] { (Common, 200), ("EDR_001", 400), ("CATA_493", 800), (Legendary, 1600) })
        foreach (int premium in new[] { 1, 2, 3 }) {
            string premiumPath = Fresh("premium-" + price.Item1 + "-" + premium, 17, d => {
                d.Collection.Add(new LocalCardCount { Id = price.Item1, Count = 1, Acquired = 1 });
                d.PremiumCollection.Add(new LocalCardCount { Id = price.Item1, Premium = premium, Count = 2, Acquired = 2 });
            });
            string committedMemory;
            using (var p = new LocalProfile(premiumPath)) {
                var ledger = new LocalCraftingLedger(p);
                Unchanged(p, premiumPath, () => LocalCrafting.CommitPremiumBatch(p, price.Item1, premium, CraftDirection.Craft, 1, 2));
                Unchanged(p, premiumPath, () => LocalCrafting.CommitPremiumBatch(p, price.Item1, premium, CraftDirection.Disenchant, 1, 1));
                Unchanged(p, premiumPath, () => LocalCrafting.CommitPremiumBatch(p, price.Item1, premium, CraftDirection.Disenchant, 3, 2));
                Unchanged(p, premiumPath, ledger.CommitMassDisenchant);
                foreach (string stage in new[] { "write", "flush", "replace" }) {
                    p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
                    Unchanged(p, premiumPath, () => LocalCrafting.CommitPremiumBatch(p, price.Item1, premium, CraftDirection.Disenchant, 1, 2));
                    p.CommitFailure = null;
                }
                var plan = LocalCrafting.CommitPremiumBatch(p, price.Item1, premium, CraftDirection.Disenchant, 1, 2);
                Check(plan.Ok && plan.DustDelta == price.Item2 && plan.Cards.Single().Premium == premium
                    && p.OwnedCount(price.Item1, premium) == 1 && p.OwnedCount(price.Item1) == 1
                    && p.DustBalance == 17 + price.Item2, "premium rarity sale changes only selected stack and dust");
                Receipt(premiumPath, 1, LocalCraftOp.Disenchant, price.Item2, (price.Item1, -1, price.Item1 == Legendary ? 1 : 2)); // receipt clamps acquisition to the playset
                committedMemory = Memory(p);
            }
            using (var p = new LocalProfile(premiumPath)) {
                Check(Memory(p) == committedMemory, "premium sale and receipt survive restart without replay");
                Unchanged(p, premiumPath, () => LocalCrafting.CommitPremiumBatch(p, price.Item1, premium, CraftDirection.Disenchant, 1, 2));
                Check(p.OwnedCount(price.Item1, premium) == 1 && p.DustBalance == 17 + price.Item2,
                    "stale premium receipt cannot pay twice");
            }
            // The receipt records which version was sold, and a corrupt version is refused on load.
            Check(Read(premiumPath).LastCraft.Cards.Single().Premium == premium, "premium receipt keeps the sold version");
            foreach (int bad in new[] { -1, 4 }) {
                var corrupt = Read(premiumPath); corrupt.LastCraft.Cards[0].Premium = bad;
                string corruptPath = premiumPath + ".bad.xml"; Write(corruptPath, corrupt);
                bool refused = false;
                try { using var p = new LocalProfile(corruptPath); } catch (InvalidOperationException) { refused = true; }
                Check(refused, "receipt premium " + bad + " refused on load");
            }
            var normalReceipt = Read(premiumPath); normalReceipt.LastCraft.Op = LocalCraftOp.Craft; normalReceipt.LastCraft.DustDelta = -5;
            normalReceipt.LastCraft.Cards[0].CountDelta = 1; Write(premiumPath + ".craft.xml", normalReceipt);
            bool craftRefused = false;
            try { using var p = new LocalProfile(premiumPath + ".craft.xml"); } catch (InvalidOperationException) { craftRefused = true; }
            Check(craftRefused, "only a disenchant receipt may carry a premium version");
        }

        // Catches priceable premium reward cards whose receipt would not validate on reload (profile would fail to open).
        int rewardSales = 0, coreRewardSales = 0;
        foreach (var reward in LocalClassRewards.Rewards.Select(row => row.CardId).Distinct()) {
            if (!LocalCrafting.TryGetPrice(reward, 1, out _, out _)) continue;
            string rewardPath = Fresh("reward-" + reward, 0, d => d.PremiumCollection.Add(
                new LocalCardCount { Id = reward, Premium = 1, Count = 2, Acquired = 2 }));
            using (var p = new LocalProfile(rewardPath)) {
                if (p.OwnedCount(reward, 1) != 2) continue; // other metas stay hidden from runtime ownership
                Check(LocalCrafting.CommitPremiumBatch(p, reward, 1, CraftDirection.Disenchant, 1, 2).Ok, "reward premium sale " + reward);
            }
            using (var p = new LocalProfile(rewardPath)) Check(p.OwnedCount(reward, 1) == 1 && p.DustBalance > 0, "reward premium receipt reloads " + reward);
            rewardSales++; if (LocalCardCatalog.IsCore(reward)) coreRewardSales++;
        }
        Check(rewardSales > 50 && coreRewardSales > 5, "reward premium sales covered: " + rewardSales + "/" + coreRewardSales);

        // A deck purchase counts copies owned in any version: two Golden X are neither charged for nor granted as Normal.
        void BuySetup(LocalProfileData d) { WelcomeGiftTests.OwnGrantOnly(d); d.GoldBalance = 100000; d.ClassXp.ForEach(entry => entry.Xp = LocalCoreProgression.LevelThresholds.Last()); }
        string buyProbe = Fresh("buy-probe", setup: BuySetup); string buyCard; int buyBase;
        using (var p = new LocalProfile(buyProbe)) {
            buyBase = p.PurchasePrice("deck-manastorm-mage");
            var purchase = p.Purchase("deck-manastorm-mage", 1, buyBase, "deck");
            buyCard = p.GetDraft(purchase.Granted.DeckId.Value).Cards.GroupBy(id => id)
                .First(group => group.Count() == 2 && LocalCrafting.TryGetPrice(group.Key, out _, out _) && purchase.Granted.Cards.Any(c => c.CardId == group.Key)).Key;
        }
        string buyGolden = Fresh("buy-golden", setup: d => { BuySetup(d); d.PremiumCollection.Add(new LocalCardCount { Id = buyCard, Premium = 1, Count = 2, Acquired = 2 }); });
        using (var p = new LocalProfile(buyGolden)) {
            int expected = buyBase - 2 * LocalShop.CopyPrice(SabberStoneCore.Model.Cards.FromId(buyCard));
            Check(p.PurchasePrice("deck-manastorm-mage") == expected, "deck price skips copies owned as Golden");
            var purchase = p.Purchase("deck-manastorm-mage", 1, expected, "deck");
            Check(purchase.PriceBeforeKit == expected && purchase.Granted.Cards.All(c => c.CardId != buyCard) && p.OwnedCount(buyCard) == 0
                && p.OwnedCount(buyCard, 1) == 2 && p.IsDeckComplete(purchase.Granted.DeckId.Value), "no Normal copies granted for Golden-owned cards");
        }
        using (var p = new LocalProfile(buyGolden)) Check(p.OwnedCount(buyCard) == 0 && p.Purchases.Count == 1, "Golden-covered purchase survives reload");

        // Premium copies earned through progression can never be disenchanted; extras from other sources can.
        string earned = Fresh("earned-premium", 0, d => d.ClassXp.ForEach(entry => entry.Xp = LocalCoreProgression.LevelThresholds.Last()));
        string earnedCard; int earnedPremium, earnedOwned;
        using (var p = new LocalProfile(earned)) {
            var grant = p.ClassRewardGrants.First(row => LocalCrafting.TryGetPrice(row.CardId, row.Premium, out _, out _) && p.OwnedCount(row.CardId, row.Premium) == row.Copies);
            int granted = p.ProgressionGrantedCount(grant.CardId, grant.Premium);
            Check(granted >= grant.Copies && p.DisenchantablePremiumCount(grant.CardId, grant.Premium) == 0, "earned premium copies are not disenchantable");
            Unchanged(p, earned, () => LocalCrafting.CommitPremiumBatch(p, grant.CardId, grant.Premium, CraftDirection.Disenchant, 1, p.OwnedCount(grant.CardId, grant.Premium)));
            Check(p.DustBalance == 0 && p.OwnedCount(grant.CardId, grant.Premium) == granted, "refused earned sale changed nothing");
            Check(p.DisenchantablePremiumCount("HERO_08", 1) == 0, "golden hero portrait copy is not disenchantable");
            earnedCard = grant.CardId; earnedPremium = grant.Premium; earnedOwned = granted;
        }
        var extra = Read(earned);
        extra.PremiumCollection.Single(row => row.Id == earnedCard && row.Premium == earnedPremium).Count = earnedOwned + 1;
        Write(earned, extra);
        using (var p = new LocalProfile(earned)) {
            Check(p.DisenchantablePremiumCount(earnedCard, earnedPremium) == 1, "a non-progression extra copy is disenchantable");
            Unchanged(p, earned, () => LocalCrafting.CommitPremiumBatch(p, earnedCard, earnedPremium, CraftDirection.Disenchant, 2, earnedOwned + 1));
            var sale = LocalCrafting.CommitPremiumBatch(p, earnedCard, earnedPremium, CraftDirection.Disenchant, 1, earnedOwned + 1);
            Check(sale.Ok && sale.DustDelta > 0 && p.OwnedCount(earnedCard, earnedPremium) == earnedOwned && p.DisenchantablePremiumCount(earnedCard, earnedPremium) == 0
                && p.ProgressionGrantedCount(earnedCard, earnedPremium) == earnedOwned, "extra copy sells at the table value and grants stay intact");
        }

        // Catches mass disenchant consuming premium copies, which would silently destroy earned rewards.
        string massPremium = Fresh("mass-premium", 0, d => {
            foreach (var card in FairPackRules.Eligible("CATACLYSM", 1).Where(c => c.Rarity == Rarity.COMMON || c.Rarity == Rarity.LEGENDARY))
                d.Collection.Add(new LocalCardCount { Id = card.Id, Count = 0, Acquired = Cap(card) });
            d.Collection.Single(c => c.Id == Common).Count = 4;
            d.Collection.Single(c => c.Id == Legendary).Count = 3;
            foreach (string id in new[] { Common, Legendary }) foreach (int premium in new[] { 1, 2, 3 })
                d.PremiumCollection.Add(new LocalCardCount { Id = id, Premium = premium, Count = 3, Acquired = 2 });
        });
        using (var p = new LocalProfile(massPremium)) {
            var ledger = new LocalCraftingLedger(p);
            Check(ledger.CommitMassDisenchant().Ok && p.OwnedCount(Common) == 2 && p.OwnedCount(Legendary) == 1
                && p.DustBalance == 2 * 20 + 2 * 800, "mass disenchant pays Normal extras only");
            foreach (string id in new[] { Common, Legendary }) foreach (int premium in new[] { 1, 2, 3 })
                Check(p.OwnedCount(id, premium) == 3, "mass disenchant left premium copies untouched");
            Check(Read(massPremium).PremiumCollection.All(row => row.Count == 3), "premium rows durable after mass disenchant");
        }

        // Catches premium sales that skip the saved-deck rule: the recipe must survive but become incomplete.
        string premiumDeck = Fresh("premium-deck", setup: d => {
            WelcomeGiftTests.OwnGrantOnly(d); d.GoldBalance = 100000;
            d.ClassXp.ForEach(entry => entry.Xp = LocalCoreProgression.LevelThresholds.Last());
        });
        long premiumDeckId; string premiumCard; string premiumRecipe;
        using (var p = new LocalProfile(premiumDeck)) {
            var purchase = p.Purchase("deck-manastorm-mage", 1, p.PurchasePrice("deck-manastorm-mage"), "deck");
            premiumDeckId = purchase.Granted.DeckId.Value;
            premiumCard = p.GetDraft(premiumDeckId).Cards.First(id => LocalCrafting.TryGetPrice(id, out _, out _));
        }
        var premiumData = Read(premiumDeck);
        premiumData.PremiumCollection.Add(new LocalCardCount { Id = premiumCard, Premium = 2, Count = 1, Acquired = 1 });
        Write(premiumDeck, premiumData);
        using (var p = new LocalProfile(premiumDeck)) {
            var draft = p.GetDraft(premiumDeckId);
            var entries = draft.CardEntries.Select(card => new LocalDeckCard { Id = card.Id, Premium = card.Premium }).ToList();
            entries.First(card => card.Id == premiumCard).Premium = 2;
            p.SavePremiumDraft(premiumDeckId, draft.Name, draft.Hero, entries, draft.SortOrder);
            premiumRecipe = Xml(p.GetDraft(premiumDeckId));
            Check(p.IsDeckComplete(premiumDeckId), "premium deck initially complete");
            Check(LocalCrafting.CommitPremiumBatch(p, premiumCard, 2, CraftDirection.Disenchant, 1, 1).Ok
                && p.IsDeckComplete(premiumDeckId)
                && p.GetDraft(premiumDeckId).CardEntries.Where(card => card.Id == premiumCard).All(card => card.Premium == 0),
                "premium sale falls back to free owned Normal copies");
            premiumRecipe = Xml(p.GetDraft(premiumDeckId));
        }
        using (var p = new LocalProfile(premiumDeck))
            Check(p.IsDeckComplete(premiumDeckId) && Xml(p.GetDraft(premiumDeckId)) == premiumRecipe,
                "repaired premium deck survives reload");

        string path = Fresh("roundtrip"); string expectedMemory;
        using (var p = new LocalProfile(path)) {
            ICraftingLedger ledger = new LocalCraftingLedger(p);
            Check(ledger.CommitBatch(Common, CraftDirection.Craft, 2, 0).Ok && ledger.OwnedCount(Common) == 2
                && p.Acquired[Common] == 2 && ledger.DustBalance == 920, "batch craft durable values");
            Receipt(path, 1, LocalCraftOp.Craft, -80, (Common, 2, 0));
            expectedMemory = Memory(p);
        }
        using (var p = new LocalProfile(path)) {
            var ledger = new LocalCraftingLedger(p);
            Check(Memory(p) == expectedMemory, "craft reload equals memory");
            Check(ledger.CommitBatch(Common, CraftDirection.Disenchant, 2, 2).Ok && ledger.OwnedCount(Common) == 0
                && p.Acquired[Common] == 2 && p.DustBalance == 960, "disenchant retains lifetime acquisition at zero");
            Receipt(path, 2, LocalCraftOp.Disenchant, 40, (Common, -2, 2));
            expectedMemory = Memory(p);
        }
        using (var p = new LocalProfile(path)) {
            var ledger = new LocalCraftingLedger(p);
            Check(Memory(p) == expectedMemory, "disenchant reload equals memory");
            Check(ledger.CommitBatch(Common, CraftDirection.Craft, 1, 0).Ok && p.Acquired[Common] == 2, "recraft caps lifetime acquisition");
            Unchanged(p, path, () => ledger.CommitBatch(Common, CraftDirection.Craft, 1, 0)); // Stale view.
            Unchanged(p, path, () => ledger.CommitBatch(Common, CraftDirection.Craft, 2, 1));
            Unchanged(p, path, () => ledger.CommitBatch("EX1_008", CraftDirection.Craft, 1, 0));
            Unchanged(p, path, () => ledger.CommitMassDisenchant());
        }
        string poor = Fresh("poor", 39);
        using (var p = new LocalProfile(poor)) Unchanged(p, poor,
            () => new LocalCraftingLedger(p).CommitBatch(Common, CraftDirection.Craft, 1, 0));
        string lifetime = Fresh("lifetime");
        using (var p = new LocalProfile(lifetime)) {
            var ledger = new LocalCraftingLedger(p);
            Check(ledger.CommitBatch(Common, CraftDirection.Craft, 1, 0).Ok && p.Acquired[Common] == 1
                && ledger.CommitBatch(Common, CraftDirection.Disenchant, 1, 1).Ok && p.Acquired[Common] == 1
                && ledger.CommitBatch(Common, CraftDirection.Craft, 1, 0).Ok && p.Acquired[Common] == 2, "lifetime acquisition increments up to playset");
        }
        foreach (bool sequenceOverflow in new[] { false, true }) {
            string overflow = Fresh("overflow-" + sequenceOverflow, sequenceOverflow ? 1000 : long.MaxValue, d => {
                d.Collection.Add(new LocalCardCount { Id = Common, Count = 1, Acquired = 1 });
                if (sequenceOverflow) {
                    d.CraftSequence = long.MaxValue;
                    d.LastCraft = new LocalCraftReceipt { Sequence = long.MaxValue, Op = LocalCraftOp.Craft, DustDelta = -40,
                        Cards = new() { new() { CardId = Common, CountDelta = 1 } } };
                }
            });
            using var p = new LocalProfile(overflow);
            Unchanged(p, overflow, () => new LocalCraftingLedger(p).CommitBatch(Common, CraftDirection.Disenchant, 1, 1));
        }
        string pending = Fresh("pending", setup: d => d.GoldBalance = 100);
        using (var p = new LocalProfile(pending)) {
            p.BuyPack(); p.BeginNativePack(FairPackRules.Standard);
            var ledger = new LocalCraftingLedger(p);
            Check(ledger.PackRevealPending, "ledger exposes pending reveal");
            Unchanged(p, pending, () => ledger.CommitBatch(Common, CraftDirection.Craft, 1, ledger.OwnedCount(Common)));
            Unchanged(p, pending, ledger.CommitMassDisenchant);
        }

        // Removing a validation call or applying mutations before replace must fail these checks.
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            string failed = Fresh("failure-" + stage);
            using var p = new LocalProfile(failed); var ledger = new LocalCraftingLedger(p);
            p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
            Unchanged(p, failed, () => ledger.CommitBatch(Common, CraftDirection.Craft, 2, 0));
            p.CommitFailure = null;
            Check(ledger.CommitBatch(Common, CraftDirection.Craft, 2, 0).Ok, "retry after failed craft");
            p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
            Unchanged(p, failed, () => ledger.CommitBatch(Common, CraftDirection.Disenchant, 2, 2));
            p.CommitFailure = null;
            var bytes = File.ReadAllBytes(failed); string memory = Memory(p);
            try { p.CommitChange(d => { d.DustBalance = -1; return true; }); throw new Exception("invalid change accepted"); }
            catch (InvalidOperationException) { }
            Check(bytes.SequenceEqual(File.ReadAllBytes(failed)) && Memory(p) == memory, "CommitChange runs V10 validation");
            try { p.CommitChange<bool>(d => { d.Collection.Clear(); throw new IOException("callback failure"); }); }
            catch (IOException) { }
            Check(bytes.SequenceEqual(File.ReadAllBytes(failed)) && Memory(p) == memory, "callback exception preserves state");
        }

        string mass = Fresh("mass", 17, d => {
            // V10 extras require acquired playsets for this entire set and rarity.
            foreach (var card in FairPackRules.Eligible("CATACLYSM", 1).Where(c => c.Rarity == Rarity.COMMON || c.Rarity == Rarity.LEGENDARY))
                d.Collection.Add(new LocalCardCount { Id = card.Id, Count = 0, Acquired = Cap(card) });
            d.Collection.Single(c => c.Id == Common).Count = 5;
            d.Collection.Single(c => c.Id == Legendary).Count = 3;
        });
        using (var p = new LocalProfile(mass)) {
            var ledger = new LocalCraftingLedger(p);
            Check(ledger.OwnedCount(Common) == 5 && ledger.OwnedCount(Legendary) == 3, "ledger includes extras");
            byte[] beforePlan = File.ReadAllBytes(mass); string memoryBeforePlan = Memory(p);
            var preview = ledger.PlanMassDisenchant();
            Check(preview.Ok && preview.DustDelta == 1660 && preview.Cards.Count == 2
                && preview.Cards.Single(card => card.CardId == Common).CountDelta == -3
                && preview.Cards.Single(card => card.CardId == Legendary).CountDelta == -2
                && beforePlan.SequenceEqual(File.ReadAllBytes(mass)) && Memory(p) == memoryBeforePlan,
                "mass plan covers exact profile extras without mutating memory or persistence");
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected"); };
                Unchanged(p, mass, ledger.CommitMassDisenchant);
            }
            int replaces = 0; p.CommitFailure = point => { if (point == "replace") replaces++; };
            var plan = ledger.CommitMassDisenchant();
            Check(plan.Ok && plan.DustDelta == 1660 && p.DustBalance == 1677 && replaces == 1
                && ledger.OwnedCount(Common) == 2 && ledger.OwnedCount(Legendary) == 1
                && p.Acquired[Common] == 2 && p.Acquired[Legendary] == 1, "mass commits extras and dust exactly once");
            Receipt(mass, 1, LocalCraftOp.MassDisenchant, 1660, (Common, -3, 2), (Legendary, -2, 1));
            Unchanged(p, mass, ledger.CommitMassDisenchant);
            expectedMemory = Memory(p);
        }
        using (var p = new LocalProfile(mass)) Check(Memory(p) == expectedMemory, "mass reload equals memory");

        string deckPath = Fresh("deck", setup: d => {
            WelcomeGiftTests.OwnGrantOnly(d); d.GoldBalance = 100000;
            d.ClassXp.ForEach(entry => entry.Xp = LocalCoreProgression.LevelThresholds.Last());
        });
        long deckId; string deckXml;
        using (var p = new LocalProfile(deckPath)) {
            var purchase = p.Purchase("deck-manastorm-mage", 1, p.PurchasePrice("deck-manastorm-mage"), "deck");
            var draft = p.GetDraft(purchase.Granted.DeckId.Value);
            deckId = draft.Id; deckXml = Xml(draft);
            string card = draft.Cards.First(id => LocalCrafting.TryGetPrice(id, out _, out _));
            var ledger = new LocalCraftingLedger(p); int owned = ledger.OwnedCount(card);
            Check(p.IsDeckComplete(deckId), "recipe deck initially complete");
            Check(ledger.CommitBatch(card, CraftDirection.Disenchant, owned, owned).Ok && !p.IsDeckComplete(deckId)
                && Xml(p.GetDraft(deckId)) == deckXml, "disenchant keeps incomplete deck recipe");
            try { p.Begin("custom:" + deckId, "priest", 1); throw new Exception("incomplete deck started"); }
            catch (InvalidOperationException) { }
            Check(ledger.CommitBatch(card, CraftDirection.Craft, owned, 0).Ok && p.IsDeckComplete(deckId)
                && Xml(p.GetDraft(deckId)) == deckXml, "craft restores complete saved deck");
        }
        using (var p = new LocalProfile(deckPath)) Check(p.IsDeckComplete(deckId) && Xml(p.GetDraft(deckId)) == deckXml, "restored deck survives reload");

        // ID-only recipes repair to exact owned versions; each premium copy can satisfy only one slot.
        // Catches golden-only ownership failing to start a recipe deck, and premium copies being counted twice.
        string anyVersion = Fresh("any-version", setup: d => {
            WelcomeGiftTests.OwnGrantOnly(d); d.GoldBalance = 100000;
            d.ClassXp.ForEach(entry => entry.Xp = LocalCoreProgression.LevelThresholds.Last());
        });
        long anyId; string anyCard;
        using (var p = new LocalProfile(anyVersion)) {
            var purchase = p.Purchase("deck-manastorm-mage", 1, p.PurchasePrice("deck-manastorm-mage"), "deck");
            var draft = p.GetDraft(purchase.Granted.DeckId.Value);
            anyId = draft.Id;
            anyCard = draft.Cards.GroupBy(id => id).First(group => group.Count() == 2 && LocalCrafting.TryGetPrice(group.Key, out _, out _)).Key;
            var ledger = new LocalCraftingLedger(p); int owned = ledger.OwnedCount(anyCard);
            Check(ledger.CommitBatch(anyCard, CraftDirection.Disenchant, owned, owned).Ok && !p.IsDeckComplete(anyId), "Normal copies sold");
        }
        void GoldenOwned(int count) {
            var data = Read(anyVersion);
            data.PremiumCollection.RemoveAll(row => row.Id == anyCard);
            if (count > 0) data.PremiumCollection.Add(new LocalCardCount { Id = anyCard, Premium = 1, Count = count, Acquired = Math.Min(2, count) });
            Write(anyVersion, data);
        }
        GoldenOwned(1);
        using (var p = new LocalProfile(anyVersion)) Check(!p.IsDeckComplete(anyId), "one Golden does not cover two Normal entries");
        GoldenOwned(2);
        using (var p = new LocalProfile(anyVersion)) {
            Check(p.IsDeckComplete(anyId) && p.GetDraft(anyId).CardEntries.Where(card => card.Id == anyCard).All(card => card.Premium == 1),
                "Golden-only ownership repairs recipe entries to exact Golden slots");
            var draft = p.GetDraft(anyId);
            var entries = draft.CardEntries.Select(card => new LocalDeckCard { Id = card.Id, Premium = card.Premium }).ToList();
            entries.First(card => card.Id == anyCard).Premium = 1;
            p.SavePremiumDraft(anyId, draft.Name, draft.Hero, entries, draft.SortOrder);
            Check(p.IsDeckComplete(anyId), "mixed input is reconciled to two owned Golden copies");
            foreach (var card in entries.Where(card => card.Id == anyCard)) card.Premium = 1;
            p.SavePremiumDraft(anyId, draft.Name, draft.Hero, entries, draft.SortOrder);
            Check(p.IsDeckComplete(anyId) && !string.IsNullOrEmpty(p.Begin("custom:" + anyId, "priest", 1).Id), "two Golden entries start with Golden ownership only");
        }
        GoldenOwned(1);
        using (var p = new LocalProfile(anyVersion)) {
            var draft = p.GetDraft(anyId);
            Check(draft.CardEntries.Count(card => card.Id == anyCard && card.Premium == 1) == 2 && !p.IsDeckComplete(anyId),
                "premium demand above the owned Golden copies is incomplete");
        }
        GoldenOwned(2);
        using (var p = new LocalProfile(anyVersion)) {
            var draft = p.GetDraft(anyId);
            var entries = draft.CardEntries.Select(card => new LocalDeckCard { Id = card.Id, Premium = card.Id == anyCard ? 0 : card.Premium }).ToList();
            entries.First(card => card.Id == anyCard).Premium = 1;
            p.SavePremiumDraft(anyId, draft.Name, draft.Hero, entries, draft.SortOrder);
        }
        GoldenOwned(1);
        using (var p = new LocalProfile(anyVersion))
            Check(!p.IsDeckComplete(anyId), "Normal demand cannot reuse the copy a premium entry already spends");

        string packs = Fresh("duplicate", 2000, d => d.GoldBalance = 10000);
        using (var p = new LocalProfile(packs)) {
            var ledger = new LocalCraftingLedger(p);
            Check(ledger.CommitBatch(Legendary, CraftDirection.Craft, 1, 0).Ok
                && ledger.CommitBatch(Legendary, CraftDirection.Disenchant, 1, 1).Ok && p.Acquired[Legendary] == 1,
                "crafted then disenchanted legendary stays acquired");
            p.Purchase("pack-standard", 10, 1000, "packs");
            for (int i = 0; i < 10; i++) Check(!p.OpenPack(FairPackRules.Standard).Cards.Any(c => c.Id == Legendary),
                "pack does not reopen crafted legendary before set completion");
        }
        string settlement = Fresh("settlement", 40);
        using (var p = new LocalProfile(settlement)) {
            var ledger = new LocalCraftingLedger(p); long before = ledger.DustBalance;
            string id = p.Begin("mage", "priest", 1).Id;
            var result = p.Settle(id, "Won", 10, false);
            int rolledDust = GrindTiers.Find(result.GrindTier).Dust(LocalEconomy.RewardV6("Won", 10, false, LocalEconomy.RewardRollSeed(id, 1)).Dust);
            Check(result.RewardVersion == 6 && result.DustReward == rolledDust && ledger.DustBalance == before + result.DustReward, "ledger sees stamped-tier V6 settlement credit live");
            Check(ledger.CommitBatch(Common, CraftDirection.Craft, 1, 0).Ok && ledger.DustBalance == result.DustReward, "craft spends post-settlement balance");
        }
        // #74: ranked milestone craft tokens pay for a Normal craft of their rarity, one copy each, automatically.
        const string Epic = "CATA_493";
        LocalCardTokens Tokens(string tokenPath) => Read(tokenPath).CardTokens;
        void TokenReceipt(string tokenPath, long sequence, string id, LocalTokenRarity rarity) {
            var receipt = Read(tokenPath).LastCraft;
            Check(Read(tokenPath).CraftSequence == sequence && receipt.Sequence == sequence && receipt.Op == LocalCraftOp.TokenCraft
                && receipt.DustDelta == 0 && receipt.TokenRarity == rarity && receipt.TokenDelta == -1 && !receipt.Undoable
                && receipt.Cards.Count == 1 && receipt.Cards[0].CardId == id && receipt.Cards[0].CountDelta == 1
                && receipt.Cards[0].AcquiredBefore == 0 && receipt.Cards[0].Premium == 0, "token craft receipt");
        }
        string tokenPath = Fresh("token-legendary", 100, d => { d.CardTokens.Legendary = 2; d.CardTokens.Epic = 1; });
        string tokenMemory;
        using (var p = new LocalProfile(tokenPath)) {
            var ledger = new LocalCraftingLedger(p);
            Check(ledger.CraftsWithToken(Legendary) && ledger.CraftsWithToken(Epic) && !ledger.CraftsWithToken(Common)
                && !ledger.CraftsWithToken("CORE_EX1_012") && !ledger.CraftsWithToken("EX1_008"), "token eligibility by rarity and craftability");
            var plan = ledger.CommitBatch(Legendary, CraftDirection.Craft, 1, 0);
            Check(plan.Ok && plan.DustDelta == 0 && plan.TokenRarity == LocalTokenRarity.Legendary && ledger.DustBalance == 100
                && ledger.OwnedCount(Legendary) == 1 && p.Acquired[Legendary] == 1 && p.CardTokenCount(LocalTokenRarity.Legendary) == 1,
                "legendary token craft with 100 dust: dust unchanged, one token spent, copy owned");
            TokenReceipt(tokenPath, 1, Legendary, LocalTokenRarity.Legendary);
            Check(Tokens(tokenPath).Legendary == 1 && Tokens(tokenPath).Epic == 1, "only the matching token is spent");
            Check(Read(tokenPath).Achievements.Career.CraftedBaseCardIds.Contains(LocalCardCatalog.CopyGroup(Legendary)),
                "a token craft counts as a craft for achievements");
            tokenMemory = Memory(p);
        }
        using (var p = new LocalProfile(tokenPath)) {
            var ledger = new LocalCraftingLedger(p);
            Check(Memory(p) == tokenMemory, "token craft survives reload and validation");
            // Non-matching rarity: full dust, tokens untouched.
            var plan = ledger.CommitBatch(Common, CraftDirection.Craft, 1, 0);
            Check(plan.Ok && plan.DustDelta == -40 && plan.TokenRarity == null && ledger.DustBalance == 60
                && Tokens(tokenPath).Legendary == 1 && Tokens(tokenPath).Epic == 1, "common craft with only rarer tokens pays dust");
            Receipt(tokenPath, 2, LocalCraftOp.Craft, -40, (Common, 1, 0));
            // Batch rule: a held token covers exactly one copy; a two-copy batch is refused, not part-paid.
            Unchanged(p, tokenPath, () => ledger.CommitBatch(Epic, CraftDirection.Craft, 2, 0));
            Check(ledger.CommitBatch(Epic, CraftDirection.Craft, 1, 0).Ok && ledger.DustBalance == 60 && Tokens(tokenPath).Epic == 0,
                "single epic copy uses the epic token");
            TokenReceipt(tokenPath, 3, Epic, LocalTokenRarity.Epic);
            Unchanged(p, tokenPath, () => ledger.CommitBatch(Epic, CraftDirection.Craft, 1, 1)); // No token left, 60 < 400.
            // No minting: the token-made legendary sells for its ordinary value; no path refunds the token or unpaid dust.
            var sale = ledger.CommitBatch(Legendary, CraftDirection.Disenchant, 1, 1);
            Check(sale.Ok && sale.DustDelta == 800 && sale.TokenRarity == null && ledger.DustBalance == 860
                && Tokens(tokenPath).Legendary == 1 && p.Acquired[Legendary] == 1, "disenchanting a token craft pays the normal value only");
            Receipt(tokenPath, 4, LocalCraftOp.Disenchant, 800, (Legendary, -1, 1));
            Check(ledger.CommitBatch(Legendary, CraftDirection.Craft, 1, 0).Ok && ledger.DustBalance == 860
                && Tokens(tokenPath).Legendary == 0, "the second legendary token recrafts it");
            Check(ledger.CommitBatch(Legendary, CraftDirection.Disenchant, 1, 1).Ok && ledger.DustBalance == 1660, "second sale");
            var paid = ledger.CommitBatch(Legendary, CraftDirection.Craft, 1, 0);
            Check(paid.Ok && paid.DustDelta == -1600 && paid.TokenRarity == null && ledger.DustBalance == 60, "without tokens the full price returns");
            Check(!ledger.CraftsWithToken(Legendary) && Tokens(tokenPath).Legendary == 0, "tokens never go negative or come back");
            tokenMemory = Memory(p);
        }
        using (var p = new LocalProfile(tokenPath)) Check(Memory(p) == tokenMemory, "token history survives reload");
        // Golden: an upgrade with a matching token held pays its full tariff and leaves the token.
        string tokenGolden = Fresh("token-golden", 400, d => {
            d.CardTokens.Common = 1; d.Collection.Add(new LocalCardCount { Id = Common, Count = 1, Acquired = 1 });
        });
        using (var p = new LocalProfile(tokenGolden)) {
            Check(LocalCrafting.CommitUpgrade(p, Common, 1, 1, 0).Ok && p.DustBalance == 40 && Tokens(tokenGolden).Common == 1
                && Read(tokenGolden).LastCraft.TokenRarity == null, "golden upgrade ignores tokens and pays full dust");
            Unchanged(p, tokenGolden, () => LocalCrafting.CommitPremiumBatch(p, Common, 1, CraftDirection.Craft, 1, 1));
            Check(Tokens(tokenGolden).Common == 1, "premium craft refusal keeps the token");
        }
        // Failed writes spend nothing.
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            string failed = Fresh("token-failure-" + stage, 0, d => d.CardTokens.Legendary = 1);
            using var p = new LocalProfile(failed); var ledger = new LocalCraftingLedger(p);
            p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
            Unchanged(p, failed, () => ledger.CommitBatch(Legendary, CraftDirection.Craft, 1, 0));
            p.CommitFailure = null;
            Check(p.CardTokenCount(LocalTokenRarity.Legendary) == 1 && ledger.CommitBatch(Legendary, CraftDirection.Craft, 1, 0).Ok
                && p.CardTokenCount(LocalTokenRarity.Legendary) == 0 && p.DustBalance == 0, "retry after failed token craft spends once");
        }
        // Infinite dust spends no token (the craft is free anyway) and records the ordinary nominal Craft receipt.
        string tokenInfiniteRoot = Path.Combine(root, "token-infinite"); Directory.CreateDirectory(tokenInfiniteRoot);
        string tokenLegacy = Path.Combine(tokenInfiniteRoot, "profile.xml");
        var tokenSlot = ProfileCatalog.Create(tokenLegacy, "Infinite", new ProfileOptions { InfiniteDust = true });
        string tokenInfinite = ProfileCatalog.ProfilePathForId(tokenLegacy, tokenSlot.Id);
        using (var p = new LocalProfile(tokenInfinite)) p.SelectCardBack(0);
        var infiniteData = Read(tokenInfinite); infiniteData.CardTokens.Legendary = 1; Write(tokenInfinite, infiniteData);
        using (var p = new LocalProfile(tokenInfinite)) {
            var ledger = new LocalCraftingLedger(p);
            Check(!ledger.CraftsWithToken(Legendary) && ledger.CommitBatch(Legendary, CraftDirection.Craft, 1, 0).Ok
                && Tokens(tokenInfinite).Legendary == 1 && Read(tokenInfinite).LastCraft.Op == LocalCraftOp.Craft
                && Read(tokenInfinite).LastCraft.DustDelta == -1600 && Read(tokenInfinite).LastCraft.TokenDelta == 0,
                "infinite dust keeps tokens");
        }
        // Saved token receipts must keep their shape.
        foreach (var corruption in new Action<LocalCraftReceipt>[] {
            r => r.DustDelta = -1600, r => r.TokenDelta = 0, r => r.TokenDelta = -2, r => r.TokenRarity = null,
            r => r.Cards[0].CountDelta = 2, r => r.Cards.Add(new LocalCraftCard { CardId = Common, CountDelta = 1 }),
            r => r.Cards[0].Premium = 1 }) {
            var data = Read(tokenPath);
            data.LastCraft = new LocalCraftReceipt { Sequence = data.CraftSequence, Op = LocalCraftOp.TokenCraft,
                TokenRarity = LocalTokenRarity.Legendary, TokenDelta = -1, Cards = new() { new() { CardId = Legendary, CountDelta = 1 } } };
            string goodPath = tokenPath + ".good.xml"; Write(goodPath, data);
            using (var good = new LocalProfile(goodPath)) { }
            corruption(data.LastCraft);
            string badPath = tokenPath + ".bad.xml"; Write(badPath, data);
            bool refused = false;
            try { using var bad = new LocalProfile(badPath); } catch (InvalidOperationException) { refused = true; }
            Check(refused, "malformed token craft receipt refused on load");
        }
        Console.WriteLine("PASS craft tokens: matching rarity free, others and golden full price, one copy per token, no minting, failures, infinite dust, reload, receipts");
        Console.WriteLine("PASS durable crafting: reload, receipts, refusals, failures, mass, lifetime acquisition, packs, decks, settlement");
        Console.WriteLine($"PASS {assertions} crafting ledger checks");
    }

    static int Cap(SabberStoneCore.Model.Card card) => card.Rarity == Rarity.LEGENDARY ? 1 : 2;
}

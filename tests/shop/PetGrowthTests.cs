using System.Text;
using System.Xml.Serialization;
using MiniJSON;

internal static class PetGrowthTests
{
    // Synthetic DBF fixture, never production admission evidence: explicit IDs prove no suffix-to-ID shortcut.
    private sealed class PetFixture : IDisposable {
        private readonly LocalShop previous = LocalShop.TestOverride;
        internal PetFixture(long price = 5000, long matchXp = 25, long[] thresholds = null) {
            thresholds ??= new[] { 0L, 25L, 75L, 150L };
            var original = LocalShop.Default;
            using var resource = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json");
            using var reader = new StreamReader(resource);
            var root = (JsonNode)Json.Deserialize(reader.ReadToEnd());
            var products = (JsonList)root["Products"];
            products.RemoveAll(value => ((JsonNode)value)["Kind"] as string == "Pet");
            products.Add(new JsonNode { ["Id"] = "pet-none", ["Kind"] = "Pet", ["NativeId"] = 0L,
                ["Price"] = 0L, ["MinQuantity"] = 1L, ["MaxQuantity"] = 1L, ["Description"] = "None" });
            foreach (var entry in new[] { ("PET_3_1", 73L), ("PET_4_1", 91L) })
                products.Add(new JsonNode { ["Id"] = "pet-" + entry.Item1, ["Kind"] = "Pet", ["NativeId"] = entry.Item2,
                    ["Pet"] = entry.Item1, ["Price"] = price, ["MinQuantity"] = 1L, ["MaxQuantity"] = 1L, ["Description"] = entry.Item1 });
            root["PetsSource"] = new JsonNode { ["Families"] = new JsonList {
                new JsonNode { ["Pet"] = "PET_3_1", ["NativeId"] = 73L, ["Forms"] = new JsonList { "PET_3_1", "PET_3_2", "PET_3_3", "PET_3_4" },
                    ["Thresholds"] = Thresholds(thresholds), ["MatchXp"] = matchXp },
                new JsonNode { ["Pet"] = "PET_4_1", ["NativeId"] = 91L, ["Forms"] = new JsonList { "PET_4_1", "PET_4_2", "PET_4_3", "PET_4_4" },
                    ["Thresholds"] = Thresholds(thresholds), ["MatchXp"] = matchXp } } };
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Json.Serialize(root)));
            LocalShop.TestOverride = LocalShop.Load(stream, () => original.Library);
        }
        public void Dispose() => LocalShop.TestOverride = previous;
        private static JsonList Thresholds(long[] values) { var list = new JsonList(); foreach (long value in values) list.Add(value); return list; }
    }
    private sealed class ProductionShop : IDisposable {
        private readonly LocalShop previous = LocalShop.TestOverride;
        internal ProductionShop() => LocalShop.TestOverride = null;
        public void Dispose() => LocalShop.TestOverride = previous;
    }
    // Owner rebalancing (price, MatchXp, thresholds, cap) must never make an existing save unloadable.
    private static void RetunedRules(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh,
        string root, string grown)
    {
        check(Read(grown).Matches.Where(m => m.PetXpAfter.HasValue).All(m => m.PetRules == "rule=1;xp=25;cap=150"),
            "settled pet receipts record the growth rules they used");
        string legacy, cheap;
        using (new ProductionShop()) {
            // A V14 receipt settled before stamping: no PetRules, legacy 100 XP per match.
            legacy = fresh("pet-legacy-rules", 5000);
            using (var p = new LocalProfile(legacy)) {
                p.Purchase("pet-PET_3_1", 1, 5000, "legacy-buy");
                var m = p.Begin("mage", "priest", 301); p.Settle(m.Id, "Won", 8, false); p.AcknowledgeRewardPresentation(m.Id);
            }
            var data = Read(legacy); var receipt = data.Matches.Single(m => m.PetXpAfter.HasValue);
            receipt.PetRules = null; receipt.PetXpAfter = 100; data.PetProgress.Single().Xp = 100;
            data.Purchases.Single(r => r.Kind == "Pet").PriceBeforeKit = 5000; Write(legacy, data);
            using (var p = new LocalProfile(legacy)) check(p.PetXp("PET_3_1") == 100, "unstamped V14 pet receipt loads under legacy rules");
            var forged = Read(legacy); forged.Matches.Single(m => m.PetXpAfter.HasValue).PetXpAfter = 101; forged.PetProgress.Single().Xp = 101;
            string bad = Path.Combine(root, "pet-legacy-forged-" + Guid.NewGuid() + ".xml"); Write(bad, forged);
            reject(() => { using var p = new LocalProfile(bad); }, "unstamped receipt still checked against legacy rules");
        }
        // Rebalance: cheaper pet, different MatchXp, thresholds and a cap below the XP already earned.
        using (new PetFixture(price: 4000, matchXp: 40, thresholds: new[] { 0L, 20L, 50L, 60L })) {
            using (var p = new LocalProfile(grown)) {
                check(p.PetXp("PET_3_1") == 75 && p.PetLevel("PET_3_1") == 4 && p.PetLevelXp("PET_3_1") == 0 && p.OwnedPets.Count == 2,
                    "retuned price/XP/thresholds keep a grown pet save loadable");
                p.SelectPet("PET_3_1");
                var m = p.Begin("mage", "priest", 302); var done = p.Settle(m.Id, "Won", 8, false);
                check(done.PetXpBefore == 75 && done.PetXpAfter == 75 && done.PetRules == "rule=1;xp=40;cap=60",
                    "a lowered cap never takes earned pet XP away; new receipts record the new rules");
                p.AcknowledgeRewardPresentation(m.Id);
            }
            using (var p = new LocalProfile(grown)) check(p.PetXp("PET_3_1") == 75, "mixed-rule pet history reopens");
            using (var p = new LocalProfile(legacy)) check(p.PetXp("PET_3_1") == 100 && p.Purchases.Count == 1, "legacy receipt loads after retune");
            cheap = fresh("pet-retuned-buy", 4000);
            using (var p = new LocalProfile(cheap)) {
                p.Purchase("pet-PET_4_1", 1, 4000, "cheap");
                check(p.Gold == 0 && p.PetXp("PET_4_1") == 0, "new price applies to new purchases");
            }
        }
        // Back on the original rules: receipts bought at the retuned price and stamped with retuned XP still load.
        using (var p = new LocalProfile(grown)) check(p.PetXp("PET_3_1") == 75, "history from a later tuning reopens under the old one");
        using (var p = new LocalProfile(cheap)) check(p.OwnedPets.Contains("PET_4_1"), "a pet bought at an older price still loads");
    }
    private static readonly XmlSerializer Serializer = new(typeof(LocalProfileData));
    private static LocalProfileData Read(string path) { using var stream = File.OpenRead(path); return (LocalProfileData)Serializer.Deserialize(stream); }
    private static void Write(string path, LocalProfileData data) { using var stream = File.Create(path); Serializer.Serialize(stream, data); }
    // The ledger machinery below (purchase, freeze, replay, thresholds, caps, upgrade) is exercised under the flat rule 1
    // (preset C), which keeps every old assertion verbatim and covers the legacy rule path. Event rules follow.
    internal static void Run(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh, string root)
    {
        var previous = PetXpPolicy.Current;
        try { PetXpPolicy.Current = PetXpPolicy.PresetC; RunFlat(check, reject, fresh, root); }
        finally { PetXpPolicy.Current = previous; }
        check(ReferenceEquals(PetXpPolicy.Current, PetXpPolicy.PresetB), "owner ruling: the default policy is preset B");
        PetEventXp.Run(check, reject, fresh, root);
    }
    private static void RunFlat(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh, string root)
    {
        using var fixture = new PetFixture();
        var shop = LocalShop.Default;
        var offer = shop.Product("pet-PET_3_1");
        check(NativeShopPurchase.PmtId(offer) == 1970000073L && NativeShopPurchase.NativeItemId(offer) == 73,
            "pet PMT/native identity uses explicit DBF family ID, never card suffix");
        var sections = NativeBundleHarness.BuildSections();
        check(sections.Any(section => section.InternalName == "pet_list") && sections.SelectMany(section => section.ProductOrder)
            .Count(row => NativeShopPurchase.Product(row.Item1).Kind == "Pet") == 3, "native Skins > Pets sub-tab includes both families and free None exactly once");
        NativeBundleHarness.Run(check);
        NativePetsHarness.Run(check, fresh);
        string poor = fresh("pet-poor", 4999);
        using (var p = new LocalProfile(poor)) {
            byte[] before = File.ReadAllBytes(poor);
            reject(() => p.Purchase(offer.Id, 1, 5000, "poor"), "pet costs5000");
            reject(() => p.SelectPet("PET_3_1"), "unowned pet selection refused");
            check(before.SequenceEqual(File.ReadAllBytes(poor)), "pet refusals preserve bytes");
        }
        string deckPath = fresh("pet-deck-snapshot", 100000);
        string pendingDeckId;
        using (var p = new LocalProfile(deckPath)) {
            p.Purchase("pet-PET_3_1", 1, 5000, "pet3");
            p.Purchase("pet-PET_4_1", 1, 5000, "pet4");
            var product = shop.Product("deck-burn-mage");
            long deckId = p.Purchase(product.Id, 1, p.PurchasePrice(product.Id), "deck").Granted.DeckId.Value;
            var deck = p.GetDraft(deckId);
            p.SaveDraft(deck.Id, deck.Name, deck.Hero, deck.Cards, deck.SortOrder, deck.Sideboards,
                new LocalDeckCosmetics { PetFamily = "PET_3_1", PetForm = "PET_3_1" });
            var pending = p.Begin("custom:" + deckId, "priest", 42);
            check(pending.Pet == "PET_3_1" && pending.PetXpBefore == 0, "deck pet and owned family XP snapshotted");
            p.SelectPet("PET_4_1");
            pendingDeckId = pending.Id;
        }
        using (var p = new LocalProfile(deckPath)) {
            check(p.Pending?.Pet == "PET_3_1", "deck pet snapshot survives restart");
            var result = p.Settle(pendingDeckId, "Lost", 4, false);
            check(result.Pet == "PET_3_1" && p.PetXp("PET_3_1") == 25 && p.PetXp("PET_4_1") == 0,
                "deck pet growth uses snapshotted family after favorite changes");
        }
        string path = fresh("pet-growth", 10000);
        string first;
        using (var p = new LocalProfile(path)) {
            var draft = p.CreateDraft("Pet test", "HERO_08");
            byte[] beforeDraft = File.ReadAllBytes(path);
            reject(() => p.SaveDraft(draft.Id, draft.Name, draft.Hero, draft.Cards, draft.SortOrder,
                cosmetics: new LocalDeckCosmetics { PetFamily = "PET_3_1", PetForm = "PET_3_1" }), "unowned deck pet refused");
            check(beforeDraft.SequenceEqual(File.ReadAllBytes(path)), "unowned deck pet preserves bytes");
            var receipt = p.Purchase(offer.Id, 1, 5000, "buy");
            check(receipt.Granted.Pet == "PET_3_1" && p.SelectedPet == "PET_3_1" && p.PetXp("PET_3_1") == 0
                && p.PetLevel("PET_3_1") == 1 && p.Gold == 5000, "pet purchase atomically owns, equips and starts level1");
            byte[] bought = File.ReadAllBytes(path);
            check(p.Purchase(offer.Id, 1, 5000, "buy").Sequence == 1 && bought.SequenceEqual(File.ReadAllBytes(path)), "pet purchase replay no debit/write");
            reject(() => p.Purchase(offer.Id, 1, 5000, "again"), "family cannot be bought twice");
            reject(() => p.SelectPet("PET_3_2"), "locked growth form refused");
            reject(() => p.SaveDraft(draft.Id, draft.Name, draft.Hero, draft.Cards, draft.SortOrder,
                cosmetics: new LocalDeckCosmetics { PetFamily = "PET_3_1", PetForm = "PET_3_2" }), "locked deck pet form refused");
            p.SaveDraft(draft.Id, draft.Name, draft.Hero, draft.Cards, draft.SortOrder,
                cosmetics: new LocalDeckCosmetics { PetFamily = "PET_3_1", PetForm = "PET_3_1" });
            check(p.GetDraft(draft.Id).PetForm == "PET_3_1", "owned unlocked deck pet accepted");
            p.Purchase("pet-PET_4_1", 1, 5000, "buy-other"); p.SelectPet("PET_3_1");
            var pending = p.Begin("mage", "priest", 81); first = pending.Id;
            check(pending.Pet == "PET_3_1" && pending.PetXpBefore == 0 && pending.PetXpAfter == null, "match freezes pet and beforeXP");
            p.SelectPet("PET_4_1");
            var settled = p.Settle(first, "Lost", 4, false);
            check(settled.PetXpAfter == 25 && p.PetXp("PET_3_1") == 25 && p.PetXp("PET_4_1") == 0,
                "natural loss grows frozen family independent of current selection/currency");
            check(p.CanUsePetForm("PET_3_2") && !p.CanUsePetForm("PET_3_3") && p.PetLevelXp("PET_3_1") == 0, "first exact threshold unlocks second form");
            byte[] settledBytes = File.ReadAllBytes(path);
            p.Settle(first, "Lost", 4, false);
            check(settledBytes.SequenceEqual(File.ReadAllBytes(path)), "pet settlement replay is byte-exact");
            settled.PetXpAfter = 999; p.PetProgress[0].Xp = 999;
            check(p.History.Single().PetXpAfter == 25 && p.PetXp("PET_3_1") == 25, "pet progress/receipt copies cannot mutate ledger");
            p.AcknowledgeRewardPresentation(first); p.SelectPet("PET_3_2");
            foreach (var outcome in new[] { ("Won", 3, false), ("Lost", 8, true), ("Tied", 8, false), ("Won", 8, false) }) {
                var m = p.Begin("mage", "priest", 82);
                int before = p.PetXp("PET_3_1");
                var done = p.Settle(m.Id, outcome.Item1, outcome.Item2, outcome.Item3);
                int gain = !outcome.Item3 && outcome.Item2 >= 4 ? 25 : 0;
                check(done.PetXpBefore == before && done.PetXpAfter == before + gain, "deterministic pet play eligibility");
                p.AcknowledgeRewardPresentation(m.Id);
            }
            p.Begin("mage", "priest", 83); p.RecoverInterrupted(); p.RecoverInterrupted();
            check(p.PetXp("PET_3_1") == 75 && p.SelectedPet == "PET_3_2", "abandon never grows or autoequips a newly unlocked form");
        }
        using (var p = new LocalProfile(path)) {
            check(p.PetXp("PET_3_1") == 75 && p.SelectedPet == "PET_3_2" && p.OwnedPets.Count == 2, "pet ownershipXPselection survive restart");
            p.SelectPet(null); var m = p.Begin("mage", "priest", 84); p.SelectPet("PET_3_1");
            var done = p.Settle(m.Id, "Won", 8, false);
            check(done.Pet == null && done.PetXpBefore == null && p.PetXp("PET_3_1") == 75, "None at match start cannot earn later selected petXP");
            p.AcknowledgeRewardPresentation(m.Id);
        }
        foreach (int xp in new[] { 0,24,25,26,74,75,76,149,150 }) {
            int level = xp < 25 ? 1 : xp < 75 ? 2 : xp < 150 ? 3 : 4;
            check(shop.PetLevel("PET_3_1", xp) == level && shop.PetLevelXp("PET_3_1", xp) == (level == 4 ? 0 : xp - new[] {0,25,75}[level-1]),
                "native level and remainder exact around every threshold");
        }
        string capped = fresh("pet-growth-cap", 5000);
        using (var p = new LocalProfile(capped)) {
            p.Purchase(offer.Id, 1, 5000, "cap-buy");
            for (int i = 0; i < 8; i++) {
                var m = p.Begin("mage", "priest", 100 + i); var done = p.Settle(m.Id, "Lost", 4, false);
                check(done.PetXpBefore == Math.Min(150, i * 25) && done.PetXpAfter == Math.Min(150, (i + 1) * 25), "settlement crosses and caps exact petXP");
                p.Settle(m.Id, "Lost", 4, false); p.AcknowledgeRewardPresentation(m.Id);
            }
            check(p.PetXp("PET_3_1") == 150 && p.PetLevel("PET_3_1") == 4 && p.PetLevelXp("PET_3_1") == 0
                && p.CanUsePetForm("PET_3_4") && p.SelectedPet == "PET_3_1", "max-level settlement caps XP without autoequipping new form");
        }
        foreach (string point in new[] { "write", "flush", "replace" }) {
            string failed = fresh("pet-fail-" + point, 5000); byte[] before = File.ReadAllBytes(failed);
            using (var p = new LocalProfile(failed)) {
                p.CommitFailure = at => { if (at == point) throw new IOException("injected " + at); };
                reject(() => p.Purchase(offer.Id, 1, 5000, "retry"), "pet purchase atomic " + point);
                check(before.SequenceEqual(File.ReadAllBytes(failed)) && p.OwnedPets.Count == 0 && p.PetProgress.Count == 0, "failed pet purchase no ownershipXPdebit");
                p.CommitFailure = null; p.Purchase(offer.Id, 1, 5000, "retry");
                var m = p.Begin("mage", "priest", 85); before = File.ReadAllBytes(failed);
                p.CommitFailure = at => { if (at == point) throw new IOException("injected " + at); };
                reject(() => p.Settle(m.Id, "Won", 8, false), "pet XP atomic " + point);
                check(before.SequenceEqual(File.ReadAllBytes(failed)) && p.PetXp("PET_3_1") == 0, "failed settlement noXP");
                p.CommitFailure = null; p.Settle(m.Id, "Won", 8, false); p.Settle(m.Id, "Won", 8, false);
                check(p.PetXp("PET_3_1") == 25, "pet XP failed retry exactly once");
            }
        }
        foreach (var damage in new Action<LocalProfileData>[] {
            d => d.PetProgress[0].Xp++, d => d.PetProgress.Add(d.PetProgress[0]), d => d.SelectedPet = "UNKNOWN",
            d => d.Matches[0].PetXpAfter++, d => d.Matches[0].PetXpBefore = null, d => d.OwnedPets.Add("PET_3_1") }) {
            var data = Read(path); damage(data); string bad = Path.Combine(root, "pet-forged-" + Guid.NewGuid() + ".xml"); Write(bad, data);
            reject(() => { using var p = new LocalProfile(bad); }, "forged pet ledger rejected");
        }
        string legacy = fresh("pet-v13", 9876); var v13 = Read(legacy); v13.Version = 13; Write(legacy, v13); byte[] original = File.ReadAllBytes(legacy);
        reject(() => { try {
            typeof(LocalProfile).GetConstructor(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(bool) }, null).Invoke(new object[] { legacy, true });
        } catch (System.Reflection.TargetInvocationException error) when (error.InnerException is InvalidOperationException) {
            throw new InvalidOperationException(error.InnerException.Message);
        } }, "V13 snapshot cannot migrate");
        string stale = fresh("pet-v14-snapshot", 100); var v14 = Read(stale); v14.Version = 14; Write(stale, v14);
        reject(() => { try {
            typeof(LocalProfile).GetConstructor(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(bool) }, null).Invoke(new object[] { stale, true });
        } catch (System.Reflection.TargetInvocationException error) when (error.InnerException is InvalidOperationException) {
            throw new InvalidOperationException(error.InnerException.Message);
        } }, "V14 read-only snapshot requires the current version and never migrates");
        check(Read(stale).Version == 14 && !File.Exists(stale + ".v14.bak"), "refused V14 snapshot left the file and made no backup");
        using (var p = new LocalProfile(legacy)) check(p.Gold == 9876 && p.OwnedPets.Count == 0 && Read(legacy).Version == 15, "explicit V13 upgrade to current V15 preserves existing state");
        check(original.SequenceEqual(File.ReadAllBytes(legacy + ".v13.bak")), "V13 upgrade keeps byte-exact original backup");
        string populated = fresh("pet-v13-populated", 5000);
        using (var p = new LocalProfile(populated)) {
            string set = FairPackRules.PackTypes.Where(type => type != FairPackRules.Standard && type != FairPackRules.Legacy)
                .OrderBy(type => FairPackRules.Eligible(type, FairPackRules.RuleVersion).Count).First();
            var sets = FairPackRules.Eligible(set, FairPackRules.RuleVersion).Select(card => card.Set).ToHashSet();
            var roster = LocalShop.StandardCollectionCards.Where(card => !LocalCardCatalog.IsCore(card.Id) && sets.Contains(card.Set))
                .GroupBy(card => LocalCardCatalog.CopyGroup(card.Id)).Select(group => group.First()).ToArray();
            p.CommitChange(next => {
                foreach (var card in roster) {
                    var row = next.Collection.SingleOrDefault(row => row.Id == card.Id);
                    if (row == null) { row = new LocalCardCount { Id = card.Id }; next.Collection.Add(row); }
                    row.Count = row.Acquired = LocalEconomy.PlayableCap(card);
                }
                LocalProfile.ReconcileCollectionAchievement(next); return true;
            });
            check(p.Achievements.Unlocks.Count > 0, "migration fixture contains an earned achievement");
            p.Purchase("pack-standard", 1, 100, "old-purchase");
            var finished = p.Begin("mage", "priest", 201); p.Settle(finished.Id, "Lost", 4, false); p.AcknowledgeRewardPresentation(finished.Id);
            p.Begin("mage", "priest", 202);
        }
        var populatedV13 = Read(populated); populatedV13.Version = 13; Write(populated, populatedV13);
        byte[] populatedOriginal = File.ReadAllBytes(populated);
        using (var p = new LocalProfile(populated)) {
            check(p.Pending != null && p.History.Count == 1 && p.Purchases.Count == 1 && p.Achievements.Unlocks.Count > 0,
                "V13 upgrade retains pending match, history, purchases and earned achievement");
        }
        var upgraded = Read(populated); upgraded.Version = 13;
        using (var before = new MemoryStream()) using (var after = new MemoryStream()) {
            Serializer.Serialize(before, populatedV13); Serializer.Serialize(after, upgraded);
            check(before.ToArray().SequenceEqual(after.ToArray()), "populated V13 upgrade changes no existing serialized value");
        }
        check(populatedOriginal.SequenceEqual(File.ReadAllBytes(populated + ".v13.bak")), "populated V13 backup preserves exact original bytes");
        RetunedRules(check, reject, fresh, root, path);
        Console.WriteLine("PASS pet families: purchase/replay/rollback, frozen match growth, threshold projection, strict receipts, restart and V13 upgrade");
    }
}

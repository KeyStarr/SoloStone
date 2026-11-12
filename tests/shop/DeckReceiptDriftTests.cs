using SabberStoneCore.Enums;
using SabberStoneCore.Model;

// A deck purchase receipt records what was paid. A later rarity correction (copy price) or Standard-legality change
// must never make it fail to load. Both are simulated in-process by editing the engine card tags after purchase.
internal static class DeckReceiptDriftTests
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh)
    {
        string path = fresh("deck-receipt-drift", 100000);
        LocalPurchaseReceipt receipt;
        using (var p = new LocalProfile(path)) {
            var product = LocalShop.Default.Product("deck-burn-mage");
            receipt = p.Purchase(product.Id, 1, p.PurchasePrice(product.Id), "drift");
        }
        var sold = receipt.Granted.Cards.Where(c => !LocalCardCatalog.IsCore(c.CardId)).Select(c => Cards.FromId(c.CardId)).ToArray();
        var repriced = sold.FirstOrDefault(card => card.Rarity == Rarity.COMMON || card.Rarity == Rarity.RARE);
        var rotated = sold.FirstOrDefault(card => card != repriced);
        check(repriced != null && rotated != null, "deck receipt fixture sells no non-Core common/rare plus a second card");
        int rarity = repriced.Tags[GameTag.RARITY], set = rotated.Tags[GameTag.CARD_SET];
        try {
            // Same playset (2), different copy price: the old validator recomputed the price and refused the save.
            repriced.Tags[GameTag.RARITY] = (int)(repriced.Rarity == Rarity.COMMON ? Rarity.RARE : Rarity.COMMON);
            rotated.Tags[GameTag.CARD_SET] = (int)CardSet.EXPERT1;
            check(!LocalShop.IsStandardCard(rotated), "Standard rotation fixture still Standard");
            using (var p = new LocalProfile(path))
                check(p.Purchases.Single().PriceBeforeKit == receipt.PriceBeforeKit && p.OwnedCount(repriced.Id) >= 1,
                    "rarity or Standard change made a deck purchase receipt unloadable");
            byte[] bytes = File.ReadAllBytes(path);
            var forged = File.ReadAllText(path).Replace("<GoldPaid>" + receipt.GoldPaid + "</GoldPaid>", "<GoldPaid>" + (receipt.GoldPaid + 1) + "</GoldPaid>");
            check(forged != File.ReadAllText(path), "deck receipt forgery fixture did not apply");
            string bad = path + ".forged.xml"; File.WriteAllText(bad, forged);
            reject(() => { using var p = new LocalProfile(bad); }, "deck receipt paid more gold than its recorded price");
            check(bytes.SequenceEqual(File.ReadAllBytes(path)), "drift reopen rewrote the save");
        } finally {
            repriced.Tags[GameTag.RARITY] = rarity; rotated.Tags[GameTag.CARD_SET] = set;
        }
        Console.WriteLine("PASS deck purchase receipts: rarity/price and Standard-legality drift still load; paid-gold forgery rejected");
        CollectionRarityDrift(check, reject, fresh);
    }

    // An epic pair (crafted, so it is also in the craft receipt, and kept in a stored deck) later corrected to
    // legendary keeps both copies: collection, craft and stored-deck validation cap at two, not today's playset.
    private static void CollectionRarityDrift(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh)
    {
        var epic = LocalShop.StandardCollectionCards.Where(card => card.Rarity == Rarity.EPIC && !LocalCardCatalog.IsCore(card.Id)
                && LocalCrafting.GetIneligibility(card.Id) == CraftIneligibility.None
                && (card.Class == CardClass.MAGE || card.Class == CardClass.NEUTRAL) && card.Type == CardType.MINION)
            .OrderBy(card => card.Id, StringComparer.Ordinal).First();
        string path = fresh("collection-rarity-drift", 0);
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(LocalProfileData));
        LocalProfileData data; using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
        data.DustBalance = 10000; using (var file = File.Create(path)) serializer.Serialize(file, data);
        long deckId;
        using (var p = new LocalProfile(path)) {
            var crafted = new LocalCraftingLedger(p).CommitBatch(epic.Id, CraftDirection.Craft, 2, 0);
            check(crafted.Ok && p.OwnedCount(epic.Id) == 2, "epic pair fixture craft failed: " + crafted.Error);
            var draft = p.CreateDraft("Drift", "HERO_08"); deckId = draft.Id;
            p.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { epic.Id, epic.Id }, draft.SortOrder);
        }
        int rarity = epic.Tags[GameTag.RARITY];
        try {
            epic.Tags[GameTag.RARITY] = (int)Rarity.LEGENDARY;
            byte[] bytes = File.ReadAllBytes(path);
            using (var p = new LocalProfile(path))
                check(p.OwnedCount(epic.Id) == 2 && p.GetDraft(deckId).Cards.Count(id => id == epic.Id) == 2,
                    "epic pair corrected to legendary lost copies or blocked load");
            check(bytes.SequenceEqual(File.ReadAllBytes(path)), "rarity drift reopen rewrote the save");
            LocalProfileData forged; using (var file = File.OpenRead(path)) forged = (LocalProfileData)serializer.Deserialize(file)!;
            forged.Collection.Single(row => row.Id == epic.Id).Acquired = 3;
            string bad = path + ".acquired3.xml"; using (var file = File.Create(bad)) serializer.Serialize(file, forged);
            reject(() => { using var p = new LocalProfile(bad); }, "acquired above the largest playset");
        } finally { epic.Tags[GameTag.RARITY] = rarity; }
        Console.WriteLine("PASS collection rarity drift: epic pair corrected to legendary keeps both copies in collection, craft receipt and stored deck");
        PackAfterRarityDrift(check, fresh);
    }

    // After the correction ships (pack manifest regenerated), the card sits in the legendary bucket while the save
    // holds an acquired pair. A guaranteed-legendary pack of that set must open instead of soft-locking.
    private static void PackAfterRarityDrift(Action<bool, string> check, Func<string, int, string> fresh)
    {
        string type = FairPackRules.PackTypes.First(t => t != FairPackRules.Standard && t != FairPackRules.Legacy);
        var legendary = FairPackRules.Eligible(type, FairPackRules.RuleVersion).Where(card => card.Rarity == Rarity.LEGENDARY)
            .OrderBy(card => card.Id, StringComparer.Ordinal).First();
        string path = fresh("pack-rarity-drift", 0);
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(LocalProfileData));
        LocalProfileData data; using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
        data.Collection.Add(new LocalCardCount { Id = legendary.Id, Count = 2, Acquired = 2 });
        var counters = data.PackCounters.Single(row => row.PackType == type).Counters;
        counters.PacksOpened = 9; counters.PacksSinceLegendary = 9; counters.PacksSinceEpic = 0; counters.FirstLegendaryReceived = false;
        data.PackSequence = Math.Max(data.PackSequence, 9);
        var unopened = data.UnopenedPacks.SingleOrDefault(row => row.PackType == type);
        if (unopened == null) data.UnopenedPacks.Add(new LocalPackCount { PackType = type, Count = 1 }); else unopened.Count = 1;
        using (var file = File.Create(path)) serializer.Serialize(file, data);
        using (var p = new LocalProfile(path)) {
            var pack = p.OpenPack(type);
            check(pack.Cards.Count == 5 && pack.Cards.Any(card => Cards.FromId(card.Id).Rarity == Rarity.LEGENDARY)
                && pack.Cards.All(card => card.Id != legendary.Id) && p.OwnedCount(legendary.Id) == 2,
                "guaranteed-legendary pack after a rarity correction did not open or redrew the full pair");
        }
        using (var p = new LocalProfile(path)) check(p.OwnedCount(legendary.Id) == 2, "pack after rarity drift did not reopen");
        Console.WriteLine("PASS pack after rarity drift: legendary bucket with an acquired pair opens and reopens");
    }
}

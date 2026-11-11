using System.Xml.Serialization;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;

int assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
void Reject(Action action, string message) {
    bool rejected = false;
    try { action(); } catch (InvalidOperationException) { rejected = true; } catch (IOException) { rejected = true; } catch (InvalidDataException) { rejected = true; }
    Check(rejected, message);
}
var serializer = new XmlSerializer(typeof(LocalProfileData));
LocalProfileData Read(string path) { using var file = File.OpenRead(path); return (LocalProfileData)serializer.Deserialize(file)!; }
void Write(string path, LocalProfileData data) { using var file = File.Create(path); serializer.Serialize(file, data); }
string root = Path.Combine(Path.GetTempPath(), "solostone-shop-" + Guid.NewGuid()); Directory.CreateDirectory(root);
string Fresh(string name, int gold = 100000) {
    string path = Path.Combine(root, name + ".xml");
    using (var p = new LocalProfile(path)) p.SelectCardBack(0);
    var data = Read(path); data.GoldBalance = gold; WelcomeGiftTests.OwnGrantOnly(data); Write(path, data); return path;
}
try {
    if (args.Contains("--crafting-only")) { CraftingLedgerTests.Run(root); return; }
    if (args.Contains("--perf-only")) { ProfilePerformanceTests.Run(root); return; }
    PetGrowthTests.Run(Check, Reject, Fresh, root);
    if (args.Contains("--pets-only")) { Console.WriteLine($"PASS {assertions} pet checks"); return; }
    CoinTests.Run(root, Check, Reject, Fresh);
    NativeShopLedgerTests.Run(Check, Reject, Fresh);
    NativeShopArtTests.Run(Check);
    DeckReceiptDriftTests.Run(Check, Reject, Fresh);
    AzalinaDeckPurchaseTests.Run(Check, Reject, Fresh);
    FabledOwnershipTests.Run(Check, Reject, Fresh);
    UnderbellyOwnershipTests.Run(Check, Fresh);
    if (args.Contains("--native-only")) { Console.WriteLine($"PASS {assertions} native shop checks"); return; }
    ProfileCopyTests.Run(Check);
    FavoriteCardBackTests.Run(Check, Reject, Fresh);
    V13FixtureMigrationTests.Run(Check, root);
    QuoteMemoTests.Run(Check, Reject, Fresh);
    ShopJsonTests.Run(Check, Reject);
    PendingCosmeticsTests.Run(root);
    AchievementExclusiveCosmeticTests.Run(root, Check, Reject);
    EarnOnlyPortraitTests.Run(root, Check, Reject);
    LegacyCosmeticsTests.Run(root);
    TieredCosmeticsTests.Run(root, Check, Reject);
    RecipeAvailabilityTests.Run(root);
    using var cosmetics = new CosmeticsFixture();
    CatalogueIndexTests.Run(Check);
    ProfilePerformanceTests.Run(root);
    V10ReservationTests.Run(root);
    WelcomeGiftTests.Run(Check, Reject, root);
    ShopControlTests.Run(root);
    string mixedPath = Fresh("mixed-draft");
    using (var p = new LocalProfile(mixedPath)) {
        var cards = LocalDecks.Get("mage").CardIds;
        cards[0] = "CATA_452";
        p.SaveDeck("Mixed draft", "HERO_08", cards);
        Check(p.CustomDeck.Cards.Contains("CS2_029") && p.CustomDeck.Cards.Contains("CATA_452"), "mixed legacy and new-set draft saved");
        Reject(() => LocalShop.ValidateDeck("Legacy recipe", "HERO_08", cards, null, true), "shop recipes remain Standard-only");
        Reject(() => LocalProfile.LedgerDeck("Wrong class", "HERO_01", cards, null, true), "expanded draft still validates class");
        Reject(() => LocalProfile.LedgerDeck("Too many copies", "HERO_08", new[] { "CATA_452", "CATA_452", "CATA_452" }, null, false), "expanded draft still validates copy limits");
    }
    using (var p = new LocalProfile(mixedPath))
        Check(p.CustomDeck.Cards.Contains("CS2_029") && p.CustomDeck.Cards.Contains("CATA_452"), "mixed draft survives reload");
    Check(typeof(LocalMatchRecord).GetField("Ladder") != null, "settlement must reserve durable ladder receipt");
    var library = RecipeLibrary.LoadText(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "recipes-v1.json")));
    var shop = LocalShop.Default;
    Check(shop.Products.Where(p => p.Kind == "HeroSkin").All(p => p.Id == "heroskin-" + p.HeroSkin), "real stable hero product IDs replace placeholders");
    NoPackGrantTests.Run(root);
    CraftingLedgerTests.Run(root);
    HubCosmeticsTests.Run(root, Check, Reject);
    Check(shop.Products.Count(p => p.Kind == "Pack") == FairPackRules.PackTypes.Count && shop.Recipes.Count == library.Recipes.Count, "catalogue dimensions");
    Check(!FairPackRules.IsKnown("BE") && FairPackRules.NewestSet == "ESCAPEFROM_VIOLET_HOLD", "pre-BE release meta");
    var expectedTypes = new[] { "STANDARD" }.Concat(LocalCardCatalog.PackEligibleSets);
    Check(FairPackRules.PackTypes.SequenceEqual(expectedTypes), "pack types come from active pool");
    Check(FairPackRules.PackTypes.Count == 6 && FairPackRules.NewestSet == LocalCardCatalog.ActiveMetaNewestSet,
        "STANDARD plus five pre-BE sets and newest set agree with pool policy");
    foreach (var booster in new[] { ("STANDARD", 713), ("EMERALD_DREAM", 975), ("THE_LOST_CITY", 982),
        ("TIME_TRAVEL", 989), ("CATACLYSM", 1030), ("ESCAPEFROM_VIOLET_HOLD", 1047) })
        Check(FairPackRules.NativeBoosterId(booster.Item1) == booster.Item2, "pinned booster ID " + booster.Item1);
    foreach (string type in FairPackRules.PackTypes)
        Check(FairPackRules.Eligible(type, 1).Count >= 5 && !FairPackRules.Eligible(type, 1).Any(c => c.Id.StartsWith("BE_")), "nonempty pre-BE pack pool " + type);
    foreach (var recipe in shop.Recipes) {
        Check(library.Validate(recipe).Count == 0 && recipe.Cards.All(c => Cards.FromId(c.Id).Implemented), "recipe legal and registered");
    }
    foreach (var product in shop.Products.Where(p => !shop.Withheld(p) && !((p.Kind == "HubTheme" || p.Kind == "Board" || p.Kind == "Pet") && p.Price == 0))) {
        string path = Fresh("kind-" + product.Id);
        LocalPurchaseReceipt receipt;
        using (var p = new LocalProfile(path)) {
            int quantity = product.Kind == "Pack" ? 50 : 1;
            int price = p.PurchasePrice(product.Id, quantity);
            int expected = product.Price * quantity;
            RecipeDeckBuild build = null;
            if (product.Kind == "Deck") {
                build = library.BuildDeck(shop.Recipe(product.RecipeId), RecipeLibrary.OwnedCount(p.Collection));
                expected = shop.Products.Single(row => row.Kind == "Recipe" && row.RecipeId == product.RecipeId).Price
                    + build.Copies.Where(c => LocalShop.DeckGrant(c) > 0).Sum(c => LocalShop.DeckGrant(c) * LocalShop.CopyPrice(c.Rarity));
                // Only a Core card with no unlock schedule entry (CORE_BT_187 before schedule v8) has no grant path.
                Check(build.Copies.All(c => LocalShop.DeckGrant(c) == c.Missing || (LocalCardCatalog.IsCore(c.Id) && c.UnlockLevel == null)),
                    "every scheduled missing copy is sold " + product.Id);
            }
            Check(price == expected, "missing-copy price " + product.Id);
            receipt = p.Purchase(product.Id, quantity, price, "request");
            Check(p.Gold == 100000 - price && p.PurchaseSequence == 1, "charge " + product.Id);
            if (product.Kind == "Pack") {
                Check(p.PackInventory.Single().PackType == product.PackType && p.PackInventory.Single().Count == 50, "bulk typed inventory +50 " + product.Id);
                Check(receipt.Granted.Packs.Single().PackType == product.PackType && receipt.Granted.Packs.Single().Count == 50, "bulk typed receipt +50 " + product.Id);
            }
            byte[] bytes = File.ReadAllBytes(path);
            Check(p.Purchase(product.Id, quantity, price, "request").Sequence == 1 && File.ReadAllBytes(path).SequenceEqual(bytes), "replay " + product.Id);
            Reject(() => p.Purchase(product.Id, quantity, price + 1, "request"), "conflicting request");
            Reject(() => p.Purchase(product.Id, quantity + 1, price, "request"), "conflicting quantity");
            Reject(() => p.Purchase("unknown", quantity, price, "request"), "conflicting product");
            if (product.Kind != "Pack") Reject(() => p.Purchase(product.Id, 1, p.PurchasePrice(product.Id), "again"), "already owned " + product.Id);
            if (product.Kind == "Deck") {
                Check(p.IsDeckComplete(receipt.Granted.DeckId!.Value) == build!.Copies.All(c => LocalShop.DeckGrant(c) == c.Missing),
                    "ready-made deck is playable at once " + product.Id);
                Check(receipt.Granted.Cards.Sum(c => c.CountAfter - c.CountBefore) == build!.Copies.Sum(LocalShop.DeckGrant), "grants equal missing copies, locked Core included");
                Check(receipt.Granted.Cards.All(c => c.CountAfter <= LocalEconomy.PlayableCap(Cards.FromId(c.CardId))), "grants never exceed playset");
                var saved = p.GetDraft(receipt.Granted.DeckId.Value);
                Check(saved.Hero == build.Deck.Hero && saved.Cards.SequenceEqual(build.CardIds) && saved.SourceRecipeId == product.RecipeId,
                    "purchase saves full library deck and recipe origin");
                Check(receipt.Granted.Cards.All(c => p.Collection[c.CardId] == c.CountAfter && (LocalCardCatalog.IsCore(c.CardId)
                    ? !p.Acquired.ContainsKey(c.CardId) && c.AcquiredBefore == c.CountBefore : p.Acquired[c.CardId] == c.CountAfter)), "deck acquired grants; Core stays out of Acquired");
            }
            if (product.Kind == "CardBack") p.SelectCardBack(product.CardBack!.Value);
            if (product.Kind == "HeroSkin") Check(p.FavoriteHero(product.Class) == product.HeroSkin, "first purchased skin auto-equips " + product.Id);
            receipt.Granted.Cards.Clear(); // Returned receipts cannot mutate durable state.
        }
        using (var p = new LocalProfile(path)) {
            int gold = p.Gold;
            var stored = p.Purchases.Single();
            if (product.Kind == "Pack") {
                Check(p.PackInventory.Single().PackType == product.PackType && p.PackInventory.Single().Count == 50, "bulk typed inventory +50 after restart " + product.Id);
                Check(stored.Granted.Packs.Single().PackType == product.PackType && stored.Granted.Packs.Single().Count == 50, "bulk typed receipt +50 after restart " + product.Id);
            }
            Check(p.Purchase(product.Id, stored.Quantity, stored.GoldPaid, "request").Sequence == 1 && p.Gold == gold, "crash/restart replay " + product.Id);
            if (product.Kind == "CardBack") Check(p.FavoriteCardBacks.Single() == product.CardBack, "selection persisted");
            if (product.Kind == "HeroSkin") Check(p.FavoriteHeroSkins[product.Class] == product.HeroSkin, "favourite persisted");
        }
    }
    foreach (string kind in shop.Products.Select(p => p.Kind).Distinct())
    foreach (string stage in new[] { "write", "flush", "replace" }) {
        var product = shop.Products.First(p => p.Kind == kind && !((p.Kind == "HubTheme" || p.Kind == "Board" || p.Kind == "Pet") && p.Price == 0));
        string path = Fresh("fail-" + kind + stage); byte[] bytes = File.ReadAllBytes(path);
        using (var p = new LocalProfile(path)) {
            int price = p.PurchasePrice(product.Id);
            p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + point); };
            Reject(() => p.Purchase(product.Id, 1, price, "retry"), "failure " + kind + stage);
            Check(File.ReadAllBytes(path).SequenceEqual(bytes) && p.Gold == 100000 && p.PurchaseSequence == 0 && p.Purchases.Count == 0, "atomic failure " + kind + stage);
            p.CommitFailure = null;
            p.Purchase(product.Id, 1, price, "retry");
        }
        using var reload = new LocalProfile(path);
        Check(reload.PurchaseSequence == 1 && reload.Purchases.Single().RequestId == "retry", "failure retry restart");
    }
    string poorPath = Fresh("poor", 0);
    using (var p = new LocalProfile(poorPath)) {
        byte[] before = File.ReadAllBytes(poorPath);
        Reject(() => p.BuyPack(), "insufficient gold");
        Reject(() => p.Purchase("pack-standard", 0, 0, "zero"), "quantity zero");
        Reject(() => p.Purchase("pack-standard", 51, 5100, "many"), "quantity 51");
        Reject(() => p.Purchase("pack-standard", 1, 99, "stale"), "stale price");
        Reject(() => p.Purchase("unknown", 1, 0, "unknown"), "unknown product");
        Reject(() => p.SelectCardBack(1), "unowned selection");
        Reject(() => p.SetFavoriteHeroSkin("HERO_01", "HERO_08a"), "wrong favourite class");
        Reject(() => p.SelectHubTheme("VIOLET"), "unowned theme selection");
        Reject(() => p.SelectHubTheme("BLACKEMPIRE"), "unsold theme selection");
        Reject(() => p.SelectHubTheme("TEST_EVENT"), "unknown theme selection");
        Reject(() => p.SelectBoard(13), "unowned board selection");
        Reject(() => p.Purchase("theme-CLASSIC", 1, 0, "free"), "free theme is owned");
        Reject(() => p.Purchase("board-1", 1, 0, "free"), "free board is owned");
        Check(File.ReadAllBytes(poorPath).SequenceEqual(before), "refusals are read only");
    }
    // Decision 2026-10-25 (owner may overrule): a ready-made deck also sells the Core copies class level has not unlocked yet.
    string corePath = Fresh("locked-core"); long coreDeckId; RecipeCardCopies[] lockedRows;
    using (var p = new LocalProfile(corePath)) {
        var recipe = shop.Recipe("manastorm-mage");
        var missing = library.MissingCopies(recipe, RecipeLibrary.OwnedCount(p.Collection));
        lockedRows = missing.LockedCore.ToArray();
        var lockedCore = lockedRows.First();
        Check(lockedCore.LockedCore > 0 && lockedCore.Purchasable == 0 && lockedCore.UnlockHero == "HERO_08"
            && lockedCore.UnlockLevel > 1, "recipe view still marks Mage Core as locked by progression");
        Check(lockedRows.Any(c => c.Id == "CORE_BT_072" && c.Needed == 1 && LocalCoreProgression.ById[c.Id].Copies == 2), "fixture: one-copy Core under a two-copy unlock");
        Reject(() => LocalShop.CopyPrice(Cards.FromId(lockedCore.Id)), "Core is not sold as a single card");
        int purchasableOnly = 100 + missing.Purchasable.Where(row => row.Value > 0).Sum(row => row.Value * LocalShop.CopyPrice(row.Key));
        int price = purchasableOnly + lockedRows.Sum(c => c.LockedCore * LocalShop.CopyPrice(c.Rarity));
        Check(p.PurchasePrice("deck-manastorm-mage") == price && price > purchasableOnly, "quote includes locked Core at per-rarity price");
        var receipt = p.Purchase("deck-manastorm-mage", 1, price, "core-deck");
        coreDeckId = receipt.Granted.DeckId!.Value;
        Check(receipt.GoldPaid == price && receipt.PriceBeforeKit == price && p.Gold == 100000 - price, "quote equals receipt and charge");
        Check(lockedRows.All(c => receipt.Granted.Cards.Count(r => r.CardId == c.Id && r.CountBefore == c.Owned && r.CountAfter == c.Needed
            && r.AcquiredBefore == c.Owned) == 1 && p.Collection[c.Id] == c.Needed), "receipt records each locked Core grant");
        Check(receipt.Granted.Cards.All(c => c.CountAfter <= LocalEconomy.PlayableCap(Cards.FromId(c.CardId))), "Core grants never exceed playset");
        Check(p.IsDeckComplete(coreDeckId) && library.MissingCopies(recipe, RecipeLibrary.OwnedCount(p.Collection)).Total == 0, "purchased deck is complete at once");
        Check(lockedRows.All(c => !p.Acquired.ContainsKey(c.Id)), "Core never enters fair-pack Acquired");
        byte[] bytes = File.ReadAllBytes(corePath);
        Check(p.Purchase("deck-manastorm-mage", 1, price, "core-deck").Sequence == receipt.Sequence && File.ReadAllBytes(corePath).SequenceEqual(bytes), "Core deck replay idempotent");
    }
    var purchased = Read(corePath);
    Check(purchased.Collection.All(c => !LocalCardCatalog.IsCore(c.Id))
        && lockedRows.All(c => purchased.CoreGrants.Single(g => g.Id == c.Id).Count == c.Needed), "bought Core lives in the Core grant ledger only");
    byte[] purchasedBytes = File.ReadAllBytes(corePath);
    using (var p = new LocalProfile(corePath)) Check(p.IsDeckComplete(coreDeckId) && lockedRows.All(c => p.Collection[c.Id] == c.Needed), "bought locked Core valid after restart");
    Check(File.ReadAllBytes(corePath).SequenceEqual(purchasedBytes), "reload neither grants nor removes bought Core");
    foreach (var (label, forge) in new (string, Action<LocalProfileData>)[] {
        ("forged Core grant missing", d => d.CoreGrants.RemoveAll(g => g.Id == "CORE_CS2_028")),
        ("forged Core grant below receipt", d => d.CoreGrants.Single(g => g.Id == "CORE_CS2_028").Count = 1),
        // No "forged Core price" case: a deck receipt's recorded price is authoritative (rarity/price tuning must not
        // brick saves), so a hand-edited copy count that keeps the ledger consistent is not detectable by design.
        ("forged Core acquired before", d => d.Purchases[0].Granted.Cards.Single(c => c.CardId == "CORE_CS2_028").AcquiredBefore = 1) }) {
        var forged = LocalProfile.Clone(purchased); forge(forged); Write(corePath, forged);
        var forgedBytes = File.ReadAllBytes(corePath);
        Reject(() => { using var p = new LocalProfile(corePath); }, "ledger rejects " + label);
        Check(forgedBytes.SequenceEqual(File.ReadAllBytes(corePath)), "rejected " + label + " is read only");
    }
    Write(corePath, purchased);
    using (var p = new LocalProfile(corePath)) {
        var match = p.Begin("mage", "priest", 1);
        var result = p.Settle(match.Id, "Won", 10, false); p.AcknowledgeRewardPresentation(match.Id);
        Check(result.UnlockedCardIds.Contains("CORE_BAR_541") && p.Collection["CORE_BAR_541"] == 2, "level unlock of a bought Core card does not double-grant");
        Check(p.IsDeckComplete(coreDeckId), "deck stays complete after a class unlock");
    }
    var coreSaved = Read(corePath);
    Check(coreSaved.Collection.All(c => !LocalCardCatalog.IsCore(c.Id)), "Core never enters paid collection ledger");
    var badCore = LocalProfile.Clone(coreSaved);
    // A forged row the Core grant ledger does not back (the recorded price is authoritative, so the row must be one
    // the ledger contradicts).
    string unbacked = LocalCoreProgression.ById.Keys.OrderBy(id => id, StringComparer.Ordinal)
        .First(id => LocalCoreProgression.IsAvailable(id) && !coreSaved.CoreGrants.Any(g => g.Id == id && g.Count >= 2));
    badCore.Purchases[0].Granted.Cards.Add(new LocalPurchaseCard { CardId = unbacked, CountAfter = 2 });
    Write(corePath, badCore);
    var rejectedBytes = File.ReadAllBytes(corePath);
    Reject(() => { using var p = new LocalProfile(corePath); }, "ledger rejects forged Core purchase grant");
    Check(rejectedBytes.SequenceEqual(File.ReadAllBytes(corePath)), "rejected receipt preserves gold, collection and receipts");
    // Every class and combined unlock: grants top up to the schedule's copies, never above playset, never twice.
    coreSaved.ClassXp.ForEach(entry => entry.Xp = LocalCoreProgression.LevelThresholds.Last());
    Write(corePath, coreSaved);
    using (var p = new LocalProfile(corePath)) {
        Check(p.IsDeckComplete(coreDeckId) && p.Collection["CORE_BT_072"] == 2, "class unlock tops a one-copy purchase up to the playset");
        Check(lockedRows.All(c => p.Collection[c.Id] == LocalCoreProgression.ById[c.Id].Copies
            && p.Collection[c.Id] <= LocalEconomy.PlayableCap(Cards.FromId(c.Id))), "unlocked Core equals schedule copies, within playset");
    }
    byte[] unlockedBytes = File.ReadAllBytes(corePath);
    using (var p = new LocalProfile(corePath)) Check(p.IsDeckComplete(coreDeckId)
        && p.Purchases.Single().Granted.Cards.Count(c => LocalCardCatalog.IsCore(c.CardId)) == lockedRows.Length, "completion and Core receipt survive restart");
    Check(File.ReadAllBytes(corePath).SequenceEqual(unlockedBytes), "unlock top-up is idempotent across restarts");
    var unlockedSaved = Read(corePath);
    Check(unlockedSaved.CoreGrants.Single(g => g.Id == "CORE_BT_072").Count == 2 && unlockedSaved.CoreGrants.All(g => g.Count <= 2), "Core grant ledger capped");
    string recipeCorePath = Fresh("recipe-core");
    using (var p = new LocalProfile(recipeCorePath)) {
        var grantsBefore = Read(recipeCorePath).CoreGrants.Select(g => g.Id + ":" + g.Count).ToArray();
        var receipt = p.Purchase("recipe-manastorm-mage", 1, p.PurchasePrice("recipe-manastorm-mage"), "recipe-core");
        Check(receipt.PriceBeforeKit == 100 && receipt.Granted.Cards.Count == 0 && receipt.Granted.DeckId == null, "recipe stays recipe-only");
        Check(Read(recipeCorePath).CoreGrants.Select(g => g.Id + ":" + g.Count).SequenceEqual(grantsBefore), "recipe grants no Core");
    }
    string pricingPath = Fresh("pricing");
    using (var p = new LocalProfile(pricingPath)) p.Purchase("recipe-manastorm-mage", 1, 100, "recipe");
    var pricing = Read(pricingPath); var recipeMage = shop.Recipe("manastorm-mage"); var firstCard = recipeMage.Cards.First(c => !LocalCardCatalog.IsCore(c.Id));
    pricing.Collection.Add(new LocalCardCount { Id = firstCard.Id, Count = 1, Acquired = 1 }); Write(pricingPath, pricing);
    using (var p = new LocalProfile(pricingPath)) {
        int expected = library.BuildDeck(recipeMage, RecipeLibrary.OwnedCount(p.Collection)).Copies.Where(c => LocalShop.DeckGrant(c) > 0).Sum(c => LocalShop.DeckGrant(c) * LocalShop.CopyPrice(c.Rarity));
        Check(p.PurchasePrice("deck-manastorm-mage") == expected, "owned recipe and partial collection price");
        Reject(() => p.Purchase("deck-manastorm-mage", 1, expected + 100, "stale-deck"), "stale deck price");
        p.Purchase("deck-manastorm-mage", 1, expected, "deck");
    }
    foreach (bool recipeOwned in new[] { false, true }) {
        string path = Fresh("complete-recipe-" + recipeOwned);
        if (recipeOwned) { using var p = new LocalProfile(path); p.Purchase("recipe-manastorm-mage", 1, 100, "recipe"); }
        var full = Read(path);
        full.ClassXp.ForEach(entry => entry.Xp = LocalCoreProgression.LevelThresholds.Last());
        foreach (var card in recipeMage.Cards.Where(c => !LocalCardCatalog.IsCore(c.Id))) { full.Collection.RemoveAll(c => c.Id == card.Id); full.Collection.Add(new LocalCardCount { Id = card.Id, Count = card.Count, Acquired = card.Count }); }
        Write(path, full);
        using var p2 = new LocalProfile(path);
        Check(p2.PurchasePrice("deck-manastorm-mage") == (recipeOwned ? 0 : 100), "complete recipe price");
        if (recipeOwned) Reject(() => p2.Purchase("deck-manastorm-mage", 1, 0, "deck"), "zero-price owned deck refused");
        else Check(p2.Purchase("deck-manastorm-mage", 1, 100, "deck").Granted.Cards.Count == 0, "recipe-only deck grant");
    }
    string packPath = Fresh("fair"); int sequence; string[] savedIds;
    using (var p = new LocalProfile(packPath)) {
        p.Purchase("pack-standard", 3, 300, "packs");
        var pack = p.BeginNativePack("STANDARD"); sequence = p.PackSequence; savedIds = pack.Cards.Select(c => c.Key).ToArray();
        Check(pack.PackType == "STANDARD" && pack.PackRuleVersion == FairPackRules.RuleVersion && p.PackCounters.Single(c => c.PackType == "STANDARD").Counters.PacksOpened == 1, "fair receipt counters");
        byte[] before = File.ReadAllBytes(packPath);
        Reject(() => p.Purchase("deck-manastorm-mage", 1, p.PurchasePrice("deck-manastorm-mage"), "blocked"), "pending reveal blocks card grants");
        Reject(() => p.OpenPack("STANDARD"), "pending reveal blocks next draw");
        Reject(() => p.BeginNativePack(FairPackRules.NewestSet), "pending type mismatch");
        Check(before.SequenceEqual(File.ReadAllBytes(packPath)) && p.BeginNativePack("STANDARD").Cards.Select(c => c.Key).SequenceEqual(savedIds), "pending replay stable");
    }
    using (var p = new LocalProfile(packPath)) {
        Check(p.PackRevealPending && p.LastPack.Cards.Select(c => c.Key).SequenceEqual(savedIds), "restart during reveal");
        Check(p.CompleteNativePackReveal(sequence, savedIds), "ack");
        Check(!p.CompleteNativePackReveal(sequence, savedIds), "ack idempotent");
    }
    using (var p = new LocalProfile(packPath)) Check(!p.PackRevealPending && p.PackSequence == sequence, "restart after ack");
    foreach (string stage in new[] { "write", "flush", "replace" }) {
        string path = Fresh("pack-" + stage);
        using var p = new LocalProfile(path); p.BuyPack(); byte[] before = File.ReadAllBytes(path);
        p.CommitFailure = point => { if (point == stage) throw new IOException("injected"); };
        Reject(() => p.BeginNativePack("STANDARD"), "pack failure " + stage);
        Check(p.PackSequence == 0 && p.UnopenedPacks == 1 && p.PackCounters.All(c => c.Counters.PacksOpened == 0) && before.SequenceEqual(File.ReadAllBytes(path)), "pack atomic " + stage);
    }
    string incompletePath = Fresh("incomplete");
    using (var p = new LocalProfile(incompletePath)) {
        p.SaveDeck("Missing cards", "HERO_08", LocalDecks.Get("mage-frost").CardIds);
        Check(!p.IsDeckComplete(p.CustomDeck.Id), "incomplete saved");
        Reject(() => p.Begin("custom:" + p.CustomDeck.Id, "priest", 1), "incomplete match refused");
    }
    using (var p = new LocalProfile(incompletePath)) Check(p.CustomDeck.Cards.SequenceEqual(LocalDecks.Get("mage-frost").CardIds), "incomplete unchanged on reload");
    string rewardsPath = Fresh("rewards");
    using (var p = new LocalProfile(rewardsPath)) {
        var match = p.Begin("mage", "priest", 2);
        var facts = new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 5 };
        var result = p.Settle(match.Id, "Won", 10, false, facts);
        var rolled = LocalEconomy.RewardV6("Won", 10, false, LocalEconomy.RewardRollSeed(match.Id, 2));
        var tier = GrindTiers.Find(match.GrindTier);
        Check(result.Packs.Count == 0 && p.PackInventory.Count == 0
            && result.RewardVersion == 6 && result.DustReward == tier.Dust(rolled.Dust)
            && p.DustBalance == tier.Dust(rolled.Dust), "typed V6 rewards and stamped-tier dust");
    }
    foreach (string type in FairPackRules.PackTypes) {
        string path = Fresh("open-" + type);
        using (var p = new LocalProfile(path)) {
            string product = shop.Products.Single(row => row.PackType == type).Id;
            p.Purchase(product, 1, 100, "typed");
            var receipt = p.BeginNativePack(type);
            Check(receipt.PackType == type && receipt.Cards.Count == 5 && p.Gold == 99900, "typed opening " + type);
        }
        using var reload = new LocalProfile(path);
        Check(reload.PackRevealPending && reload.LastPack.PackType == type, "typed pending restart " + type);
    }
    string extrasPath = Fresh("extras");
    var extras = Read(extrasPath);
    var completeRarity = FairPackRules.Eligible("EMERALD_DREAM", 1).Where(c => c.Rarity == Rarity.COMMON).ToArray();
    foreach (var card in completeRarity) extras.Collection.Add(new LocalCardCount { Id = card.Id, Count = 0, Acquired = 2 });
    extras.Collection.Single(c => c.Id == completeRarity[0].Id).Count = 3;
    Write(extrasPath, extras);
    using (var p = new LocalProfile(extrasPath)) Check(p.Collection[completeRarity[0].Id] == 3, "extras allowed after full set-rarity acquisition even after disenchant");
    extras.Collection.Single(c => c.Id == completeRarity[1].Id).Acquired = 1; Write(extrasPath, extras);
    Reject(() => { using var p = new LocalProfile(extrasPath); }, "extras refused before full set-rarity acquisition");
    foreach (string id in new[] { "TIME_003", "EDR_000", "BE_036" }) {
        string path = Fresh("excluded-" + id);
        var invalid = Read(path); int cap = LocalEconomy.PlayableCap(Cards.FromId(id));
        invalid.Collection.Add(new LocalCardCount { Id = id, Count = cap + 1, Acquired = cap }); Write(path, invalid);
        Reject(() => { using var p = new LocalProfile(path); }, "pack-excluded cards cannot hold extras " + id);
    }
    using (var catalogStream = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json"))
    using (var recipeStream = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.recipes-v1.json")) {
        string catalogText = new StreamReader(catalogStream!).ReadToEnd(), recipeText = new StreamReader(recipeStream!).ReadToEnd();
        foreach (string invalidCatalog in new[] { catalogText.Replace("\"Version\": 1", "\"Version\": 2"),
            catalogText.Replace("\"Price\": 100", "\"Price\": -1"), catalogText.Replace("STANDARD", "ALIEN") }) {
            using var c = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidCatalog));
            using var r = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(recipeText));
            Reject(() => LocalShop.Load(c, r), "invalid catalogue rejected at load");
        }
        foreach (int id in LocalShop.LadderExclusiveCardBacks.Values) {
            using var c = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(catalogText.Replace("\"Products\": [", "\"Products\": [{\"Id\":\"reserved-back\",\"Kind\":\"CardBack\",\"Price\":500,\"MinQuantity\":1,\"MaxQuantity\":1,\"CardBack\":" + id + ",\"Description\":\"Reserved tier back\"},")));
            using var r = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(recipeText));
            Reject(() => LocalShop.Load(c, r), "ladder-exclusive card back cannot be sold");
        }
        using var goodCatalog = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(catalogText));
        using var badRecipe = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(recipeText.Replace(firstCard.Id, "BAD")));
        var invalidShop = LocalShop.Load(goodCatalog, badRecipe);
        Reject(() => { _ = invalidShop.Recipes; }, "unregistered recipe rejected on recipe access");
    }
    var good = Read(packPath);
    var corruptions = new Dictionary<string, Action<LocalProfileData>> {
        ["unknown-pack"] = p => p.UnopenedPacks[0].PackType = "ALIEN",
        ["negative-pack"] = p => p.UnopenedPacks[0].Count = -1,
        ["counter"] = p => p.PackCounters[1].Counters.PacksSinceLegendary = int.MaxValue,
        ["acquired"] = p => p.Collection[0].Acquired = -1,
        ["extra"] = p => p.Collection[0].Count = 3,
        ["dust"] = p => p.DustBalance = -1,
        ["card-negative"] = p => p.Collection[0].Count = -1,
        ["acquired-cap"] = p => p.Collection[0].Acquired = 3,
        ["craft-receipt-sequence"] = p => { p.CraftSequence = 1; p.LastCraft = new LocalCraftReceipt { Sequence = 2, Op = LocalCraftOp.Craft }; },
        ["duplicate-pack"] = p => p.UnopenedPacks.Add(new LocalPackCount { PackType = p.UnopenedPacks[0].PackType, Count = 1 }),
        ["duplicate-request"] = p => { p.Purchases.Add(LocalProfile.Clone(p.Purchases[0])); p.Purchases[1].Sequence = 2; p.PurchaseSequence = 2; },
        ["receipt-kind"] = p => p.Purchases[0].Kind = "Recipe",
        ["receipt-version"] = p => p.Purchases[0].CatalogVersion = 99,
        ["hero-favourite"] = p => p.FavoriteHeroSkins.Add(new LocalHeroFavorite { Class = "HERO_08", HeroCardId = "HERO_08a" }),
        ["craft-sequence"] = p => p.CraftSequence = 1,
        ["ladder"] = p => p.LadderState.StarIndex = -1,
        ["ladder-rating"] = p => p.LadderState.HiddenRating = double.NaN,
        ["purchase-sequence"] = p => p.PurchaseSequence++,
        ["price"] = p => p.Purchases[0].GoldPaid++,
        ["grant"] = p => p.Purchases[0].Granted.Packs[0].Count++,
        ["selection"] = p => p.FavoriteCardBacks = new List<int> { 99 },
        ["owned"] = p => p.OwnedCardBacks.Add(99),
        ["default-back"] = p => p.OwnedCardBacks.Clear(),
        ["receipt-type"] = p => p.LastPack.PackType = "ALIEN",
        ["pending-acquired"] = p => { p.PackRevealPending = true; p.LastPack.Cards[0].AcquiredBefore = 2; },
        ["pending-counters"] = p => { p.PackRevealPending = true; p.LastPack.CountersBefore.PacksOpened++; p.LastPack.CountersBefore.PacksSinceLegendary++; p.LastPack.CountersBefore.PacksSinceEpic++; },
    };
    foreach (var test in corruptions) {
        string path = Path.Combine(root, "invalid-" + test.Key + ".xml"); var invalid = LocalProfile.Clone(good); test.Value(invalid); Write(path, invalid); byte[] before = File.ReadAllBytes(path);
        Reject(() => { using var p = new LocalProfile(path); }, "reject " + test.Key);
        Check(before.SequenceEqual(File.ReadAllBytes(path)), "rejection preserved bytes " + test.Key);
    }
    Console.WriteLine("PASS shop: " + assertions + " assertions; products, migration, atomic failures, restart, typed packs and reservations");
} finally { Directory.Delete(root, true); }

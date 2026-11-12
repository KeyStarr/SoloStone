using System.Text;
using System.Xml.Serialization;
using MiniJSON;

internal static class NativeShopLedgerTests
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh)
    {
        NativeBundleHarness.Run(check);
        NativeBundleHarness.RunDeckSectionFilters(check);
        // Offered products exclude unverified cosmetics and recipes withheld by the library.
        var offeredIds = NativeShopPurchase.Products.Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var expectedIds = LocalShop.Default.Products.Where(p => !p.verify && !LocalShop.Default.Withheld(p)).Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        check(offeredIds.SequenceEqual(expectedIds) && !NativeShopPurchase.Products.Any(p => p.verify)
            && NativeShopPurchase.Products.Count(p => p.Kind == "Pack") == 6
            && LocalShop.Default.Products.Where(p => (p.Kind is "Pack" or "Recipe" or "Deck") && !LocalShop.Default.Withheld(p)).All(p => !p.verify && offeredIds.Contains(p.Id)),
            "native shop offers exactly the non-pending products (verified packs/recipes/decks plus admitted cosmetics)");
        foreach (var pending in LocalShop.Default.Products.Where(p => p.verify))
            reject(() => NativeShopPurchase.Product(NativeShopPurchase.PmtId(pending)), "pending cosmetics cannot resolve for purchase");
        var withheldIds = LocalShop.Default.Library.Withheld.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var product in LocalShop.Default.Products) {
            bool withheld = (product.Kind is "Recipe" or "Deck") && withheldIds.Contains(product.RecipeId);
            check(LocalShop.Default.Withheld(product) == withheld, product.Id + ": withheld exactly when its recipe is withheld");
            check(!withheld || !offeredIds.Contains(product.Id), product.Id + ": withheld recipe has no offered product");
            if (withheld)
                reject(() => NativeShopPurchase.Product(NativeShopPurchase.PmtId(product)), product.Id + ": withheld product cannot resolve for purchase");
        }
        check(!LocalShop.Default.Withheld(null), "null product is not withheld");
        NativeBundleHarness.RunCollectionMapping(check);
        using var cosmetics = new CosmeticsFixture();
        NativeBundleHarness.Run(check);
        var products = NativeShopPurchase.Products;
        check(products.Select(NativeShopPurchase.PmtId).Distinct().Count() == products.Count, "all PMT ids unique");
        foreach (var product in products) {
            long expected = product.Kind switch {
                "Pack" => product.PackType == "STANDARD" ? 1900000001 : 1900000000L + FairPackRules.NativeBoosterId(product.PackType),
                "CardBack" => 1910000000L + product.CardBack.Value,
                "Coin" => 1980000000L + product.Coin.Value,
                "HeroSkin" => 1920000000L + GameUtils.TranslateCardIdToDbId(product.HeroSkin),
                "Recipe" => 1930000000L + product.NativeId,
                "HubTheme" => 1950000000L + LocalHubCosmetics.ThemeNativeId(product.HubTheme),
                "Board" => 1960000000L + LocalHubCosmetics.BoardNativeId(product.Board.Value),
                "Pet" => 1970000000L + (product.Pet == null ? 999999 : product.NativeId),
                _ => 1940000000L + product.NativeId };
            check(NativeShopPurchase.PmtId(product) == expected && NativeShopPurchase.Product(expected).Id == product.Id, "stable/reversible PMT " + product.Id);
        }
        check(NativeShopPurchase.MaxDeckNativeId == LocalShop.MaxDeckNativeId, "native PMT deck range matches catalogue NativeId bound");
        // Test control's product allowlist is NativeShopPurchase.IsLocalPmtId: every offered kind must fall inside it.
        check(products.Select(p => p.Kind).Distinct().Count() == 9 && products.All(p => NativeShopPurchase.IsLocalPmtId(NativeShopPurchase.PmtId(p))), "every offer inside the reserved PMT ranges");
        foreach (long outside in new[] { 1900000000L, 1901000000L, 1910000000L, 1920000000L, 1930000000L, 1930000129L, 1940000000L, 1940000129L, 1950000000L, 1950001000L, 1960000000L, 1960200000L, 1899999999L })
            check(!NativeShopPurchase.IsLocalPmtId(outside), "PMT outside reserved ranges " + outside);
        var badHero = products.First(p => p.Kind == "HeroSkin");
        GameUtils.MissingTranslations.Add(badHero.HeroSkin);
        var excluded = new List<string>();
        NativeShopPurchase.Seed(null, excluded.Add);
        check(NativeShopPurchase.Products.Count == products.Count - 1 && excluded.Count == 1
            && NativeShopPurchase.Product(NativeShopPurchase.ProductId).Kind == "Pack", "bad hero translation excludes only that offer");
        GameUtils.MissingTranslations.Clear();
        NativeShopPurchase.Seed(null, excluded.Add);
        NativeShopPurchase.Seed(p => p.Kind != "Pack", excluded.Add);
        var noPacks = NativeBundleHarness.BuildSections();
        check(noPacks[0].InternalName == "decks_1" && !noPacks.Any(s => s.InternalName == "cards" || s.InternalName == "packs"), "omitted packs omit Cards category");
        foreach (string category in new[] { "decks_1", "decks_2" }) {
            var subs = noPacks.SkipWhile(s => s.InternalName != category).Skip(1).TakeWhile(s => !s.Attributes.ContainsKey("tab_id"))
                .Where(s => s.Attributes.ContainsKey("subtab_id")).ToArray();
            check(subs.Select(s => int.Parse(s.Attributes["tab_order"])).SequenceEqual(Enumerable.Range(0, subs.Length)), "omitted packs leave contiguous class subtab order");
        }
        NativeShopPurchase.Seed(null, excluded.Add);
        var skins = NativeShopPurchase.Products.Where(p => p.Kind == "HeroSkin").ToArray();
        var classes = skins.Select(p => p.Class).ToArray();
        foreach (var skin in skins) skin.Class = "UNKNOWN";
        check(!NativeBundleHarness.BuildSections().Any(s => s.InternalName == "hero_skins"), "subtab with all child classes empty omitted");
        for (int i = 0; i < skins.Length; i++) skins[i].Class = classes[i];
        NativeBundleHarness.RunRuntime(check, reject, fresh);
        NativeBundleHarness.RunDeckDetails(check, fresh);
        NativeBundleHarness.RunLayout(check);
        NativeShopProjectionHarness.Run(check, fresh);
        CatalogContracts(check, reject);
        DeckRecords(check, reject);
        // Free themes/boards (Random owned, Classic, boards 1-7) are owned from the start: never bought.
        foreach (var product in products.Where(p => p.Kind == "Pack").Concat(products.Where(p => p.Kind != "Pack"
            && !(NativeShopPurchase.IsSelectable(p) && p.Price == 0)).GroupBy(p => p.Kind).Select(g => g.First())))
        {
            long pmt = NativeShopPurchase.PmtId(product), tx = 123456789;
            string path = fresh("native-" + product.Id, 100000);
            int price;
            using (var profile = new LocalProfile(path))
            {
                price = profile.PurchasePrice(product.Id);
                var guard = new NativeShopPurchase();
                check(!NativeShopPurchase.Owned(profile, product, price), "unowned initial " + product.Kind);
                reject(() => guard.Commit(profile, tx, pmt, "XSH", 1, price - 1), "fresh native quote mismatch refused");
                check(profile.Gold == 100000 && profile.PurchaseSequence == 0, "stale quote leaves ledger untouched");
                var receipt = guard.Commit(profile, tx, pmt, "XSH", 1, price);
                check(receipt.RequestId == tx.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    && receipt.ProductId == product.Id && receipt.Kind == product.Kind && receipt.GoldPaid == price
                    && profile.Gold == 100000 - price, "durable native receipt " + product.Kind);
                if (product.Kind == "Pack") check(profile.PackInventory.Single().PackType == product.PackType, "typed pack grant");
                if (product.Kind == "Deck") check(receipt.Granted.DeckId.HasValue && receipt.Granted.Cards.Count > 0
                    && profile.GetDraft(receipt.Granted.DeckId.Value).SourceRecipeId == product.RecipeId, "ready-made cards and saved deck");
                if (product.Kind == "Deck") {
                    long allocated = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 100; i++) NativeShopPurchase.Owned(profile, product, price);
                    check(GC.GetAllocatedBytesForCurrentThread() - allocated < 50000, "deck ownership does not XML-clone receipts");
                }
                check(NativeShopPurchase.Owned(profile, product, profile.PurchasePrice(product.Id)) == (product.Kind != "Pack"), "owned after purchase " + product.Kind);
                byte[] bytes = File.ReadAllBytes(path);
                check(guard.Commit(profile, tx, pmt, "XSH", 1, price).Sequence == receipt.Sequence
                    && bytes.SequenceEqual(File.ReadAllBytes(path)), "native replay has no write or debit");
                foreach (var invalid in new Action[] {
                    () => guard.Commit(profile, tx, pmt, "USD", 1, price),
                    () => guard.Commit(profile, 0, pmt, "XSH", 1, price),
                    () => guard.Commit(profile, tx, 4, "XSH", 1, price),
                    () => guard.Commit(profile, tx, pmt, "XSH", 2, price),
                    () => guard.Commit(profile, tx, pmt, "XSH", 1, price - 1),
                    () => guard.Commit(profile, tx, pmt, "XSH", 1, long.MaxValue),
                    () => guard.Commit(profile, tx, NativeShopPurchase.PmtId(products.First(p => p.Id != product.Id)), "XSH", 1, price)
                }) reject(invalid, "native invalid purchase refused");
                if (product.Kind != "Pack") reject(() => guard.Commit(profile, tx + 1, pmt, "XSH", 1, profile.PurchasePrice(product.Id)), "owned refused at ledger");
                check(bytes.SequenceEqual(File.ReadAllBytes(path)), "refusals leave durable state unchanged");
            }
            using (var reopened = new LocalProfile(path)) {
                check(new NativeShopPurchase().Commit(reopened, tx, pmt, "XSH", 1, price).Sequence == 1
                    && reopened.PurchaseSequence == 1 && reopened.Gold == 100000 - price, "replay survives restart " + product.Kind);
                check(NativeShopPurchase.Owned(reopened, product, reopened.PurchasePrice(product.Id)) == (product.Kind != "Pack"), "owned survives restart");
            }
            using (var poor = new LocalProfile(fresh("native-poor-" + product.Id, 0)))
                reject(() => new NativeShopPurchase().Commit(poor, tx, pmt, "XSH", 1, poor.PurchasePrice(product.Id)), "insufficient gold " + product.Kind);
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                string file = fresh("native-failed-" + product.Id + stage, 100000);
                using var profile = new LocalProfile(file);
                var before = File.ReadAllBytes(file);
                // Capture through the known test path; no real profile is involved.
                profile.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
                reject(() => new NativeShopPurchase().Commit(profile, tx, pmt, "XSH", 1, price), "failed native save " + product.Kind + stage);
                check(profile.Gold == 100000 && profile.PurchaseSequence == 0 && before.SequenceEqual(File.ReadAllBytes(file)), "failed write leaves memory and file intact");
                profile.CommitFailure = null;
                check(new NativeShopPurchase().Commit(profile, tx, pmt, "XSH", 1, price).Sequence == 1, "same transaction retry after failed write");
            }
        }
        PriceChanges(check, reject, fresh);
    }

    private static string CatalogText() {
        using var stream = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json");
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    private static LocalShop Load(JsonNode root, RecipeLibrary library) {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Json.Serialize(root)));
        return LocalShop.Load(stream, () => library);
    }
    private static void CatalogContracts(Action<bool, string> check, Action<Action, string> reject)
    {
        var previous = LocalShop.TestOverride;
        try {
            var root = (JsonNode)Json.Deserialize(CatalogText().Replace("\"verify\": true", "\"verify\": false"));
            var rows = (JsonList)root["Products"];
            var expected = NativeShopPurchase.Products.ToDictionary(p => p.Id, NativeShopPurchase.PmtId);
            rows.Reverse(); LocalShop.TestOverride = Load(root, previous.Library);
            check(NativeShopPurchase.Products.All(p => expected[p.Id] == NativeShopPurchase.PmtId(p)
                && NativeShopPurchase.Product(expected[p.Id]).Id == p.Id), "PMT identities survive catalogue reorder");
            NativeBundleHarness.Run(check);
            foreach (var row in rows.Cast<JsonNode>().Where(p => (string)p["Kind"] == "Recipe" || (string)p["Kind"] == "Deck").Take(2)) {
                object original = row["NativeId"];
                foreach (var invalid in new object[] { 0, -1, LocalShop.MaxDeckNativeId + 1, 1.5, null }) {
                    row["NativeId"] = invalid; reject(() => Load(root, previous.Library), "invalid permanent NativeId");
                }
                row.Remove("NativeId"); reject(() => Load(root, previous.Library), "missing NativeId"); row["NativeId"] = original;
            }
            var recipe = rows.Cast<JsonNode>().First(p => (string)p["Kind"] == "Recipe");
            object saved = recipe["NativeId"]; recipe["NativeId"] = 1L;
            reject(() => Load(root, previous.Library), "duplicate or mismatched NativeId refused"); recipe["NativeId"] = saved;
            foreach (var row in rows.Cast<JsonNode>().Where(p => (string)p["Kind"] == "HeroSkin" && (string)p["Class"] != "HERO_08")) row["verify"] = true;
            LocalShop.TestOverride = Load(root, previous.Library);
            var sections = NativeBundleHarness.BuildSections();
            check(sections.Count(s => s.InternalName.StartsWith("local-skins-")) == 1
                && sections.Single(s => s.InternalName.StartsWith("local-skins-")).Label == "Mage", "empty hero classes omitted");
        }
        finally { LocalShop.TestOverride = previous; }
    }
    private static void DeckRecords(Action<bool, string> check, Action<Action, string> reject)
    {
        var all = new List<NativeShopDecks.Row>();
        foreach (var product in NativeShopPurchase.Products.Where(p => p.Kind == "Recipe" || p.Kind == "Deck")) {
            var recipe = LocalShop.Default.Recipe(product.RecipeId);
            var rows = NativeShopDecks.Records(product, recipe, GameUtils.TranslateCardIdToDbId);
            all.AddRange(rows);
            check(rows.All(r => r.Id >= NativeShopDecks.FirstId && r.Id < NativeShopDecks.LastId), "reserved synthetic IDs");
            var copies = rows.Where(r => r.Table == "DeckCard").ToArray();
            check(copies.Select(r => (int)r.Values["CARD_ID"]).SequenceEqual(recipe.Cards.SelectMany(c => Enumerable.Repeat(GameUtils.TranslateCardIdToDbId(c.Id), c.Count))), "one DeckCard per recipe copy");
            check(copies.All(r => (int)r.Values["DECK_ID"] == NativeShopDecks.Id(product))
                && (int)copies.Last().Values["NEXT_CARD"] == 0, "linked and indexed deck rows");
            var template = rows.Single(r => r.Table == "DeckTemplate");
            check((int)template.Values["CLASS_ID"] == (int)LocalDecks.HeroClass(recipe.Hero)
                && (int)template.Values["HERO_CARD_ID"] == GameUtils.TranslateCardIdToDbId(recipe.Hero)
                && (int)template.Values["FORMAT_TYPE"] == 2, "template class/hero/Standard");
            check(rows.Count(r => r.Table == "SideboardCard") == recipe.Sideboards.Sum(b => b.Cards.Sum(c => c.Count)), "sideboard copies preserved");
            // Page headers identify the offer kind and the researched recipe label.
            var deckRow = rows.Single(r => r.Table == "Deck");
            string name = product.Kind == "Recipe" ? recipe.Name + " (Recipe)" : recipe.Name;
            check((string)deckRow.Values["NAME"] == name && (string)rows.Single(r => r.Table == "SellableDeck").Values["GOLDEN_NAME"] == name
                && NativeShopDecks.DisplayName(product, recipe) == name, product.Id + ": tile name says recipe only for recipes");
            string label = RecipeLibrary.Label(recipe);
            var pieces = label.Split(", ");
            string capital(string value) => char.ToUpperInvariant(value[0]) + value.Substring(1);
            check(pieces.Length == 3 && new[] { "Meta", "Fan favourite" }.Contains(pieces[0])
                && new[] { "Easy", "Medium", "Hard" }.Contains(pieces[1])
                && new[] { "Aggro", "Tempo", "Midrange", "Control", "Combo" }.Contains(pieces[2])
                && pieces[0] == (recipe.Tag == "meta" ? "Meta" : "Fan favourite")
                && pieces[1] == capital(recipe.Difficulty) && pieces[2] == capital(recipe.Style), product.Id + ": valid label pieces");
            string header = (product.Kind == "Recipe" ? "Recipe — " : "Ready-made deck — ") + label;
            check((string)deckRow.Values["ALT_DESCRIPTION"] == header && NativeShopDecks.Header(product, recipe) == header
                && header.Length <= 49, product.Id + ": labelled page header fits native limit");
        }
        var recipeNames = NativeShopPurchase.Products.Where(p => p.Kind == "Recipe")
            .Select(p => NativeShopDecks.DisplayName(p, LocalShop.Default.Recipe(p.RecipeId))).ToArray();
        check(recipeNames.Length == LocalShop.Default.Recipes.Count && recipeNames.Length >= 60 && recipeNames.Contains("Timethief Rafaam (Recipe)") && recipeNames.Contains("Egg Death Knight (Recipe)") && recipeNames.All(n => n.EndsWith(" (Recipe)"))
            && recipeNames.Distinct().Count() == recipeNames.Length, "recipe display names");
        SyntheticDeckRanges(check, reject);
        var store = all.Select(r => r.Table).Distinct().ToDictionary(t => t, _ => new List<int> { 1, 123456 });
        var inserted = new HashSet<string>();
        void Inject() => NativeShopDecks.Ensure(all, t => store[t], inserted, r => store[r.Table].Add(r.Id));
        Inject(); int count = store.Values.Sum(ids => ids.Count); Inject();
        check(store.Values.Sum(ids => ids.Count) == count && inserted.Count == all.Count, "idempotent public DBF insertion plan");
        store["SellableDeck"].RemoveAll(id => id >= NativeShopDecks.FirstId);
        Inject();
        check(store.Values.Sum(ids => ids.Count) == count, "DBF reload restores missing sellable deck records");
        store["Deck"].Add(NativeShopDecks.LastId + 1);
        reject(Inject, "retail max overlap fails closed before insertion");
        var offers = NativeShopPurchase.Products.Where(p => p.Kind == "Recipe" || p.Kind == "Deck").ToArray();
        var brokenOffer = offers[1];
        var tables = all.Select(r => r.Table).Distinct().ToDictionary(t => t, _ => new List<int> { 1 });
        var added = new HashSet<string>(); var failed = new HashSet<string>(); var excluded = new List<string>();
        void InjectEach() => NativeShopDecks.EnsureEach(offers, p => p == brokenOffer ? throw new InvalidOperationException("Missing native recipe card")
            : NativeShopDecks.Records(p, LocalShop.Default.Recipe(p.RecipeId), GameUtils.TranslateCardIdToDbId), t => tables[t], added, failed, r => tables[r.Table].Add(r.Id), excluded.Add);
        InjectEach(); int tableRows = tables.Values.Sum(ids => ids.Count); InjectEach();
        check(excluded.Count == 1 && failed.Single() == brokenOffer.Id && tables["SellableDeck"].Count == offers.Length
            && tables.Values.Sum(ids => ids.Count) == tableRows, "broken synthetic deck excludes only that offer, once");
    }
    private static void SyntheticDeckRanges(Action<bool, string> check, Action<Action, string> reject)
    {
        var recipes = new List<DeckRecipe> { LocalShop.Default.Recipes.First(r => r.Cards.Sum(c => c.Count) == 30) };
        var sideboard = LocalShop.Default.Recipes.FirstOrDefault(r => r.Sideboards.Count != 0);
        if (sideboard != null && !recipes.Contains(sideboard)) recipes.Add(sideboard);
        foreach (var recipe in recipes) {
            var seen = new HashSet<(string Table, int Id)>();
            foreach (string kind in new[] { "Recipe", "Deck" })
                for (int nativeId = 1; nativeId <= LocalShop.MaxDeckNativeId; nativeId++) {
                    var product = new LocalShopProduct { Kind = kind, NativeId = nativeId, RecipeId = recipe.Id };
                    var rows = NativeShopDecks.Records(product, recipe, GameUtils.TranslateCardIdToDbId);
                    check(rows.All(r => r.Id >= NativeShopDecks.FirstId && r.Id < NativeShopDecks.LastId),
                        recipe.Id + ": synthetic range " + kind + " " + nativeId);
                    check(rows.All(r => seen.Add((r.Table, r.Id))), recipe.Id + ": unique table/id across NativeIds and kinds " + kind + " " + nativeId);
                    check(rows.Count(r => r.Table == "DeckCard") == recipe.Cards.Sum(c => c.Count)
                        && rows.Count(r => r.Table == "SideboardCard") == recipe.Sideboards.Sum(b => b.Cards.Sum(c => c.Count)),
                        recipe.Id + ": synthetic copies preserved " + kind + " " + nativeId);
                }
        }
        foreach (string kind in new[] { "Recipe", "Deck" }) {
            long prefix = kind == "Recipe" ? 1930000000L : 1940000000L;
            var product = new LocalShopProduct { Kind = kind, NativeId = 128, RecipeId = recipes[0].Id };
            check(NativeShopPurchase.IsLocalPmtId(prefix + 128) && NativeShopPurchase.PmtId(product) == prefix + 128
                && !NativeShopPurchase.IsLocalPmtId(prefix + 129), kind + ": NativeId 128 local, 129 outside PMT range");
            product.NativeId = 129;
            reject(() => NativeShopPurchase.PmtId(product), kind + ": NativeId 129 cannot generate a local PMT");
        }
    }
    private static void PriceChanges(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh)
    {
        var decks = NativeShopPurchase.Products.Where(p => p.Kind == "Deck").ToArray();
        var guard = new NativeShopPurchase();
        using (var profile = new LocalProfile(fresh("native-price-refresh", 100000))) {
            var before = decks.ToDictionary(p => p.Id, p => profile.PurchasePrice(p.Id));
            guard.Commit(profile, 200, NativeShopPurchase.PmtId(decks[0]), "XSH", 1, before[decks[0].Id]);
            var after = decks.ToDictionary(p => p.Id, p => NativeBundleHarness.BuildBundle(p, profile.PurchasePrice(p.Id)).GoldCost.Value.CurrentCost);
            check(after[decks[0].Id] == 0 && NativeShopPurchase.Owned(profile, decks[0], after[decks[0].Id]), "zero-price deck remains Owned");
            check(NativeShopPurchase.Owned(profile, decks[0], 100), "deck receipt remains Owned even if copies later go missing");
            string zeroPath = fresh("native-zero-without-receipt", 100000);
            using (var seed = new LocalProfile(zeroPath)) {
                var recipe = LocalShop.Default.Products.Single(p => p.Kind == "Recipe" && p.RecipeId == decks[1].RecipeId);
                seed.Purchase(recipe.Id, 1, seed.PurchasePrice(recipe.Id), "zero-recipe");
            }
            var serializer = new XmlSerializer(typeof(LocalProfileData));
            LocalProfileData zeroData;
            using (var input = File.OpenRead(zeroPath)) zeroData = (LocalProfileData)serializer.Deserialize(input);
            foreach (var card in LocalShop.Default.Recipe(decks[1].RecipeId).Cards) {
                var owned = zeroData.Collection.SingleOrDefault(c => c.Id == card.Id);
                if (owned == null) zeroData.Collection.Add(new LocalCardCount { Id = card.Id, Count = card.Count, Acquired = card.Count });
                else { owned.Count = Math.Max(owned.Count, card.Count); owned.Acquired = Math.Max(owned.Acquired, owned.Count); }
            }
            using (var output = File.Create(zeroPath)) serializer.Serialize(output, zeroData);
            using var zero = new LocalProfile(zeroPath);
            int zeroPrice = zero.PurchasePrice(decks[1].Id);
            check(zeroPrice == 0 && !zero.HasDeckReceipt(decks[1].RecipeId) && NativeShopPurchase.Owned(zero, decks[1], zeroPrice), "real zero-price deck is Owned without a deck receipt");
            var changed = decks.Skip(1).First(p => after[p.Id] < before[p.Id]);
            reject(() => guard.Commit(profile, 201, NativeShopPurchase.PmtId(changed), "XSH", 1, before[changed.Id]), "overlapping deck stale quote refused");
            check(after[changed.Id] == profile.PurchasePrice(changed.Id), "rebuild uses changed missing copies");
        }
        string pendingPath = fresh("native-pending-reveal", 100000);
        using (var p = new LocalProfile(pendingPath)) {
            p.Purchase("pack-standard", 1, 100, "seed-pack");
            p.BeginNativePack();
            check(p.PackRevealPending, "pending reveal fixture");
            reject(() => guard.Commit(p, 300, NativeShopPurchase.PmtId(decks[0]), "XSH", 1, p.PurchasePrice(decks[0].Id)), "ready-made purchase refuses pending reveal");
        }
    }
}

using System.Xml.Serialization;

internal static class RecipeAvailabilityTests
{
    internal static void Run(string root)
    {
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        LocalShop Shop(Func<RecipeLibrary> loader) {
            using var catalog = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json");
            return LocalShop.Load(catalog!, loader);
        }
        string Fresh(string name) {
            string path = Path.Combine(root, name + ".xml");
            using (var profile = new LocalProfile(path)) profile.SelectCardBack(0);
            LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
            data.GoldBalance = 100000;
            WelcomeGiftTests.OwnGrantOnly(data);
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            return path;
        }
        void Refused(Action action) {
            try { action(); }
            catch (InvalidOperationException error) when (error.Message.StartsWith("Recipe library unavailable;")) { return; }
            throw new Exception("recipe action must report unavailable library");
        }
        try {
            foreach (string failure in new[] { "unavailable", "invalid", "validation" }) {
                int calls = 0;
                LocalShop.TestOverride = Shop(() => {
                    calls++;
                    if (failure == "unavailable") throw new InvalidOperationException("Recipe validation requires the source engine (ENGINE_SOURCE).");
                    if (failure == "invalid") return RecipeLibrary.LoadText("{}");
                    using var recipes = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.recipes-v1.json");
                    var invalid = RecipeLibrary.LoadText(new StreamReader(recipes!).ReadToEnd());
                    invalid.Recipes[0].Cards[0].Id = "BAD";
                    return invalid;
                });
                string path = Fresh("recipes-" + failure);
                long draftId;
                using (var profile = new LocalProfile(path)) {
                    var draft = profile.CreateDraft("Custom", "HERO_08"); draftId = draft.Id;
                    profile.SaveDraft(draft.Id, "Saved custom", draft.Hero, new[] { "CS2_029" }, draft.SortOrder);
                    profile.BuyPack();
                    Check(calls == 0, "load, custom saves and pack purchase must not try recipes");
                    var recipeProduct = LocalShop.Default.Products.First(p => p.Kind == "Recipe");
                    var deckProduct = LocalShop.Default.Products.First(p => p.Kind == "Deck");
                    byte[] before = File.ReadAllBytes(path); int gold = profile.Gold; long sequence = profile.PurchaseSequence;
                    Refused(() => profile.Purchase(recipeProduct.Id, 1, recipeProduct.Price, "refused-recipe"));
                    Refused(() => profile.Purchase(deckProduct.Id, 1, 0, "refused-deck"));
                    Refused(() => { _ = LocalShop.Default.Recipes; });
                    Check(calls == 1, "failed recipe load must be cached");
                    Check(File.ReadAllBytes(path).SequenceEqual(before) && profile.Gold == gold && profile.PurchaseSequence == sequence
                        && profile.OwnedRecipes.Count == 0 && profile.CustomDecks.Count == 1, "refusals must leave bytes and memory unchanged");
                    profile.SelectCardBack(0);
                    profile.BuyPack();
                }
                using (var profile = new LocalProfile(path))
                    Check(profile.GetDraft(draftId).Cards.SequenceEqual(new[] { "CS2_029" }) && profile.PackInventory.Single().Count == 2,
                        "custom save and packs persist after failed recipe initialization");
                Check(calls == 1, "commits and reload after recipe failure must not retry library");
            }
            LocalShop.TestOverride = null;
            string ownedPath = Fresh("recipes-owned");
            var product = LocalShop.Default.Product("deck-egg-death-knight");
            var excludedCopies = new Dictionary<string, int> { ["TIME_003"] = 2, ["TLC_252"] = 2, ["TLC_106"] = 1 };
            Check(!FairPackRules.Eligible("STANDARD", 1).Any(card => excludedCopies.ContainsKey(card.Id)),
                "grant-only recipe cards stay outside random packs");
            long purchasedDeck;
            using (var profile = new LocalProfile(ownedPath)) {
                var receipt = profile.Purchase(product.Id, 1, profile.PurchasePrice(product.Id), "owned-deck");
                purchasedDeck = receipt.Granted.DeckId!.Value;
                Check(excludedCopies.All(row => receipt.Granted.Cards.Single(card => card.CardId == row.Key).CountAfter == row.Value
                    && profile.Collection[row.Key] == row.Value && profile.Acquired[row.Key] == row.Value),
                    "ready-made deck grants pack-excluded Standard copies and acquisition history");
                var recipe = LocalShop.Default.Recipe(product.RecipeId);
                LocalShop.ValidateDeck(recipe.Name, recipe.Hero, profile.GetDraft(purchasedDeck).Cards, null, true);
            }
            int ownedCalls = 0;
            LocalShop.TestOverride = Shop(() => { ownedCalls++; throw new InvalidDataException("invalid library"); });
            using (var profile = new LocalProfile(ownedPath)) {
                Check(profile.OwnedRecipes.Contains(product.RecipeId) && profile.GetDraft(purchasedDeck).SourceRecipeId == product.RecipeId,
                    "owned recipes and purchased decks load without library");
                Check(excludedCopies.All(row => profile.Collection[row.Key] == row.Value && profile.Acquired[row.Key] == row.Value),
                    "pack-excluded purchases persist without recipe library");
                profile.BuyPack();
                Check(ownedCalls == 0, "historical deck receipts must not initialize recipes");
                byte[] before = File.ReadAllBytes(ownedPath);
                Refused(() => profile.SaveDeckFromRecipe(product.RecipeId, profile.GetDraft(purchasedDeck)));
                Check(ownedCalls == 1 && File.ReadAllBytes(ownedPath).SequenceEqual(before), "recipe save refusal must preserve profile");
                var draft = profile.CreateDraft("Still custom", "HERO_08");
                profile.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { "CS2_029" }, draft.SortOrder);
            }
            using (var profile = new LocalProfile(ownedPath))
                Check(profile.CustomDecks.Count == 2 && profile.PurchaseSequence == 2 && ownedCalls == 1,
                    "owned recipe profile survives non-recipe commits and reload after failed library");
            Console.WriteLine("PASS recipe availability: lazy cached failures, custom saves, packs, owned recipe/deck reload and atomic refusals");
        }
        finally { LocalShop.TestOverride = null; }
    }
}

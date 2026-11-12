using System.Xml.Serialization;

internal static class AzalinaDeckPurchaseTests
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh)
    {
        var previous = LocalShop.TestOverride;
        try
        {
            // Test-only recipe swap exercises the real product/grant/receipt path without shipping a new product.
            using var recipes = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.recipes-v1.json")!;
            var library = RecipeLibrary.LoadText(new StreamReader(recipes).ReadToEnd());
            var recipe = library.Recipes.Single(row => row.Id == "medivh-imbue-priest");
            recipe.Name = "Azalina test deck";
            var ids = LocalDecks.Get("test-standard-azalina-priest").CardIds;
            recipe.Cards = ids.GroupBy(id => id).Select(group => new RecipeCard { Id = group.Key, Count = group.Count() }).ToList();
            recipe.Guide.StrategyCards = recipe.Guide.KeepCards = new List<string> { ids[0] };   // guide card lists must name deck cards
            check(library.Validate().Count == 0, "synthetic twenty-card shop recipe validates");
            using var catalog = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json")!;
            LocalShop.TestOverride = LocalShop.Load(catalog, () => library);
            string path = fresh("azalina-purchase", 100000);
            long deckId;
            using (var profile = new LocalProfile(path))
            {
                var product = LocalShop.Default.Products.Single(row => row.Kind == "Deck" && row.RecipeId == recipe.Id);
                int price = profile.PurchasePrice(product.Id);
                byte[] before = File.ReadAllBytes(path);
                LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("Azalina purchase fault"); };
                try { reject(() => profile.Purchase(product.Id, 1, price, "azalina-buy"), "twenty-card purchase failure"); }
                finally { LocalProfile.CommitFaultHook = null; }
                check(before.SequenceEqual(File.ReadAllBytes(path)) && profile.CustomDecks.Count == 0, "failed purchase is atomic");
                var receipt = profile.Purchase(product.Id, 1, price, "azalina-buy");
                deckId = receipt.Granted.DeckId.Value;
                var saved = profile.GetDraft(deckId);
                check(saved.Cards.SequenceEqual(ids) && profile.IsDeckComplete(deckId)
                    && LocalProfile.PlayableDeck(saved.Name, saved.Hero, saved.Cards, saved.Sideboards).Build().Count == 20,
                    "real GrantDeck and LedgerDeck persist a complete twenty-card purchase");
                check(profile.Purchase(product.Id, 1, price, "azalina-buy").Granted.DeckId == deckId
                    && profile.CustomDecks.Count == 1, "purchase retry does not duplicate twenty-card deck");
            }
            using (var profile = new LocalProfile(path))
                check(profile.GetDraft(deckId).Cards.SequenceEqual(ids) && profile.IsDeckComplete(deckId)
                    && profile.Purchases.Single().Granted.DeckId == deckId, "twenty-card purchased deck and receipt survive restart");
        }
        finally { LocalProfile.CommitFaultHook = null; LocalShop.TestOverride = previous; }
        Console.WriteLine("PASS Azalina ready-made deck purchase, failure, retry and restart");
    }
}

using System.Text;
using System.Text.Json.Nodes;

// Shop quote memo (LocalProfile.PurchasePrice) and the cached clone serializer: exact invalidation, errors never memoized.
internal static class QuoteMemoTests
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh)
    {
        // Clone keeps deep-copy semantics with a cached serializer.
        var source = new LocalPurchaseReceipt { RequestId = "source", Granted = new LocalPurchaseGrants {
            Packs = new List<LocalPackCount> { new() { PackType = "STANDARD", Count = 1 } } } };
        var first = LocalProfile.Clone(source); var second = LocalProfile.Clone(source);
        first.RequestId = "changed"; first.Granted.Packs[0].Count = 9; first.Granted.Packs.Add(new LocalPackCount());
        check(!ReferenceEquals(first, second) && !ReferenceEquals(first.Granted, source.Granted)
            && source.RequestId == "source" && source.Granted.Packs.Count == 1 && source.Granted.Packs[0].Count == 1
            && second.RequestId == "source" && second.Granted.Packs.Count == 1 && second.Granted.Packs[0].Count == 1,
            "cached-serializer clones stay deep, detached copies");
        var shop = LocalShop.Default;
        var pack = shop.Products.First(p => p.Kind == "Pack" && !p.verify);
        var leaked = shop.Product(pack.Id); leaked.Price = 1;
        var recipeId = shop.Recipes[0].Id;
        shop.Recipe(recipeId).Cards.Clear();
        check(shop.Product(pack.Id).Price == pack.Price && !ReferenceEquals(shop.Product(pack.Id), shop.ProductRef(pack.Id))
            && shop.Recipe(recipeId).Cards.Count > 0 && ReferenceEquals(shop.RecipeRef(recipeId), shop.RecipeRef(recipeId)),
            "public product and recipe reads stay detached; internal refs are the catalogue's own");

        string path = fresh("quote-memo", 1000000);
        var decks = shop.Products.Where(p => p.Kind == "Deck" && !p.verify && !shop.Withheld(p)).ToArray();
        var recipes = decks.ToDictionary(d => d.Id, d => new HashSet<string>(shop.Recipe(d.RecipeId).Cards.Select(c => c.Id)
            .Where(id => !LocalCardCatalog.IsCore(id))));
        var bought = decks.First(d => decks.Any(o => o != d && recipes[o.Id].Overlaps(recipes[d.Id])));
        Dictionary<string, int> before, after;
        using (var profile = new LocalProfile(path))
        {
            before = decks.ToDictionary(d => d.Id, d => profile.PurchasePrice(d.Id));
            object ledger = profile.LedgerIdentity;
            check(decks.All(d => profile.PurchasePrice(d.Id) == before[d.Id]) && ReferenceEquals(ledger, profile.LedgerIdentity),
                "repeated quotes are stable for one ledger");
            // Errors throw on every call, before and after the same product was quoted successfully.
            for (int i = 0; i < 2; i++)
            {
                reject(() => profile.PurchasePrice(pack.Id, 0), "invalid quantity throws every call");
                reject(() => profile.PurchasePrice(pack.Id, 51), "over-max quantity throws every call");
                reject(() => profile.PurchasePrice("no-such-product"), "unknown product throws every call");
                reject(() => profile.PurchasePrice(null!), "null product throws every call");
            }
            check(profile.PurchasePrice(pack.Id, 3) == pack.Price * 3 && profile.PurchasePrice(pack.Id) == pack.Price, "quantity is part of the memo key");
            var verify = shop.Products.FirstOrDefault(p => p.verify);
            if (verify != null) for (int i = 0; i < 2; i++) reject(() => profile.PurchasePrice(verify.Id), "pending native check throws every call");
            profile.Purchase(bought.Id, 1, before[bought.Id], "memo-deck");
            check(!ReferenceEquals(ledger, profile.LedgerIdentity), "a commit replaces the ledger identity");
            after = decks.ToDictionary(d => d.Id, d => profile.PurchasePrice(d.Id));
            check(after[bought.Id] == 0, "the bought deck's quote drops to zero at once");
            check(decks.Any(d => d != bought && recipes[d.Id].Overlaps(recipes[bought.Id]) && after[d.Id] < before[d.Id]),
                "a deck sharing the granted cards is cheaper at once");
            // Shop swap (TestOverride): the same profile quotes the new instance at once, and the original after restore.
            var previous = LocalShop.TestOverride;
            try
            {
                string text;
                using (var stream = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json")!)
                    text = new StreamReader(stream).ReadToEnd();
                var json = JsonNode.Parse(text)!;
                foreach (var row in json["Products"]!.AsArray())
                {
                    if ((string)row!["Id"]! == pack.Id) row["Price"] = pack.Price + 7;
                    row["verify"] = false;
                }
                var library = shop.Library;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString())))
                    LocalShop.TestOverride = LocalShop.Load(stream, () => library);
                check(profile.PurchasePrice(pack.Id) == pack.Price + 7 && profile.PurchasePrice(pack.Id, 2) == (pack.Price + 7) * 2,
                    "a swapped shop instance is quoted at once");
                if (verify != null) check(profile.PurchasePrice(verify.Id) > 0, "a product no longer pending is quoted on the swapped shop");
            }
            finally { LocalShop.TestOverride = previous; }
            check(profile.PurchasePrice(pack.Id) == pack.Price, "restoring the shop restores its quotes");
            if (verify != null) reject(() => profile.PurchasePrice(verify.Id), "restored shop: pending product throws again");
            // Reads from another thread see the same memo safely.
            var quotes = new int[4][];
            var threads = Enumerable.Range(0, 4).Select(t => new Thread(() => quotes[t] = decks.Select(d => profile.PurchasePrice(d.Id)).ToArray())).ToArray();
            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();
            check(quotes.All(q => q.SequenceEqual(decks.Select(d => after[d.Id]))), "concurrent quotes agree");
        }
        // An independent profile instance (fresh memo) agrees with every memoized quote.
        using (var reopened = new LocalProfile(path))
            check(decks.All(d => reopened.PurchasePrice(d.Id) == after[d.Id]), "memoized quotes equal a fresh computation");
        Console.WriteLine("PASS quote memo: deep clones, stable quotes, exact invalidation on commit and shop swap, errors every call");
    }
}

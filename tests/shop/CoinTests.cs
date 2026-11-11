using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using System.Xml.Serialization;

internal static class CoinTests
{
    internal static void Run(string root, Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh)
    {
        var shipped = LocalShop.Default;
        var shippedCoins = shipped.Products.Where(p => p.Kind == "Coin").ToArray();
        check(shippedCoins.Length == 30 && shippedCoins.All(p => p.Coin > 1 && p.Id == "coin-" + p.Coin && !p.verify),
            "30 shipped coins natively admitted (lane 2 check 2026-11-01); default coin never sold");
        using var fixture = new CosmeticsFixture(coins: true);
        var shop = LocalShop.Default;
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData Read(string path) { using var f = File.OpenRead(path); return (LocalProfileData)serializer.Deserialize(f)!; }
        void Write(string path, LocalProfileData data) { using var f = File.Create(path); serializer.Serialize(f, data); }
        string Catalog() {
            using var source = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json");
            return new StreamReader(source!).ReadToEnd();
        }
        LocalShop Load(JsonNode node) {
            using var f = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
            return LocalShop.Load(f, () => shop.Library);
        }
        JsonNode CoinsCatalog() {
            var node = JsonNode.Parse(Catalog())!;
            var entries = node["Products"]!.AsArray();
            foreach (var product in entries.Where(p => (string)p!["Kind"] == "Coin").ToArray()) entries.Remove(product);
            var source = node["CosmeticsSource"]!;
            source["ClientCoins"] = new JsonArray(source["ClientCoins"]!.AsArray().Select(n => (int)n!).Concat(new[] { 2, 3 })
                .Distinct().OrderBy(id => id).Select(id => JsonValue.Create(id)).ToArray());
            source["PriceHistory"]!["Coin"] = new JsonArray(source["PriceHistory"]!["Coin"]!.AsArray().Select(n => (int)n!).Concat(new[] { 300, 800 })
                .Distinct().OrderBy(price => price).Select(price => JsonValue.Create(price)).ToArray());
            foreach (var p in shop.Products.Where(p => p.Kind == "Coin"))
                node["Products"]!.AsArray().Add(JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(p,
                    new System.Text.Json.JsonSerializerOptions { IncludeFields = true })));
            return node;
        }
        foreach (var mutate in new Action<JsonNode>[] {
            n => n["Products"]!.AsArray().Last()!["Coin"] = 1,
            n => n["Products"]!.AsArray().Last()!["Coin"] = 999,
            n => n["Products"]!.AsArray().Last()!["Coin"] = null,
            n => n["Products"]!.AsArray().Last()!["Kind"] = "CardBack",
            n => n["CosmeticsSource"]!["ClientCoins"] = new JsonArray(1),
            n => n["CosmeticsSource"]!["ClientCoins"] = new JsonArray(2, 2),
            n => n["Products"]!.AsArray().Add(n["Products"]!.AsArray().Last()!.DeepClone()),
        }) { var node = CoinsCatalog(); mutate(node); reject(() => Load(node), "invalid coin catalogue refused"); }

        NativeBundleHarness.RunCoinPurchase(check, fresh);
        string path = fresh("coin-purchase", 1100);
        using (var p = new LocalProfile(path)) {
            check(p.OwnedCoins.SequenceEqual(new[] { 1 }) && p.FavoriteCoins.SequenceEqual(new[] { 1 }), "default coin always owned and favorite");
            byte[] before = File.ReadAllBytes(path);
            foreach (string id in new[] { "coin-1", "coin-999" }) reject(() => p.Purchase(id, 1, 300, "bad-" + id), "unknown/default coin refused");
            reject(() => p.SetCoinFavorite(2, true), "unowned coin favorite refused");
            reject(() => p.Purchase("coin-2", 1, 500, "stale"), "stale coin quote refused");
            check(before.SequenceEqual(File.ReadAllBytes(path)), "refusals preserve old bytes");
            var receipt = p.Purchase("coin-2", 1, 300, "coin-order");
            check(receipt.Kind == "Coin" && receipt.GoldPaid == 300 && receipt.PriceBeforeKit == 300 && receipt.Granted.Coin == 2
                && p.Gold == 800 && p.OwnedCoins.SequenceEqual(new[] { 1, 2 }) && p.PurchaseSequence == 1, "coin exact debit receipt and ownership");
            before = File.ReadAllBytes(path);
            check(p.Purchase("coin-2", 1, 300, "coin-order").Sequence == 1, "coin idempotent replay");
            reject(() => p.Purchase("coin-3", 1, 300, "coin-order"), "coin conflicting replay refused");
            reject(() => p.Purchase("coin-2", 1, 300, "duplicate"), "already owned coin refused");
            check(before.SequenceEqual(File.ReadAllBytes(path)), "coin replay/refusals have no partial write");
            p.SetCoinFavorite(2, true);
            check(p.FavoriteCoins.SequenceEqual(new[] { 1, 2 }), "coin multiple favorites");
            var random = new Random(7);
            var rolled = Enumerable.Range(0, 100).Select(_ => p.MatchCoin(random)).ToHashSet();
            check(rolled.SetEquals(new[] { 1, 2 }), "coin match rolls favorites");
            p.SetCoinFavorite(1, false);
            check(p.MatchCoin(new Random(1)) == 2, "coin default can be unequipped");
            NativeBundleHarness.RunCoins(check, reject, p);
        }
        using (var p = new LocalProfile(path)) {
            check(p.Gold == 800 && p.OwnedCoins.SequenceEqual(new[] { 1, 2 }) && p.FavoriteCoins.SequenceEqual(new[] { 2 }), "coin purchase and equip survive restart");
            p.SetCoinFavorite(2, false);
            check(p.FavoriteCoins.SequenceEqual(new[] { 1 }), "last coin favorite restores default");
            p.SetCoinFavorite(1, false);
            check(p.FavoriteCoins.SequenceEqual(new[] { 1 }), "default coin cannot leave empty favorites");
        }
        string poor = fresh("coin-poor", 299);
        using (var p = new LocalProfile(poor)) {
            byte[] before = File.ReadAllBytes(poor);
            reject(() => p.Purchase("coin-2", 1, 300, "short"), "coin insufficient gold refused");
            check(p.Gold == 299 && p.PurchaseSequence == 0 && before.SequenceEqual(File.ReadAllBytes(poor)), "short coin purchase unchanged");
        }
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            string file = fresh("coin-fault-" + stage, 300);
            using var p = new LocalProfile(file);
            byte[] before = File.ReadAllBytes(file);
            p.CommitFailure = point => { if (point == stage) throw new IOException(stage); };
            reject(() => p.Purchase("coin-2", 1, 300, "fault"), "coin atomic " + stage);
            check(p.Gold == 300 && p.PurchaseSequence == 0 && !p.OwnsCoin(2) && before.SequenceEqual(File.ReadAllBytes(file)), "coin failed transaction preserves memory and bytes");
            p.CommitFailure = null;
            p.Purchase("coin-2", 1, 300, "fault");
            before = File.ReadAllBytes(file);
            p.CommitFailure = point => { if (point == stage) throw new IOException(stage); };
            reject(() => p.SetCoinFavorite(2, true), "coin favorite atomic " + stage);
            check(p.FavoriteCoins.SequenceEqual(new[] { 1 }) && before.SequenceEqual(File.ReadAllBytes(file)), "failed coin favorite preserves state");
        }
        var currentOnly = CoinsCatalog(); currentOnly["CosmeticsSource"]!["PriceHistory"]!.AsObject().Remove("Coin");
        LocalShop.TestOverride = Load(currentOnly);
        using (var p = new LocalProfile(path)) check(p.OwnsCoin(2), "current coin price accepted without a legacy price or history");
        var changed = CoinsCatalog();
        changed["Products"]!.AsArray().Single(n => (string)n!["Id"] == "coin-2")!["Price"] = 900;
        LocalShop.TestOverride = Load(changed);
        using (var p = new LocalProfile(path)) check(p.OwnsCoin(2), "historic coin price accepted");
        changed["Products"]!.AsArray().Remove(changed["Products"]!.AsArray().Single(n => (string)n!["Id"] == "coin-2"));
        LocalShop.TestOverride = Load(changed);
        using (var p = new LocalProfile(path)) {
            check(p.OwnsCoin(2), "withdrawn coin receipt validates from client IDs and price history");
            byte[] before = File.ReadAllBytes(path);
            check(p.Purchase("coin-2", 1, 300, "coin-order").Sequence == 1 && before.SequenceEqual(File.ReadAllBytes(path)), "withdrawn coin replay stays byte exact");
        }
        foreach (int price in new[] { 0, 500, 777 }) {
            var bad = Read(path); bad.Purchases[0].GoldPaid = bad.Purchases[0].PriceBeforeKit = price;
            bad.GoldBalance = 1100 - price;
            string file = Path.Combine(root, "coin-forged-" + price + ".xml"); Write(file, bad);
            reject(() => { using var p = new LocalProfile(file); }, "coin unknown price refused; no legacy flat price");
        }
        LocalShop.TestOverride = shop;
        string old = fresh("coin-old-v15", 300);
        var xml = XDocument.Load(old);
        xml.Root!.Element("OwnedCoins")!.Remove(); xml.Root.Element("FavoriteCoins")!.Remove(); xml.Save(old);
        byte[] oldBytes = File.ReadAllBytes(old);
        using (var p = new LocalProfile(old)) check(p.OwnedCoins.SequenceEqual(new[] { 1 }) && p.FavoriteCoins.SequenceEqual(new[] { 1 }), "V15 without coin elements loads defaults");
        check(oldBytes.SequenceEqual(File.ReadAllBytes(old)), "old V15 read does not rewrite save");
        using (var p = new LocalProfile(fresh("coin-cheat", 0), new ProfileOptions { AllCosmetics = true })) {
            check(p.OwnsCoin(2) && p.OwnedCoins.SequenceEqual(new[] { 1, 2, 3 }), "AllCosmetics coin ownership is a projection");
            p.SetCoinFavorite(2, true);
        }
        Console.WriteLine("PASS coins: ledger, replay, failures, restart, historic/withdrawn receipts, favorites and V15 defaults");
    }
}

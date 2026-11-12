using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Serialization;

// Honored win portraits (LocalClassRewards.Portraits) are earn-only: never listed or sold, but earlier purchases keep working.
internal static class EarnOnlyPortraitTests
{
    internal static void Run(string root, Action<bool, string> check, Action<Action, string> reject)
    {
        var honored = LocalClassRewards.Portraits.Select(row => row.Honored).ToArray();
        check(honored.Length == 11, "eleven earn-only honored portraits");
        var real = LocalShop.Default;
        check(real.Products.All(p => p.Kind != "HeroSkin" || !honored.Contains(p.HeroSkin)), "real catalogue lists no earn-only portrait");
        check(real.Products.Any(p => p.Kind == "HeroSkin"), "other hero skins stay on sale");

        // A catalogue that still contains them must not list or sell them.
        using var resource = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json")!;
        var json = JsonNode.Parse(new StreamReader(resource).ReadToEnd())!;
        var products = json["Products"]!.AsArray();
        string hero = LocalClassRewards.Portraits.Single(row => row.Honored == "HERO_08c").Hero;
        products.Add(new JsonObject { ["Id"] = "heroskin-HERO_08c", ["Kind"] = "HeroSkin", ["Price"] = 1000, ["MinQuantity"] = 1,
            ["MaxQuantity"] = 1, ["Class"] = hero, ["HeroSkin"] = "HERO_08c", ["Description"] = "Honored", ["verify"] = false });
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString()));
        var injected = LocalShop.Load(stream, () => throw new Exception("Recipe load must stay lazy"));
        check(injected.Products.All(p => p.HeroSkin != "HERO_08c") && injected.Products.Count == real.Products.Count, "injected earn-only product is not listed");
        reject(() => injected.Product("heroskin-HERO_08c"), "injected earn-only product is not purchasable");
        check(injected.IsHeroSkinForClass(hero, "HERO_08c") && injected.HeroSkinIds.Contains("HERO_08c"), "earned portrait stays a valid skin for its class");

        // Purchase is refused on a profile; ownership from an earlier purchase survives load and restart.
        string path = Path.Combine(root, "earn-only.xml");
        using (var p = new LocalProfile(path)) p.SelectCardBack(0);
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData data;
        using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
        data.GoldBalance = 5000;
        using (var file = File.Create(path)) serializer.Serialize(file, data);
        using (var p = new LocalProfile(path))
            reject(() => p.Purchase("heroskin-HERO_08c", 1, 1000, "earn-only-buy"), "earn-only portrait purchase refused");
        data.OwnedHeroSkins.Add("HERO_08c"); data.PurchaseSequence = 1; data.GoldBalance -= 1000;
        data.Purchases.Add(new LocalPurchaseReceipt { Sequence = 1, RequestId = "prior-honored", CatalogVersion = real.Version,
            ProductId = "heroskin-HERO_08c", Kind = "HeroSkin", Quantity = 1, GoldPaid = 1000, PriceBeforeKit = 1000,
            Granted = new LocalPurchaseGrants { HeroSkin = "HERO_08c" } });
        using (var file = File.Create(path)) serializer.Serialize(file, data);
        using (var p = new LocalProfile(path))
            check(p.OwnedHeroSkins.Contains("HERO_08c") && p.Gold == 4000, "earlier honored purchase kept on load");
        using (var p = new LocalProfile(path))
            check(p.OwnedHeroSkins.Contains("HERO_08c"), "earlier honored purchase kept on restart");
    }
}

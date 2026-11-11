using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class ShopJsonTests
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        string Resource(string name) {
            using var stream = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop." + name + "-v1.json")!;
            return new StreamReader(stream).ReadToEnd();
        }
        LocalShop Catalog(string text) {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var shop = LocalShop.Load(stream, () => throw new Exception("Recipe load must stay lazy"));
            check(stream.CanRead, "catalog stream remains caller-owned");
            return shop;
        }
        void SameFields(object model, JsonNode expected) {
            var actual = JsonSerializer.SerializeToNode(model, model.GetType(), new JsonSerializerOptions { IncludeFields = true })!;
            foreach (var field in model.GetType().GetFields()) {
                var value = expected[field.Name];
                if (!expected.AsObject().ContainsKey(field.Name) && field.FieldType.IsValueType
                    && Nullable.GetUnderlyingType(field.FieldType) == null)
                    value = JsonSerializer.SerializeToNode(Activator.CreateInstance(field.FieldType));
                check(JsonNode.DeepEquals(actual[field.Name], value), "JSON field preserved: " + field.Name);
            }
        }
        string catalog = Resource("catalog");
        var expectedCatalog = JsonNode.Parse(catalog)!;
        var shop = Catalog(catalog);
        var backs = shop.Products.Where(p => p.Kind == "CardBack").ToArray();
        check(backs.Length == 47 && backs.All(p => p.Id == "cardback-" + p.CardBack), "DBF card backs listed (verify flag set by native admission)");
        check(!backs.Any(p => p.CardBack == 0 || p.CardBack == 158
            || LocalShop.LadderExclusiveCardBacks.Values.Contains(p.CardBack.Value)), "default, random and reserved backs excluded");
        var skins = expectedCatalog["Products"]!.AsArray().Where(p => (string)p!["Kind"]! == "HeroSkin").ToArray();
        check(skins.Length == 77 && skins.Count(p => (string)p!["Curation"]! == "Primary") == 55
            && skins.Count(p => (string)p!["Curation"]! == "Alternate") == 22, "curated hero counts");
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "data/shop/cosmetics-curation-v1.json"))) directory = directory.Parent;
        var curation = JsonNode.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "data/shop/cosmetics-curation-v1.json")))!;
        var curatedSkins = curation["HeroSkins"]!.AsObject().SelectMany(hero => hero.Value!.AsObject()
            .SelectMany(group => group.Value!.AsArray().Select(id => hero.Key + ":" + (string)id! + ":" + (group.Key == "Primary" ? "Primary" : "Alternate")))).ToHashSet();
        check(curatedSkins.SetEquals(skins.Select(p => (string)p!["Class"]! + ":" + (string)p["HeroSkin"]! + ":" + (string)p["Curation"]!))
            && curation["CardBacks"]!.AsArray().Select(id => (int)id!).ToHashSet().SetEquals(backs.Select(p => p.CardBack!.Value)), "catalogue matches curation data");
        var coins = shop.Products.Where(p => p.Kind == "Coin").ToArray();
        check(coins.Length == 30 && curation["Coins"]!.AsArray().Select(id => (int)id!).ToHashSet()
            .SetEquals(coins.Select(p => p.Coin!.Value)), "shipped coins exactly match curation");
        check(coins.All(p => p.Coin > 1 && !p.verify), "shipped coins admitted; default never sold");
        // Three price tiers per kind, cheapest first; every curated item sells at exactly its tier price.
        var cosmetics = expectedCatalog["Products"]!.AsArray().Where(p => (string)p!["Kind"]! is "HeroSkin" or "CardBack" or "Coin")
            .ToDictionary(p => (string)p!["Id"]!, p => p!);
        foreach (var (kind, prices) in new[] { ("HeroSkin", new[] { 1000, 1800, 2500 }), ("CardBack", new[] { 300, 500, 800 }), ("Coin", new[] { 250, 400, 600 }) }) {
            var tiers = curation["PriceTiers"]![kind]!.AsArray();
            check(tiers.Select(t => (int)t!["Price"]!).SequenceEqual(prices), kind + " tier prices " + string.Join("/", prices));
            check(tiers.Sum(t => t!["Items"]!.AsArray().Count) == (kind == "HeroSkin" ? 77 : kind == "CardBack" ? 47 : 30), kind + " tiers cover every curated item");
            foreach (var tier in tiers)
                foreach (var item in tier!["Items"]!.AsArray()) {
                    var row = cosmetics[(kind == "HeroSkin" ? "heroskin-" : kind == "CardBack" ? "cardback-" : "coin-") + item];
                    check((string)row["Kind"]! == kind && (int)row["Price"]! == (int)tier["Price"]! && (string)row["Tier"]! == (string)tier["Name"]!,
                        "tier price applied: " + item);
                }
        }
        // Curation must not invalidate receipts, ownership or favourites for a retired product.
        var original = LocalShop.TestOverride;
        string path = Path.Combine(Path.GetTempPath(), "shop-curation-" + Guid.NewGuid() + ".xml");
        try {
            using (var profile = new LocalProfile(path)) profile.SelectCardBack(0);
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(LocalProfileData));
            LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
            data.GoldBalance = 10000;
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            using (var fixture = new CosmeticsFixture())
            using (var profile = new LocalProfile(path)) {
                profile.Purchase("heroskin-HERO_08b", 1, 1000, "retired-hero");
                profile.Purchase("cardback-99", 1, 800, "retired-back");
                profile.SetFavoriteHeroSkin("HERO_08", "HERO_08b"); profile.SelectCardBack(99);
            }
            var retired = JsonNode.Parse(catalog)!;
            var entries = retired["Products"]!.AsArray();
            foreach (var entry in entries.Where(p => (string)p!["Id"]! == "heroskin-HERO_08b" || (string)p!["Id"]! == "cardback-99").ToArray()) entries.Remove(entry);
            LocalShop.TestOverride = Catalog(retired.ToJsonString());
            byte[] before = File.ReadAllBytes(path);
            using (var profile = new LocalProfile(path)) {
                check(profile.OwnedHeroSkins.Contains("HERO_08b") && profile.OwnedCardBacks.Contains(99)
                    && profile.FavoriteCardBacks.Single() == 99 && profile.FavoriteHeroSkins["HERO_08"] == "HERO_08b", "retired cosmetics remain owned and selected after restart");
                check(profile.Purchase("heroskin-HERO_08b", 1, 1000, "retired-hero").Sequence == 1
                    && before.SequenceEqual(File.ReadAllBytes(path)), "retired purchase replay preserves bytes");
                reject(() => LocalShop.Default.Product("heroskin-HERO_08b"), "retired cosmetic is not sold");
            }
        }
        finally { LocalShop.TestOverride = original; File.Delete(path); }
        var reserved = expectedCatalog["CosmeticsSource"]!["ReservedLadderCardBacks"]!.AsObject();
        check(reserved.Count == 7 && LocalShop.LadderExclusiveCardBacks.Count == 7, "seven ladder reservations");
        foreach (var tier in LocalShop.LadderExclusiveCardBacks)
            check(tier.Value == (int)reserved[tier.Key]!["Id"]!, "runtime reservation agrees with generated DBF data: " + tier.Key);
        check(shop.Version == (int)expectedCatalog["Version"]!, "catalog version preserved");
        check(shop.Products.Count == expectedCatalog["Products"]!.AsArray().Count, "catalog count preserved");
        for (int i = 0; i < shop.Products.Count; i++) SameFields(shop.Products[i], expectedCatalog["Products"]![i]!);
        check(Catalog(catalog.Insert(1, "\"unknown\":null,")).Version == 1, "unknown fields remain ignored");
        var optional = JsonNode.Parse(catalog)!;
        optional["Products"]![0]!["CardBack"] = null;
        optional["Products"]![0]!["NativeStatus"] = "pending";
        optional["Products"]![0]!["verify"] = true;
        var product = Catalog(optional.ToJsonString()).Products[0];
        check(product.CardBack == null && product.NativeStatus == "pending" && product.verify, "optional nullable/string/bool fields preserved");
        foreach (string field in new[] { "Price", "MinQuantity", "MaxQuantity", "CardBack", "verify", "Id", "Description", "Curation" }) {
            var wrong = JsonNode.Parse(catalog)!;
            wrong["Products"]![0]![field] = new JsonObject();
            reject(() => Catalog(wrong.ToJsonString()), "reject wrong field type: " + field);
        }
        foreach (var value in new JsonNode[] { JsonValue.Create(1.5), JsonValue.Create(2147483648L), JsonValue.Create(-2147483649L), null }) {
            var wrong = JsonNode.Parse(catalog)!; wrong["Products"]![0]!["Price"] = value;
            reject(() => Catalog(wrong.ToJsonString()), "reject fractional/out-of-range/null price");
        }
        foreach (string bad in new[] { "null", "[]", "{}", catalog + "{}", catalog.Replace("1,", "1"),
            catalog.Replace("\"Version\": 1", "\"Version\": 1,\"Version\": 1"),
            catalog.Replace("\"Version\": 1", "\"Version\": 1,\"Ver\\u0073ion\": 1"),
            catalog.Replace("\"Version\": 1", "\"Version\": 01"), catalog.Insert(catalog.LastIndexOf('}'), ","),
            catalog.Replace("Five Normal", "Five\nNormal"), catalog.Replace("Five Normal", @"Five\qNormal"), catalog.TrimEnd()[..^1] })
            reject(() => Catalog(bad), "reject malformed catalog JSON");
        Console.WriteLine("PASS shop JSON fixtures, fields/defaults, malformed syntax/types");
    }
}

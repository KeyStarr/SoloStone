using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Serialization;

// Cosmetic price tiers (2026-10-25): tier prices come from the generated catalogue; receipts paid at the old flat
// prices (1000 skin, 500 back) stay valid after an item moved tier, new purchases pay the tier price.
internal static class TieredCosmeticsTests
{
    internal static void Run(string root, Action<bool, string> check, Action<Action, string> reject)
    {
        string text;
        using (var source = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json")!)
            text = new StreamReader(source).ReadToEnd();
        var original = LocalShop.TestOverride;
        var library = LocalShop.Default.Library;
        LocalShop Load(JsonNode node) {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
            return LocalShop.Load(stream, () => library);
        }
        JsonNode Admitted() {
            var node = JsonNode.Parse(text)!;
            foreach (var p in node["Products"]!.AsArray()) if (p!["verify"] != null) p["verify"] = false;
            return node;
        }
        var tiered = Load(Admitted());
        // Legendary, Diamond and Classic skins; Legendary, Ornate and Classic backs.
        var expected = new Dictionary<string, int> {
            ["heroskin-HERO_01bb"] = 2500, ["heroskin-HERO_10am"] = 1800, ["heroskin-HERO_08a"] = 1000,
            ["cardback-99"] = 800, ["cardback-56"] = 800, ["cardback-122"] = 500, ["cardback-9"] = 300 };
        foreach (var row in expected)
            check(tiered.Product(row.Key).Price == row.Value, "tier price " + row.Key + " = " + row.Value);
        // Pre-tier catalogue: the same items at the flat prices they used to cost.
        var flat = Admitted();
        foreach (var p in flat["Products"]!.AsArray().Where(p => expected.ContainsKey((string)p!["Id"]!)))
            p!["Price"] = (string)p["Kind"]! == "HeroSkin" ? LocalShop.LegacyHeroSkinPrice : LocalShop.LegacyCardBackPrice;
        var legacy = new[] { "heroskin-HERO_01bb", "heroskin-HERO_10am", "cardback-99", "cardback-9" };
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        string path = Path.Combine(root, "tiered-cosmetics.xml");
        try {
            using (var profile = new LocalProfile(path)) profile.SelectCardBack(0);
            LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
            data.GoldBalance = 10000;
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            LocalShop.TestOverride = Load(flat);
            using (var profile = new LocalProfile(path))
                foreach (string id in legacy) profile.Purchase(id, 1, profile.PurchasePrice(id), "flat-" + id);
            LocalShop.TestOverride = tiered;
            byte[] before = File.ReadAllBytes(path);
            using (var profile = new LocalProfile(path)) {
                check(profile.Gold == 7000 && profile.OwnedHeroSkins.Contains("HERO_01bb") && profile.OwnedHeroSkins.Contains("HERO_10am")
                    && profile.OwnedCardBacks.Contains(99) && profile.OwnedCardBacks.Contains(9),
                    "flat-price receipts stay valid after their items moved tier, up or down");
                check(profile.Purchase("cardback-99", 1, LocalShop.LegacyCardBackPrice, "flat-cardback-99").Sequence == 3
                    && File.ReadAllBytes(path).SequenceEqual(before), "flat-price request replay keeps the paid price, byte-exact");
                check(profile.PurchasePrice("cardback-56") == 800, "new purchase quotes the tier price");
                reject(() => profile.Purchase("cardback-56", 1, LocalShop.LegacyCardBackPrice, "stale-quote"), "stale flat quote refused");
                check(File.ReadAllBytes(path).SequenceEqual(before), "stale quote leaves the profile untouched");
                check(profile.Purchase("cardback-56", 1, 800, "tier-back").GoldPaid == 800 && profile.Gold == 6200, "tier purchase pays the tier price");
            }
            using (var profile = new LocalProfile(path)) check(profile.OwnedCardBacks.Contains(56), "tier receipt reloads");
            // A card back receipt price its kind never sold at is forged; one in the append-only history is not.
            var widened = Admitted();
            widened["CosmeticsSource"]!["PriceHistory"]!["CardBack"]!.AsArray().Add(600);
            var historic = Load(widened);
            check(historic.IsHistoricCosmeticPrice("CardBack", 600) && !tiered.IsHistoricCosmeticPrice("CardBack", 600)
                && tiered.IsHistoricCosmeticPrice("CardBack", 300) && tiered.IsHistoricCosmeticPrice("HeroSkin", 2500)
                && !tiered.IsHistoricCosmeticPrice("CardBack", 2500) && !tiered.IsHistoricCosmeticPrice("Pack", 100), "price history per kind");
            foreach (var bad in new JsonNode?[] { JsonValue.Create(0), JsonValue.Create("500"), null }) {
                var broken = Admitted();
                broken["CosmeticsSource"]!["PriceHistory"]!["CardBack"] = new JsonArray(bad);
                reject(() => Load(broken), "invalid price history refused");
            }
            var unknownKind = Admitted();
            unknownKind["CosmeticsSource"]!["PriceHistory"]!["Pack"] = new JsonArray(100);
            reject(() => Load(unknownKind), "price history only for cosmetics");
            foreach (int forged in new[] { 600, 1000, 2500 }) {
                LocalProfileData bad;
                using (var file = File.OpenRead(path)) bad = (LocalProfileData)serializer.Deserialize(file)!;
                var receipt = bad.Purchases.Single(r => r.ProductId == "cardback-99");
                bad.GoldBalance += receipt.GoldPaid - forged;
                receipt.GoldPaid = receipt.PriceBeforeKit = forged;
                string badPath = Path.Combine(root, "tiered-forged-" + forged + ".xml");
                using (var file = File.Create(badPath)) serializer.Serialize(file, bad);
                reject(() => { using var p = new LocalProfile(badPath); }, "forged cosmetic receipt price rejected: " + forged);
                if (forged != 600) continue;
                LocalShop.TestOverride = historic;
                using (var p = new LocalProfile(badPath)) check(p.OwnedCardBacks.Contains(99), "receipt at a historical price stays valid");
                LocalShop.TestOverride = tiered;
            }
        }
        finally { LocalShop.TestOverride = original; }
        Console.WriteLine("PASS tiered cosmetics: tier prices, flat-price receipts kept, new purchases at tier price, forged prices rejected");
    }
}

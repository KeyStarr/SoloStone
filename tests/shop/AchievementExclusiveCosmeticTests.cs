using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Serialization;

internal static class AchievementExclusiveCosmeticTests
{
    internal static void Run(string root, Action<bool, string> check, Action<Action, string> reject)
    {
        var expected = new[] {
            ("cardback-519", "CardBack", "Wyrmrest", 519, (string)null, (string)null),
            ("cardback-516", "CardBack", "End Time", 516, (string)null, (string)null),
            ("cardback-166", "CardBack", "Stolen Thunder", 166, (string)null, (string)null),
            ("cardback-452", "CardBack", "Warlord Draka", 452, (string)null, (string)null),
            ("cardback-138", "CardBack", "Lightforged", 138, (string)null, (string)null),
            ("cardback-189", "CardBack", "Book of Explorers", 189, (string)null, (string)null),
            ("heroskin-HERO_11n", "HeroSkin", "Zombie Queen Scarlet", 0, "HERO_11n", "HERO_11"),
            ("heroskin-HERO_10bm", "HeroSkin", "Gunglaive Kurtrus", 0, "HERO_10bm", "HERO_10"),
            ("heroskin-HERO_06k", "HeroSkin", "Guff Runetotem", 0, "HERO_06k", "HERO_06"),
            ("heroskin-HERO_05ao", "HeroSkin", "Hakkar the Houndmaster", 0, "HERO_05ao", "HERO_05"),
            ("heroskin-HERO_08f", "HeroSkin", "Scholar Jaina", 0, "HERO_08f", "HERO_08"),
            ("heroskin-HERO_04am_Uther", "HeroSkin", "Lightbringer Uther", 0, "HERO_04am_Uther", "HERO_04"),
            ("heroskin-HERO_09bc", "HeroSkin", "Field Medic Xyrella", 0, "HERO_09bc", "HERO_09"),
            ("heroskin-HERO_03i", "HeroSkin", "Chef Scabbs", 0, "HERO_03i", "HERO_03"),
            ("heroskin-HERO_02k", "HeroSkin", "Ten Storm Thrall", 0, "HERO_02k", "HERO_02"),
            ("heroskin-HERO_07e", "HeroSkin", "Felheart Gul'dan", 0, "HERO_07e", "HERO_07"),
            ("heroskin-HERO_01ap_Boulderfist_h", "HeroSkin", "Boulderfist the Ogre", 0, "HERO_01ap_Boulderfist_h", "HERO_01")
        };
        var all = AchievementExclusiveCosmetics.All.ToArray();
        check(all.Length == 17 && all.Count(row => row.Kind == "CardBack") == 6 && all.Count(row => row.Kind == "HeroSkin") == 11,
            "closed achievement cosmetic dimensions");
        check(all.Select(row => (row.Id, row.Kind, row.Name, row.CardBackId, row.HeroSkinId, row.Class)).SequenceEqual(expected),
            "closed achievement cosmetic metadata");

        var rewards = LocalAchievementCatalog.All.Select(row => row.Reward)
            .Where(row => row.CardBackId > 0 || row.HeroSkinId != null)
            .Select(row => row.CardBackId > 0 ? "cardback-" + row.CardBackId : "heroskin-" + row.HeroSkinId)
            .OrderBy(id => id, StringComparer.Ordinal);
        check(rewards.SequenceEqual(all.Select(row => row.Id).OrderBy(id => id, StringComparer.Ordinal)),
            "achievement reward catalog equals closed cosmetic registry");

        var shop = LocalShop.Default;
        check(all.All(row => shop.Products.All(product => product.Id != row.Id)), "achievement cosmetics absent from sale products");
        foreach (var row in all)
        {
            reject(() => shop.Product(row.Id), "achievement cosmetic product lookup: " + row.Id);
            if (row.Kind == "HeroSkin")
                check(shop.HeroSkinIds.Contains(row.HeroSkinId) && shop.IsHeroSkinForClass(row.Class, row.HeroSkinId)
                    && !shop.IsHeroSkinForClass("HERO_99", row.HeroSkinId), "achievement skin class: " + row.Id);
        }
        check(!all.Where(row => row.Kind == "HeroSkin").Select(row => row.HeroSkinId)
            .Intersect(LocalClassRewards.Portraits.Select(row => row.Honored), StringComparer.Ordinal).Any(),
            "achievement skins do not collide with class-win portraits");
        check(!all.Where(row => row.Kind == "CardBack").Select(row => row.CardBackId)
            .Intersect(LocalShop.LadderExclusiveCardBacks.Values).Any(), "achievement backs do not collide with ladder reservations");

        using (var stream = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.npc-cosmetics-v1.json"))
        using (var reader = new StreamReader(stream ?? throw new Exception("Missing NPC cosmetic test resource")))
        {
            var npc = JsonNode.Parse(reader.ReadToEnd())!;
            var npcSkins = npc["heroSkins"]!.AsArray().Select(row => (string)row!["skin"]!).ToHashSet(StringComparer.Ordinal);
            var npcBacks = npc["cardBacks"]!.AsArray().Select(row => (int)row!).ToHashSet();
            check(!all.Any(row => row.Kind == "HeroSkin" ? npcSkins.Contains(row.HeroSkinId) : npcBacks.Contains(row.CardBackId)),
                "achievement cosmetics do not collide with NPC assignments");
        }

        string CatalogText()
        {
            using var stream = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json")!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        var injectedJson = JsonNode.Parse(CatalogText())!;
        injectedJson["Products"]!.AsArray().Add(new JsonObject {
            ["Id"] = "cardback-519", ["Kind"] = "CardBack", ["Price"] = 500, ["MinQuantity"] = 1, ["MaxQuantity"] = 1,
            ["CardBack"] = 519, ["Description"] = "must stay earn-only", ["verify"] = false
        });
        injectedJson["Products"]!.AsArray().Add(new JsonObject {
            ["Id"] = "heroskin-HERO_08f", ["Kind"] = "HeroSkin", ["Price"] = 1000, ["MinQuantity"] = 1, ["MaxQuantity"] = 1,
            ["HeroSkin"] = "HERO_08f", ["Class"] = "HERO_08", ["Description"] = "must stay earn-only", ["verify"] = false
        });
        using var catalog = new MemoryStream(Encoding.UTF8.GetBytes(injectedJson.ToJsonString()));
        var injected = LocalShop.Load(catalog, () => shop.Library);
        check(injected.Products.All(row => row.Id != "cardback-519" && row.Id != "heroskin-HERO_08f"),
            "injected achievement cosmetics filtered from sale");
        reject(() => injected.Product("cardback-519"), "injected achievement back purchase lookup");
        reject(() => injected.Product("heroskin-HERO_08f"), "injected achievement skin purchase lookup");

        string path = Path.Combine(root, "achievement-exclusive-sale.xml");
        var previous = LocalShop.TestOverride;
        try
        {
            LocalShop.TestOverride = injected;
            using var profile = new LocalProfile(path);
            reject(() => profile.Purchase("cardback-519", 1, 0, "exclusive-back"), "achievement back purchase");
            reject(() => profile.Purchase("heroskin-HERO_08f", 1, 0, "exclusive-skin"), "achievement skin purchase");
        }
        finally { LocalShop.TestOverride = previous; File.Delete(path); }

        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData Read(string profilePath) { using var file = File.OpenRead(profilePath); return (LocalProfileData)serializer.Deserialize(file)!; }
        void Write(string profilePath, LocalProfileData data) { using var file = File.Create(profilePath); serializer.Serialize(file, data); }
        foreach (var exclusive in new[] { all.Single(row => row.CardBackId == 519), all.Single(row => row.HeroSkinId == "HERO_08f") })
        {
            string forgedPath = Path.Combine(root, "achievement-exclusive-forged-" + exclusive.Kind + ".xml");
            using (var profile = new LocalProfile(forgedPath)) profile.SelectCardBack(0);
            var funded = Read(forgedPath); funded.GoldBalance = 100000; Write(forgedPath, funded);
            var sale = shop.Products.First(row => row.Kind == exclusive.Kind);
            using (var profile = new LocalProfile(forgedPath))
                profile.Purchase(sale.Id, 1, profile.PurchasePrice(sale.Id), "forge-source");
            var forged = Read(forgedPath);
            var receipt = forged.Purchases.Single();
            receipt.ProductId = exclusive.Id;
            if (exclusive.Kind == "CardBack")
            {
                forged.OwnedCardBacks.Remove(sale.CardBack!.Value);
                forged.OwnedCardBacks.Add(exclusive.CardBackId);
                receipt.Granted.CardBack = exclusive.CardBackId;
            }
            else
            {
                forged.OwnedHeroSkins.Remove(sale.HeroSkin);
                forged.OwnedHeroSkins.Add(exclusive.HeroSkinId);
                receipt.Granted.HeroSkin = exclusive.HeroSkinId;
            }
            Write(forgedPath, forged);
            byte[] before = File.ReadAllBytes(forgedPath);
            reject(() => { using var ignored = new LocalProfile(forgedPath); }, "forged achievement purchase receipt: " + exclusive.Id);
            check(File.ReadAllBytes(forgedPath).SequenceEqual(before), "forged achievement receipt preserves bytes: " + exclusive.Id);
        }
        Console.WriteLine("PASS achievement-exclusive cosmetic registry");
    }
}

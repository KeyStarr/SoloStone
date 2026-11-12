using System.Text;
using System.Xml.Serialization;

internal static class LegacyCosmeticsTests
{
    internal static void Run(string root)
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
        string path = Path.Combine(root, "legacy-cosmetics.xml");
        var original = LocalShop.Default;
        using var catalogSource = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json");
        string catalog = new StreamReader(catalogSource!).ReadToEnd()
            .Replace("\"verify\": true", "\"verify\": false")
            .Replace("heroskin-HERO_08a", "hero-mage").Replace("heroskin-HERO_01a", "hero-warrior")
            .Replace("\"Id\": \"cardback-1\"", "\"Id\": \"card-back-1\"")
            .Replace("\"Id\": \"cardback-2\"", "\"Id\": \"card-back-2\"");
        // Legacy ids were sold at the flat pre-tier prices; their fallback products keep those prices.
        var legacyCatalog = System.Text.Json.Nodes.JsonNode.Parse(catalog)!;
        foreach (var p in legacyCatalog["Products"]!.AsArray().Where(p => (string)p!["Id"]! is "card-back-1" or "card-back-2" or "hero-mage" or "hero-warrior"))
            p!["Price"] = (string)p["Kind"]! == "HeroSkin" ? LocalShop.LegacyHeroSkinPrice : LocalShop.LegacyCardBackPrice;
        catalog = legacyCatalog.ToJsonString();
        try {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(catalog));
            LocalShop.TestOverride = LocalShop.Load(stream, () => original.Library);
            using (var profile = new LocalProfile(path)) profile.SelectCardBack(0);
            var serializer = new XmlSerializer(typeof(LocalProfileData));
            LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
            data.GoldBalance = 10000;
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            using (var profile = new LocalProfile(path)) {
                foreach (string id in new[] { "card-back-1", "card-back-2", "hero-mage", "hero-warrior" })
                    profile.Purchase(id, 1, profile.PurchasePrice(id), "legacy-" + id);
                profile.SelectCardBack(2);
                profile.SetFavoriteHeroSkin("HERO_08", "HERO_08a");
                profile.SetFavoriteHeroSkin("HERO_01", "HERO_01a");
            }
        } finally { LocalShop.TestOverride = null; }
        byte[] before = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path)) {
            Check(profile.Gold == 7000 && profile.FavoriteCardBacks.Single() == 2 && profile.OwnedCardBacks.SequenceEqual(new[] { 0, 1, 2 })
                && profile.FavoriteHeroSkins["HERO_08"] == "HERO_08a" && profile.FavoriteHeroSkins["HERO_01"] == "HERO_01a",
                "Historical cosmetics receipts and selections survive catalogue replacement");
            Check(profile.Purchase("card-back-1", 1, 500, "legacy-card-back-1").Sequence == 1,
                "Historical request replay retains original receipt");
            bool refused = false;
            try { profile.Purchase("card-back-1", 1, 500, "new-request"); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused && File.ReadAllBytes(path).SequenceEqual(before), "Historical unknown back cannot be sold again or rewrite profile");
        }
        Console.WriteLine("PASS historical cosmetics receipts: preserved ownership/favourites, byte-exact reopen/replay, no archived sales");
    }
}

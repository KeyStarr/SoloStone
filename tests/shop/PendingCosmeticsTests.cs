using System.Xml.Serialization;
using MiniJSON;

internal static class PendingCosmeticsTests
{
    internal static void Run(string root)
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
        string path = Path.Combine(root, "pending-cosmetics.xml");
        using (var profile = new LocalProfile(path)) profile.SelectCardBack(0);
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData data;
        using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
        data.GoldBalance = 100000;
        using (var file = File.Create(path)) serializer.Serialize(file, data);
        using (var profile = new LocalProfile(path)) {
            byte[] before = File.ReadAllBytes(path);
            var rows = ((JsonList)TestControlShop.Catalog(profile, true)["products"]).Cast<JsonNode>().ToArray();
            foreach (var product in LocalShop.Default.Products.Where(p => p.verify)) {
                var row = rows.Single(r => (string)r["id"] == product.Id);
                Check(!(bool)row["purchasable"] && (string)row["reason"] == "pending native check", "Pending item stays listed but blocked: " + product.Id);
                bool refused = false;
                try { profile.Purchase(product.Id, 1, product.Price, "pending-" + product.Id); }
                catch (InvalidOperationException e) when (e.Message == "pending native check") { refused = true; }
                Check(refused, "Direct purchase refuses pending item: " + product.Id);
            }
            Check(profile.Gold == 100000 && profile.PurchaseSequence == 0 && profile.OwnedHeroSkins.Count == 0
                && profile.OwnedCardBacks.SequenceEqual(new[] { 0 }) && File.ReadAllBytes(path).SequenceEqual(before), "Pending refusals preserve gold, ledger and file");
        }
        using (var fixture = new CosmeticsFixture())
        using (var profile = new LocalProfile(path)) profile.Purchase("heroskin-HERO_08a", 1, 1000, "historical");
        byte[] committed = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path)) {
            Check(profile.OwnedHeroSkins.Contains("HERO_08a") && profile.Gold == 99000, "Pending catalogue keeps historical ownership loadable");
            Check(profile.Purchase("heroskin-HERO_08a", 1, 1000, "historical").Sequence == 1
                && File.ReadAllBytes(path).SequenceEqual(committed), "Pending catalogue keeps historical replay byte-exact");
        }
        Console.WriteLine("PASS pending cosmetics: listed, purchase refused without mutation, historical load/replay preserved");
    }
}

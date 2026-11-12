using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text;

// Admit catalogue cosmetics only inside tests, including a curated back (122).
internal sealed class CosmeticsFixture : IDisposable
{
    private readonly LocalShop previous = LocalShop.TestOverride;
    internal CosmeticsFixture(bool coins = false, bool coinFive = false)
    {
        var original = LocalShop.Default;
        using (var source = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json"))
        using (var reader = new StreamReader(source)) {
            string text = reader.ReadToEnd().Replace("\"verify\": true", "\"verify\": false");
            if (coins) {
                var node = JsonNode.Parse(text);
                var products = node["Products"].AsArray();
                // Isolate fixture sale rows; shipped coin IDs may overlap these native harness IDs.
                foreach (var product in products.Where(p => (string)p["Kind"] == "Coin").ToArray()) products.Remove(product);
                foreach (int id in coinFive ? new[] { 2, 3, 5 } : new[] { 2, 3 })
                    products.Add(JsonNode.Parse(CoinProduct(id, id == 3 ? 800 : 300)));
                var sourceData = node["CosmeticsSource"];
                var ids = sourceData["ClientCoins"]?.AsArray().Select(n => (int)n) ?? Enumerable.Empty<int>();
                sourceData["ClientCoins"] = new JsonArray(ids.Concat(coinFive ? new[] { 2, 3, 5 } : new[] { 2, 3 })
                    .Distinct().OrderBy(id => id).Select(id => JsonValue.Create(id)).ToArray());
                var prices = sourceData["PriceHistory"]["Coin"]?.AsArray().Select(n => (int)n) ?? Enumerable.Empty<int>();
                sourceData["PriceHistory"]["Coin"] = new JsonArray(prices.Concat(new[] { 300, 800 })
                    .Distinct().OrderBy(price => price).Select(price => JsonValue.Create(price)).ToArray());
                text = node.ToJsonString();
            }
            using (var catalog = new MemoryStream(Encoding.UTF8.GetBytes(text)))
                LocalShop.TestOverride = LocalShop.Load(catalog, () => original.Library);
        }
    }
    private static string CoinProduct(int id, int price) => "{\"Id\":\"coin-" + id + "\",\"Kind\":\"Coin\",\"Coin\":" + id
        + ",\"Price\":" + price + ",\"MinQuantity\":1,\"MaxQuantity\":1,\"Description\":\"Fixture coin\",\"verify\":false}";
    public void Dispose() => LocalShop.TestOverride = previous;
}

using System.IO.Compression;
using System.Text.Json;

// Stand-in for the client's GameUtils card id translation (production resolves it through the game at run time).
// DBF ids come from the client's card export when one is set up (data/carddb/, built from a client and never committed),
// so with a client every unknown id stays unknown and the "every catalogue hero skin is a real card" check holds.
// Without it they come from the public card file (facts/). That file lacks the hero skins and pet forms the catalogue
// offers, so only those get a made-up, distinct DBF id; any other unknown id translates to 0 in both cases.
internal static class GameUtils
{
    private static bool fromExport;
    private static readonly Lazy<Dictionary<string, int>> ids = new(() =>
    {
        string root = RepoRoot.Find("data/pools/offline-standard-v1.json")!;
        using var policy = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "data/pools/offline-standard-v1.json")));
        string export = Path.Combine(root, "data/carddb", policy.RootElement.GetProperty("clientBuild").GetString()!, "cards.json.gz");
        if (!File.Exists(export)) return PublicFacts.Cards.Value.ToDictionary(card => card.Key, card => card.Value.Dbf);
        fromExport = true;
        using var file = File.OpenRead(export);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var json = JsonDocument.Parse(gzip);
        return json.RootElement.GetProperty("cards").EnumerateArray()
            .ToDictionary(card => card.GetProperty("cardId").GetString()!, card => card.GetProperty("dbfId").GetInt32());
    });
    private static readonly Lazy<HashSet<string>> offered = new(() =>
    {
        var shop = LocalShop.Default;
        var cards = new HashSet<string>(StringComparer.Ordinal);
        foreach (var product in shop.Products)
        {
            if (product.Kind == "HeroSkin") cards.Add(product.HeroSkin);
            if (product.Kind == "Pet" && product.Pet != null) cards.UnionWith(shop.FormsForPet(product.Pet));
        }
        return cards;
    });
    private static readonly Dictionary<string, int> madeUp = new();
    internal static readonly HashSet<string> MissingTranslations = new();
    internal static readonly HashSet<int> MissingHeroes = new();
    internal static object GetCardHeroRecordForCardId(int id) => id > 0 && !MissingHeroes.Contains(id) ? new object() : null;
    internal static string TranslateDbIdToCardId(int id) =>
        ids.Value.FirstOrDefault(pair => pair.Value == id).Key ?? madeUp.FirstOrDefault(pair => pair.Value == id).Key;
    internal static int TranslateCardIdToDbId(string cardId)
    {
        if (MissingTranslations.Contains(cardId)) return 0;
        if (ids.Value.TryGetValue(cardId, out int id) || madeUp.TryGetValue(cardId, out id)) return id;
        if (fromExport || !offered.Value.Contains(cardId)) return 0;
        return madeUp[cardId] = 9_000_000 - madeUp.Count - 1;
    }
}

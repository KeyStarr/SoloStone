using System.IO.Compression;
using System.Text.Json;

// Pinned card export replaces only Unity's DBF lookup; production uses GameUtils at runtime.
internal static class GameUtils
{
    private static readonly Lazy<Dictionary<string, int>> ids = new(() => {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "data/carddb"))) root = root.Parent;
        using var file = File.OpenRead(Path.Combine(root.FullName, "data/carddb/37.0.0.254477/cards.json.gz"));
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var json = JsonDocument.Parse(gzip);
        return json.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("cardId").GetString(), c => c.GetProperty("dbfId").GetInt32());
    });
    internal static readonly HashSet<string> MissingTranslations = new();
    internal static readonly HashSet<int> MissingHeroes = new();
    internal static object GetCardHeroRecordForCardId(int id) => id > 0 && !MissingHeroes.Contains(id) ? new object() : null;
    internal static string TranslateDbIdToCardId(int id) => ids.Value.FirstOrDefault(pair => pair.Value == id).Key;
    internal static int TranslateCardIdToDbId(string cardId) => !MissingTranslations.Contains(cardId) && ids.Value.TryGetValue(cardId, out var id) ? id : 0;
}

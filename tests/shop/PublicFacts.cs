using System.Text.Json;

// The public per-meta card file (facts/<meta>-*.json) stands in for the client's card export (data/carddb/.../cards.json.gz,
// built from a client and never committed): card id, DBF id, rarity tag and set tag, for the policy's active meta.
internal static class PublicFacts
{
    internal sealed record Row(string Id, int Dbf, int Rarity, int Set);
    internal const int TagSet = 183, TagRarity = 203, EpicRarity = 4, LegendaryRarity = 5;
    internal const int EventSet = 1941;  // the set number the game gives the event cards

    internal static readonly Lazy<Dictionary<string, Row>> Cards = new(() =>
    {
        string root = RepoRoot.Find("data/pools/offline-standard-v1.json")!;
        using var policy = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "data/pools/offline-standard-v1.json")));
        string meta = policy.RootElement.GetProperty("activeMeta").GetString()!;
        // The card file is <meta>-<date>.json; <meta>-<date>.<locale>.json beside it hold only translated text.
        string file = Directory.GetFiles(Path.Combine(root, "facts"), meta + "-*.json").Where(path => Path.GetFileName(path).Count(c => c == '.') == 1).OrderBy(path => path, StringComparer.Ordinal).Last();
        using var json = JsonDocument.Parse(File.ReadAllText(file));
        var rows = new Dictionary<string, Row>(StringComparer.Ordinal);
        foreach (var list in new[] { json.RootElement.GetProperty("cards"), json.RootElement.GetProperty("overlay").GetProperty("cards") })
            foreach (var card in list.EnumerateArray())
            {
                var tags = card.GetProperty("tags").EnumerateArray().ToDictionary(tag => tag[0].GetInt32(), tag => tag[1].GetInt32());
                string id = card.GetProperty("id").GetString()!;
                rows.TryAdd(id, new Row(id, card.GetProperty("dbf").GetInt32(), tags.GetValueOrDefault(TagRarity), tags.GetValueOrDefault(TagSet)));
            }
        return rows;
    });
}

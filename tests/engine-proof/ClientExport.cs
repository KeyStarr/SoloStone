using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

/// <summary>
/// The card data exported from a client (data/carddb, built locally with a maintainer tool, never committed) and the two
/// tiers of this suite (docs/TESTING.md). Most card proofs compare the engine's card with that export, then run the card's
/// scenario on the engine alone. Without the export (the CI tier) the comparison is skipped, loudly, and the scenario still
/// runs. Proofs that exist only to compare the engine with the export (pool oracles, hero powers, the generator check) run in
/// the client tier alone: <see cref="Oracle"/> is true there and nowhere else.
/// </summary>
internal static class ClientExport
{
    internal const string CardsPath = "data/carddb/37.0.0.254477/cards.json.gz";
    internal const string OverridesPath = "data/carddb/37.0.0.254477/set-overrides.json";
    internal const string CardDefsPath = "engine/SabberStoneCore/resources/Data/CardDefs.xml";

    private static readonly object Gate = new();
    private static JsonDocument document;   // kept for the life of the process: rows handed out stay valid
    private static Dictionary<string, JsonElement> rows;
    private static readonly Dictionary<string, int> skips = new();

    /// <summary>True only in the engine-proof-client suite, which runs the proofs that need the client's data.</summary>
    internal static bool ClientTier { get; set; }
    internal static bool Present => File.Exists(CardsPath);
    /// <summary>Compare the engine with the export, in the proofs whose whole point is that comparison (client tier only).</summary>
    internal static bool Oracle => ClientTier && Present;
    internal static int Skipped { get { lock (Gate) return skips.Values.Sum(); } }

    /// <summary>The export's cards by cardId, or null when this machine has no export.</summary>
    internal static IReadOnlyDictionary<string, JsonElement> TryCards()
    {
        lock (Gate)
        {
            if (rows != null) return rows;
            if (!File.Exists(CardsPath)) return null;
            using var file = File.OpenRead(CardsPath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            document = JsonDocument.Parse(gzip);
            return rows = document.RootElement.GetProperty("cards").EnumerateArray()
                .ToDictionary(c => c.GetProperty("cardId").GetString());
        }
    }

    /// <summary>The export's cards by cardId; throws when absent, for the proofs that only the client tier runs.</summary>
    internal static IReadOnlyDictionary<string, JsonElement> RequireCards() => TryCards()
        ?? throw new FileNotFoundException("this proof needs the card export built from a client", CardsPath);

    /// <summary>Counts a skipped export comparison; the first skip of each kind is printed, the end of the run prints the total.</summary>
    internal static void Skip(string what)
    {
        lock (Gate)
        {
            bool first = !skips.ContainsKey(what);
            skips[what] = skips.GetValueOrDefault(what) + 1;
            if (first) Console.WriteLine("SKIP " + what + ": compared only where the client export is available (docs/TESTING.md, Engine proofs)");
        }
    }

    internal static void PrintSkips()
    {
        lock (Gate)
            foreach (var skip in skips.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                Console.WriteLine("SKIPPED " + skip.Value + " x " + skip.Key);
    }

    private static readonly string[] Schools = { "NONE", "ARCANE", "FIRE", "FROST", "NATURE", "HOLY", "SHADOW", "FEL" };

    /// <summary>
    /// A Past-scope member row for a pool-manifest fixture (the rows of OfflinePoolCatalog.xml). With the export it is the client's
    /// row, as before. Without it the row comes from the engine's own definition of the card, which is enough for the
    /// fixtures that only need the card to be admitted to a pool; the comparison with the export stays in the client tier.
    /// </summary>
    internal static XElement PoolMember(string id, string proofKey, string spellSchool = null)
    {
        var rows = TryCards();
        if (rows != null)
        {
            var row = rows[id];
            return new XElement("Card", new XAttribute("cardId", id), new XAttribute("dbfId", row.GetProperty("dbfId").GetInt32()),
                new XAttribute("scope", "Past"), new XAttribute("set", row.GetProperty("cardSet").GetProperty("name").GetString()),
                new XAttribute("proofKey", proofKey), new XAttribute("registrationKind", "standard-module"),
                new XAttribute("type", row.GetProperty("cardType").GetString()), new XAttribute("cost", row.GetProperty("cost").GetInt32()),
                new XAttribute("attack", row.GetProperty("atk").GetInt32()), new XAttribute("health", row.GetProperty("health").GetInt32()),
                new XAttribute("rarity", row.GetProperty("rarity").GetString()),
                new XAttribute("spellSchool", spellSchool ?? row.GetProperty("spellSchool").GetString()),
                row.GetProperty("cardClasses").EnumerateArray().Select(c => new XElement("Class", new XAttribute("name", c.GetString()))),
                row.GetProperty("races").EnumerateArray().Select(c => new XElement("Race", new XAttribute("name", c.GetString()))),
                row.GetProperty("tags").EnumerateObject().Select(t => new XElement("Tag", new XAttribute("id", t.Name), new XAttribute("value", t.Value.GetInt32()))));
        }
        var card = Cards.FromId(id);
        return new XElement("Card", new XAttribute("cardId", id), new XAttribute("dbfId", card.AssetId),
            new XAttribute("scope", "Past"), new XAttribute("set", card.Set.ToString()),
            new XAttribute("proofKey", proofKey), new XAttribute("registrationKind", "standard-module"),
            new XAttribute("type", card.Type.ToString()), new XAttribute("cost", card.Cost),
            new XAttribute("attack", card.ATK), new XAttribute("health", card.Health),
            new XAttribute("rarity", card.Rarity.ToString()),
            new XAttribute("spellSchool", spellSchool ?? Schools[card[GameTag.SPELL_SCHOOL]]),
            new XElement("Class", new XAttribute("name", (int)card.Class == 14 ? "DEMONHUNTER" : card.Class.ToString())),
            card.Races.Select(r => new XElement("Race", new XAttribute("name", r.ToString()))),
            card.Tags.Select(t => new XElement("Tag", new XAttribute("id", (int)t.Key), new XAttribute("value", t.Value))));
    }
}

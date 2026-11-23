using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// A co-op boss mode (the Nefarian Rises! Tavern Brawl first) as authored data: data/coop/<id>-v1.json. Two people play the two
// seats at one client (hot-seat), each with a preset deck, against a boss minion. Retail served the decks from its servers;
// they are authored from the public source the file names. Pure model and strict parser, no client or engine types.
public sealed class CoopSeat
{
    // The name the handover screen calls this player by, the engine hero, and an optional hero skin the client shows instead.
    public string Name, Hero, Skin;
    public IReadOnlyList<string> Deck;
}

public sealed class CoopDefinition
{
    public const string Schema = "solostone.coop/1";
    public string Id, Name, Source, Boss;
    public int Version, Scenario, Board;
    // Whether each turn ends on a pass-the-device screen that hides the next player's hand until they press Continue. A team
    // shares what it sees and skips it; players with something to hide from each other (a versus mode) keep it. Default on.
    public bool HandoverScreen = true;
    public IReadOnlyList<CoopSeat> Seats;
    public string ResourceName => "SoloStone.Coop." + Id + "-v" + Version + ".json";

    // NpcWorldJson.Parser must be installed (plugin: EmbeddedJson.Parse; tests: MiniJSON).
    public static CoopDefinition Parse(string text)
    {
        var root = NpcWorldJson.Object(NpcWorldJson.Parse(text));
        if (NpcWorldJson.Text(root, "schema") != Schema) throw new InvalidDataException("Co-op definition schema must be " + Schema);
        var client = NpcWorldJson.Object(root.TryGetValue("client", out object clientValue) ? clientValue : null);
        var definition = new CoopDefinition {
            Id = Required(NpcWorldJson.Text(root, "id"), "id"),
            Version = NpcWorldJson.Integer(root, "version"),
            Name = Required(NpcWorldJson.Text(root, "name"), "name"),
            Source = Required(NpcWorldJson.Text(root, "source"), "source"),
            Boss = CardId(NpcWorldJson.Text(root, "boss"), "boss"),
            Scenario = NpcWorldJson.Integer(client, "scenario"),
            Board = NpcWorldJson.Integer(client, "board"),
            HandoverScreen = NpcWorldJson.Boolean(root, "handoverScreen", true),
        };
        if (definition.Version <= 0 || definition.Scenario <= 0 || definition.Board <= 0)
            throw new InvalidDataException("version, client.scenario and client.board must be positive");
        var seats = new List<CoopSeat>();
        foreach (object item in NpcWorldJson.List(root, "seats"))
        {
            var row = NpcWorldJson.Object(item);
            var deck = NpcWorldJson.Strings(row, "deck", true).Select(id => CardId(id, "seats.deck")).ToList();
            if (deck.Count != 30) throw new InvalidDataException("A seat's deck has 30 cards; got " + deck.Count);
            string skin = NpcWorldJson.Text(row, "skin", false);
            seats.Add(new CoopSeat {
                Name = Required(NpcWorldJson.Text(row, "name"), "seats.name"),
                Hero = CardId(NpcWorldJson.Text(row, "hero"), "seats.hero"),
                Skin = skin == null ? null : CardId(skin, "seats.skin"),
                Deck = deck,
            });
        }
        if (seats.Count != 2) throw new InvalidDataException("A co-op mode has exactly two seats; got " + seats.Count);
        definition.Seats = seats;
        return definition;
    }

    private static string Required(string value, string field) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException(field + " is required") : value;

    private static string CardId(string value, string field)
    {
        Required(value, field);
        if (value.Any(c => !(char.IsLetterOrDigit(c) || c == '_'))) throw new InvalidDataException(field + ": invalid card id " + value);
        return value;
    }
}

// Plugin-only: the co-op modes this build ships, embedded as SoloStone.Coop.<id>-v<n>.json and parsed once, on first use.
internal static class CoopCatalog
{
    private const string Prefix = "SoloStone.Coop.";
    private static readonly Lazy<IReadOnlyList<CoopDefinition>> all = new Lazy<IReadOnlyList<CoopDefinition>>(Load);

    internal static IReadOnlyList<CoopDefinition> All => all.Value;

    private static IReadOnlyList<CoopDefinition> Load()
    {
        if (NpcWorldJson.Parser == null) NpcWorldJson.Parser = EmbeddedJson.Parse;
        var assembly = Assembly.GetExecutingAssembly();
        var definitions = new List<CoopDefinition>();
        foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal))
            using (Stream stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
            {
                var definition = CoopDefinition.Parse(reader.ReadToEnd());
                if (definition.ResourceName != name) throw new InvalidDataException(name + " holds " + definition.ResourceName);
                definitions.Add(definition);
            }
        return definitions;
    }
}

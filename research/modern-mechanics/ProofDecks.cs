using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Model;

public sealed class ProofDeck
{
    private readonly string[] _cardIds;

    public ProofDeck(string id, string name, string hero, IEnumerable<string> cardIds)
    {
        Id = id;
        Name = name;
        Hero = hero;
        _cardIds = new List<string>(cardIds).ToArray();
    }

    public string Id { get; }
    public string Name { get; }
    public string Hero { get; }
    public string[] CardIds => (string[])_cardIds.Clone();
}

public static class ProofDecks
{
    private static readonly ProofDeck Mage = Build(
        "proof-mage", "Patient Elemental Mage", "HERO_08",
        "CORE_GVG_085", "CORE_LOOT_413", "CORE_EX1_007", "CORE_SW_066",
        "CORE_CS2_024", "CORE_CS2_023", "CORE_CS2_029", "CORE_CS2_028", "CORE_CS2_032",
        "TIME_045", "TLC_101", "TLC_226", "DINO_435", "CORE_BT_156", "EDR_469");

    private static readonly ProofDeck Warrior = Build(
        "proof-warrior", "Patient Board Warrior", "HERO_01",
        "CORE_GVG_085", "CORE_LOOT_413", "CORE_EX1_007", "CORE_SW_066", "CORE_CS2_179",
        "CORE_CS2_108", "CORE_EX1_391", "CORE_AT_064", "CORE_EX1_606", "CORE_EX1_604",
        "TIME_045", "TLC_101", "DINO_435", "CORE_BT_156", "EDR_469");

    private static ProofDeck Build(string id, string name, string hero, params string[] cards) =>
        new ProofDeck(id, name, hero, cards.SelectMany(card => new[] { card, card }));

    public static ProofDeck Get(string id) => id switch
    {
        "proof-mage" => Mage,
        "proof-warrior" => Warrior,
        _ => throw new ArgumentException($"Unknown proof deck: {id}", nameof(id))
    };

    public static SabberStoneCore.Model.Game CreateGame(string player, string opponent, int seed)
    {
        ProofDeck first = Get(player), second = Get(opponent);
        return new SabberStoneCore.Model.Game(new GameConfig
        {
            StartPlayer = 1,
            Player1Name = first.Name,
            Player2Name = second.Name,
            Player1HeroCard = Cards.FromId(first.Hero),
            Player2HeroCard = Cards.FromId(second.Hero),
            Player1Deck = first.CardIds.Select(Cards.FromId).ToList(),
            Player2Deck = second.CardIds.Select(Cards.FromId).ToList(),
            FillDecks = false,
            Shuffle = true,
            RandomSeed = seed,
            SkipMulligan = true,
            History = true,
            Logging = false
        });
    }
}

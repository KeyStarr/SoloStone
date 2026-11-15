using System.Linq;

// A run's next boss fight as the match to build: the player's run deck and hero at the run's Health with its passive
// treasures, the boss with its own hero and deck on the run mode's mission and board, under the mode's fight rules.
public static class RunEncounterSpec
{
    public static EncounterSpec ToSpec(this RunEncounter encounter, RunDefinition definition) => new EncounterSpec {
        PlayerHero = encounter.Hero,
        PlayerDeck = encounter.Deck.ToList(),
        PlayerStartingHealth = encounter.PlayerHealth,
        PlayerLoadout = encounter.Passives.Count == 0 ? null : encounter.Passives.ToList(),
        PlayerHand = encounter.Boss.PlayerHand.Count == 0 ? null : encounter.Boss.PlayerHand.ToList(),
        NpcHero = encounter.Boss.Hero,
        NpcDeck = encounter.Boss.Deck.ToList(),
        NpcBoard = encounter.Boss.Board.Count == 0 ? null : encounter.Boss.Board.ToList(),
        NpcStartingMana = encounter.BossStartingMana,
        NpcCoin = definition.BossCoin,
        FirstPlayer = definition.PlayerFirst ? 1 : (int?)null,
        MissionId = definition.Client.Scenario,
        BoardId = definition.Client.Board,
    };
}

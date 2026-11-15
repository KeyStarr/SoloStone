using System.Collections.Generic;

// What makes a match other than an ordinary ladder or casual one: a run mode's boss fight (Dungeon Run first), or a debug
// scenario's npcHero / npcDeck / missionId. Every field left at its default keeps the ordinary match. NativeMatch builds the
// game from it; the setup fields (Health, mana, Coin, loadout) reach the engine in ENGINE_SOURCE builds only.
public sealed class EncounterSpec
{
    // The player's side. Null hero or deck keeps the player's own deck; a run passes its run deck (any size, no deck rules).
    public string PlayerHero;
    public IReadOnlyList<string> PlayerDeck;
    public int PlayerStartingHealth;
    public IReadOnlyList<string> PlayerLoadout;

    // The NPC's side: a boss hero (its own Hero Power comes with it) and deck replace the ladder opponent's.
    public string NpcHero;
    public IReadOnlyList<string> NpcDeck;
    public string NpcName;
    public int NpcStartingHealth, NpcStartingMana;
    public bool NpcCoin = true;

    // 1: the player goes first, 2: the NPC; null keeps the match's own seat rule.
    public int? FirstPlayer;

    // The client's mission (scenario) id: the client then runs that mission's own game entity (Dungeon Run: 2663, the boss's
    // portrait, voice lines and music). Board: the board the mission is played on; 0 keeps the player's cosmetic.
    public int MissionId, BoardId;

    public bool ReplacesPlayerDeck => PlayerDeck != null && PlayerDeck.Count > 0;
    public bool ReplacesNpcDeck => NpcDeck != null && NpcDeck.Count > 0;
    public bool IsMission => MissionId != 0;
}

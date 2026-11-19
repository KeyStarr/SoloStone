using PegasusGame;
using SabberStoneCore.Enums;
using NativeHistory = PegasusGame.PowerHistory;
using EngineZone = SabberStoneCore.Enums.Zone;

namespace TransportInvariants;

// #80: Card.MillCard marks an opposing actor moved DECK -> GRAVEYARD as being drawn by the opponent and nothing clears the flag,
// so ZoneChangeList waits forever on any later zone or controller change, SHOW or Rewind reset diff of it. The model must flag
// each of those, and nothing else (a human burn, a dead minion and an actor never milled move freely).
internal static class MillLockProof
{
    private static PowerHistoryData Full(int id, int owner, int zone) => new() { FullEntity = new() { Entity = id, Name = "", Tags = new() {
        new() { Name = (int)GameTag.ENTITY_ID, Value = id }, new() { Name = (int)GameTag.CONTROLLER, Value = owner },
        new() { Name = (int)GameTag.ZONE, Value = zone } } } };
    private static PowerHistoryData Zone(int id, int zone) => new() { TagChange = new() { Entity = id, Tag = (int)GameTag.ZONE, Value = zone } };

    private static ClientModel Burned(int owner, int from, out int id)
    {
        id = 100;
        var model = new ClientModel();
        model.Apply(new NativeHistory { List = { Full(id, owner, from), Zone(id, (int)EngineZone.GRAVEYARD) } });
        return model;
    }

    internal static void Run()
    {
        var burned = Burned(2, (int)EngineZone.DECK, out int id);
        SelfChecks.Check(burned.MillFaults.Count == 0 && burned.Entities[id].MillLocked, "the burn itself is not a fault; it locks the actor");
        foreach (var (name, entry) in new[] {
            ("retirement", Zone(id, (int)EngineZone.REMOVEDFROMGAME)), ("return to deck", Zone(id, (int)EngineZone.DECK)),
            ("controller", new PowerHistoryData { TagChange = new() { Entity = id, Tag = (int)GameTag.CONTROLLER, Value = 1 } }),
            ("SHOW", new PowerHistoryData { ShowEntity = new() { Entity = id, Name = "CS2_033", Tags = new() } }) })
        {
            var model = burned.Copy();
            model.Apply(new NativeHistory { List = { entry } });
            SelfChecks.Check(model.MillFaults.Count == 1, "mill-locked opposing actor: " + name + " must be a fault");
            SelfChecks.Check(Invariants.Compare(null!, model).First().Tag == "MILL_LOCK", // the first violation needs no game
                "mill-locked opposing actor: " + name + " must reach the invariant oracle");
        }
        var reset = burned.Copy();
        reset.Apply(new NativeHistory { List = {
            new PowerHistoryData { PowerStart = new() { Type = (HistoryBlock.Type)(int)BlockType.GAME_RESET } },
            new PowerHistoryData { ResetGame = new() { SourceIsRewind = true } },
            Full(id, 2, (int)EngineZone.DECK),
            new PowerHistoryData { PowerEnd = new() } } });
        SelfChecks.Check(reset.MillFaults.Count == 1, "a Rewind reset FULL that restores a mill-locked actor to the deck is a fault");
        var kept = burned.Copy();
        kept.Apply(new NativeHistory { List = {
            new PowerHistoryData { PowerStart = new() { Type = (HistoryBlock.Type)(int)BlockType.GAME_RESET } },
            new PowerHistoryData { ResetGame = new() { SourceIsRewind = true } },
            Full(id, 2, (int)EngineZone.GRAVEYARD),
            new PowerHistoryData { PowerEnd = new() } } });
        SelfChecks.Check(kept.MillFaults.Count == 0, "a reset that leaves a mill-locked actor in GRAVEYARD is clean");
        // Not milled: the human's own burn, an opposing minion that died on the board, and a card that never was in a deck.
        var own = Burned(1, (int)EngineZone.DECK, out id);
        own.Apply(new NativeHistory { List = { Zone(id, (int)EngineZone.DECK) } });
        var dead = Burned(2, (int)EngineZone.PLAY, out id);
        dead.Apply(new NativeHistory { List = { Zone(id, (int)EngineZone.PLAY) } });
        var hand = Burned(2, (int)EngineZone.HAND, out id);
        hand.Apply(new NativeHistory { List = { Zone(id, (int)EngineZone.HAND) } });
        SelfChecks.Check(own.MillFaults.Count + dead.MillFaults.Count + hand.MillFaults.Count == 0,
            "only an opposing DECK -> GRAVEYARD actor is mill-locked");
        Console.WriteLine("PASS mill lock model: a burned opposing card is never moved, shown or reset again (#80)");
    }
}

using SabberStoneCore.Enums;
using SabberStoneCore.Config;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class LocationProofs
{
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("locations: " + message);
    }
    internal static Game Game(int seed = 1, bool history = true)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, SkipMulligan = true,
            History = history, Logging = false, FormatType = FormatType.FT_WILD,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame();
        game.Player1.BaseMana = 10;
        return game;
    }
    internal static Minion Minion(Controller c, string id = "CS2_182", int position = -1) =>
        (Minion)Entity.FromCard(c, Cards.FromId(id), zone: c.BoardZone, zonePos: position);
    internal static BoardObject Location(Controller c, string id = "CORE_REV_990", int position = -1) =>
        (BoardObject)Entity.FromCard(c, Cards.FromId(id), zone: c.BoardZone, zonePos: position);
    internal static BoardObject Play(Controller c, string id, int position = -1)
    {
        var source = (BoardObject)Entity.FromCard(c, Cards.FromId(id), zone: c.HandZone);
        Check(c.Game.Process(PlayCardTask.Any(c, source, zonePosition: position)), "play location");
        return source;
    }
    internal static void End(Game game) => Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "end turn");
    internal static void Use(BoardObject source, IPlayable target = null) =>
        Check(source.Game.Process(new ActivateLocationTask(source.Controller, source, target)), "activate location");
    internal static void Run()
    {
        Check(Enum.GetName(typeof(CardType), 39) == "LOCATION", "LOCATION type 39");
        Console.WriteLine("PASS location types");
        LocationReviewProof.Run();
        LocationBoardProof.Run();
        LocationCardSetProof.Run();
        LocationTransformProof.Run();
        LocationActivationProof.Run();
        LocationDeathProof.Run();
        LocationSnapshotProof.Run();
        SanguineDepthsProof.Run();
        UnderbellyNetworkProof.Run();
        CosmicSpringsProof.Run();
        PastGnomereganProof.Run();
        DemolitionRenovatorProof.Run();
        TwistingNetherProof.Run();
    }
}

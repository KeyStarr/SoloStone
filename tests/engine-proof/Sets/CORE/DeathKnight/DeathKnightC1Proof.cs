using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class DeathKnightC1Proof
{
    internal static Game NewGame(long seed, CardClass hero = CardClass.DEATHKNIGHT)
    {
        var game = new Game(new GameConfig { EnableCorpses = true, RandomSeed = seed, StartPlayer = 1,
            Shuffle = false, SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
            Player1HeroClass = hero, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
        { PoolPilotProof.EmptyHand(player); player.BaseMana = 10; }
        return game;
    }
    internal static void Case(string id, Action<Game, Controller, string> scenario, [System.Runtime.CompilerServices.CallerFilePath] string proofFile = "")
        => StandardSetsProof.Case(id, (original, _, cardId) => {
            var game = NewGame(original.Seed);
            scenario(game, game.Player1, cardId);
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), "c1 final clone");
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshot(game.SaveSnapshot())), "c1 final snapshot");
        }, proofFile);
    internal static bool HasPlay(Controller player, IPlayable source)
        => player.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id);
    internal static void FullBoard(Controller player)
    {
        LocationProofs.Location(player);
        while (!player.BoardZone.IsFull) Summon(player, "CS2_182");
        Core2026Proof.Check(player.BoardZone.SlotCount == 7 && player.BoardZone.Count == 6, "location occupies seventh slot");
    }
    internal static void Answer(Game game)
    {
        var player = game.Player1;
        Core2026Proof.Check(player.Choice != null && player.Choice.Choices.Count > 0, "Discover offers exist");
        int chosen = player.Choice.Choices.First();
        PoolPilotProof.Resume(game, next => Core2026Proof.Check(next.Process(ChooseTask.Pick(next.Player1, chosen)), "resumed Discover accepted"));
    }
}

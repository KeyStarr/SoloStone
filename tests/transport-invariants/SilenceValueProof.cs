using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;

internal static class SilenceValueProof
{
    internal static void Run()
    {
        foreach (int actor in new[] { 1, 2 })
        {
            var game = new Game(new GameConfig { RandomSeed = 71, StartPlayer = actor, SkipMulligan = true,
                EnableCorpses = true, History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
            game.StartGame();
            var player = game.CurrentPlayer;
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new(match);
            session.Flush();
            var source = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("CORE_RLK_745"),
                new Dictionary<GameTag, int> { [GameTag.SILENCED] = 0, [GameTag.CONTROLLER] = player.PlayerId });
            Generic.SummonBlock(game, source, -1, null);
            CorpseRules.Gain(player, 4);
            session.Flush();
            session.Process(EndTurnTask.Any(player));
            var copy = player.BoardZone.Single(m => m.Id != source.Id);
            foreach (var minion in new[] { source, copy })
                SelfChecks.Check(!minion.IsSilenced && minion.NativeTags[GameTag.SILENCED] == 0
                    && session.Client.Entities[minion.Id].Get(GameTag.SILENCED) == 0,
                    "zero-Silence Horror and its end-turn copy agree with client");
            session.PublishDecision(game.CurrentPlayer);
            var violations = Invariants.Compare(game, session.Client).ToArray();
            SelfChecks.Check(violations.Length == 0, "zero Silence strict copy parity: " + string.Join("; ", violations.Select(v => v.ToString())));
            source.Silence();
            game.DeathProcessingAndAuraUpdate();
            session.Flush();
            SelfChecks.Check(source.IsSilenced && session.Client.Entities[source.Id].Get(GameTag.SILENCED) == 1
                && !copy.IsSilenced && copy[GameTag.REBORN] == 1,
                "real Silence publishes one without silencing independent copy");
            SelfChecks.Check(!Invariants.Compare(game, session.Client).Any(), "real Silence strict parity");
        }
        Console.WriteLine("PASS Silence values transport: zero-Silence Malignant Horror end-turn copies, real Silence; human/opponent; violations=0");
    }
}

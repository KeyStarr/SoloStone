using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;

// Regression: secrets created straight into the secret zone (Desperate Measures DAL_141 and its twinspell DAL_141ts)
// never went through a positioning zone, so the client kept ZONE_POSITION=0 while the engine said 1
// (strict-transport violations on EX1_136 and DAL_570 in the stress-deck corpus). Checks engine value, client value,
// cast order, clone, snapshot.
internal static class SecretPositionProof
{
    internal static void Run()
    {
        foreach (int actor in new[] { 1, 2 })
        {
            var game = new Game(new GameConfig { RandomSeed = 7, StartPlayer = actor, SkipMulligan = true,
                History = true, Logging = false,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
            game.StartGame();
            var player = game.CurrentPlayer;
            player.BaseMana = 10;
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new(match);
            session.Flush();
            var measures = Generic.DrawCard(player, Cards.FromId("DAL_141"));
            session.Flush();
            session.Process(PlayCardTask.Any(player, measures));
            var first = player.SecretZone.Single();
            SelfChecks.Check(first.ZonePosition == 0 && first[GameTag.ZONE_POSITION] == 1
                && session.Client.Entities[first.Id].Get(GameTag.ZONE_POSITION) == 1,
                "Desperate Measures first generated secret has one-based engine/native position 1");
            var twin = player.HandZone.Single(card => card.Card.Id == "DAL_141ts");
            session.Process(PlayCardTask.Any(player, twin));
            var second = player.SecretZone.Single(card => card.Id != first.Id);
            // SecretZone is not positional; casting must publish the existing cache, not reorder secrets.
            SelfChecks.Check(second.ZonePosition == 0 && second[GameTag.ZONE_POSITION] == 1
                && session.Client.Entities[second.Id].Get(GameTag.ZONE_POSITION) == 1,
                "Desperate Measures twinspell generated secret publishes its cached position");
            SelfChecks.Check(first.OrderOfPlay < second.OrderOfPlay
                && player.SecretZone.Select(card => card.Id).SequenceEqual(new[] { first.Id, second.Id }),
                "generated secrets retain their distinct identities and cast order");
            var clone = game.CloneExact();
            SelfChecks.Check(clone.IdEntityDic[second.Id].ZonePosition == 0
                && clone.IdEntityDic[second.Id][GameTag.ZONE_POSITION] == 1,
                "secret positions survive exact clone");
            var restored = Game.LoadSnapshot(game.SaveSnapshot());
            SelfChecks.Check(restored.IdEntityDic[first.Id].ZonePosition == 0
                && restored.IdEntityDic[first.Id][GameTag.ZONE_POSITION] == 1,
                "secret positions survive snapshot restore");
            SelfChecks.Check(!Invariants.Compare(game, session.Client).Any(),
                "generated secrets retain strict parity before next decision");
        }
        Console.WriteLine("PASS secret positions transport: Desperate Measures/twinspell generated history, cast order, clone/snapshot; human/opponent; violations=0");
    }
}

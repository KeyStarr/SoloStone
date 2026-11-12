using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Entity = SabberStoneCore.Model.Entities.Entity;

internal static class SummonCopyReadinessProof
{
    internal static void Run()
    {
        foreach (int actor in new[] { 1, 2 })
        {
            var game = new Game(new GameConfig { RandomSeed = 9, StartPlayer = actor, SkipMulligan = true,
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
            var source = (Minion)Generic.DrawCard(player, Cards.FromId("CATA_469"));
            var reborn = Generic.DrawCard(player, Cards.FromId("CAP_801"));
            var specialist = Generic.DrawCard(player, Cards.FromId("CAP_804"));
            var enemy = (Minion)Entity.FromCard(player.Opponent, Cards.FromId("CS2_231"));
            Generic.SummonBlock(game, enemy, -1, null);
            session.Flush();
            session.Process(PlayCardTask.Any(player, source));
            session.Process(PlayCardTask.Any(player, reborn, source));
            session.Process(MinionAttackTask.Any(player, source, enemy));
            SelfChecks.Check(source.IsExhausted && source[GameTag.EXHAUSTED] == 1,
                "summon-copy fixture: Broodmother attacked before copying");
            session.Process(PlayCardTask.Any(player, specialist, source));
            var copy = player.BoardZone.Single(m => m.Card.Id == "CATA_469" && m.Id != source.Id);
            SelfChecks.Check(!copy.IsExhausted && copy.AttackableByRush && source.IsExhausted,
                "summon-copy grants fresh Rush readiness without readying the source");
            SelfChecks.Check(session.Client.Entities[copy.Id].Get(GameTag.EXHAUSTED) == 0,
                "summon-copy Rush readiness reaches client after copying exhausted source");
            SelfChecks.Check(copy[GameTag.EXHAUSTED] == 0 && source[GameTag.EXHAUSTED] == 1,
                "summon-copy raw exhaustion agrees with engine readiness; source unchanged");
            session.PublishDecision(player);
            SelfChecks.Check(!Invariants.Compare(game, session.Client).Any(),
                "summon-copy strict parity before next decision");
        }
        Console.WriteLine("PASS summon-copy readiness transport: exhausted Broodmother copied by Specter Specialist; fresh Rush; human/opponent; violations=0");
    }
}

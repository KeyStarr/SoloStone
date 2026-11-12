using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;

internal static class LegacyPoolEligibilityProof
{
    internal static readonly HashSet<string> Played = new(StringComparer.Ordinal);
    internal static void Run()
    {
        Played.Clear();
        foreach (string id in OfflinePoolPolicy.Load().Members.Where(m => m.Scope == "Past").Select(m => m.CardId))
        {
            var game = new Game(new GameConfig { RandomSeed = 27, StartPlayer = 1, SkipMulligan = true,
                History = true, Logging = false, Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            NativeSession session = null;
            var match = new NativeMatch((packetId, packet) => session.Receive(packetId, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match);
            foreach(var p in new[]{game.Player1,game.Player2})
            {
                foreach(var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
                Generic.SummonBlock(game,(Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("CS2_182")),-1,null);
            }
            game.Player1.BaseMana = 10;
            var definition=OfflineCardPools.Candidates(game,game.Player1.HeroClass,OfflinePoolFilter.Random(scope:PoolScope.Past),0)
                .Single(card=>card.Id==id);
            var source = Generic.DrawCard(game.Player1, definition);
            session.Flush();
            SelfChecks.Check(!Invariants.Compare(game, session.Client).Any(), id + " legacy pool hand transport");
            var play=game.Player1.Options().FirstOrDefault(t=>t.PlayerTaskType==PlayerTaskType.PLAY_CARD && t.Source?.Id==source.Id);
            SelfChecks.Check(play!=null,id+" qualified Past play is legal");
            session.Process(play);
            while (game.Player1.Choice != null)
            {
                session.PublishDecision(game.Player1);
                int pick = game.Player1.Choice.ChoiceAction == ChoiceAction.REWIND
                    ? game.Rewind.KeepEntityId : game.Player1.Choice.Choices[0];
                session.Process(ChooseTask.Pick(game.Player1,pick));
            }
            SelfChecks.Check(!Invariants.Compare(game, session.Client).Any(), id + " legacy pool play transport");
            if(source is Minion minion)
            {
                minion.IsExhausted = false;
                session.Flush();
                var attack = game.Player1.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
                    && t.Source?.Id == source.Id).OrderByDescending(t => t.Target == game.Player2.Hero).FirstOrDefault();
                if (minion.AttackDamage == 0)
                    SelfChecks.Check(attack == null, id + " zero-Attack minion cannot attack");
                else
                {
                    SelfChecks.Check(attack != null, id + " qualified Past attack is legal");
                    session.Process(attack);
                }
                SelfChecks.Check(!Invariants.Compare(game, session.Client).Any(), id + " legacy pool attack transport");
            }
            Played.Add(id);
        }
        Console.WriteLine($"PASS qualified Past pool transport: {Played.Count} members; hand/play; legal attack or zero-Attack check; violations=0");
    }
}

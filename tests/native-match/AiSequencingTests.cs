using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // Regression for NPC turn planning (2026-10-27): two orders of the same plays (spell then area effect, or the
    // reverse) reached identical visible boards but not identical states, because one left a delayed "returns to hand
    // at end of turn" effect and the other did not. Search keys that ignored it could merge the two and drop the better
    // line. Search keys and the planner's frontier now keep distinct own delayed effects apart. The full-hand cases
    // check that a return that cannot happen (hand full) earns no value.
    private static void AiSequencingTests()
    {
        foreach (bool bot in new[] { false, true }) foreach (bool reverseIds in new[] { false, true })
        {
            var (match, _) = AnimationMatch(botTurn: bot); var game = match.Game; var player = game.CurrentPlayer;
            foreach (var old in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(old));
            var target = (Minion)EngineEntity.FromCard(player.Opponent, Cards.FromId("CORE_EX1_014"), zone: player.Opponent.BoardZone); target.BaseHealth = 6;
            IPlayable bolt, area;
            if (reverseIds) { bolt = EngineEntity.FromCard(player, Cards.FromId("END_025"), zone: player.HandZone); area = EngineEntity.FromCard(player, Cards.FromId("CATA_308"), zone: player.HandZone); }
            else { area = EngineEntity.FromCard(player, Cards.FromId("CATA_308"), zone: player.HandZone); bolt = EngineEntity.FromCard(player, Cards.FromId("END_025"), zone: player.HandZone); }
            bolt.Cost = 3; area.Cost = 1;
            var root = FairBotState.Create(game, player.PlayerId, 17, DeckStyle.Control, new FairOptions { RemoveOpponentSecrets = true });
            var early = root.Apply(root.Actions.First(a => a.Source == bolt.Id && a.Target == target.Id)); early = early.Apply(early.Actions.First(a => a.Source == area.Id));
            var late = root.Apply(root.Actions.First(a => a.Source == area.Id)); late = late.Apply(late.Actions.First(a => a.Source == bolt.Id && a.Target == target.Id));
            Check(GoldenPublicKey.For(early, false) == GoldenPublicKey.For(late, false), "visible board and actions match");
            Check(GoldenPublicKey.For(early) != GoldenPublicKey.For(late), "pending return effect distinguishes states");
            var earlyEnd = early.Apply(early.Actions.First(a => a.Type == PlayerTaskType.END_TURN));
            var lateEnd = late.Apply(late.Actions.First(a => a.Type == PlayerTaskType.END_TURN));
            Check(lateEnd.Facts.Own.Hand.Count == earlyEnd.Facts.Own.Hand.Count + 1, "end turn returns lethal Firebolt");
            var lateGame = late.FingerprintGame; var latePlayer = lateGame.Player1.PlayerId == player.PlayerId ? lateGame.Player1 : lateGame.Player2;
            while (!latePlayer.HandZone.IsFull) EngineEntity.FromCard(latePlayer, Cards.FromId("CS2_182"), zone: latePlayer.HandZone);
            var earlyGame = early.FingerprintGame; var earlyPlayer = earlyGame.Player1.PlayerId == player.PlayerId ? earlyGame.Player1 : earlyGame.Player2;
            while (!earlyPlayer.HandZone.IsFull) EngineEntity.FromCard(earlyPlayer, Cards.FromId("CS2_182"), zone: earlyPlayer.HandZone);
            int full = latePlayer.HandZone.Count; var fullEnd = late.Apply(late.Actions.First(a => a.Type == PlayerTaskType.END_TURN));
            var earlyFullEnd = early.Apply(early.Actions.First(a => a.Type == PlayerTaskType.END_TURN));
            Check(fullEnd.Facts.Own.Hand.Count == full && !fullEnd.Facts.Own.Hand.Any(c => c.EntityId == bolt.Id), "full hand prevents delayed return");
            Check(Math.Abs(fullEnd.Evaluate() - earlyFullEnd.Evaluate()) < .000001, "full hand grants no phantom delayed value");
            if (!bot && !reverseIds) {
                var profile = NpcCatalog.All[0]; var plan = DeckPlans.ForId(profile.PlanId);
                var evaluator = new DeckPlanEvaluator(plan, profile.Budget.Brain);
                var originalReply = ReplyModel.Job(early, profile, 42, evaluator, replyNodes: 32);
                var returningReply = ReplyModel.Job(late, profile, 42, evaluator, replyNodes: 32);
                while (!originalReply.IsDone) originalReply.StepNode();
                while (!returningReply.IsDone) returningReply.StepNode();
                Check(Math.Abs(originalReply.Value - returningReply.Value) < .000001,
                    "full-hand reply scoring assigns no bonus for an unreturned card");
            }
        }
        // A different delayed effect can be harmful; compare actual end-turn consequences, not effect presence.
        var (appleMatch, _) = AnimationMatch(); var g = appleMatch.Game; var owner = g.CurrentPlayer;
        var source = EngineEntity.FromCard(owner, Cards.FromId("EDR_482"), zone: owner.GraveyardZone);
        var without = FairBotState.Create(g, owner.PlayerId, 21, DeckStyle.Control, new FairOptions { RemoveOpponentSecrets = true });
        SabberStoneCore.Actions.Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_482e"), source, owner, 2);
        var with = FairBotState.Create(g, owner.PlayerId, 21, DeckStyle.Control, new FairOptions { RemoveOpponentSecrets = true });
        Check(GoldenPublicKey.For(without, false) == GoldenPublicKey.For(with, false) && GoldenPublicKey.For(without) != GoldenPublicKey.For(with), "independent delayed damage distinguishes otherwise equal state");
        double noDamage = without.Apply(without.Actions.First(a => a.Type == PlayerTaskType.END_TURN)).Evaluate();
        double damage = with.Apply(with.Actions.First(a => a.Type == PlayerTaskType.END_TURN)).Evaluate();
        Check(damage < noDamage, "end-turn comparison detects harmful delayed effect");
        var beam = new TurnBeam(without, state => state.Evaluate(), 30, 6, false, dedupe: true);
        var first = new TurnLine { State = without, Value = without.Evaluate(), Actions = new List<BotAction> { without.Actions.First(a => a.Type == PlayerTaskType.END_TURN) } };
        var second = new TurnLine { State = with, Value = with.Evaluate(), Actions = new List<BotAction> { with.Actions.First(a => a.Type == PlayerTaskType.END_TURN) } };
        typeof(TurnBeam).GetField("next", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(beam, new List<TurnLine> { first, second });
        typeof(TurnBeam).GetField("parent", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(beam, 1);
        beam.StepNode();
        var frontier = (List<TurnLine>)typeof(TurnBeam).GetField("frontier", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(beam);
        Check(frontier.Contains(first) && frontier.Contains(second), "beam retains both delayed and no-delay states");
        // A tied lower-valued pair must not displace the static leader or the hold line at a small reply limit.
        var shortlistGame = g.CloneExact(); var chooser = shortlistGame.CurrentPlayer;
        var one = EngineEntity.FromCard(chooser, Cards.FromId("CS2_182"), zone: chooser.HandZone);
        var two = EngineEntity.FromCard(chooser, Cards.FromId("CS2_200"), zone: chooser.HandZone);
        var rootView = FairBotState.Create(shortlistGame, chooser.PlayerId, 21, DeckStyle.Control, new FairOptions { RemoveOpponentSecrets = true });
        var gameA = shortlistGame.CloneExact(); var ownerA = gameA.CurrentPlayer;
        ownerA.SetasideZone.Add(ownerA.HandZone.Remove(gameA.IdEntityDic[one.Id]));
        var gameB = shortlistGame.CloneExact(); var ownerB = gameB.CurrentPlayer;
        ownerB.SetasideZone.Add(ownerB.HandZone.Remove(gameB.IdEntityDic[two.Id]));
        var gameC = gameB.CloneExact(); var ownerC = gameC.CurrentPlayer;
        var timerSource = EngineEntity.FromCard(ownerC, Cards.FromId("EDR_482"), zone: ownerC.GraveyardZone);
        SabberStoneCore.Actions.Generic.AddEnchantmentBlock(gameC, Cards.FromId("EDR_482e"), timerSource, ownerC, 2);
        FairBotState View(SabberStoneCore.Model.Game game) => FairBotState.Create(game, game.CurrentPlayer.PlayerId, 21,
            DeckStyle.Control, new FairOptions { RemoveOpponentSecrets = true });
        var leader = new TurnLine { State = View(gameA), Value = 100 };
        var alternate = new TurnLine { State = View(gameB), Value = 50 };
        var harmful = new TurnLine { State = View(gameC), Value = 50 };
        var holdLine = new TurnLine { State = rootView, Value = 0 };
        var testProfile = NpcCatalog.All[0];
        var shortJob = TurnPlanner.Job(rootView, testProfile, DeckPlans.ForId(testProfile.PlanId), 21, 100);
        var shortBeam = (TurnBeam)typeof(TurnPlannerJob).GetField("beam", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shortJob);
        shortBeam.Leaves.AddRange(new[] { leader, alternate, harmful, holdLine });
        typeof(TurnBeam).GetField("<IsDone>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(shortBeam, true);
        typeof(TurnPlannerJob).GetField("replyLimit", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(shortJob, 3);
        shortJob.StepNode();
        var shortlisted = (List<TurnLine>)typeof(TurnPlannerJob).GetField("leaves", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shortJob);
        Check(shortlisted.Count == 3 && ReferenceEquals(shortlisted[0], leader) && shortlisted.Contains(holdLine),
            "small reply shortlist preserves static leader and hold despite tied effect pair");
        var (privacyMatch, _) = AnimationMatch(); var pg = privacyMatch.Game; var viewer = pg.CurrentPlayer;
        foreach (var old in viewer.Opponent.HandZone.ToArray()) viewer.Opponent.SetasideZone.Add(viewer.Opponent.HandZone.Remove(old));
        var copy = pg.CloneExact();
        var enemy1 = pg.CurrentPlayer.Opponent; var enemy2 = copy.CurrentPlayer.Opponent;
        var hidden1 = EngineEntity.FromCard(enemy1, Cards.FromId("END_025"), zone: enemy1.HandZone);
        var hidden2 = EngineEntity.FromCard(enemy2, Cards.FromId("CS2_029"), zone: enemy2.HandZone);
        SabberStoneCore.Actions.Generic.AddEnchantmentBlock(pg, Cards.FromId("EDR_482e"), hidden1, pg.CurrentPlayer, 2);
        SabberStoneCore.Actions.Generic.AddEnchantmentBlock(copy, Cards.FromId("EDR_482e"), hidden2, copy.CurrentPlayer, 2);
        var public1 = FairBotState.Create(pg, viewer.PlayerId, 33, DeckStyle.Control, new FairOptions { RemoveOpponentSecrets = true });
        var public2 = FairBotState.Create(copy, viewer.PlayerId, 33, DeckStyle.Control, new FairOptions { RemoveOpponentSecrets = true });
        Check(public1.OwnVisibleEffectsKey() == public2.OwnVisibleEffectsKey() && GoldenPublicKey.For(public1) == GoldenPublicKey.For(public2),
            "effect signatures cannot disclose opposing hidden hand identity");
        Console.WriteLine("PASS delayed effect keys, both seats and entity order, full hand and harmful timer");
    }
}

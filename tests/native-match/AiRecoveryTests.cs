using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
internal static partial class Program
{
    private static void AiRecoveryTests()
    {
        foreach (bool bot in new[] { false, true }) foreach (bool friendlyFirst in new[] { false, true })
        {
            var (match, _) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            Minion friendly = null, enemy = null;
            void Friend() { friendly = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.BoardZone); friendly.BaseHealth = 20; }
            void Enemy() { enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone); enemy.BaseHealth = 20; }
            if (friendlyFirst) { Friend(); Enemy(); } else { Enemy(); Friend(); }
            var haunt = EngineEntity.FromCard(owner, Cards.FromId("CAP_801"), zone: owner.HandZone); haunt.Cost = 0;
            var view = FairBotState.Create(match.Game, owner.PlayerId, 7, DeckStyle.Control, new FairOptions { RemoveOpponentSecrets = true });
            var tasks = owner.Options().Where(t => t.Source?.Id == haunt.Id || t.PlayerTaskType == PlayerTaskType.END_TURN).Select(BotAction.From).ToArray();
            int budget = 16; var chosen = BotDriver.BoundedFallback(view, tasks, () => budget-- > 0);
            Check(chosen.Source == haunt.Id && chosen.Target == friendly.Id, "fallback favors friendly buff regardless of entity order");
            var noFriend = tasks.Where(t => t.Target != friendly.Id).ToArray(); budget = 16;
            chosen = BotDriver.BoundedFallback(view, noFriend, () => budget-- > 0);
            Check(chosen.Type == PlayerTaskType.END_TURN, "fallback declines harmful buff without friendly target");
            var legalTasks = owner.Options().Where(t => t.Source?.Id == haunt.Id || t.PlayerTaskType == PlayerTaskType.END_TURN).ToList();
            int decisionIndex = 0;
            var driver = BotDriver.Decide(match.Game, owner.PlayerId, DeckStyle.Control,
                _ => throw new InvalidOperationException("driver-failure"), ref decisionIndex,
                new DriverOptions { FairViewFactory = (g, id, seed, style) => view }, legalTasks);
            Check(driver.Task.Source?.Id == haunt.Id && driver.Task.Target?.Id == friendly.Id
                && driver.Trace.FallbackReason == "InvalidOperationException:driver-failure" && driver.Trace.PilotDecision.Nodes == 2,
                "synchronous driver uses safe fallback and reports both candidate attempts");
        }
        var (historyMatch, _) = AnimationMatch(); var game = historyMatch.Game; var player = game.CurrentPlayer;
        var minion = (Minion)EngineEntity.FromCard(player, Cards.FromId("TLC_249"), zone: player.BoardZone);
        minion.ToBeDestroyed = true; game.DeathProcessingAndAuraUpdate();
        var natural = game.HistoryFacts.Query.DeathrattlesResolved().Where(f => f.EntityId == minion.Id).ToArray();
        Check(natural.Length == 1 && natural[0].Incarnation > 0, "natural deathrattle retains linked death");
        player.GraveyardZone.Remove(minion); minion.Reset(); SabberStoneCore.Actions.Generic.SummonBlock(game, minion, -1, null);
        game.TaskQueue.StartEvent(); minion.ActivateTask(PowerActivation.DEATHRATTLE, fromDeath: false);
        typeof(SabberStoneCore.Model.Game).GetMethod("ProcessTasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(game, null);
        game.DeathProcessingAndAuraUpdate(); game.TaskQueue.EndEvent();
        var events = game.HistoryFacts.Query.DeathrattlesResolved().Where(f => f.EntityId == minion.Id).ToArray();
        Check(events.Length == 2 && events[1].Incarnation == 0, "manual living activation does not reuse prior death");
        var restored = SabberStoneCore.Model.Game.LoadSnapshot(game.SaveSnapshot());
        Check(restored.HistoryFacts.Query.DeathrattlesResolved().Count(f => f.EntityId == minion.Id) == 2, "manual activation survives validated snapshot");
        var (umbraMatch, _) = AnimationMatch(); var ug = umbraMatch.Game; var up = ug.CurrentPlayer;
        var dead = (Minion)EngineEntity.FromCard(up, Cards.FromId("TLC_249"), zone: up.BoardZone);
        dead.ToBeDestroyed = true; ug.DeathProcessingAndAuraUpdate();
        var umbra = EngineEntity.FromCard(up, Cards.FromId("TLC_106"), zone: up.HandZone); umbra.Cost = 0;
        var viewUmbra = FairBotState.Create(ug, up.PlayerId, 7, DeckStyle.Control, new FairOptions { RemoveOpponentSecrets = true });
        var umbraAction = BotAction.From(up.Options().First(t => t.Source?.Id == umbra.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD));
        var resolved = viewUmbra.Apply(umbraAction).FingerprintGame;
        Check(viewUmbra.Facts.Opponent.Hand.All(c => c.Unknown), "fair Umbra view hides opposing hand identity");
        var activations = resolved.HistoryFacts.Query.DeathrattlesResolved().Where(f => f.Incarnation == 0).ToArray();
        Check(activations.Length > 0 && !resolved.HistoryFacts.Query.Deaths().Any(f => f.EntityId == activations[0].EntityId),
            "Umbra resolves reconstructed Deathrattle without fabricating a death");
        Check(ug.Process(up.Options().First(t => t.Source?.Id == umbra.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD)), "actual Umbra play succeeds");
        SabberStoneCore.Model.Game.LoadSnapshot(ug.SaveSnapshot());
        var projected = ug.CloneExact(); projected.ProjectHistoryForFairView(up.Opponent.PlayerId);
        Check(projected.HistoryFacts.Query.DeathrattlesResolved().Count(f => f.Incarnation == 0) > 0,
            "fair history retains public Deathrattle activation");
        var (swimmerMatch, _) = AnimationMatch(); var sg = swimmerMatch.Game; var sp = sg.CurrentPlayer;
        var egg = (Minion)EngineEntity.FromCard(sp, Cards.FromId("FP1_007"), zone: sp.BoardZone);
        var swimmer = EngineEntity.FromCard(sp, Cards.FromId("JAIL_395"), zone: sp.HandZone); swimmer.Cost = 0;
        Check(sg.Process(sp.Options().First(t => t.Source?.Id == swimmer.Id && t.Target?.Id == egg.Id)), "Sewer Swimmer play succeeds");
        Check(egg.Zone == sp.BoardZone && sp.BoardZone.Any(m => m.Card.Id == "FP1_007t")
            && sg.HistoryFacts.Query.DeathrattlesResolved().Any(f => f.EntityId == egg.Id && f.Incarnation == 0),
            "Sewer Swimmer triggers living Deathrattle without death fact");
        var (impMatch, _) = AnimationMatch(); var ig = impMatch.Game; var ip = ig.CurrentPlayer;
        while (!ip.HandZone.IsFull) EngineEntity.FromCard(ip, Cards.FromId("CS2_182"), zone: ip.HandZone);
        var imp = EngineEntity.FromCard(ip, Cards.FromId("JAIL_398"), zone: ip.DeckZone);
        SabberStoneCore.Actions.Generic.Draw(ip, imp);
        Check(ig.HistoryFacts.Query.DeathrattlesResolved().Any(f => f.EntityId == imp.Id && f.Incarnation == 0),
            "overdrawn Impfernal triggers offboard Deathrattle without death fact");
        var (faultMatch, _) = AnimationMatch(); var faultGame = faultMatch.Game; var faultOwner = faultGame.CurrentPlayer;
        var card = Cards.FromId("CS2_029"); var priorTask = card.Power.PowerTask;
        try {
            card.Power.PowerTask = new CustomTask((g, c, s, t, stack) => throw new InvalidOperationException("injected-original"));
            var fault = EngineEntity.FromCard(faultOwner, card, zone: faultOwner.HandZone); fault.Cost = 0;
            var task = faultOwner.Options().First(t => t.Source?.Id == fault.Id && t.Target?.Id == faultOwner.Opponent.Hero.Id);
            var clone = faultGame.CloneExact(history:true);
            try { clone.Process(clone.CurrentPlayer.Options().First(t => t.Source?.Id == fault.Id && t.Target?.Id == clone.CurrentPlayer.Opponent.Hero.Id));
                Check(false, "injected action must fail"); }
            catch (InvalidOperationException error) { Check(error.Message == "injected-original", "action scope preserves original exception"); }
            var view = FairBotState.Create(faultGame, faultOwner.PlayerId, 9, DeckStyle.Control, new FairOptions { RemoveOpponentSecrets = true });
            int reports = 0, budget = 2; view.FaultReporter = _ => reports++;
            var actions = new[] { BotAction.From(task), BotAction.From(faultOwner.Options().First(t => t.PlayerTaskType == PlayerTaskType.END_TURN)) };
            var before = faultGame.Hash(); var rngBefore = faultGame.CloneExact(); var choice = BotDriver.BoundedFallback(view, actions, () => budget-- > 0);
            Check(choice.Type == PlayerTaskType.END_TURN && reports == 1 && budget == 1 && faultGame.Hash() == before,
                "failed speculative clone is reported, charged and cannot alter authority");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var left = random.GetValue(rngBefore); var right = random.GetValue(faultGame); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), "fallback leaves authoritative RNG unchanged");
            var safeMinion = (Minion)EngineEntity.FromCard(faultOwner, Cards.FromId("CS2_200"), zone: faultOwner.BoardZone);
            var safeCard = EngineEntity.FromCard(faultOwner, Cards.FromId("CAP_801"), zone: faultOwner.HandZone); safeCard.Cost = 0;
            view = FairBotState.Create(faultGame, faultOwner.PlayerId, 9, DeckStyle.Control, new FairOptions { RemoveOpponentSecrets = true });
            reports = 0; budget = 16; view.FaultReporter = _ => reports++;
            actions = faultOwner.Options().Where(t => t.PlayerTaskType == PlayerTaskType.END_TURN || t.Source?.Id == fault.Id && t.Target?.Id == faultOwner.Opponent.Hero.Id
                || t.Source?.Id == safeCard.Id && t.Target?.Id == safeMinion.Id).Select(BotAction.From).ToArray();
            before = faultGame.Hash(); choice = BotDriver.BoundedFallback(view, actions, () => budget-- > 0);
            Check(choice.Source == safeCard.Id && reports == 1 && faultGame.Hash() == before,
                "failed candidate does not poison succeeding fair clone");
            reports = 0;
            var beam = new TurnBeam(view, state => state.Evaluate(), 40, 8, false);
            while (!beam.IsDone) beam.StepNode();
            Check(reports > 0 && beam.Leaves.Any(leaf => leaf.Actions.Any(action => action.Source == safeCard.Id)) && faultGame.Hash() == before,
                "failed search branch is observable and does not discard successful branches");
        }
        finally { card.Power.PowerTask = priorTask; }
        Console.WriteLine("PASS bounded fallback friendly and enemy target order, both seats");
    }
}

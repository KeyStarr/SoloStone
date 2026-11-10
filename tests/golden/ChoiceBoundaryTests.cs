using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class ChoiceBoundaryTests
{
    private static FairBotState BeforeChoice(string cardId)
    {
        var game = Fixture(); game.Player1.UsedMana = 10;
        var card = Entity.FromCard(game.Player1, Cards.FromId(cardId), zone: game.Player1.HandZone);
        card.Cost = 0;
        return FairBotState.Create(game, 1, 71, DeckStyle.Tempo);
    }
    private static string Key(FairBotState state) => (string)typeof(GoldenPilot)
        .GetMethod("ReuseKey", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { state });

    public static void TestGeddonDrawReplacementChoiceDoesNotFallback()
    {
        // tiny gauntlet, runSeed 417 / pair 1 / seed 0: Geddon's turn-start Discover at turn 16.
        var game = Fixture();
        var geddon = Entity.FromCard(game.Player1, Cards.FromId("CATA_591"), zone: game.Player1.HandZone);
        Check(game.Process(game.Player1.Options().First(t => t.Source?.Id == geddon.Id)), "play Geddon");
        Check(game.Process(EndTurnTask.Any(game.Player1)), "end own turn");
        Check(game.Process(EndTurnTask.Any(game.Player2)), "start Geddon draw replacement");
        var choice = game.Player1.Choice;
        Check(choice != null && game.IdEntityDic[choice.SourceId].Card.Id == "CATA_591e", "Geddon choice required");
        int[] bound = choice.Choices.Select(id => game.IdEntityDic[id][SabberStoneCore.Actions.DiscoverRealRules.BoundEntity]).ToArray();
        Check(bound.Length == 3 && bound.All(id => game.IdEntityDic[id].Zone == game.Player1.DeckZone),
            "offers must be proxies bound to real deck cards");
        var before = game.SaveSnapshot();
        var pilot = new GoldenPilot(NpcCatalog.All[0]);
        var view = pilot.Options.FairViewFactory(game, 1, 17037, DeckStyle.Tempo);
        // Call outside BotDriver's catch so the original exception remains visible.
        var decision = pilot.Decide(view, new GoldenContext(game.Turn, 0, 17037, pilot.Profile));
        Check(decision.Action.Type == PlayerTaskType.CHOOSE && view.Actions.Contains(decision.Action), "select a legal Geddon offer");
        var settled = view.Apply(decision.Action);
        Check(!settled.HasPendingChoice && settled.Facts.Own.Hand.Count == 1, "chosen deck card reaches hand");
        Check(settled.Facts.Own.DeckCount == view.Facts.Own.DeckCount - 3, "draw chosen card and destroy rejected offers");
        Check(before.SequenceEqual(game.SaveSnapshot()), "search must preserve authoritative choice and deck");
    }

    public static void TestDiscoverOfferIdentityInReuseKey()
    {
        var root = BeforeChoice("CAP_105");
        var offered = root.Apply(root.OrderedActions.First(a => a.Type == PlayerTaskType.PLAY_CARD));
        var changed = offered.FingerprintGame.CloneExact();
        foreach (int id in changed.Player1.Choice.Choices) {
            var old = changed.IdEntityDic[id];
            changed.Player1.SetasideZone.ChangeEntity(old, Entity.FromCard(changed.Player1,
                Cards.FromId(old.Card.Id == "CS2_182" ? "CS2_200" : "CS2_182"), id: id));
        }
        var different = FairBotState.Create(changed, 1, 71, DeckStyle.Tempo);
        Check(offered.Actions.SequenceEqual(different.Actions), "same entity ids must keep the cached pick legal");
        var play = root.OrderedActions.First(a => a.Type == PlayerTaskType.PLAY_CARD);
        var pilot = new GoldenPilot(NpcCatalog.All[0]);
        pilot.Accept(root, new[] { play, offered.OrderedActions.First() }, 0, LethalOutcome.Incomplete);
        Check(pilot.Reuse(different) == null, "different cards at the same choice ids reused a cached pick");
        Check(Key(offered) != Key(different), "offered card identities must change the reuse key");
        Check(Key(offered) == Key(FairBotState.Create(Variant(offered.FingerprintGame), 1, 71, DeckStyle.Tempo)), "choice key exposed hidden identities");
    }

    public static void TestNeverReuseIntoOrOutOfChoice()
    {
        var root = BeforeChoice("CAP_105");
        var play = root.OrderedActions.First(a => a.Type == PlayerTaskType.PLAY_CARD);
        var offered = root.Apply(play);
        Check(offered.HasPendingChoice, "fixture must open a choice");
        var pick = offered.OrderedActions.First(); var resolved = offered.Apply(pick);
        var end = resolved.Actions.First(a => a.Type == PlayerTaskType.END_TURN);
        var pilot = new GoldenPilot(NpcCatalog.All[0]);
        pilot.Accept(root, new[] { play, pick, end }, 0, LethalOutcome.Incomplete);
        Check(pilot.Reuse(offered) == null && !pilot.PlanReused, "choice-creating action retained cached pick");
        pilot.Accept(offered, new[] { pick, end }, 0, LethalOutcome.Incomplete);
        Check(pilot.Reuse(resolved) == null && !pilot.PlanReused, "choice answer retained continuation");
    }

    public static void TestRewindKeepDiscardsContinuation()
    {
        var game = Fixture();
        var card = Entity.FromCard(game.Player1, Cards.FromId("TIME_034"), zone: game.Player1.HandZone);
        Check(game.Process(game.Player1.Options().First(t => t.Source?.Id == card.Id)), "Rewind setup");
        Check(game.Player1.Choice?.ChoiceAction == ChoiceAction.REWIND, "real Rewind offer required");
        var offered = View(game); var keep = offered.RewindKeep;
        Check(keep != null && !offered.HasPendingChoice, "fair Rewind must retain only the keep action");
        var resolved = offered.Apply(keep);
        var pilot = new GoldenPilot(NpcCatalog.All[0]);
        pilot.Accept(offered, new[] { keep, resolved.Actions.First(a => a.Type == PlayerTaskType.END_TURN) }, 0, LethalOutcome.Incomplete);
        Check(pilot.Reuse(resolved) == null, "Rewind keep retained a cached continuation");
    }

    public static void TestRewindActionDiscardsContinuation()
    {
        var root = BeforeChoice("TIME_034");
        var play = root.Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD);
        var child = root.Apply(play);
        Check(!child.HasPendingChoice, "fair simulation suppresses newly created Rewind offers");
        var pilot = new GoldenPilot(NpcCatalog.All[0]);
        pilot.Accept(root, new[] { play, child.Actions.First(a => a.Type == PlayerTaskType.END_TURN) }, 0, LethalOutcome.Incomplete);
        Check(pilot.Reuse(child) == null, "Rewind action cached across its suppressed offer");
    }

    public static void TestReplyReservationCoversChoiceSettlement()
    {
        string expected = null;
        foreach (int slice in new[] { 1, 7, 1000 }) {
            var job = ReplyModel.Job(View(Fixture(enemy: CardClass.WARRIOR)), NpcCatalog.All[0], 71,
                new DeckPlanEvaluator(DeckPlans.DragonWarrior), replyNodes: 1);
            job.StepNode(); job.StepNode();
            typeof(ReplyJob).GetProperty("Samples").SetValue(job, new[] { new HandSample(new[] { "CAP_105" }, 1) });
            Run(job, slice);
            Check(job.Outcomes.Count == 1, "capped Discover sample lost its probability mass");
            Check(job.Nodes <= ReplyModel.NodeAllowance(1, 1), "reply settlement exceeded reservation");
            string actual = job.Nodes + "|" + job.Value.ToString("R");
            if (expected == null) expected = actual; else Check(actual == expected, "reply settlement changed with slicing");
        }
    }

    // WP1 Reborn Priest: a capped own-turn leaf stopped inside a pending choice made ReplyJob end the turn
    // over it ("Cannot end a pending choice") and the whole decision fell back to the greedy pilot.
    public static void TestReplySettlesPendingOwnChoiceBeforeEndTurn()
    {
        var root = BeforeChoice("CAP_105");
        var offered = root.Apply(root.OrderedActions.First(a => a.Type == PlayerTaskType.PLAY_CARD));
        Check(offered.HasPendingChoice, "fixture must open a choice");
        var job = ReplyModel.Job(offered, NpcCatalog.All[0], 71, new DeckPlanEvaluator(DeckPlans.DragonWarrior), replyNodes: 8);
        Run(job);
        Check(job.Outcomes.Count > 0, "reply leaf inside a pending choice must settle the choice and still score replies");
    }

    public static void TestReplySettlementYieldsBetweenChoiceAndEndTurn()
    {
        string expected = null;
        foreach (int slice in new[] { 1, 7, 1000 }) {
            var root = BeforeChoice("CAP_105");
            var beam = new TurnBeam(root, s => s.Evaluate(), 1, 4, true);
            beam.StepNode();
            var leaf = beam.Leaves.Single(n => n.State.HasPendingChoice);
            int turn = leaf.State.Facts.Turn, nodes;
            var pending = leaf.State;
            do { nodes = beam.Nodes; beam.StepNode(); } while (!beam.IsDone && ReferenceEquals(pending, leaf.State));
            Check(!leaf.State.HasPendingChoice && leaf.State.Facts.Turn == turn,
                "one node must answer Discover without also applying END_TURN");
            Check(beam.Nodes == nodes + 1, "settlement action must consume a node");
            Check(leaf.Actions.Last().Type == PlayerTaskType.CHOOSE, "settlement choice omitted from witness");
            Run(beam, slice);
            Check(beam.Leaves.All(n => n.State.Facts.Turn == turn + 1), "settlement must finish at the next turn");
            Check(beam.Leaves.All(n => n.Actions.Count(a => a.Type == PlayerTaskType.END_TURN) == 1), "settlement witness must contain exactly one END_TURN");
            string actual = beam.Nodes + "|" + string.Join(";", beam.Leaves.Select(n => n.Key + ":" + n.Value.ToString("R") + ":" + Key(n.State)));
            if (expected == null) expected = actual; else Check(actual == expected, "settlement changed with slicing");
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class ReviewTests
{
    public static void TestFailedReplySampleIsDropped()
    {
        string expected = null;
        foreach (int slice in new[] { 1, 7, 1000 }) {
            var job = ReplyModel.Job(View(Fixture()), NpcCatalog.All[0], 71, new DeckPlanEvaluator(DeckPlans.DragonWarrior));
            job.StepNode(); job.StepNode();
            typeof(ReplyJob).GetProperty("Samples").SetValue(job, new[] {
                new HandSample(new[] { "NOT_A_CARD" }, .8), new HandSample(new[] { "CS2_182" }, .2) });
            Run(job, slice);
            Check(job.Outcomes.Count == 1 && Math.Abs(job.Value - job.Outcomes[0].Key) < 1e-9,
                "failed sample must be dropped and surviving mass renormalized");
            string actual = job.Value.ToString("R") + ":" + job.Nodes;
            if (expected == null) expected = actual; else Check(actual == expected, "sample failures depend on slicing");
        }
    }
    public static void TestFailedPositiveMassLeavesSafeBaseline()
    {
        var job = ReplyModel.Job(View(Fixture()), NpcCatalog.All[0], 71, new DeckPlanEvaluator(DeckPlans.DragonWarrior));
        job.StepNode(); job.StepNode(); double baseline = job.Value;
        typeof(ReplyJob).GetProperty("Samples").SetValue(job, new[] {
            new HandSample(new[] { "NOT_A_CARD" }, 1), new HandSample(new[] { "CS2_182" }, 0) });
        Run(job);
        Check(job.Value == baseline && !double.IsNaN(job.Value), "no surviving positive mass must keep deterministic baseline");
    }
    public static void TestDrawLeafReceivesReplyPenalty()
    {
        var g = Fixture();
        Board(g, false, "CS2_200", true); Board(g, false, "CS2_200", true);
        foreach (var card in g.Player1.DeckZone.ToArray()) { g.Player1.DeckZone.Remove(card); g.Player1.SetasideZone.Add(card); }
        g.Player1.DeckZone.AddGenerated(Cards.FromId("CS2_182"), g.Player2.Hero);
        var shield = Entity.FromCard(g.Player1, Cards.FromId("EX1_606"), zone: g.Player1.HandZone); shield.Cost = 10;
        var hold = View(g); var draw = hold.Apply(hold.Actions.First(a => a.Source == shield.Id));
        Check(!draw.CanExpand, "fixture must reach unknown draw boundary");
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        // Pinned to the 4-sample budget this fixture was built on: with more samples a burst hand appears in
        // which Shield Block's armor genuinely averts an on-board lethal threat, which is not what this checks.
        var p = NpcCatalog.All[0]; var profile = new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin,
            p.CardBack, p.Personality, new Budget(3000, 1400, 400, 4, 3, 96), p.PlanId, p.HeroSkinPower);
        var job = ReplyModel.Job(draw, profile, 71, evaluator); Run(job);
        Check(job.Samples.Count > 0 && job.Value < draw.Evaluate(evaluator) - 10,
            "unknown draw must face enemy attacks, not retain static score");
        var holding = ReplyModel.Job(hold, profile, 71, evaluator); Run(holding);
        Check(holding.Value > job.Value, "draw boundary alone made costly Shield Block beat holding: " + holding.Value + " / " + job.Value + " outcomes " + string.Join(";", holding.Outcomes) + " / " + string.Join(";", job.Outcomes));
        var planner = TurnPlanner.Job(hold, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71); Run(planner);
        Check(!planner.Result.Line.Any(a => a.Source == shield.Id), "planner selected overpriced draw only to evade replies");
    }
    public static void TestReplyHoldBonusChangesLine()
    {
        var g = Fixture(); g.Player1.UsedMana = 8;
        var card = Entity.FromCard(g.Player1, Cards.FromId("CS2_182"), zone: g.Player1.HandZone); card.Cost = 1;
        var plain = new DeckPlan("plain", "npc-dragon-warrior", null, null, null, null, null, null, null, null, null,
            DeckPlans.DragonWarrior.Weights);
        var hold = new DeckPlan("hold", "npc-dragon-warrior", null, null, null,
            new[] { new HoldRule(HoldCondition.ThreatOrLethal, 500, new[] { "CS2_182" }) }, null, null, null, null, null,
            DeckPlans.DragonWarrior.Weights);
        var a = TurnPlanner.Job(View(g), NpcCatalog.All[0], plain, 71); Run(a);
        var b = TurnPlanner.Job(View(g), NpcCatalog.All[0], hold, 71); Run(b);
        Check(a.Result.Line.Any(x => x.Source == card.Id), "plain evaluator should play body");
        Check(!b.Result.Line.Any(x => x.Source == card.Id), "restored hand hold bonus must change chosen reply-scored line");
        var pilot = new GoldenPilot(NpcCatalog.All[0], hold);
        Check(pilot.Decide(View(g), new GoldenContext(g.Turn, 0, 71, pilot.Profile)).Action.Source != card.Id,
            "pilot must retain intentional hold while planning budget remains");
    }
    public static void TestExhaustedBudgetStillActsAndChecksLethal()
    {
        var p = NpcCatalog.All[0];
        var tiny = new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack,
            p.Personality, new Budget(1, 1, 100, 1, 1, 1), p.PlanId);
        var g = Fixture(); g.Player1.UsedMana = 10;
        for (int i = 0; i < 5; i++) Board(g, true, "CS2_182", true);
        var pilot = new GoldenPilot(tiny);
        for (int i = 0; i < 5; i++) {
            if (i == 4) g.Player2.Hero.Damage = 29;
            var state = View(g);
            var answer = pilot.Decide(state, new GoldenContext(g.Turn, i, 71, tiny));
            Check(answer.Action.Type != PlayerTaskType.END_TURN, "budget forced end with unused attacks");
            if (i == 4) Check(pilot.LastLethal == LethalOutcome.Found, "exhausted budget skipped lethal check");
            Check(g.Process(answer.Action.Resolve(g.Player1.Options())), "attack rejected");
        }
        Check(pilot.RemainingNodes == 0, "fixture must exhaust planning budget");
    }
    public static void TestGeneratedPlaysDoNotDepleteDeckPrior()
    {
        var g = Fixture(); Check(g.Process(EndTurnTask.Any(g.Player1)), "enemy turn");
        var original = g.Player2.HandZone.Single();
        string id = original.Card.Id;
        Check(g.Process(g.Player2.Options().First(t => t.Source?.Id == original.Id)), "original play");
        var generated = Entity.FromCard(g.Player2, Cards.FromId(id), zone: g.Player2.HandZone);
        generated.Cost = 0;
        Check(g.Process(g.Player2.Options().First(t => t.Source?.Id == generated.Id)), "generated play");
        var pilot = new GoldenPilot(NpcCatalog.All[0]);
        pilot.Options.FairViewFactory(g, 1, 71, DeckStyle.Tempo);
        Check(pilot.PublicInputs.OpponentPlayed.SequenceEqual(new[] { id }), "generated play consumed a deck copy");
        var expected = CardFrequencies.Sample(CardClass.MAGE, 5, 4, 10, 71, new[] { id });
        var actual = CardFrequencies.Sample(CardClass.MAGE, 5, 4, 10, 71, pilot.PublicInputs.OpponentPlayed);
        Check(expected.Zip(actual, (a, b) => a.Weight == b.Weight && a.Cards.SequenceEqual(b.Cards)).All(x => x),
            "generated play changed population or sample weights");
    }
    public static void TestReplyUsesStartingClassAfterHeroReplacement()
    {
        var g = Fixture();
        g.Player2.AddHeroAndPower(Cards.FromId("HERO_01"), Cards.FromId("CS2_102"));
        Check(g.Player2.BaseClass == CardClass.MAGE && g.Player2.Hero.Card.Class == CardClass.WARRIOR, "class fixture");
        var job = ReplyModel.Job(View(g), NpcCatalog.All[0], 71, new DeckPlanEvaluator(DeckPlans.DragonWarrior)); Run(job);
        var expected = CardFrequencies.Sample(CardClass.MAGE, 1, NpcCatalog.All[0].Budget.ReplyK, 10, 71);
        Check(job.Samples.Zip(expected, (a, b) => a.Weight == b.Weight && a.Cards.SequenceEqual(b.Cards)).All(x => x),
            "transformed hero changed opponent deck class prior");
    }
    public static void TestAreaThreatUsesWordsAndGameTextSpacing()
    {
        Check(new CardFrequency("EX1_259", 1).AreaThreat, "all_enemy must still match Lightning Storm");
        Check(!CardFrequency.IsAreaThreat("rally: deal damage to a random minion"), "Rally must not match all");
    }
    public static void TestLowBudgetStillSearchesOwnActions()
    {
        var g = Fixture(); g.Player1.UsedMana = 10;
        var a = Board(g, true, "CS2_182", true); var b = Board(g, true, "CS2_182", true);
        var job = TurnPlanner.Job(View(g), NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 100); Run(job);
        Check(job.Result.Line.Count(x => x.Type == PlayerTaskType.MINION_ATTACK) == 2,
            "reply reserve collapsed small own-turn beam");
        Check(job.Result.Nodes <= 100, "scaled reply search exceeded allowance");
    }
    public static void TestReplyBeamUsesOneHorizon()
    {
        var state = View(Fixture()); var end = state.Apply(state.Actions.Single(a => a.Type == PlayerTaskType.END_TURN));
        var reply = end.Determinize(new[] { "CS2_182" }, 71).ForReply();
        var beam = new TurnBeam(reply, s => s.Evaluate(), 30, 4, true); Run(beam);
        Check(beam.Leaves.All(n => n.State.Facts.Turn == reply.Facts.Turn + 1
            && n.State.Facts.Opponent.Hand.Count == reply.Facts.Opponent.Hand.Count + 1),
            "all capped and completed reply leaves must include the bot's next draw");
    }
}

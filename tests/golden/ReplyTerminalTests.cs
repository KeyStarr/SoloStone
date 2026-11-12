using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Enchants;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class ReplyTerminalTests
{
    public static void TestRandomReplyRiskPrefersArmorToExposure()
    {
        var game = Fixture(CardClass.WARRIOR, CardClass.MAGE); game.Player1.Hero.Damage = 29;
        game.Player2.BaseMana = 0; Board(game, true, "CS2_182");
        var exposed = View(game);
        var armored = exposed.Apply(exposed.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER));
        ReplyJob Score(FairBotState leaf) {
            var job = ReplyModel.Job(leaf, NpcCatalog.All[0], 2, new DeckPlanEvaluator(DeckPlans.DragonWarrior));
            job.StepNode(); job.StepNode();
            typeof(ReplyJob).GetProperty("Samples").SetValue(job, new[] { new HandSample(new[] { "EX1_277" }, 1) });
            Run(job); return job;
        }
        var risk = Score(exposed); var defense = Score(armored);
        Check((risk.Survival ?? 1) < (defense.Survival ?? 1), "independent Missiles rolls must retain random death risk");
        Check(Math.Abs(risk.Value) < 1000 && Math.Abs(defense.Value) < 1000, "random risk contaminated ordinary utility");
    }

    public static void TestIncompleteReplyVerificationKeepsFiniteUtility()
    {
        var game = Fixture(CardClass.WARRIOR, CardClass.MAGE); game.Player1.Hero.Damage = 24;
        var job = ReplyModel.Job(View(game), NpcCatalog.All[0], 2, new DeckPlanEvaluator(DeckPlans.DragonWarrior));
        job.StepNode(); job.StepNode();
        typeof(ReplyJob).GetProperty("Samples").SetValue(job, new[] { new HandSample(new[] { "CS2_029" }, 1) });
        var field = typeof(ReplyJob).GetField("verification", BindingFlags.Instance | BindingFlags.NonPublic);
        int guard = 0;
        while (!job.IsDone && field.GetValue(job) == null && guard++ < 10000) job.StepNode();
        var original = (LethalJob)field.GetValue(job);
        Check(original != null, "Fireball must enter witness verification");
        var line = (BotAction[])typeof(LethalJob).GetField("candidate", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(original);
        var capped = LethalJob.Verify(original.State, line, 1, measureReplies: true);
        field.SetValue(job, capped); Run(job);
        Check(capped.Result.Outcome == LethalOutcome.Incomplete && job.Survival == 0 && job.Untested.Single().Key == .75,
            "capped holdouts must stay unknown, not count as survival");
        Check(Math.Abs(job.Value) < 1000 && job.Outcomes.All(o => Math.Abs(o.Key) < 1000),
            "capped witness retained terminal score");
    }

    public static void TestVerifiedSampleRiskCannotContaminateUtility()
    {
        var game = Fixture(CardClass.WARRIOR, CardClass.MAGE); game.Player1.Hero.Damage = 24;
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        foreach (int slice in new[] { 1, 7, 1000 }) {
            var job = ReplyModel.Job(View(game), NpcCatalog.All[0], 2, evaluator);
            job.StepNode(); job.StepNode();
            var samples = Enumerable.Range(0, 12).Select(i => new HandSample(new[] { i == 0 ? "CS2_029" : "CS2_182" }, 1.0 / 12)).ToArray();
            typeof(ReplyJob).GetProperty("Samples").SetValue(job, samples);
            Run(job, slice);
            double expected = ReplyModel.Blend(samples.Select((s, i) => new System.Collections.Generic.KeyValuePair<double, double>(i == 0 ? 0 : 1, s.Weight)), NpcCatalog.All[0].Personality.ReplyBlendBeta);
            Check(job.Survival.HasValue && Math.Abs(job.Survival.Value - expected) < 1e-12,
                "one verified Fireball hand must retain its weighted survival risk");
            Check(Math.Abs(job.Value) < 1000 && job.Outcomes.All(o => Math.Abs(o.Key) < 1000),
                "terminal sentinel entered ordinary utility");
            var safe = new TurnLine { Value = -998000 };
            var risky = new TurnLine { Value = job.Value, Survival = job.Survival };
            Check(TurnLine.Rank(new[] { risky, safe }).First() == safe, "better board value erased verified death risk");
        }
    }
    private static T Field<T>(object job, string name) => (T)job.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(job);
    private static NpcProfile Tier(bool elite)
    {
        var p = NpcCatalog.All[0];
        var b = elite ? new Budget(280000, 70000, 4000, 14, 9, 160, 25, false, 45, 55)
            : new Budget(28000, 7000, 500, 8, 3, 160, 16, false, 45, 55);
        return new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack, p.Personality, b, p.PlanId);
    }
    private static ReplyJob Sample(FairBotState state, NpcProfile profile, string[] cards, int nodes = -1)
    {
        var job = ReplyModel.Job(state, profile, 2, new DeckPlanEvaluator(DeckPlans.DragonWarrior), replyNodes: nodes);
        job.StepNode(); job.StepNode();
        typeof(ReplyJob).GetProperty("Samples").SetValue(job, new[] { new HandSample(cards, 1) });
        return job;
    }
    public static void TestEndTurnBurnRecordsEmptyAndCappedPrefixWitnesses()
    {
        foreach (bool elite in new[] { false, true }) foreach (int slice in new[] { 1, 7, 1000 })
        foreach (bool prefix in new[] { false, true }) {
            var game = Fixture(enemy: CardClass.WARRIOR); game.Player1.Hero.Damage = 26; game.Player2.BaseMana = prefix ? 4 : 0;
            if (!prefix) Board(game, false, "CATA_999").IsFrozen = true;
            var job = Sample(View(game), Tier(elite), new[] { prefix ? "CATA_999" : "CS2_182" }, 1);
            LethalJob verification = null; TurnBeam beam = null;
            while (!job.IsDone && verification == null) {
                job.StepNode(); beam = Field<TurnBeam>(job, "beam") ?? beam;
                verification = Field<LethalJob>(job, "verification");
            }
            Check(verification != null, "end-turn burn must enter verification");
            var witness = Field<BotAction[]>(verification, "candidate");
            Check(witness.Length == (prefix ? 2 : 1) && witness.Last().Type == PlayerTaskType.END_TURN
                && (!prefix || witness.First().Type == PlayerTaskType.PLAY_CARD), "wrong empty/capped-prefix lethal witness");
            Check(beam.Leaves.All(l => l.Actions.Last().Type == PlayerTaskType.END_TURN), "settlement omitted END_TURN");
            Check(beam.Leaves.Any(l => prefix ? l.Actions.Count > 1 : l.Actions.Count == 1), "missing empty/capped-prefix control");
            foreach (var leaf in beam.Leaves.Where(l => l.Value >= 999000)) {
                var replay = verification.State;
                foreach (var action in leaf.Actions) replay = replay.Apply(action);
                Check(replay.IsTerminal, "settled witness does not reproduce endpoint");
            }
            Run(job, slice);
            Check(job.Survival == 0 && job.Untested.Single().Key == 0 && Math.Abs(job.Value) < 1000,
                "deterministic end-turn burn lost survival protection");
            Check(job.Nodes <= ReplyModel.NodeAllowance(1, 1), "settlement/replay exceeded upstream reservation");
        }
    }
    public static void TestSevenAttackReplyCompletesFourHoldoutsAtBothTiers()
    {
        foreach (bool elite in new[] { false, true }) foreach (int slice in new[] { 1, 7, 1000 }) {
            var game = Fixture(); game.Player1.Hero.Damage = 23; game.Player2.BaseMana = 0;
            for (int i = 0; i < 7; i++) { var m = Board(game, false, "CS2_231"); m.AttackDamage = 1; }
            var profile = Tier(elite); var job = Sample(View(game), profile, new[] { "CS2_182" });
            typeof(ReplyJob).GetProperty("Samples").SetValue(job, Enumerable.Range(0, profile.Budget.ReplyK)
                .Select(_ => new HandSample(new[] { "CS2_182" }, 1.0 / profile.Budget.ReplyK)).ToArray());
            Run(job, slice);
            Check(job.Survival == 0 && job.Untested.Count == profile.Budget.ReplyK && job.Untested.All(s => s.Key == 0),
                "seven deterministic attacks looked survivable");
            Check(job.Nodes <= ReplyModel.NodeAllowance(profile.Budget.ReplyK, profile.Budget.ReplyR), "long witness exceeded upstream reservation");
        }
    }
    public static void TestMidWitnessInterruptionKeepsUnknownMassDistinct()
    {
        var game = Fixture(); game.Player2.Hero.Damage = 23;
        for (int i = 0; i < 7; i++) { var m = Board(game, true, "CS2_231", true); m.AttackDamage = 1; }
        var root = View(game); var at = root; var line = new List<BotAction>();
        for (int i = 0; i < 7; i++) { var a = at.Actions.First(x => x.Type == PlayerTaskType.MINION_ATTACK && x.Target == game.Player2.Hero.Id); line.Add(a); at = at.Apply(a); }
        foreach (int slice in new[] { 1, 7, 1000 }) {
            var capped = LethalJob.Verify(root, line, 10, measureReplies: true); Run(capped, slice);
            Check(capped.Result.Outcome == LethalOutcome.Incomplete && capped.Result.Nodes == 10
                && capped.VerifiedReplyWinShare == .25 && capped.UntestedReplyShare == .75,
                "partially replayed second holdout changed completed/unknown mass");
        }
    }
    public static void TestWinThenFaultRetainsCompletedMass()
    {
        var game = Fixture(); game.Player2.Hero.Damage = 24;
        int calls = 0, faults = 0;
        var card = Cards.FromId("CS2_029").Clone();
        card.Power = new Power { PowerTask = new FuncNumberTask((IPlayable source) => {
            if (++calls == 2) throw new InvalidOperationException("injected second-holdout fault");
            SabberStoneCore.Actions.Generic.DamageCharFunc.Invoke(source, source.Controller.Opponent.Hero, 6, false);
            return 0;
        }) };
        var burn = Entity.FromCard(game.Player1, card, zone: game.Player1.HandZone);
        var root = View(game); root.FaultReporter = _ => faults++;
        var line = new[] { root.Actions.Single(a => a.Source == burn.Id && a.Target == game.Player2.Hero.Id) };
        var job = LethalJob.Verify(root, line, 4, measureReplies: true); job.StepNode();
        Check(job.VerifiedReplyWinShare == .25 && job.UntestedReplyShare == .75, "first win mass");
        Run(job);
        Check(faults == 1 && calls == 4 && job.Result.Outcome == LethalOutcome.Incomplete
            && job.VerifiedReplyWinShare == .75 && job.UntestedReplyShare == .25,
            "fault erased earlier wins or became survival evidence");
    }

    public static void TestFailedFirstHoldoutStillMeasuresLaterWins()
    {
        var game = Fixture(CardClass.MAGE); game.Player2.Hero.Damage = 29; Board(game, false, "CS2_182");
        var missiles = Entity.FromCard(game.Player1, Cards.FromId("EX1_277"), zone: game.Player1.HandZone);
        var root = View(game); var cast = root.Actions.Single(a => a.Source == missiles.Id);
        bool found = false;
        for (int seed = 1; seed <= 100; seed++) {
            var state = root.WithArtificialSeed(seed);
            var wins = Enumerable.Range(1, 4).Select(s => state.WithArtificialSeed(s).Apply(cast).Facts.Own.PlayState == PlayState.WON).ToArray();
            if (wins[0] || !wins.Skip(1).Any(w => w)) continue;
            foreach (int slice in new[] { 1, 7, 1000 }) {
                var job = LethalJob.Verify(state, new[] { cast }, 4, measureReplies: true); Run(job, slice);
                Check(job.VerifiedReplyWinShare == wins.Count(w => w) / 4.0 && job.UntestedReplyShare == 0,
                    "first counterexample erased later verified wins");
            }
            found = true; break;
        }
        Check(found, "missing failed-first/successful-later random control");
    }
    public static void TestKnownReturnedChargeRetainsLethalRisk()
    {
        var game = Fixture(CardClass.ROGUE); game.Player1.Hero.Damage = 28;
        var charge = Board(game, false, "CS2_173");
        var sap = Entity.FromCard(game.Player1, Cards.FromId("EX1_581"), zone: game.Player1.HandZone);
        var root = View(game); root = root.Apply(root.Actions.Single(a => a.Source == sap.Id && a.Target == charge.Id));
        Check(root.Facts.Opponent.Hand.Any(c => !c.Unknown && c.CardId == "CS2_173"), "returned Charge must be public");
        foreach (bool elite in new[] { false, true }) foreach (int slice in new[] { 1, 7, 1000 }) {
            var job = Sample(root, Tier(elite), new[] { "CS2_182" }); Run(job, slice);
            Check(job.Survival == 0 && job.Untested.Single().Key == 0, "known returned Charge lethal lost");
        }
    }
    public static void TestPlannerSelectsArmorAgainstMissilesAtBothTiers()
    {
        foreach (bool elite in new[] { false, true }) foreach (int slice in new[] { 1, 7, 1000 }) foreach (bool pass in new[] { false, true }) {
            var game = Fixture(); game.Player1.Hero.Damage = 29; game.Player2.BaseMana = 0; Board(game, true, "CS2_182");
            var profile = Tier(elite); var job = TurnPlanner.Job(View(game), profile, DeckPlans.DragonWarrior, 2, comparePass: pass);
            // Force the same public hand hypothesis on every real planner finalist.
            while (!job.IsDone) {
                for (int i = 0; i < slice && !job.IsDone; i++) {
                    job.StepNode(); var reply = Field<ReplyJob>(job, "reply");
                    if (reply != null && reply.Samples.Count > 0 && reply.Outcomes.Count == 0 && Field<TurnBeam>(reply, "beam") == null)
                        typeof(ReplyJob).GetProperty("Samples").SetValue(reply, new[] { new HandSample(new[] { "EX1_277" }, 1) });
                }
            }
            Check(!pass || job.HasPassComparison, "planner did not finish paired pass comparison");
            Check(job.Result.Line.First().Type == PlayerTaskType.HERO_POWER, "planner failed to select Armor Up; elite=" + elite + " pass=" + pass);
        }
    }
    public static void TestClockCutKeepsZeroSurvivalDefensiveEscapeAtBothTiers()
    {
        foreach (bool elite in new[] { false, true }) {
            var game = Fixture(); game.Player1.BaseMana = 4; game.Player1.Hero.Damage = 23; game.Player2.BaseMana = 0;
            for (int i = 0; i < 7; i++) { var enemy = Board(game, false, "CS2_231"); enemy.AttackDamage = 1; }
            var large = (Minion)Entity.FromCard(game.Player1, Cards.FromId("CS2_200"), zone: game.Player1.HandZone);
            large.Cost = 4; large.AttackDamage = 60; large.Health = 60; large.HasStealth = true;
            var taunt = Entity.FromCard(game.Player1, Cards.FromId("CS2_179"), zone: game.Player1.HandZone);
            var profile = Tier(elite); var job = TurnPlanner.Job(View(game), profile, DeckPlans.DragonWarrior, 2);
            var scored = Field<List<TurnLine>>(job, "scored");
            while (!job.IsDone && scored.Count == 0) {
                job.StepNode(); var reply = Field<ReplyJob>(job, "reply");
                if (reply != null && reply.Samples.Count > 0 && reply.Outcomes.Count == 0 && Field<TurnBeam>(reply, "beam") == null)
                    typeof(ReplyJob).GetProperty("Samples").SetValue(reply, new[] { new HandSample(new[] { "CS2_182" }, 1) });
            }
            Check(scored.Count == 1 && scored[0].Survival == 0 && scored[0].Actions.Any(a => a.Source == large.Id), "first finalist must lose to seven attacks; elite=" + elite + " scored=" + string.Join(";", scored.Select(l => l.Key + "/" + l.Survival)));
            int steps = Field<int>(job, "steps"); var result = job.StopSearch();
            Check(result.Nodes == steps && result.Line.First().Source == taunt.Id, "clock cut missed unchecked public-safe Taunt escape");
        }
    }

    public static void TestInterruptedPassComparisonRetainsOriginalAtBothTiers()
    {
        foreach (bool elite in new[] { false, true }) foreach (int slice in new[] { 1, 7, 1000 }) {
            var game = Fixture(); game.Player1.Hero.Damage = 24; game.Player2.BaseMana = 3;
            var body = Board(game, true, "CS2_182", true); var root = View(game); var profile = Tier(elite);
            var hit = root.Actions.Single(a => a.Source == body.Id && a.Target == game.Player2.Hero.Id);
            var end = root.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
            var pilot = new GoldenPilot(profile); var context = new GoldenContext(game.Turn, 0, 2, profile);
            pilot.Job(root, context); pilot.Accept(root, new[] { hit, end }, 0, LethalOutcome.Incomplete, interrupted: true);
            var job = pilot.Job(root.Apply(hit), context);
            Check(job.StopSearch(SearchStop.Clock) == null && job.Validating, "clock-cut cached pass must start validation");
            var validation = Field<TurnPlannerJob>(job, "validation"); bool cut = false;
            for (int block = 0; block < 100000 && !job.IsDone && !cut; block += slice) {
                for (int i = 0; i < slice && !job.IsDone && !cut; i++) {
                    job.StepNode(); var reply = Field<ReplyJob>(validation, "reply");
                    if (reply == null) continue;
                    if (reply.Samples.Count > 0 && reply.Outcomes.Count == 0 && Field<TurnBeam>(reply, "beam") == null)
                        typeof(ReplyJob).GetProperty("Samples").SetValue(reply, new[] { new HandSample(new[] { "CS2_029" }, 1) });
                    var verify = Field<LethalJob>(reply, "verification");
                    cut = verify != null && verify.Result.Nodes == 1;
                }
            }
            Check(cut && !validation.HasPassComparison, "must interrupt reply witness before comparing pass/continuation");
            int steps = Field<int>(validation, "steps"); var answer = job.StopValidation();
            Check(answer.Action.Type == PlayerTaskType.END_TURN && answer.Stop == "Clock"
                && answer.SearchInfo.ValidationFallback && !answer.SearchInfo.ValidationCompared
                && answer.SearchInfo.ValidationNodes == steps,
                "unfinished reply promoted pass comparison or ran extra search");
        }
    }

}

using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static ReplyTests;

public static class TemporaryMinionTests
{
    private static FairBotState GhoulState()
    {
        var game = Fixture(CardClass.DEATHKNIGHT); game.Player1.BaseMana = 2;
        var state = View(game);
        return state.Apply(state.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER));
    }
    private static Game FairGame(FairBotState state) => (Game)typeof(FairBotState)
        .GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(state);

    public static void TestEndpointInterruptionAndTinyCaps()
    {
        var state = GhoulState(); var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        var beam = new TurnBeam(state, s => s.Evaluate(evaluator), 20, 8, false, evaluator);
        beam.StepNode(); // root endpoint
        Check(beam.EndpointScores == 1 && !beam.Progress.Any(), "root endpoint must not invent a child");
        beam.StepNode(); // attack expansion; ghoul survives face attack but still expires
        Check(beam.Expansions == 1 && !beam.Progress.Any() && beam.ScoredLeaves.All(l => l.Actions.Count == 0),
            "unfinished child must be absent from ranked candidates");
        beam.StepNode(); // child endpoint
        var child = beam.Progress.Single();
        Check(beam.EndpointScores == 2 && child.Actions.Count == 1, "scored child must become a candidate");
        Check(child.State.Facts.Own.Board.Count == 1 && child.State.Actions.Any(a => a.Type == PlayerTaskType.END_TURN),
            "endpoint scoring must retain legal unended continuation");
        for (int cap = 1; cap <= 8; cap++) {
            var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, cap);
            int steps = 0;
            while (!job.IsDone) { job.StepNode(); Check(++steps <= cap, "tiny cap overrun"); }
            Check(job.Result.Nodes <= cap, "reported tiny cap overrun");
            if (cap == 2) Check(job.Result.Candidates.All(c => c.Key.Type != PlayerTaskType.MINION_ATTACK),
                "node cut admitted unfinished attack");
            if (cap == 3) Check(job.Result.Candidates.Any(c => c.Key.Type == PlayerTaskType.MINION_ATTACK),
                "scored attack lost at cap");
            var replay = state;
            foreach (var action in job.Result.Line) {
                Check(replay.Actions.Contains(action), "tiny-cap continuation illegal"); replay = replay.Apply(action);
            }
        }
        foreach (int stop in new[] { 2, 3 }) {
            var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 100);
            for (int i = 0; i < stop; i++) job.StepNode();
            var result = job.StopSearch();
            Check(result.Candidates.Any(c => c.Key.Type == PlayerTaskType.MINION_ATTACK) == (stop == 3),
                "clock cut must exclude exactly the unfinished child");
        }
    }

    public static void TestEndpointRefusalAndSimulationFailureDiscountExpiringCredit()
    {
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        foreach (bool refusal in new[] { true, false }) {
            var state = GhoulState(); var game = FairGame(state);
            if (refusal) {
                var fire = Enchantment.GetInstance(game.Player1, game.Player1.Hero, game.Player1, Cards.FromId("CATA_EVENT_001e"));
                fire[GameTag.TAG_SCRIPT_DATA_NUM_2] = 1;
                bool refused = false;
                try { state.ForReplyEndTurn(); } catch (Exception e) when (FairHistory.IsPrivate(e)) { refused = true; }
                Check(refused, "fixture must exercise real private-history refusal");
            }
            int faults = 0; state.FaultReporter = _ => faults++;
            Func<FairBotState, double> score = s => {
                if (!refusal && s.Facts.CurrentPlayerId != s.Facts.Own.PlayerId)
                    throw new SimulationFailureException(state.Actions.Single(a => a.Type == PlayerTaskType.END_TURN), new Exception("injected endpoint fault"));
                return s.Evaluate(evaluator);
            };
            var beam = new TurnBeam(state, score, 3, 8, false, evaluator);
            beam.StepNode(); beam.StepNode();
            Check(!beam.Progress.Any(), "failed projection child still pending before its scoring step");
            beam.StepNode();
            var child = beam.Progress.Single();
            Check(child.Value <= child.State.Evaluate(evaluator) - child.State.Facts.Own.Board.Sum(evaluator.FriendlyBoardCredit),
                "failed endpoint retained temporary minion credit");
            Check(child.State.Actions.Any(a => a.Type == PlayerTaskType.END_TURN), "failed endpoint corrupted continuation");
            beam.StepNode(); Check(beam.Nodes <= 3 && beam.IsDone, "failure cap overrun");
            Check(refusal ? faults == 0 : faults == 2, "simulation fault reporting changed");
        }
    }

    private static void RefuseEndpoint(FairBotState state)
    {
        var game = FairGame(state);
        var fire = Enchantment.GetInstance(game.Player1, game.Player1.Hero, game.Player1, Cards.FromId("CATA_EVENT_001e"));
        fire[GameTag.TAG_SCRIPT_DATA_NUM_2] = 1;
    }

    public static void TestFailedEndpointPreservesPermanentBodiesAndDevelopment()
    {
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        foreach (bool refusal in new[] { true, false })
        foreach (string position in new[] { "ogre", "development", "ghoul", "power-overwhelming" }) {
            var game = Fixture(position == "ghoul" ? CardClass.DEATHKNIGHT : CardClass.WARRIOR);
            game.Turn = 12;
            if (position == "development") {
                game.Player1.BaseMana = 6;
                Entity.FromCard(game.Player1, Cards.FromId("CS2_200"), zone: game.Player1.HandZone);
            } else {
                game.Player1.UsedMana = 10;
                Board(game, false, "CS2_232"); // Ironbark Protector: 8/8 Taunt.
                if (position != "ghoul") {
                    var body = Board(game, true, position == "ogre" ? "CS2_200" : "CS2_231", true);
                    if (position == "power-overwhelming")
                        new AddEnchantmentTask("EX1_316e", EntityType.TARGET).Process(game, game.Player1, body, body);
                } else { game.Player1.BaseMana = 2; game.Player1.UsedMana = 0; }
            }
            var state = View(game);
            if (position == "ghoul") state = state.Apply(state.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER));
            if (refusal) RefuseEndpoint(state);
            else FairGame(state).Player1[(GameTag)39999] = 1; // Force projection on both branches.
            int failures = 0;
            state.FaultReporter = _ => failures++;
            Func<FairBotState, double> score = candidate => {
                if (!refusal && candidate.Facts.CurrentPlayerId != candidate.Facts.Own.PlayerId)
                    throw new SimulationFailureException(state.Actions.Single(a => a.Type == PlayerTaskType.END_TURN), new Exception("endpoint fault"));
                return candidate.Evaluate(evaluator);
            };
            var beam = new TurnBeam(state, score, 100, 8, false, evaluator);
            Run(beam);
            var leaves = beam.ScoredLeaves.ToArray();
            var hold = leaves.Single(l => l.Actions.Count == 0);
            var action = leaves.Where(l => l.Actions.Count > 0).OrderByDescending(l => l.Value).First();
            if (refusal) {
                foreach (var branch in new[] { state, state.Apply(action.Actions[0]) }) {
                    bool refused = false;
                    try { branch.ForReplyEndTurn(); } catch (Exception e) when (FairHistory.IsPrivate(e)) { refused = true; }
                    Check(refused, position + " must refuse both branches");
                }
            } else Check(failures >= 2, position + " must fault both branches");
            Check(position == "ogre" ? hold.Value > action.Value : action.Value > hold.Value,
                position + " incorrect failed-endpoint ranking: hold=" + hold.Value + " action=" + action.Value);
            if (position == "ogre" || position == "development")
                Check(hold.Value == state.Evaluate(evaluator), "permanent value must survive refusal");
            var expected = position == "development" ? PlayerTaskType.PLAY_CARD : PlayerTaskType.MINION_ATTACK;
            Check(action.Actions[0].Type == expected, position + " wrong candidate action");
            if (refusal) {
                var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 100);
                Run(job);
                Check(job.Result.Line[0].Type == (position == "ogre" ? PlayerTaskType.END_TURN : expected),
                    position + " wrong refused planner decision: " + PlannerTests.Describe(job.Result));
            }
        }
    }

    public static void TestFailureDiscountsOnlyTemporaryAttackAndActiveDeathTriggers()
    {
        var game = Fixture(); game.Turn = 12; game.Player1.UsedMana = 10;
        var body = Board(game, true, "CS2_200", true);
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        var baseline = View(game);
        new AddEnchantmentTask("CS2_045e", EntityType.TARGET).Process(game, game.Player1, body, body);
        var state = View(game);
        Check(Math.Abs(state.EndpointFailureValue(state.Evaluate(evaluator), evaluator) - baseline.Evaluate(evaluator)) < 1e-9,
            "one-turn Attack must lose only buff credit");
        Check(Math.Abs(state.EndpointFailureValue(state.Evaluate(), null) - baseline.Evaluate()) < 1e-9,
            "legacy fallback must preserve body and remove buff");
        Check(state.Facts.Own.Board.Single().Attack == 9, "fallback mutated original attack");
        var ghoul = Board(game, true, "HERO_11bpt", true);
        ghoul.ActivatedTrigger.Remove();
        state = View(game);
        Check(Math.Abs(state.EndpointFailureValue(state.Evaluate(evaluator), evaluator) - (state.Evaluate(evaluator) - evaluator.FriendlyAttackCredit(state.Facts.Own.Board.First(), 3))) < 1e-9,
            "removed death trigger must not forfeit the body");
        // A conditional/custom death listener is unknown, so its current board credit remains.
        new SabberStoneCore.Triggers.Trigger(TriggerType.TURN_END) {
            Condition = SabberStoneCore.Conditions.SelfCondition.IsDamaged,
            SingleTask = new DestroyTask(EntityType.SOURCE)
        }.Activate(ghoul);
        state = View(game);
        Check(Math.Abs(state.EndpointFailureValue(state.Evaluate(evaluator), evaluator) - (state.Evaluate(evaluator) - evaluator.FriendlyAttackCredit(state.Facts.Own.Board.First(), 3))) < 1e-9,
            "conditional expiry must retain uncertain board value");
    }

    public static void TestCostExpiryProbeTracksLiveOverlaysWithoutAllocating()
    {
        var game = Fixture();
        var card = (Playable)Entity.FromCard(game.Player1, Cards.FromId("CS2_200"), zone: game.Player1.HandZone);
        long handle = CostRules.SetCostThisTurn(card, 0);
        Check(CostRules.HasTurnExpiry(game, game.Turn), "new overlay must expire");
        var clone = game.CloneExact();
        CostRules.RemoveApplication(card, handle);
        Check(!CostRules.HasTurnExpiry(game, game.Turn) && CostRules.HasTurnExpiry(clone, clone.Turn), "clone overlay independence");
        CostRules.ExpireTurn(clone, clone.Turn);
        Check(!CostRules.HasTurnExpiry(clone, clone.Turn), "expired overlay must not stay live");
        for (int i = 0; i < 512; i++) Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.SetasideZone);
        var actor = Enchantment.GetInstance(game.Player1, card, card, Cards.FromId("CS2_045e"));
        CostRules.AddModifier(card, EffectOperator.SUB, 1, game.Turn, actor.Id);
        actor.Remove();
        Check(!CostRules.HasTurnExpiry(game, game.Turn), "enchantment removal retained cost application");
        CostRules.SetCostThisTurn(card, 0);
        var copy = Generic.Copy(game.Player1, card, card, Zone.HAND);
        CostRules.ExpireTurn(game, game.Turn);
        Check(card.Cost == 6 && copy.Cost == 6 && !CostRules.HasTurnExpiry(game, game.Turn), "copied overlay expiry");
        var costState = typeof(Game).GetField("CostState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        Check((int)costState.GetType().GetField("OverlayCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(costState) == 0,
            "past applications must leave cheap live-count gate closed");
        CostRules.AddPaymentOverride(card, SabberStoneCore.Model.Costs.CostPaymentKind.Health);
        CostRules.AddModifier(card, EffectOperator.SUB, 1, game.Turn + 2);
        for (int i = 0; i < 100; i++) Check(!CostRules.HasTurnExpiry(game, game.Turn), "future/null expiry");
        int endingTurn = game.Turn;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) CostRules.HasTurnExpiry(game, endingTurn);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "cost expiry probe allocated " + allocated + " bytes");
        Check(CostRules.HasTurnExpiry(game, game.Turn + 2), "future overlay missing");
        CostRules.ExpireTurn(game, game.Turn + 2);
        Check(!CostRules.HasTurnExpiry(game, game.Turn + 2), "null expiry payment must remain permanent");
        CostRules.AddNextCardRule(game.Player1, setCost: 0, expiryTurn: game.Turn);
        Check(CostRules.HasTurnExpiry(game, game.Turn), "controller rule missing");
    }

    public static void TestBatchAManaBoundaryStaysQuiet()
    {
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        foreach (int mana in new[] { 0, 3, 5, 9, 10 }) {
            var game = Fixture(); game.Turn = 12; game.Player1.BaseMana = mana;
            Entity.FromCard(game.Player1, Cards.FromId("CS2_200"), zone: game.Player1.HandZone);
            var state = View(game);
            Check(!state.NeedsEndpointProjection(evaluator), "ordinary capacity must keep quiet fast path");
            Check(state.Evaluate(evaluator) == state.ForReplyEndTurn().Evaluate(evaluator),
                "current-player flip reprices retained mana at " + mana);
            game.Player2.BaseMana = mana;
            state = View(game);
            Check(state.NeedsEndpointProjection(evaluator) == (mana < 10), "opponent capacity growth needs projection below cap");
            double delta = state.ForReplyEndTurn().Evaluate(evaluator) - state.Evaluate(evaluator);
            Check(mana < 10 ? delta < 0 : delta == 0, "opponent refresh must price its public capacity gain");
        }
    }

    public static void TestBatchAThreatSecretsAndOverloadBoundary()
    {
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        foreach (bool windfury in new[] { false, true }) {
            var game = Fixture(); game.Turn = 12; game.Player1.Hero.Damage = 25;
            var enemy = Board(game, false, "CS2_200"); enemy.HasWindfury = windfury;
            enemy.IsExhausted = true; enemy.NumAttacksThisTurn = windfury ? 2 : 1;
            var state = View(game);
            Check(!state.NeedsEndpointProjection(evaluator), "ordinary refreshed threat should stay quiet");
            Check(state.Evaluate(evaluator) == state.ForReplyEndTurn().Evaluate(evaluator), "refresh reprices forecast enemy attacks");
        }
        foreach (string kind in new[] { "owed", "locked", "secret" }) {
            var game = Fixture(CardClass.MAGE); game.Turn = 12;
            if (kind == "owed") game.Player1.OverloadOwed = 2;
            if (kind == "locked") game.Player1.OverloadLocked = 2;
            if (kind == "secret") {
                var secret = Entity.FromCard(game.Player1, Cards.FromId("CORE_EX1_289"), zone: game.Player1.HandZone);
                Check(game.Process(PlayCardTask.Spell(game.Player1, secret)), "arm own Secret");
            }
            var state = View(game);
            Check(evaluator.BoundaryChangesValue(state.Facts) && state.NeedsEndpointProjection(evaluator), kind + " requires conservative boundary projection");
        }
    }

    public static void TestQuietBoundaryAndUnsafeBoundaryScan()
    {
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        var quiet = Fixture(); quiet.Turn = 12; Board(quiet, true, "CS2_182", true); Board(quiet, false, "CS2_121");
        var state = View(quiet);
        Check(!state.NeedsEndpointProjection(evaluator), "ordinary board must use fast path");
        Check(state.Evaluate(evaluator) == state.ForReplyEndTurn().Evaluate(evaluator), "quiet boundary changes score");
        var beam = new TurnBeam(state, s => s.Evaluate(evaluator), 1, 8, false, evaluator);
        beam.StepNode(); Check(beam.Expansions == 1 && beam.EndpointScores == 0, "quiet cap-1 must expand without score reservation");
        foreach (string kind in new[] { "freeze", "overload", "fatigue", "death", "enchant", "cost", "controller", "hand", "start", "end", "unknown", "damage-history" }) {
            var game = quiet.CloneExact();
            if (kind == "freeze") game.Player1.BoardZone[0].IsFrozen = true;
            if (kind == "overload") game.Player2.OverloadOwed = 2;
            if (kind == "fatigue") foreach (var c in game.Player2.DeckZone.ToArray()) game.Player2.DeckZone.Remove(c);
            if (kind == "death") game.Player1.BoardZone[0].ToBeDestroyed = true;
            if (kind == "enchant") new AddEnchantmentTask("EX1_316e", EntityType.TARGET).Process(game, game.Player1, game.Player1.BoardZone[0], game.Player1.BoardZone[0]);
            if (kind == "cost") CostRules.SetCostThisTurn((Playable)Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.HandZone), 0);
            if (kind == "controller") Enchantment.GetInstance(game.Player1, game.Player1.Hero, game.Player1, Cards.FromId("CATA_EVENT_001e"));
            if (kind == "hand") TemporaryRules.Make(Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.HandZone));
            if (kind == "end") Board(game, false, "EX1_298");
            if (kind == "unknown") game.Player1[(GameTag)39999] = 1;
            if (kind == "damage-history") {
                game.Turn = 1; // Keep the fixture's actual turn-history scope aligned for damage facts.
                Entity.FromCard(game.Player1, Cards.FromId("JAIL_421"), zone: game.Player1.HandZone);
                Check(game.Process(game.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
                    && t.Target == game.Player2.BoardZone[0])), "damage-history attack setup");
            }
            var candidate = View(game);
            if (kind == "start") {
                var fair = FairGame(candidate);
                new SabberStoneCore.Triggers.Trigger(TriggerType.TURN_START) { SingleTask = new DamageTask(1, EntityType.OP_HERO) }
                    .Activate(fair.Player2.BoardZone[0]);
            }
            if (kind == "damage-history") Check(candidate.Facts.DistinctFriendlyCharactersDamagedThisTurn > 0,
                "damage-history fixture must retain the real engine fact");
            Check(candidate.NeedsEndpointProjection(evaluator), kind + " boundary was certified quiet");
        }
        Check(state.NeedsEndpointProjection(null), "unknown evaluator must project");
        quiet.Turn = DeckPlans.DragonWarrior.AggressionCurve.First().FromTurn - 1;
        Check(View(quiet).NeedsEndpointProjection(evaluator), "aggression threshold must project");
    }

    public static void TestPendingChoiceAndTerminalSkipEndpointAndProjectedLethalStops()
    {
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        var game = Fixture();
        var discover = Entity.FromCard(game.Player1, Cards.FromId("EDR_856"), zone: game.Player1.HandZone);
        Check(game.Process(game.Player1.Options().First(t => t.Source?.Id == discover.Id)), "Discover setup");
        var choice = View(game); Check(choice.HasPendingChoice && !choice.NeedsEndpointProjection(evaluator), "choice needs no endpoint step");
        var choiceBeam = new TurnBeam(choice, s => s.Evaluate(evaluator), 10, 8, false, evaluator);
        choiceBeam.StepNode(); Check(choiceBeam.Expansions == 1 && choiceBeam.EndpointScores == 0, "choice should expand immediately");
        game = Fixture(); Board(game, true, "EX1_298"); game.Player2.Hero.Health = 8;
        var state = View(game); var terminal = state.ForReplyEndTurn();
        Check(terminal.IsTerminal && !terminal.NeedsEndpointProjection(evaluator), "terminal needs no endpoint step");
        var terminalBeam = new TurnBeam(terminal, s => s.Evaluate(evaluator), 10, 8, false, evaluator);
        terminalBeam.StepNode(); Check(terminalBeam.EndpointScores == 0, "terminal consumed endpoint score");
        var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 2500);
        job.StepNode();
        Check(!job.IsDone, "projected lethal must await replay verification");
        Run(job, 1);
        Check(job.IsDone && job.Result.Value >= 999000 && job.Result.Nodes == 5 && job.Result.Stop == SearchStop.Terminal,
            "verified projected lethal must bypass further beam and reply work");
        Check(job.Result.Line.Single().Type == PlayerTaskType.END_TURN, "projected lethal must remain executable");
        game = Fixture(); Board(game, true, "CS2_182", true); game.Player2.Hero.Health = 1;
        var immediate = new TurnBeam(View(game), s => s.Evaluate(evaluator), 10, 8, false, evaluator);
        Run(immediate, 1);
        Check(immediate.ScoredLeaves.Single().Value >= 999000 && immediate.ScoredLeaves.Single().Holds,
            "immediate lethal must retain its state, without a second Apply during planner completion");
    }

    public static void TestUnknownDrawProjectedWinRemainsUncertain()
    {
        var game = Fixture(CardClass.MAGE); Board(game, true, "EX1_298"); game.Player2.Hero.Health = 8;
        var draw = Entity.FromCard(game.Player1, Cards.FromId("CS2_023"), zone: game.Player1.HandZone);
        var state = FairBotState.Create(game, 1, 71, DeckStyle.Tempo, new FairOptions { RemoveOpponentSecrets = true });
        state = state.Apply(state.Actions.First(a => a.Source == draw.Id));
        Check(!state.CanExpand && state.Actions.Length == 0 && state.ForReplyEndTurn().IsTerminal,
            "fixture: unknown draw blocks actions but Ragnaros wins at boundary");
        var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 100);
        Run(job, 1);
        Check(job.Result.Value < 999000, "unknown boundary must not certify projected win");
    }

    public static void TestGhoulChargeAttacksFace() => GhoulCharge(false);
    public static void TestGhoulChargeAttacksTaunt() => GhoulCharge(true);

    private static void GhoulCharge(bool taunt)
    {
        foreach (int cap in new[] { 100, 1926, 2500, 7000 }) {
            var game = Fixture(CardClass.DEATHKNIGHT);
            game.Player1.BaseMana = 2;
            int target = game.Player2.Hero.Id;
            if (taunt) {
                var blocker = Board(game, false, "CS2_121");
                blocker.AttackDamage = 2; blocker.Health = 3; blocker.HasTaunt = true;
                target = blocker.Id;
            }
            var state = View(game);
            var power = state.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER);
            state = state.Apply(power);
            var ghoul = state.Facts.Own.Board.Single();
            Check(ghoul.CardId == "HERO_11bpt", "Ghoul Charge summons the real ghoul");
            Check(state.Actions.Any(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Source == ghoul.EntityId && a.Target == target),
                "ghoul attack must be legal");
            Check(state.ForReplyEndTurn().Facts.Own.Board.Count == 0, "unspent ghoul really dies at end of turn");
            var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, cap);
            Run(job);
            Console.WriteLine("GHOUL taunt=" + taunt + " cap=" + cap + " " + PlannerTests.Describe(job.Result)
                + " candidates=" + string.Join(";", job.Result.Candidates));
            Check(job.Result.Line[0].Type == PlayerTaskType.MINION_ATTACK && job.Result.Line[0].Source == ghoul.EntityId
                && job.Result.Line[0].Target == target, "ghoul must attack before expiring: " + PlannerTests.Describe(job.Result));

            string expected = null;
            foreach (var variant in new[] { game, Variant(game) }) {
                var p = NpcCatalog.All[0]; var b = p.Budget;
                var profile = new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack, p.Personality,
                    new Budget(b.TurnNodes, cap, b.LethalNodes, b.ReplyK, b.ReplyL, b.ReplyR, b.OwnBeamWidth), p.PlanId);
                var pilot = new GoldenPilot(profile); int index = 0;
                var powerDecision = BotDriver.Decide(variant, 1, DeckStyle.Tempo,
                    s => pilot.DecidePilot(s, new GoldenContext(variant.Turn, index, 71, profile)), ref index, pilot.Options);
                Check(powerDecision.Trace.Kind == "search" && powerDecision.Task.PlayerTaskType == PlayerTaskType.HERO_POWER,
                    "DK should use Ghoul Charge: " + PlannerTests.Trace(powerDecision.Trace));
                Check(variant.Process(powerDecision.Task), "real hero power accepted");
                var attackDecision = BotDriver.Decide(variant, 1, DeckStyle.Tempo,
                    s => pilot.DecidePilot(s, new GoldenContext(variant.Turn, index, 71, profile)), ref index, pilot.Options);
                var attack = attackDecision.Trace.PilotDecision.Action;
                Check(attackDecision.Trace.Kind == "search" && attack.Type == PlayerTaskType.MINION_ATTACK
                    && attack.Target == target, "real driver must spend the ghoul attack: " + PlannerTests.Trace(attackDecision.Trace));
                string trace = PlannerTests.Trace(powerDecision.Trace) + "|" + PlannerTests.Trace(attackDecision.Trace);
                if (expected == null) expected = trace; else Check(expected == trace, "hidden cards/seed changed ghoul decisions");
                Check(variant.Process(attackDecision.Task), "real ghoul attack accepted");
                Check(taunt ? variant.Player2.BoardZone.Single().Health == 2 : variant.Player2.Hero.Health == 29,
                    "real ghoul attack deals one damage");
            }
        }
    }

    public static void TestExpiringEnchantedMinionAttacksButPermanentMinionSurvives()
    {
        foreach (bool temporary in new[] { false, true }) {
            var game = Fixture(); game.Player1.UsedMana = 10;
            var attacker = Board(game, true, "CS2_231", true);
            if (temporary) new AddEnchantmentTask("EX1_316e", EntityType.TARGET).Process(game, game.Player1, attacker, attacker);
            else { attacker.AttackDamage = 5; attacker.Health = 5; }
            var target = Board(game, false, "CS2_121"); target.AttackDamage = 6; target.Health = 7; target.HasTaunt = true;
            var state = View(game);
            Check(state.ForReplyEndTurn().Facts.Own.Board.Count == (temporary ? 0 : 1), "real enchantment expiry");
            var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 100); Run(job);
            Check(job.Result.Line[0].Type == (temporary ? PlayerTaskType.MINION_ATTACK : PlayerTaskType.END_TURN),
                "expiry must distinguish identical stats: " + PlannerTests.Describe(job.Result));
        }
    }

    public static void TestGhoulMayPassWhenAttackTriggersLethalDeathrattle()
    {
        var game = Fixture(CardClass.DEATHKNIGHT); game.Player1.BaseMana = 2; game.Player1.Hero.Health = 2;
        var bomb = Board(game, false, "EX1_097"); bomb.Health = 1;
        var state = View(game); state = state.Apply(state.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER));
        var attack = state.Actions.Single(a => a.Type == PlayerTaskType.MINION_ATTACK);
        Check(state.Apply(attack).Facts.Own.PlayState == PlayState.LOST, "Abomination attack must actually kill the NPC");
        var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 100); Run(job);
        Check(job.Result.Line[0].Type == PlayerTaskType.END_TURN, "do not force a lethal attack merely because ghoul expires");
    }
}

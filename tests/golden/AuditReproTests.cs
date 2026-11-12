using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

// Proof-only regressions. AUDIT=1 selects these instead of the normal green suite.
public static class AuditReproTests
{
    private static readonly DeckPlanEvaluator Warrior = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
    private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private static NpcProfile Profile(Budget budget)
    {
        var p = NpcCatalog.All[0];
        return new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack, p.Personality, budget, p.PlanId);
    }
    private static string F(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    private static void Require(List<string> errors, bool ok, string message) { if (!ok) errors.Add(message); }
    private static void Finish(List<string> errors) => Check(errors.Count == 0, string.Join("; ", errors));
    private static void PassBoth(Game game)
    {
        Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "fixture: first pass");
        Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "fixture: second pass");
    }
    private static IPlayable Hand(Game g, string id) => Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone);
    private static void Equip(Game g, Controller p)
    {
        var weapon = Entity.FromCard(p, Cards.FromId("CS2_106"), zone: p.HandZone);
        Check(g.Process(PlayCardTask.Any(p, weapon)), "fixture: equip Fiery War Axe");
        Check(p.Hero.Weapon.AttackDamage == 3 && p.Hero.Weapon.Durability == 2, "fixture: 3/2 weapon");
    }

    public static void Audit20261031_A1_LifeUtilityIsMonotonic()
    {
        var errors = new List<string>();
        var g = Fixture(); g.Turn = 7; g.Player1.UsedMana = 10; g.Player2.Hero.Damage = 17;
        var body = Board(g, true, "CS2_231", true); body.AttackDamage = 1;
        var root = View(g); var hit = root.Actions.Single(a => a.Source == body.Id && a.Target == g.Player2.Hero.Id);
        double delta = root.Apply(hit).Evaluate(Warrior) - root.Evaluate(Warrior);
        var planner = TurnPlanner.Job(root, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71); Run(planner);
        Require(errors, delta > 0, "13->12 free damage delta=" + F(delta));
        Require(errors, planner.Result.Line.Contains(hit), "planner skips free face attack");
        // All finite life thresholds in every plan linked into this test project, on both sides of each override.
        foreach (var plan in DeckPlans.All) {
            var evaluator = new DeckPlanEvaluator(plan);
            var ownLives = plan.AggressionCurve.Select(p => p.OwnLifeAtMost).Where(n => n > 0 && n < 30).Concat(new[] { 30 }).Distinct().ToArray();
            var enemyLives = plan.AggressionCurve.Select(p => p.OpponentLifeAtMost).Where(n => n > 0 && n < 30).Concat(new[] { 30 }).Distinct().ToArray();
            foreach (int turn in plan.AggressionCurve.Select(p => p.FromTurn).Distinct())
            foreach (int own in ownLives) foreach (int enemy in enemyLives) {
                var low = Fixture(); low.Turn = turn; low.Player1.Hero.Damage = 30 - own; low.Player2.Hero.Damage = 30 - enemy;
                double score = View(low).Evaluate(evaluator);
                if (own < 30) {
                    var healed = low.CloneExact(); healed.Player1.Hero.Damage--;
                    double d = View(healed).Evaluate(evaluator) - score;
                    Require(errors, d > 0, plan.Id + " turn=" + turn + " heal " + own + "->" + (own+1) + " enemy=" + enemy + " delta=" + F(d));
                }
                if (enemy < 30) {
                    var high = low.CloneExact(); high.Player2.Hero.Damage--;
                    double d = score - View(high).Evaluate(evaluator);
                    Require(errors, d > 0, plan.Id + " turn=" + turn + " damage " + (enemy+1) + "->" + enemy + " own=" + own + " delta=" + F(d));
                }
            }
        }
        Finish(errors);
    }

    public static void Audit20261031_A2_FutureManaAndOverloadHaveValue()
    {
        var errors = new List<string>();
        var g = Fixture(CardClass.SHAMAN); g.Player1.BaseMana = 5; g.Player1.UsedMana = 5;
        Hand(g, "CS2_200"); // Undiscounted six-cost Ogre, playable next turn only without debt.
        var debt = g.CloneExact(); debt.Player1.OverloadOwed = 2;
        double before = View(g).Evaluate(Warrior) - View(debt).Evaluate(Warrior);
        PassBoth(g); PassBoth(debt);
        Check(g.Player1.RemainingMana == 6 && debt.Player1.RemainingMana == 4 && debt.Player1.OverloadLocked == 2, "fixture: overload settlement");
        double after = View(g).Evaluate(Warrior) - View(debt).Evaluate(Warrior);
        Require(errors, before > 0, "debt 0/2 value gap=" + F(before));
        Require(errors, after > 0, "settled mana 6/4 value gap=" + F(after));
        var ramp = Fixture(CardClass.DRUID); ramp.Player1.BaseMana = 3;
        var growth = Hand(ramp, "CS2_013"); Hand(ramp, "CS2_200");
        Check(ramp.Process(ramp.Player1.Options().First(t => t.Source == growth)), "fixture: Wild Growth");
        var noRamp = ramp.CloneExact(); noRamp.Player1.BaseMana--;
        Check(ramp.Player1.BaseMana == 4, "fixture: Wild Growth capacity");
        double retained = View(ramp).Evaluate(Warrior) - View(noRamp).Evaluate(Warrior);
        Require(errors, retained > 0, "Wild Growth retained capacity value=" + F(retained));
        // Public capacity/debt must have equal, opposite value for either owner, without a known hand.
        var symmetric = Fixture(); symmetric.Player1.BaseMana = symmetric.Player2.BaseMana = 5;
        double neutral = View(symmetric).Evaluate(Warrior);
        foreach (bool locked in new[] { false, true }) {
            var ownDebt = symmetric.CloneExact(); var enemyDebt = symmetric.CloneExact();
            if (locked) { ownDebt.Player1.OverloadLocked = 2; enemyDebt.Player2.OverloadLocked = 2; }
            else { ownDebt.Player1.OverloadOwed = 2; enemyDebt.Player2.OverloadOwed = 2; }
            double ownCost = neutral - View(ownDebt).Evaluate(Warrior);
            double enemyBenefit = View(enemyDebt).Evaluate(Warrior) - neutral;
            Require(errors, ownCost > 0 && Math.Abs(ownCost - enemyBenefit) < 1e-9,
                "symmetric " + (locked ? "locked" : "owed") + " own=" + F(ownCost) + " enemy=" + F(enemyBenefit));
        }
        var enemyRamp = symmetric.CloneExact(); enemyRamp.Player2.BaseMana++;
        Require(errors, View(enemyRamp).Evaluate(Warrior) < neutral, "opponent capacity has no cost");
        Entity.FromCard(symmetric.Player2, Cards.FromId("CS2_200"), zone: symmetric.Player2.HandZone);
        Require(errors, Math.Abs(View(symmetric).Evaluate(Warrior) - View(Variant(symmetric)).Evaluate(Warrior)) < 1e-9,
            "opponent hidden hand affects resource value");
        Finish(errors);
    }

    public static void Audit20261031_A3_ThreatUsesLegalAttackersAndBlockers()
    {
        var errors = new List<string>();
        foreach (bool dormant in new[] { false, true }) {
            var g = Fixture(); g.Player1.Hero.Damage = 26;
            var enemy = Board(g, false, dormant ? "CS2_200" : "EX1_045", true);
            enemy.AttackDamage = 4;
            if (dormant) enemy[GameTag.DORMANT] = 1;
            var state = View(g);
            var armor = state.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER);
            double gain = state.Apply(armor).Evaluate(Warrior) - state.Evaluate(Warrior);
            var noEnemy = g.CloneExact(); var removed = noEnemy.Player2.BoardZone.Single();
            noEnemy.Player2.BoardZone.Remove(removed); noEnemy.Player2.SetasideZone.Add(removed);
            var control = View(noEnemy); double baseline = control.Apply(control.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER)).Evaluate(Warrior) - control.Evaluate(Warrior);
            var enemyTurn = g.CloneExact(); Check(enemyTurn.Process(EndTurnTask.Any(enemyTurn.Player1)), "fixture: enemy turn");
            Check(!enemyTurn.Player2.Options().Any(a => a.PlayerTaskType == PlayerTaskType.MINION_ATTACK), "fixture: attacker must be unable to attack");
            var develop = Hand(g, "CS2_120"); var choices = View(g);
            double bodyGain = choices.Apply(choices.Actions.First(a => a.Source == develop.Id)).Evaluate(Warrior) - choices.Evaluate(Warrior);
            Require(errors, Math.Abs(gain - baseline) < 1e-9, (dormant ? "dormant" : "Ancient Watcher") + " invented threat=" + F(gain-baseline) + " armor=" + F(gain) + " development=" + F(bodyGain));
        }
        var blocked = Fixture(); blocked.Player1.Hero.Damage = 26;
        Board(blocked, false, "CS2_200", true).AttackDamage = 4;
        var taunt = Board(blocked, true, "CS2_121"); taunt.HasTaunt = true; taunt.HasStealth = true;
        var plain = blocked.CloneExact(); plain.Player1.BoardZone.Single().HasTaunt = false;
        double fakeBlock = View(blocked).Evaluate(Warrior) - View(plain).Evaluate(Warrior);
        var safe = blocked.CloneExact(); safe.Player1.Hero.Damage = 0;
        var safePlain = plain.CloneExact(); safePlain.Player1.Hero.Damage = 0;
        double intrinsic = View(safe).Evaluate(Warrior) - View(safePlain).Evaluate(Warrior);
        Check(blocked.Process(EndTurnTask.Any(blocked.Player1)) && blocked.Player2.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Target == blocked.Player1.Hero), "fixture: stealth taunt does not block face");
        Require(errors, Math.Abs(fakeBlock - intrinsic) < 1e-9, "stealthed Taunt invented protection=" + F(fakeBlock-intrinsic));
        foreach (bool rush in new[] { false, true }) {
            var g = Fixture(); g.Player1.Hero.Damage = 22;
            var blocker = Board(g, true, "CS2_231"); blocker.HasTaunt = true;
            var face = Board(g, false, "CS2_200", true); face.AttackDamage = face.Health = 8;
            Check(g.Process(EndTurnTask.Any(g.Player1)), "fixture: minion-only attacker's turn");
            var clear = Board(g, false, "CS2_231", true);
            if (rush) { clear.IsRush = true; clear.AttackableByRush = true; }
            else clear.CantAttackHeroes = true;
            var root = View(g); var armored = g.CloneExact(); armored.Player1.Hero.Armor = 2;
            var armorView = View(armored);
            var empty = g.CloneExact();
            foreach (var m in empty.Player2.BoardZone.ToArray()) empty.Player2.SetasideZone.Add(empty.Player2.BoardZone.Remove(m));
            double baseScore = View(empty).Evaluate(Warrior); empty.Player1.Hero.Armor = 2;
            double intrinsicArmor = View(empty).Evaluate(Warrior) - baseScore;
            Require(errors, !DeckPlanEvaluator.PublicBoardSurvives(root.Facts), "minion-only clearer masks public lethal: rush=" + rush);
            Require(errors, Math.Abs(armorView.Evaluate(Warrior) - root.Evaluate(Warrior) - intrinsicArmor - 50) < 1e-9,
                "minion-only clearer loses face threat: rush=" + rush);
            var reply = root.Determinize(new[] { "CS2_182" }, 71).ForReply();
            var armoredReply = armorView.Determinize(new[] { "CS2_182" }, 71).ForReply();
            double replyGain = Warrior.ReplyValue(armoredReply.Facts, root.Facts, armoredReply.ForReplyHand(root.Facts))
                - Warrior.ReplyValue(reply.Facts, root.Facts, reply.ForReplyHand(root.Facts));
            Require(errors, Math.Abs(replyGain - intrinsicArmor - 50) < 1e-9, "reversed reply loses minion-only clearer: rush=" + rush);
            // Authoritative legal line: the restricted 1/1 trades, leaving the 8/8 to kill the hero.
            Check(g.Process(g.Player2.Options().Single(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
                && t.Source == clear && t.Target == blocker)), "fixture: minion-only attacker clears Taunt");
            Check(g.Process(g.Player2.Options().Single(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
                && t.Source == face && t.Target == g.Player1.Hero)) && g.Player1.PlayState == PlayState.LOST,
                "fixture: 8/8 deals lethal after restricted attack");
            var restrictedOnly = armored.CloneExact();
            var removed = restrictedOnly.Player2.BoardZone.Single(m => m.Id == face.Id);
            restrictedOnly.Player2.SetasideZone.Add(restrictedOnly.Player2.BoardZone.Remove(removed));
            var ownBlocker = restrictedOnly.Player1.BoardZone.Single();
            restrictedOnly.Player1.SetasideZone.Add(restrictedOnly.Player1.BoardZone.Remove(ownBlocker));
            restrictedOnly.Player1.Hero.Damage = 29; restrictedOnly.Player1.Hero.Armor = 0;
            Require(errors, DeckPlanEvaluator.PublicBoardSurvives(View(restrictedOnly).Facts), "restricted attacker counted as face damage: rush=" + rush);
        }
        Finish(errors);
    }

    public static void Audit20261031_A4_WeaponSwingImprovesPressure()
    {
        var errors = new List<string>(); var g = Fixture(); g.Turn = 7; Equip(g, g.Player1); g.Player1.UsedMana = 10;
        var root = View(g); var hit = root.Actions.Single(a => a.Type == PlayerTaskType.HERO_ATTACK);
        double delta = root.Apply(hit).Evaluate(Warrior) - root.Evaluate(Warrior);
        var planner = TurnPlanner.Job(root, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71); Run(planner);
        Require(errors, delta > 0, "own swing delta=" + F(delta));
        Require(errors, planner.Result.Line.Contains(hit), "planner preserves weapon instead of face swing");
        var human = Fixture(CardClass.WARRIOR, CardClass.WARRIOR); human.Turn = 7;
        Check(human.Process(EndTurnTask.Any(human.Player1)), "fixture: human turn"); Equip(human, human.Player2);
        Check(human.Process(EndTurnTask.Any(human.Player2)), "fixture: bot turn");
        var leaf = View(human); var end = leaf.ForReplyEndTurn();
        var reply = end.Determinize(Enumerable.Repeat("CS2_182", end.Facts.Opponent.Hand.Count(c => c.Unknown)).ToArray(), 71).ForReply();
        var swing = reply.Actions.Single(a => a.Type == PlayerTaskType.HERO_ATTACK); var swung = reply.Apply(swing);
        double humanDelta = Warrior.ReplyValue(swung.Facts, leaf.Facts, swung.ForReplyHand(leaf.Facts)) - Warrior.ReplyValue(reply.Facts, leaf.Facts, reply.ForReplyHand(leaf.Facts));
        Require(errors, humanDelta < 0, "opponent swing improves NPC score=" + F(humanDelta));
        Finish(errors);
    }

    public static void Audit20261031_A5_UntriggeredSecretRetainsValue()
    {
        var g = Fixture(CardClass.MAGE); var barrier = Hand(g, "CORE_EX1_289");
        Check(g.Process(PlayCardTask.Spell(g.Player1, barrier)), "fixture: arm Ice Barrier");
        Check(barrier.ActivatedTrigger != null, "fixture: barrier must be armed");
        var absent = g.CloneExact(); var secret = absent.Player1.SecretZone.Single(); secret.ActivatedTrigger.Remove(); absent.Player1.SecretZone.Remove(secret); absent.Player1.SetasideZone.Add(secret);
        double before = View(g).Evaluate(Warrior) - View(absent).Evaluate(Warrior);
        PassBoth(g); PassBoth(absent);
        Check(g.Player1.SecretZone.Single().Card.Id == "CORE_EX1_289", "fixture: no hero attack, secret survives");
        double after = View(g).Evaluate(Warrior) - View(absent).Evaluate(Warrior);
        Check(before > 0 && after > 0, "armed Ice Barrier retained value=" + F(before) + ", after pass=" + F(after));
    }

    public static void Audit20261031_B1_ClockCutRejectsStaleRandomTrade()
    {
        var g = Fixture(CardClass.MAGE); g.Player1.BaseMana = 1;
        var attacker = Board(g, true, "CS2_120", true); attacker.AttackDamage = 2; attacker.Health = 2;
        var target = Board(g, false, "CS2_182"); target.AttackDamage = 3; target.Health = 4;
        var missiles = Hand(g, "EX1_277");
        FairBotState expected = null, actual = null; BotAction cast = null;
        for (int seed = 1; seed <= 100 && (expected == null || actual == null); seed++) {
            var state = LethalTests.View(g, seed); var action = state.Actions.Single(a => a.Source == missiles.Id);
            var child = state.Apply(action); int hp = child.Facts.Opponent.Board.Single().Health;
            if (hp == 2) { expected = state; cast = action; }
            if (hp == 4) actual = child;
        }
        Check(expected != null && actual != null, "fixture: need two real missile outcomes");
        var predicted = expected.Apply(cast);
        var trade = predicted.Actions.Single(a => a.Source == attacker.Id && a.Target == target.Id);
        Check(actual.Actions.Contains(trade) && predicted.Apply(trade).Facts.Opponent.Board.Count == 0 && actual.Apply(trade).Facts.Opponent.Board.Count == 1, "fixture: same legal trade changes from kill to sacrifice");
        var pilot = new GoldenPilot(NpcCatalog.All[0]); var ctx = new GoldenContext(g.Turn, 0, 71, pilot.Profile);
        pilot.Job(expected, ctx); pilot.Accept(expected, new[] { cast, trade, predicted.Actions.Single(a => a.Type == PlayerTaskType.END_TURN) }, 0, LethalOutcome.None);
        Check(!pilot.CanReuse(actual), "fixture: normal reuse must reject mismatch");
        var job = pilot.Job(actual, ctx); while (job.Preparing) job.StepNode();
        var prepared = Field<BoundedFallbackJob>(job, "minimum").Result;
        var answer = job.StopSearch(SearchStop.Clock);
        Check(answer.Action.Equals(prepared), "clock mismatch must use already prepared answer");
        Check(!pilot.PlanReused && !answer.Action.Equals(trade), "clock reused stale sacrifice=" + answer.Action + " reused=" + pilot.PlanReused);
    }

    public static void Audit20261031_B2_PilotRejectsLuckyRandomCertainty()
    {
        for (int seed = 1; seed <= 100; seed++) {
            var g = Fixture(CardClass.MAGE); g.Player1.BaseMana = 3; g.Player1.Hero.Damage = 24; g.Player2.Hero.Damage = 27;
            Board(g, false, "CS2_200"); var missiles = Hand(g, "EX1_277"); var nova = Hand(g, "CS2_026");
            var state = LethalTests.View(g, seed); var cast = state.Actions.Single(a => a.Source == missiles.Id);
            if (state.Apply(cast).Facts.Own.PlayState != PlayState.WON) continue;
            var verified = LethalSolver.Find(state, 2000);
            if (verified.Outcome != LethalOutcome.None) continue;
            int replayLosses = Enumerable.Range(1,4).Count(s => state.WithArtificialSeed(s).Apply(cast).Facts.Own.PlayState != PlayState.WON);
            Check(replayLosses > 0 && state.Actions.Any(a => a.Source == nova.Id), "fixture: unverified missiles and available defense");
            var pilot = new GoldenPilot(NpcCatalog.All[0]); var answer = pilot.Decide(state, new GoldenContext(g.Turn, 0, seed, pilot.Profile));
            Check(answer.Candidates.Any(c => c.Key.Source == nova.Id), "failed verification erased defensive finalist");
            foreach (int cap in Enumerable.Range(1, 12)) {
                var limited = TurnPlanner.Job(state, pilot.Profile, pilot.Plan, seed, cap);
                Run(limited, 1);
                Check(limited.Result.Nodes <= cap && limited.Result.Value < 999000
                    && limited.Result.Candidates.All(c => c.Value < 999000), "node cut published unverified win at cap " + cap);
            }
            var cut = TurnPlanner.Job(state, pilot.Profile, pilot.Plan, seed);
            bool interruptedVerification = false;
            for (int i = 0; i < 6000 && !cut.IsDone; i++) {
                cut.StepNode();
                if (Field<LethalJob>(Field<TurnBeam>(cut, "beam"), "verification") == null) continue;
                interruptedVerification = true;
                int steps = Field<int>(cut, "steps"); var stopped = cut.StopSearch();
                Check(stopped.Value < 999000 && stopped.Nodes == steps, "clock cut certified pending winner or ran more search");
                break;
            }
            Check(interruptedVerification, "fixture: clock must interrupt planner lethal verification");
            Check(answer.Value < 999000, "seed=" + seed + " lethalSolver=" + verified.Outcome + " pilot=" + F(answer.Value) + " replay misses=" + replayLosses + "/4 action=" + answer.Action);
            return;
        }
        throw new Exception("fixture: no lucky missiles seed rejected by verification in 1..100");
    }

    public static void Audit20261031_B3_CappedPrefixKeepsAttacks()
    {
        var g = Fixture(); g.Player1.UsedMana = 10;
        for (int i = 0; i < 2; i++) Board(g, true, "CS2_200", true);
        for (int i = 0; i < 7; i++) Board(g, false, "CS2_231");
        var profile = Profile(new Budget(1000, 40, 100, 1, 1, 1, 8)); var state = View(g);
        var planner = TurnPlanner.Job(state, profile, DeckPlans.DragonWarrior, 71, 40); Run(planner);
        Check(Field<TurnBeam>(planner, "beam").Capped, "fixture: own beam must cap");
        var prefix = state; bool premature = false;
        foreach (var a in planner.Result.Line) {
            if (a.Type == PlayerTaskType.END_TURN) {
                premature = prefix.Actions.Any(x => x.Type == PlayerTaskType.MINION_ATTACK && prefix.Apply(x).Evaluate(Warrior) > prefix.Evaluate(Warrior)); break;
            }
            prefix = prefix.Apply(a);
        }
        var pilot = new GoldenPilot(profile); var first = pilot.Decide(state, new GoldenContext(g.Turn, 0, 71, profile));
        var child = state.Apply(first.Action); var second = pilot.Decide(child, new GoldenContext(g.Turn, 1, 71, profile));
        Check(!premature && second.Action.Type != PlayerTaskType.END_TURN,
            "capped line=" + string.Join(";", planner.Result.Line) + " profitable attack remains=" + premature + " pilot continuation=" + second.Action.Type + " reused=" + pilot.PlanReused);
    }

    public static void Audit20261031_B4_ClockCutKeepsDefensiveAlternative()
    {
        var g = Fixture(); g.Player1.BaseMana = 4; g.Player1.Hero.Damage = 24;
        var enemy = Board(g, false, "CS2_200"); enemy.AttackDamage = 4; enemy.HasWindfury = true;
        var large = (Minion)Hand(g, "CS2_200"); large.Cost = 4; large.AttackDamage = 60; large.Health = 60; // Outrank the batch-A public Windfury penalty.
        var taunt = Hand(g, "CS2_179"); // Sen'jin, 3/5 Taunt, survives first Windfury swing.
        var profile = Profile(new Budget(10000, 6000, 100, 1, 3, 300, 8)); var state = View(g);
        var job = TurnPlanner.Job(state, profile, DeckPlans.DragonWarrior, 71);
        var scored = Field<List<TurnLine>>(job, "scored");
        for (int i = 0; i < 6000 && !job.IsDone && scored.Count == 0; i++) job.StepNode();
        Check(scored.Count == 1 && scored[0].Survival == 0 && scored[0].Actions.Any(a => a.Source == large.Id), "fixture: first completed finalist must be losing large body; scored=" + string.Join(";", scored.Select(x => x.Key + "/" + x.Survival)));
        var leaves = Field<List<TurnLine>>(job, "leaves");
        Check(leaves.Any(l => l.Actions.Any(a => a.Source == taunt.Id)), "fixture: defensive finalist must still be queued");
        var defense = state.Apply(state.Actions.First(a => a.Source == taunt.Id));
        var defenseReply = ReplyModel.Job(defense, profile, 71, Warrior); Run(defenseReply);
        Check(defenseReply.Survival != 0 && defenseReply.Value > -999000, "fixture: Taunt actually survives same sampled reply");
        int beforeStop = Field<int>(job, "steps");
        var result = job.StopSearch();
        Check(result.Nodes == beforeStop, "defensive clock fallback must not run extra search");
        Check(result.Line.First().Source == taunt.Id, "first completed losing finalist selected; value=" + F(result.Value) + " selected=" + result.Line.First() + " defensive reply=" + F(defenseReply.Value));
    }

    public static void Audit20261031_B5_C5_ReadyNeighborsKeepCenterPlacement()
    {
        var errors = new List<string>();
        foreach (bool five in new[] { true, false }) {
            var g = Fixture(CardClass.SHAMAN); g.Player1.BaseMana = 2;
            int count = five ? 5 : 4, center = five ? 3 : 2;
            for (int i = 0; i < count; i++) {
                var m = Board(g, true, "CS2_231", i == center-1 || i == center); m.AttackDamage = 1; m.Health = 1;
                if (i == 0 || !five && i == count-1) { m.AttackDamage = five ? 10 : 8; m.Health = m.AttackDamage; }
            }
            var totem = Hand(g, "EX1_565");
            g.Player1.BaseMana = totem.Cost; // Frozen EX1_565 costs 3; the audit's two-mana fixture cannot play it.
            var state = View(g);
            var legal = state.Actions.Where(a => a.Source == totem.Id).ToArray();
            Check(legal.Length == count + 1, "fixture: all legal placement slots available");
            var placements = (IEnumerable<BotAction>)typeof(TurnBeam).GetMethod("Placements", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { state.Facts, legal });
            var kept = placements.Select(a => a.Position).ToArray();
            var optimal = state.Apply(legal.Single(a => a.Position == center));
            int damage = 0;
            foreach (int id in g.Player1.BoardZone.Where(m => !m.IsExhausted).Select(m => m.Id)) {
                var hit = optimal.Actions.Single(a => a.Source == id && a.Target == g.Player2.Hero.Id);
                int before = optimal.Facts.Opponent.Hero.Health; optimal = optimal.Apply(hit); damage += before - optimal.Facts.Opponent.Hero.Health;
            }
            Check(damage == 6, "fixture: center must permit two buffed attacks");
            var job = TurnPlanner.Job(state, NpcCatalog.All[2], DeckPlans.ZeeShaman, 71); Run(job);
            var chosen = job.Result.Line.FirstOrDefault(a => a.Source == totem.Id);
            Require(errors, kept.Contains(center), (five ? "B5" : "C5") + " center=" + center + " pruned; kept=" + string.Join(",", kept));
            var planned = state;
            foreach (var action in job.Result.Line.TakeWhile(a => a.Type != PlayerTaskType.END_TURN)) planned = planned.Apply(action);
            int plannedDamage = 30 - planned.Facts.Opponent.Hero.Health;
            Require(errors, chosen != null && chosen.Position == center && plannedDamage == 6,
                "planner placement=" + (chosen == null ? "none" : chosen.Position.ToString()) + " face damage=" + plannedDamage + "/6");
        }
        Finish(errors);
    }

    public static void Audit20261031_C2_SecretLethalSurvivesAuthority()
    {
        var g = Fixture(CardClass.MAGE, CardClass.MAGE);
        Check(g.Process(EndTurnTask.Any(g.Player1)), "fixture: enemy turn");
        var counter = Entity.FromCard(g.Player2, Cards.FromId("CORE_EX1_287"), zone: g.Player2.DeckZone);
        SabberStoneCore.Actions.Generic.Draw(g.Player2, counter);
        Check(g.Process(PlayCardTask.Spell(g.Player2, counter)) && counter.ActivatedTrigger != null, "fixture: armed Counterspell");
        Check(g.Process(EndTurnTask.Any(g.Player2)), "fixture: bot turn");
        foreach (var c in g.Player1.HandZone.ToArray()) { g.Player1.HandZone.Remove(c); g.Player1.SetasideZone.Add(c); }
        g.Player2.Hero.Damage = 24; Hand(g, "CS2_029"); Hand(g, "GAME_005");
        var pilot = new GoldenPilot(NpcCatalog.All[0]); var state = pilot.Options.FairViewFactory(g, 1, 71, DeckStyle.Tempo);
        Check(state.Facts.OpponentSecretCount == 1, "fixture: public secret count");
        var lethal = LethalSolver.Find(state, 2000); Check(lethal.Outcome == LethalOutcome.Found, "fixture: solver should find probe-then-Fireball lethal");
        var replay = g.CloneExact();
        foreach (var a in lethal.Line) Check(replay.Process(a.Resolve(replay.Player1.Options())), "fixture: authoritative replay legal");
        var probe = g.CloneExact();
        Check(probe.Process(probe.Player1.Options().First(t => t.Source?.Card.Id == "GAME_005")), "fixture: Coin probe");
        Check(probe.Process(probe.Player1.Options().First(t => t.Source?.Card.Id == "CS2_029" && t.Target == probe.Player2.Hero))
            && probe.Player1.PlayState == PlayState.WON, "fixture: probe-then-Fireball must really win");
        var decision = pilot.Decide(state, new GoldenContext(g.Turn, 0, 71, pilot.Profile));
        Check(replay.Player1.PlayState == PlayState.WON, "certified line=" + string.Join(";", lethal.Line) + " authority enemy HP=" + replay.Player2.Hero.Health + " pilot=" + F(decision.Value));
    }

    public static void Audit20261031_C3_PublicHandKnowledgeSurvivesRebuild()
    {
        var errors = new List<string>(); var g = Fixture(); var mukla = Hand(g, "EX1_014");
        var root = View(g); var branch = root.Apply(root.Actions.First(a => a.Source == mukla.Id));
        Check(branch.Facts.Opponent.Hand.Count(c => c.CardId == "EX1_014t") == 2, "fixture: search branch knows two Bananas");
        Check(g.Process(g.Player1.Options().First(a => a.Source == mukla)), "fixture: authoritative Mukla");
        var pilot = new GoldenPilot(NpcCatalog.All[0]); var rebuilt = pilot.Options.FairViewFactory(g, 1, 71, DeckStyle.Tempo);
        int bananas = rebuilt.Facts.Opponent.Hand.Count(c => c.CardId == "EX1_014t");
        Require(errors, bananas == 2, "live rebuild knows Bananas=" + bananas + "/2");
        // Start with an original deck legendary so the live public-play ledger sees its provenance.
        var bounce = new Game(new GameConfig {
            Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.ROGUE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = new[] { Cards.FromId("EX1_116") }.Concat(Enumerable.Repeat(Cards.FromId("CS2_182"), 29)).ToList(),
            StartPlayer = 1, Shuffle = false, SkipMulligan = true, History = true, Logging = false, RandomSeed = 7 });
        bounce.StartGame(); bounce.Player2.BaseMana = 10;
        var leeroy = bounce.Player2.HandZone.Single(c => c.Card.Id == "EX1_116");
        Check(bounce.Process(EndTurnTask.Any(bounce.Player1)), "fixture: rogue turn");
        Check(bounce.Process(bounce.Player2.Options().First(t => t.Source == leeroy)), "fixture: play Leeroy");
        var step = Entity.FromCard(bounce.Player2, Cards.FromId("EX1_144"), zone: bounce.Player2.HandZone);
        Check(bounce.Process(bounce.Player2.Options().First(t => t.Source == step && t.Target == leeroy)), "fixture: Shadowstep Leeroy");
        Check(bounce.Process(EndTurnTask.Any(bounce.Player2)), "fixture: return bot turn");
        var bounced = pilot.Options.FairViewFactory(bounce, 1, 71, DeckStyle.Tempo);
        Check(bounce.Player2.HandZone.Any(c => c.Card.Id == "EX1_116" && c.Cost == 3), "fixture: returned discounted Leeroy");
        Require(errors, bounced.Facts.Opponent.Hand.Any(c => c.CardId == "EX1_116" && c.Cost == 3), "returned Leeroy identity/discount masked");
        Check(bounce.HistoryFacts.Query.Plays().Any(f => f.DbfId == Cards.FromId("EX1_116").AssetId && f.OriginKnown && f.StartedInDeck),
            "fixture: authoritative Leeroy must have original-deck provenance");
        // A singleton prior distinguishes true exclusion from merely missing a low-probability card in four samples.
        // Once the returned physical copy is known, it must not also occupy a sampled unknown slot.
        bool knownLeeroy = bounced.Facts.Opponent.Hand.Any(c => c.CardId == "EX1_116");
        var samples = CardFrequencies.Sample(new[] { new CardFrequency("EX1_116", 1) }, knownLeeroy ? 0 : 1, 1, 10, 71, pilot.PublicInputs.OpponentPlayed);
        bool possible = samples.Any(s => s.Cards.Contains("EX1_116"));
        Console.WriteLine("C3 bounce: live played-prior includes Leeroy=" + pilot.PublicInputs.OpponentPlayed.Contains("EX1_116")
            + "; singleton prior can sample returned Leeroy=" + possible);
        Require(errors, bounced.Facts.Opponent.Hand.Any(c => c.CardId == "EX1_116") || possible,
            "returned legendary neither known nor possible in reply prior");
        Finish(errors);
    }
}

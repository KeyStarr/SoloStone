using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class SurvivalScoringTests
{
    public static void TestPassLeaderRetainsBestStaticContinuation()
    {
        var root = View(Fixture()); var action = root.Actions.First(a => a.Type != PlayerTaskType.END_TURN);
        TurnLine Line(double margin) => new TurnLine { PublicAttackMargin = margin,
            PublicAttackDamage = 15, Actions = new System.Collections.Generic.List<BotAction> { action } };
        var pass = new TurnLine { PublicAttackMargin = 5, PublicAttackDamage = 15 };
        var continuation = Line(6); var spendA = Line(4); var spendB = Line(3); var defense = Line(12);
        var sorted = new[] { pass, continuation, spendA, spendB, defense }.ToList();
        foreach (bool enabled in new[] { false, true }) {
            var leaves = new[] { pass, spendA, spendB }.ToList();
            var result = TurnPlannerJob.ReserveFinalists(leaves, sorted, pass, 3, true, enabled);
            Check(leaves.SequenceEqual(enabled ? new[] { pass, continuation, defense } : new[] { pass, spendA, spendB }),
                "pass-leading hand diversity must retain best continuation only with survival-scoring ON");
            Check(!enabled || result.Evicted == spendA, "defense cannot evict mandatory best continuation");
        }
    }
    public static void TestPassAndLeaderPrecedeDefenseReservation()
    {
        var root = View(Fixture()); var action = root.Actions.First(a => a.Type != PlayerTaskType.END_TURN);
        TurnLine Line(double margin, double damage) => new TurnLine { PublicAttackMargin = margin,
            PublicAttackDamage = damage, Actions = new System.Collections.Generic.List<BotAction> { action } };
        var leader = Line(5, 15); var continuation = Line(6, 15); var hold = Line(4, 15);
        var defense = Line(12, 8); var pass = new TurnLine { PublicAttackMargin = 3, PublicAttackDamage = 15 };
        var sorted = new[] { leader, continuation, hold, defense, pass }.ToList();
        var leaves = new[] { leader, continuation, hold }.ToList();
        var reserved = TurnPlannerJob.ReserveFinalists(leaves, sorted, hold, 3, true, true);
        Check(leaves.SequenceEqual(new[] { pass, leader, defense }), "distinct pass and hold: pass, leader and reserved defense survive");
        Check(reserved.Defense == defense && reserved.Evicted == continuation, "reserve only after mandatory pass/leader slots");
        foreach (int limit in new[] { 0, 1, 2 }) {
            leaves = new[] { leader, continuation }.Take(limit).ToList();
            reserved = TurnPlannerJob.ReserveFinalists(leaves, sorted, hold, limit, false, true);
            Check(reserved.Defense == null, "no reservation at reply limit " + limit);
        }
    }

    public static void TestCappedRepliesSkipDefenseMargin()
    {
        var game = Fixture(); Board(game, false, "CS2_231").AttackDamage = 20;
        foreach (bool comparePass in new[] { false, true }) {
            var job = new TurnPlannerJob(View(game), NpcCatalog.All[0], DeckPlans.DragonWarrior,
                71, 100, null, comparePass: comparePass);
            Run(job);
            Check(job.DiagnosticReplyLimit <= 2, "fixture is capped below reservation allowance");
            Check(job.DiagnosticBeam.All(n => n.PublicAttackMargin == 0 && n.PublicAttackDamage == 0),
                "capped replies must skip exact margin arithmetic");
        }
    }

    public static void TestPublicBurnSpellPowerAndChargeCoverLowBoardPressure()
    {
        foreach (int seat in new[] { 1, 2 }) {
            var game = Fixture();
            if (seat == 2) Check(game.Process(EndTurnTask.Any(game.Player1)), "second-seat setup");
            var own = game.CurrentPlayer; var enemy = own.Opponent;
            own.Hero.Damage = 18;
            enemy.Hero.HeroPower[GameTag.COST] = 20;
            foreach (var c in enemy.DeckZone.ToArray()) { enemy.DeckZone.Remove(c); enemy.SetasideZone.Add(c); }
            var attacker = (Minion)Entity.FromCard(enemy, Cards.FromId("CS2_231"), zone: enemy.BoardZone);
            attacker.AttackDamage = 4;
            var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
            // Synthetic public hand: FairFacts consumes already-redacted views in production.
            FairFacts Facts() => new FairFacts(game, own, 0, new System.Collections.Generic.Dictionary<string, int>());
            foreach (string id in new[] { "EX1_308", "EX1_308" }) Entity.FromCard(enemy, Cards.FromId(id), zone: enemy.HandZone);
            Check(evaluator.PublicIncomingDamage(Facts()) == 12, "4 board + 8 public burn at 12 life: seat=" + seat);
            foreach (var c in enemy.HandZone.ToArray()) { enemy.HandZone.Remove(c); enemy.SetasideZone.Add(c); }
            Entity.FromCard(enemy, Cards.FromId("CS2_029"), zone: enemy.HandZone);
            var mage = (Minion)Entity.FromCard(enemy, Cards.FromId("CS2_052"), zone: enemy.BoardZone);
            mage.AttackDamage = 0; mage[GameTag.SPELLPOWER] = 2;
            Check(evaluator.PublicIncomingDamage(Facts()) == 12, "public spell power amplifies burn");
            foreach (var c in enemy.HandZone.ToArray()) { enemy.HandZone.Remove(c); enemy.SetasideZone.Add(c); }
            Entity.FromCard(enemy, Cards.FromId("EX1_116"), zone: enemy.HandZone);
            Check(evaluator.PublicIncomingDamage(Facts()) == 10, "public playable Charge contributes face allowance");
            enemy.BaseMana = 0; enemy.OverloadOwed = 1;
            Check(evaluator.PublicIncomingDamage(Facts()) == 4, "unplayable Charge contributes no allowance");
        }
        var hidden = Fixture(); Entity.FromCard(hidden.Player2, Cards.FromId("EX1_414"), zone: hidden.Player2.HandZone);
        var a = new DeckPlanEvaluator(DeckPlans.DragonWarrior).PublicIncomingDamage(View(hidden).Facts);
        var b = new DeckPlanEvaluator(DeckPlans.DragonWarrior).PublicIncomingDamage(View(Variant(hidden)).Facts);
        Check(a == b && a > 0, "class prior allowance independent of hidden cards and draw order: " + a + "/" + b);
    }

    public static void TestQuietBoardAndMinorPressureKeepTempoFinalist()
    {
        foreach (double damage in new[] { 0.0, 9.0, 10.0 }) {
            var leader = new TurnLine { PublicAttackMargin = 20 - damage, PublicAttackDamage = damage };
            var tempo = new TurnLine { PublicAttackMargin = 20 - damage, PublicAttackDamage = damage };
            var hold = new TurnLine { PublicAttackMargin = 20 - damage, PublicAttackDamage = damage };
            var defense = new TurnLine { PublicAttackMargin = 30 - damage, PublicAttackDamage = damage };
            var sorted = new[] { leader, tempo, hold, defense }.ToList();
            var leaves = sorted.Take(3).ToList();
            var result = TurnPlannerJob.ReserveFinalists(leaves, sorted, hold, 3, false, true);
            Check((result.Defense != null) == (damage >= 10), "half-life threshold inclusive; visible damage=" + damage);
            Check(leaves.Contains(leader) && leaves.Contains(hold), "ordinary leader and hold stay protected");
        }
    }

    // Mined 299:71: two equivalent draw/removal bodies crowd the healing removal out of reply search.
    public static void TestHealingRemovalGetsReplyCoverage()
    {
      foreach (int seat in new[] { 1, 2 }) {
        var game = seat == 1 ? Fixture(CardClass.MAGE, CardClass.DEATHKNIGHT) : Fixture(CardClass.DEATHKNIGHT, CardClass.MAGE);
        if (seat == 2) Check(game.Process(EndTurnTask.Any(game.Player1)), "second-seat setup");
        var own = game.CurrentPlayer; var enemy = own.Opponent;
        foreach (var card in own.HandZone.ToArray()) { own.HandZone.Remove(card); own.SetasideZone.Add(card); }
        game.Turn = seat == 1 ? 15 : 16; own.BaseMana = 8; own.UsedMana = 0; own.Hero.Damage = 21;
        var shade = (Minion)Entity.FromCard(enemy, Cards.FromId("TIME_610t2"), zone: enemy.BoardZone); shade.AttackDamage = 3; shade[GameTag.HEALTH] = 1;
        var pyro = (Minion)Entity.FromCard(enemy, Cards.FromId("NEW1_020"), zone: enemy.BoardZone); pyro.AttackDamage = 6;
        var doctor = (Minion)Entity.FromCard(enemy, Cards.FromId("JAIL_446"), zone: enemy.BoardZone); doctor.AttackDamage = 4; doctor[GameTag.HEALTH] = 5;
        foreach (string id in new[] { "TIME_858", "TIME_858", "END_025", "CATA_483" })
            Entity.FromCard(own, Cards.FromId(id), zone: own.HandZone);
        foreach (bool enabled in new[] { false, true }) {
            var root = FairBotState.Create(game, seat, 71, DeckStyle.Tempo,
                new FairOptions { RemoveOpponentSecrets = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true });
            // attack-allocation's extra combat reply also covers this removal line; isolate survival-scoring.
            var profile = NpcCatalog.All[0].WithFixes(new AiFixes(enabled ? new[] { "attack-allocation" } : new[] { "survival-scoring", "attack-allocation" }));
            var job = TurnPlanner.Job(root, profile, DeckPlans.DragonWarrior, 71);
            Run(job);
            bool covered = job.DiagnosticReplies.Any(n => n.Actions.Any(a => root.Facts.Own.Hand.Any(c => c.EntityId == a.Source && c.CardId == "END_025")));
            Check(covered == enabled, "healing removal reply coverage: seat=" + seat + " enabled=" + enabled + " covered=" + covered);
            if (enabled) Check(job.Result.Line.Any(a => root.Facts.Own.Hand.Any(c => c.EntityId == a.Source && c.CardId == "END_025")),
                "covered healing removal must beat the slower draw line");
        }
      }
    }

    // Mined 491:32: the lethal screen intentionally returns raw attack below life, masking Taunt protection.
    public static void TestDefenseMarginUsesBlockersBelowLethal()
    {
        var game = Fixture(); game.Player1.Hero.Damage = 6;
        foreach (int attack in new[] { 1, 3, 9, 3 }) Board(game, false, "CS2_231").AttackDamage = attack;
        foreach (int ignored in new[] { 0, 1 }) {
            var taunt = Board(game, true, "CS2_179"); taunt.AttackDamage = 0; taunt[GameTag.HEALTH] = 6;
        }
        var state = View(game); var facts = state.Facts;
        Check(DeckPlanEvaluator.PublicThreatBound(facts) == 16, "fixture: below-life screen retains raw attack");
        Check(DeckPlanEvaluator.PublicFaceDamage(facts.Own, facts.Opponent, facts.CurrentPlayerId) == 1,
            "defensive diversity must see both Taunts even below lethal");
        var beam = new TurnBeam(state, s => s.Evaluate(new DeckPlanEvaluator(DeckPlans.DragonWarrior)), 1, 8, false,
            defensiveDiversity: true);
        while (!beam.IsDone) beam.StepNode();
        Check(beam.Leaves.Any(n => n.Actions.Count == 0 && n.PublicAttackMargin == 23), "root margin must be 24 minus 1");
    }
}

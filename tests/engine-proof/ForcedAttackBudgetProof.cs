using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static SilenceKeywordsProof;

internal static class ForcedAttackBudgetProof
{
    internal static void Run()
    {
        var failures = new List<string>();
        void Case(string label, Action test)
        {
            try { test(); Console.WriteLine("PASS forced budget " + label); }
            catch (Exception e) { failures.Add(label + ": " + e.Message); }
        }
        foreach (bool history in new[] { false, true })
        {
            foreach (bool rush in new[] { false, true })
                Case($"unused {(rush ? "Rush" : "Charge")} history={history}", () => Budget(history, rush, false, 0));
            Case($"Windfury after one history={history}", () => Budget(history, false, true, 1));
            Case($"spent history={history}", () => Budget(history, false, false, 1));
            Case($"optimized player attacks history={history}", () => OptimizedPlayerAttacks(history));
            Case($"nested forced combat history={history}", () => NestedCombat(history));
            Case($"registered attack triggers history={history}", () => CardTriggers(history));
            Case($"combat effects history={history}", () => CombatEffects(history));
            foreach (bool forced in new[] { false, true })
                Case($"Swarmer forced={forced} history={history}", () => Swarmer(history, forced));
            Case($"attack fact conditions history={history}", () => AttackFactConditions(history));
            foreach (bool deferDeaths in new[] { false, true })
                Case($"direct hero deferDeaths={deferDeaths} history={history}", () => Hero(history, deferDeaths));
        }
        if (failures.Count != 0) throw new Exception(string.Join("\n", failures));
    }

    // TRL_119 is a registered effect: its forced combat must not spend or refresh normal attacks.
    private static void Budget(bool history, bool rush, bool windfury, int used)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        var attacker = Summon(p, "CS2_171"); // Stonetusk Boar: printed Charge and Beast.
        attacker.BaseHealth = 30;
        if (rush) { attacker.HasCharge = false; attacker.IsRush = true; attacker.IsExhausted = false; }
        attacker.HasWindfury = windfury;
        var target = Summon(p.Opponent, "CS2_182"); target.BaseHealth = 40;
        for (int i = 0; i < used; i++)
            Core2026Proof.Check(g.Process(MinionAttackTask.Any(p, attacker, target)), "initial normal attack accepted");
        bool exhausted = attacker.IsExhausted;
        int cursor = history ? g.PowerHistory.Full.Count : 0;
        Play(g, p, "TRL_119", attacker);
        Core2026Proof.Check(target.Damage == used + 2 && attacker.Damage == (used + 1) * 4, "forced combat deals damage and retaliation");
        Core2026Proof.Check(attacker.NumAttacksThisTurn == used && attacker.IsExhausted == exhausted,
            $"normal budget preserved: used={attacker.NumAttacksThisTurn}, exhausted={attacker.IsExhausted}, expected={used}/{exhausted}");
        Core2026Proof.Check(!history || !g.PowerHistory.Full.Skip(cursor).OfType<PowerHistoryTagChange>().Any(t => t.EntityId == attacker.Id
            && (t.Tag == GameTag.NUM_ATTACKS_THIS_TURN || t.Tag == GameTag.EXHAUSTED)), "no false readiness tags during forced attack");
        bool available = windfury || used == 0;
        Core2026Proof.Check(CanAttack(attacker, target) == available, "normal attack option matches remaining allowance");
        if (available)
        {
            Core2026Proof.Check(g.Process(MinionAttackTask.Any(p, attacker, target)), "remaining normal attack accepted without resetting readiness");
            Core2026Proof.Check(attacker.NumAttacksThisTurn == used + 1 && attacker.IsExhausted && !CanAttack(attacker, target),
                "exactly one remaining normal attack consumed");
        }
        else Core2026Proof.Check(!g.Process(MinionAttackTask.Any(p, attacker, target)), "forced attack cannot refresh spent minion");
    }

    private static void CombatEffects(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        var attacker = Summon(p, "CS2_033"); attacker.BaseHealth = 30;
        attacker.IsExhausted = false;
        attacker.HasLifeSteal = true; attacker.Poisonous = true; attacker.IsFrozen = true;
        var target = Summon(p.Opponent, "CS2_200"); // Water Elemental has printed Freeze; Ogre survives three damage without Poisonous.
        p.Hero.Damage = 10;
        int starts = 0, after = 0, ownAfter = 0;
        g.TriggerManager.AttackTrigger += entity => { if (entity.Id == attacker.Id) starts++; };
        g.TriggerManager.AfterAttackTrigger += entity => { if (entity.Id == attacker.Id) after++; };
        attacker.AfterAttackTrigger += _ => ownAfter++;
        new AttackTask(EntityType.SOURCE, EntityType.TARGET).Process(g, p, attacker, target);
        Core2026Proof.Check(target.Damage == 3 && target.IsFrozen && target.Zone == p.Opponent.GraveyardZone,
            "forced outgoing damage applies Freeze and Poisonous");
        Core2026Proof.Check(attacker.Damage == 6 && attacker.IsFrozen && p.Hero.Damage == 7, "retaliation, Frozen bypass and Lifesteal preserved");
        Core2026Proof.Check(starts == 1 && after == 1 && ownAfter == 1, "attack and both AFTER_ATTACK dispatch paths fire once");
        var q = g.HistoryFacts.Query;
        Core2026Proof.Check(q.AttacksStarted().Single().AttackerId == attacker.Id
            && q.AttacksFinished().Single().Origin == HistoryAttackOrigin.Forced
            && q.AttacksFinished().Single().ReachedCombat && q.FriendlyAttacksReachedCombat(p.Id) == 1,
            "genuine forced attack history facts retained");
        Core2026Proof.Check(p.NumFriendlyMinionsThatAttackedThisTurn == 1 && attacker.NumAttacksThisTurn == 0,
            "attack bookkeeping remains separate from normal budget");
        End(g);
        Core2026Proof.Check(!attacker.IsFrozen, "forced combat does not postpone thaw after skipped normal attack");
    }

    private static void OptimizedPlayerAttacks(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        var attacker = Summon(p, "CS2_171");
        var task = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(task.SkipPrePhase && g.Process(task) && attacker.NumAttacksThisTurn == 1 && attacker.IsExhausted,
            "prevalidated ordinary minion attack still consumes normal allowance");
        Play(g, p, "CS2_106");
        task = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK);
        Core2026Proof.Check(task.SkipPrePhase && g.Process(task) && p.Hero.NumAttacksThisTurn == 1 && p.Hero.IsExhausted,
            "prevalidated ordinary hero attack still consumes normal allowance");
        var direct = Summon(p, "CS2_171");
        Core2026Proof.Check(Generic.AttackBlock(p, direct, p.Opponent.Hero, false, true)
            && direct.NumAttacksThisTurn == 1 && !direct.IsExhausted,
            "validated direct attack retains legacy no-exhaustion behavior when death processing is deferred");
    }

    private static void NestedCombat(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        Play(g, p, "BOT_406");
        var attacker = Summon(p.Opponent, "CS2_182"); attacker.BaseHealth = 30;
        var neighbor = Summon(p.Opponent, "CS2_182"); neighbor.BaseHealth = 30;
        bool exhausted = attacker.IsExhausted;
        var task = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == attacker);
        Core2026Proof.Check(g.Process(task) && p.Hero.NumAttacksThisTurn == 1 && p.Hero.IsExhausted,
            "Supercollider normal attack consumes hero allowance");
        Core2026Proof.Check(attacker.NumAttacksThisTurn == 0 && attacker.IsExhausted == exhausted
            && attacker.Damage == 5 && neighbor.Damage == 4,
            "Supercollider nested forced attack preserves victim allowance and resolves retaliation");
        Core2026Proof.Check(g.HistoryFacts.Query.AttacksFinished().Count(f => f.Origin == HistoryAttackOrigin.Player) == 1
            && g.HistoryFacts.Query.AttacksFinished().Count(f => f.Origin == HistoryAttackOrigin.Forced) == 1,
            "nested forced attack has separate origin from optimized player attack");
    }

    private static void CardTriggers(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        End(g);
        Play(g, p.Opponent, "AT_060"); // Bear Trap: real AFTER_ATTACK task.
        End(g);
        var attacker = Summon(p, "AT_031"); // Cutpurse: real ATTACK task.
        new AttackTask(EntityType.SOURCE, EntityType.OP_HERO).Process(g, p, attacker, null);
        Core2026Proof.Check(p.HandZone.Count(c => c.Card.Id == "GAME_005") == 1
            && p.Opponent.SecretZone.IsEmpty && p.Opponent.BoardZone.Single().Card.Id == "CS2_125",
            "forced attack runs registered ATTACK and AFTER_ATTACK tasks");
        Core2026Proof.Check(attacker.NumAttacksThisTurn == 0 && attacker.IsExhausted,
            "summoning sickness survives forced combat");
    }

    private static void Hero(bool history, bool deferDeaths)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        Play(g, p, "CS2_106");
        var hero = p.Hero; var target = Summon(p.Opponent, "CS2_200");
        Core2026Proof.Check(Generic.AttackBlock(p, hero, target, true, deferDeaths), "direct forced hero attack accepted");
        Core2026Proof.Check(hero.NumAttacksThisTurn == 0 && !hero.IsExhausted && hero.Weapon.Durability == 1
            && target.Damage == 3 && hero.Damage == 6, "forced hero attack preserves allowance but spends durability and deals combat damage");
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && hero.NumAttacksThisTurn == 1 && hero.IsExhausted,
            "ordinary hero attack still consumes allowance");
    }

    private static void Swarmer(bool history, bool forced)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        var swarmer = Summon(p, "OG_034");
        End(g); End(g);
        Play(g, p, "CS2_106");
        Core2026Proof.Check(!CanAttack(swarmer, p.Opponent.Hero), "ready Swarmer blocked before hero attacks");
        Core2026Proof.Check(Generic.AttackBlock(p, p.Hero, p.Opponent.Hero, forced, false), "hero combat accepted");
        Core2026Proof.Check(p.Hero.NumAttacksThisTurn == (forced ? 0 : 1), "hero normal allowance accounts only for ordinary combat");
        Core2026Proof.Check(CanAttack(swarmer, p.Opponent.Hero)
            && g.Process(MinionAttackTask.Any(p, swarmer, p.Opponent.Hero)), "hero combat unlocks normal Swarmer attack");
        End(g); End(g);
        Core2026Proof.Check(!CanAttack(swarmer, p.Opponent.Hero), "previous-turn hero combat does not unlock Swarmer");
    }

    private static void AttackFactConditions(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        var attacker = Summon(p, "CS2_171");
        Core2026Proof.Check(SelfCondition.IsNotAttackingThisTurn(0).Eval(attacker)
            && !SelfCondition.HasMyHeroAttackedThisTurn.Eval(attacker)
            && SelfCondition.HasMyHeroNotAttackedThisTurn.Eval(attacker), "conditions start without attack facts");
        new AttackTask(EntityType.SOURCE, EntityType.OP_HERO).Process(g, p, attacker, null);
        Core2026Proof.Check(attacker.NumAttacksThisTurn == 0 && SelfCondition.IsNotAttackingThisTurn(1).Eval(attacker)
            && !SelfCondition.IsNotAttackingThisTurn(0).Eval(attacker), "character attack condition counts forced combat");
        Play(g, p, "CS2_106");
        Core2026Proof.Check(Generic.AttackBlock(p, p.Hero, p.Opponent.Hero, true, false), "forced hero combat accepted");
        Core2026Proof.Check(p.Hero.NumAttacksThisTurn == 0 && SelfCondition.HasMyHeroAttackedThisTurn.Eval(attacker)
            && !SelfCondition.HasMyHeroNotAttackedThisTurn.Eval(attacker), "hero conditions count forced combat");
        End(g); End(g);
        Core2026Proof.Check(SelfCondition.IsNotAttackingThisTurn(0).Eval(attacker)
            && !SelfCondition.HasMyHeroAttackedThisTurn.Eval(attacker)
            && SelfCondition.HasMyHeroNotAttackedThisTurn.Eval(attacker), "conditions exclude previous-turn combat");
    }
}

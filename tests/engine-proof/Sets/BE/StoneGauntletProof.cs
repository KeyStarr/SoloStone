using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_StoneGauntletProof
{
    internal static void Run() => StandardSetsProof.Case("BE_558", (g, p, id) => {
        // stats-health/{stone-health-floor,stone-lethal-rescue}.yaml: owner-approved C/E assumptions.
        foreach (int health in new[] { 1, 3, 5, 6, 20 })
        {
            var test = g.CloneExact(history: true); var owner = test.Player1;
            Play(test, owner, id); owner.Hero.Health = health; owner.Hero.Armor = 7;
            owner.Hero.IsExhausted = false;
            Core2026Proof.Check(test.Process(HeroAttackTask.Any(owner, owner.Opponent.Hero)), "Gauntlet hero attack accepted");
            int converted = Math.Min(5, health - 1);
            Core2026Proof.Check(owner.Hero.Health == health - converted && owner.Hero.Armor == 7 + converted, "partial conversion preserves one Health and existing Armor");
        }
        foreach (int retaliation in new[] { 5, 6 })
        {
            var test = g.CloneExact(history: true); var owner = test.Player1; Play(test, owner, id);
            owner.Hero.Health = 5; var enemy = Summon(owner.Opponent, "CS2_182"); enemy.AttackDamage = retaliation;
            owner.Hero.IsExhausted = false;
            Core2026Proof.Check(test.Process(HeroAttackTask.Any(owner, enemy)), "lethal retaliation attack");
            Core2026Proof.Check(owner.Hero.Health == 1 && owner.Hero.Armor == 0 && !owner.Hero.ToBeDestroyed && test.State == State.RUNNING,
                "zero/negative post-combat Health rescued before death");
        }
        {
            var test = g.CloneExact(history: true); var owner = test.Player1; Play(test, owner, id); owner.Hero.Health = 10;
            owner.Hero.IsExhausted = false; Core2026Proof.Check(test.Process(HeroAttackTask.Any(owner, owner.Opponent.Hero)), "first durability attack");
            End(test); End(test); owner.Hero.IsExhausted = false;
            Core2026Proof.Check(test.Process(HeroAttackTask.Any(owner, owner.Opponent.Hero)) && owner.Hero.Health == 1 && owner.Hero.Armor == 9 && owner.Hero.Weapon == null,
                "last durability still converts before weapon removal");
        }
        Play(g, p, id); p.Hero.Health = 20;
        var minion = Summon(p, "CS2_182"); Attack(g, minion, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Health == 20 && p.Hero.Armor == 0, "friendly minion does not convert");
        End(g); var enemyMinion = Summon(p.Opponent, "CS2_182"); Attack(g, enemyMinion, p.Hero);
        Core2026Proof.Check(p.Hero.Health == 16 && p.Hero.Armor == 0, "opponent attack does not convert"); End(g);
        Play(g, p, "CS2_106"); p.Hero.IsExhausted = false;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.Hero.Health == 16 && p.Hero.Armor == 0, "removed weapon trigger inactive");
    });
}

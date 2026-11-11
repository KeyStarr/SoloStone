using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HoldThemOffProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_913", (g, p, id) => {

        var target = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, target, false);
        Play(g, p, id, target);
        Core2026Proof.Check(target.AttackDamage == 9 && target.Health == 10 && target.HasLifeSteal, "+5/+5 and Lifesteal");
        p.Hero.Damage = 15; Attack(g, target, p.Opponent.Hero); Core2026Proof.Check(p.Hero.Damage == 6, "buff grants working Lifesteal");
        var clone = g.CloneExact(); ((Minion)clone.IdEntityDic[target.Id]).Silence();
        Core2026Proof.Check(target.HasLifeSteal && !((Minion)clone.IdEntityDic[target.Id]).HasLifeSteal, "clone isolated");
        End(g); End(g); Core2026Proof.Check(target.AttackDamage == 9, "buff permanent");
        target.Silence(); Core2026Proof.Check(target.AttackDamage == 4 && target.Health == 5 && !target.HasLifeSteal, "Silence removes buff");
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.AttackDamage == 9 && enemy.HasLifeSteal, "enemy target supported");
    });
}

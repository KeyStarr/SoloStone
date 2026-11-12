using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class JudgmentProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_326", (g, p, id) => {

        var chosen = Summon(p, "CS2_182"); chosen.Damage = 2;
        var other = Summon(p, "CS2_120"); var enemy = Summon(p.Opponent, "EX1_301");
        var held = Add(p, id); var options = p.Options().Where(t => t.Source == held && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target == chosen) && options.All(t => t.Target is Minion && t.Target.Controller == p), "friendly minion required");
        PlayEntity(g, p, held, chosen);
        Core2026Proof.Check(new[] {chosen, other, enemy}.All(m => m.AttackDamage == 4 && m.Health == 3), "snapshot current stats applied to both boards including damaged target");
        var clone = g.CloneExact(); ((Minion)clone.IdEntityDic[other.Id]).Silence();
        Core2026Proof.Check(other.AttackDamage == 4 && ((Minion)clone.IdEntityDic[other.Id]).AttackDamage == 2, "clone enchantment independent");
        End(g); End(g); Core2026Proof.Check(enemy.Health == 3, "stats persist");
        other.Silence(); Core2026Proof.Check(other.AttackDamage == 2 && other.Health == 3, "Silence restores printed stats");
    });
}

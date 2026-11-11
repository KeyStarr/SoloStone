using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ForestsGiftProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_138", (g, p, id) => {
        var spell = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD), "requires friendly minion");
        var target = Summon(p, "CS2_182"); var friend = Summon(p, "CS2_120"); var enemy = Summon(p.Opponent, "CS2_182");
        var options = p.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Length == 2 && options.All(t => t.Target is Minion && t.Target.Controller == p), "only friendly minions targetable");
        target.Damage = 2; PlayEntity(g, p, spell, target);
        Core2026Proof.Check(target.AttackDamage == 6 && target.BaseHealth == 7 && target.Damage == 2 && target.Health == 5 && friend.AttackDamage == 2 && enemy.Health == 5, "snapshots two friendly minions; preserves damage");
        Kill(g, friend); Core2026Proof.Check(target.AttackDamage == 6 && target.BaseHealth == 7, "buff fixed after board changes");
        var clone = g.Clone(); ((Minion)clone.IdEntityDic[target.Id]).Silence(); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(((Minion)clone.IdEntityDic[target.Id]).AttackDamage == 4 && target.AttackDamage == 6, "clone buff isolated");
        End(g); End(g); Core2026Proof.Check(target.AttackDamage == 6, "permanent buff");
        target.Silence(); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(target.AttackDamage == 4 && target.Health == 5 && target.BaseHealth == 5 && target.Damage == 0, "Silence removes local Health buff using normal damage adjustment");
        Play(g, p, id, target); Core2026Proof.Check(target.AttackDamage == 5 && target.Health == 6, "one minion includes target itself");
        var untargeted = Add(p, id); untargeted.Card.Power.PowerTask.Process(g, p, untargeted, null, new TaskStack());
        Core2026Proof.Check(target.AttackDamage == 5 && target.Health == 6, "missing target safely fizzles");
        var dormant = Summon(p, "CS2_120"); dormant[GameTag.DORMANT] = 1;
        var activeOnly = Add(p, id);
        var activeOptions = p.Options().Where(t => t.Source?.Id == activeOnly.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(activeOptions.Length == 1 && activeOptions[0].Target?.Id == target.Id, "Dormant minion cannot be targeted");
        PlayEntity(g, p, activeOnly, target);
        Core2026Proof.Check(target.AttackDamage == 6 && target.Health == 7 && dormant.AttackDamage == 2 && dormant.Health == 3, "one active minion plus one Dormant grants only +1/+1");
        dormant[GameTag.DORMANT] = 0; g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(target.AttackDamage == 6 && target.Health == 7, "awakening Dormant minion does not change snapshot buff");
    });
}

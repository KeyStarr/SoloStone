using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SpiderlingProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_202", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(p.Hero.AttackDamage == 1 && p.Opponent.Hero.AttackDamage == 0, "own hero attack on own turn");
        var other = Summon(p, id); Core2026Proof.Check(p.Hero.AttackDamage == 2, "aura stacks");
        var clone = g.Clone(); ((Minion)clone.IdEntityDic[source.Id]).Silence(); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.Player1.Hero.AttackDamage == 1 && p.Hero.AttackDamage == 2, "Silence aura clone isolated");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage == 0 && p.Opponent.Hero.AttackDamage == 0, "aura inactive opponent turn");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage == 2, "aura returns own turn");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(p.Hero.AttackDamage == 1, "Silence removes one aura");
        Kill(g, other); Core2026Proof.Check(p.Hero.AttackDamage == 0, "source death removes aura");
    });
}

using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SolitaryPrisonerProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_204", (g, p, id) => {
        var held = Add(p, id); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(held.Cost == 2 && held[GameTag.COST] == 2, "empty battlefield visible cost two");
        var enemy = Summon(p.Opponent, "CS2_182"); Core2026Proof.Check(held.Cost == 5 && held[GameTag.COST] == 5, "enemy minion blocks discount");
        Kill(g, enemy); var friend = Summon(p, "CS2_182"); Core2026Proof.Check(held.Cost == 5, "friendly minion blocks discount");
        var clone = g.Clone(); Kill(clone, (Minion)clone.IdEntityDic[friend.Id]);
        Core2026Proof.Check(clone.IdEntityDic[held.Id][GameTag.COST] == 2 && held.Cost == 5, "cost clone isolated");
        Kill(g, friend); End(g); End(g); Core2026Proof.Check(held.Cost == 2, "empty battlefield discount persists across turns");
        PlayEntity(g, p, held); var source = (Minion)held; Core2026Proof.Check(source.AttackDamage == 5 && source.Health == 3 && p.UsedMana == 2, "discounted play pays two");
        var next = Add(p, id); Core2026Proof.Check(next.Cost == 5, "played source blocks subsequent copy discount");
    });
}

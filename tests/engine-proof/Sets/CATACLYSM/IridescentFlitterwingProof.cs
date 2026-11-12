using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class IridescentFlitterwingProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_133", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.CantBeTargetedBySpells && source.CantBeTargetedByHeroPowers, "Elusive protections");
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[friend.Id]).AttackDamage == 5 && friend.AttackDamage == 4, "clone end trigger isolated");
        End(g);
        Core2026Proof.Check(friend.AttackDamage == 5 && friend.Health == 6 && source.AttackDamage == 4 && enemy.AttackDamage == 4, "end buffs only other friendly minions");
        End(g); Core2026Proof.Check(friend.AttackDamage == 5, "opponent turn does not trigger; buff persists");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); End(g); End(g);
        Core2026Proof.Check(friend.AttackDamage == 5 && !source.CantBeTargetedBySpells, "Silence removes trigger and Elusive");
        var removed = (Minion)Play(g, p, id); Kill(g, removed); End(g); End(g);
        Core2026Proof.Check(friend.AttackDamage == 5, "dead source stops triggering");
    });
}

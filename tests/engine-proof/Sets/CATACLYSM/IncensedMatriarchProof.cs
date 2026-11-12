using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class IncensedMatriarchProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_305", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.DRAGON), "printed Dragon");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[source.Id]).Health == 6 && source.Health == 3, "clone end trigger isolated");
        End(g); Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 6, "full Health gains three max and current Health");
        End(g); Core2026Proof.Check(source.Health == 6, "opponent turn does not trigger and buff persists");
        source.Damage = 1; End(g); Core2026Proof.Check(source.BaseHealth == 6 && source.Health == 5, "damaged source does not grow");
        End(g); source.Damage = 0; End(g); Core2026Proof.Check(source.Health == 9, "healed source grows again");
        End(g); source.Silence(); g.DeathProcessingAndAuraUpdate(); End(g);
        Core2026Proof.Check(source.Health == 3 && source.AttackDamage == 3, "Silence removes buffs and own trigger");
        End(g); var removed = (Minion)Play(g, p, id); Kill(g, removed); End(g);
        Core2026Proof.Check(removed.Zone.Type == Zone.GRAVEYARD && removed.BaseHealth == 3, "dead source stops triggering");
    });
}

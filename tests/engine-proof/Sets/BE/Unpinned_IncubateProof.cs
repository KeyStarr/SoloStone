using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_IncubateProof
{
    internal static void Run() => StandardSetsProof.Case("BE_724", (g, p, id) => {
        var source = Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 3 && p.HandZone.All(x => x.Card.Id.StartsWith("BE_720t")
            && x.Card.Id != "BE_720t3" && x.Card.Id != "BE_720t12" && x.Card.Cost == 1
            && x is Minion m && m.AttackDamage == 2 && m.Health == 1 && x.Card.IsRace(Race.BEAST)
            && x[GameTag.DISPLAYED_CREATOR] == source.Id), "Unpinned_ClassPoolAndTeal: exact count, fixed other-class tokens, stats and creator");
        
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        p.HandZone.Remove(p.HandZone.Last());
        p.UsedMana = 0;
        var overflow = Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 10, "hand capacity respected");

    });
}

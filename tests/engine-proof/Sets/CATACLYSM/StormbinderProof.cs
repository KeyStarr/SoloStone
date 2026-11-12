using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class StormbinderProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_724", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 7 && source.Health == 7 && p.OverloadOwed == 3, "printed 7/7 incurs three Overload");
        p.Opponent.OverloadOwed = 2; p.Opponent.OverloadLocked = 1;
        Kill(g, source);
        Core2026Proof.Check(p.OverloadOwed == 0 && p.OverloadLocked == 0 && p.Opponent.OverloadOwed == 2 && p.Opponent.OverloadLocked == 1, "Deathrattle unlocks only own owed and locked crystals");
        source = (Minion)Play(g, p, id); End(g); End(g);
        Core2026Proof.Check(p.OverloadLocked == 3 && p.OverloadOwed == 0, "Overload locks three crystals next turn");
        var clone = g.CloneExact(history: true); Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(clone.Player1.OverloadLocked == 0 && p.OverloadLocked == 3, "unlock in clone leaves original mana locked");
        Kill(g, source); Core2026Proof.Check(p.OverloadLocked == 0, "Deathrattle unlocks currently locked mana");
        source = (Minion)Play(g, p, id); source.Silence(); g.DeathProcessingAndAuraUpdate(); Kill(g, source);
        Core2026Proof.Check(p.OverloadOwed == 3, "Silence removes unlock Deathrattle");
    });
}

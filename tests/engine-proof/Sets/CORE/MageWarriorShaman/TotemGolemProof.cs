using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TotemGolemProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_AT_052", (g, p, id) => {
        var summoned = Summon(p, id); Kill(g, summoned);
        Core2026Proof.Check(p.OverloadOwed == 0, "summoning does not incur Overload");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 4 && source.IsRace(Race.TOTEM), "printed stats and keywords");
        Core2026Proof.Check(p.OverloadOwed == 1 && p.OverloadThisGame == 1, "exact Overload amount owed and recorded");
        var clone = g.Clone(); End(clone); End(clone);
        Core2026Proof.Check(clone.Player1.OverloadLocked == 1 && clone.Player1.OverloadOwed == 0 && p.OverloadOwed == 1 && p.OverloadLocked == 0, "clone locks correct amount independently");
        source.Silence(); Kill(g, source); End(g); End(g);
        Core2026Proof.Check(p.OverloadLocked == 1 && p.OverloadOwed == 0 && p.Opponent.OverloadLocked == 0, "Silence and death do not cancel owed mana; opponent unaffected");
        End(g); End(g); Core2026Proof.Check(p.OverloadLocked == 0, "mana unlocks following turn");
    });
}

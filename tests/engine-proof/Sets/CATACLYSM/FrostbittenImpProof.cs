using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FrostbittenImpProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_612", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsFrozen, "battlecry freezes self"); source.IsExhausted = false;
        Core2026Proof.Check(!source.CanAttack, "frozen minion cannot attack");
        End(g); Core2026Proof.Check(!source.IsFrozen, "missed attack opportunity thaws at turn end");
        var summoned = Summon(p.Opponent, id); Core2026Proof.Check(!summoned.IsFrozen, "summon does not run battlecry");
    });
}

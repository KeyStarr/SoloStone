using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SpecterofDespairProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_942", (g, p, id) => {
        var source = (Minion)Play(g, p, id); source.IsExhausted = false;
        Core2026Proof.Check(source.HasTaunt && !source.CanAttack && !p.Options().Any(t => t.Source?.Id == source.Id && t.PlayerTaskType == PlayerTaskType.MINION_ATTACK), "Taunt and cannot attack");
        source.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!source.HasTaunt && source.CanAttack, "silence removes cannot attack");
    });
}

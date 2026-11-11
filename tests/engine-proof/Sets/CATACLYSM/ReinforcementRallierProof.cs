using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ReinforcementRallierProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_558", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 2, "2/2 body"); Elusive(g, p, source);
    });
}

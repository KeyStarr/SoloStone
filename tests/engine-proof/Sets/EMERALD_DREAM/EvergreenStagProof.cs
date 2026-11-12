using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EvergreenStagProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_272", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasTaunt && source.HasLifeSteal, "Stag Taunt and Lifesteal");
        Elusive(g, p, source);
    });
}

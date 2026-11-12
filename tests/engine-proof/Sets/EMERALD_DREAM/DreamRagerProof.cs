using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DreamRagerProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_598", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Elusive(g, p, source);
    });
}

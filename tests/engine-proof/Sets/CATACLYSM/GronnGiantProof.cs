using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_GronnGiantProof
{
    internal static void Run() => Unpinned_CostMemory();
    // Owner-approved working assumptions: docs/design/costmemory.md.
    private static void Unpinned_CostMemory() => StandardSetsProof.Case("CATA_616", (g, p, id) => {
        var giant = Add(p, id);
        var first = Add(p, "CS2_200"); first.Cost = 3;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, first)) && giant.Cost == 6, "modified paid Cost discount");
        first.Cost = 9;
        var later = Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(giant.Cost == 6 && later.Cost == 6, "durable previous play, generated Giant");
        var zero = Add(p, "GAME_005");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, zero)) && giant.Cost == 9 && later.Cost == 9, "zero replaces previous Cost");
    });
}

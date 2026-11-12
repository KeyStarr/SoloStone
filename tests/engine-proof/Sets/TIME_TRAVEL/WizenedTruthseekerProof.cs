using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_WizenedTruthseekerProof
{
    internal static void Run() => Unpinned_CostMemory();
    // Owner-approved working assumptions: docs/design/costmemory.md.
    private static void Unpinned_CostMemory() => StandardSetsProof.Case("TIME_057", (g, p, id) => {
        var own = (Playable)Add(p, "CS2_182"); var enemy = (Playable)Add(p.Opponent, "CS2_182");
        Effects.ReduceCost(3).ApplyTo(own); CostRules.SetCostThisTurn(enemy, 0);
        Play(g, p, id);
        Core2026Proof.Check(own.Cost == 4 && enemy.Cost == 4, "both hands restored");
        Effects.ReduceCost(1).ApplyTo(own);
        Core2026Proof.Check(own.Cost == 3, "later reductions remain effective");
    });
}

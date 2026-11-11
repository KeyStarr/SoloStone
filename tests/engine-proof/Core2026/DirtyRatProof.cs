using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class DirtyRatProof
{
    internal static void Run() => Case("CORE_CFM_790", (g, p, id) => {
        var minion = Add(p.Opponent, "CS2_182"); Add(p.Opponent, "CS2_029");
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.BoardZone.Single().Id == minion.Id && p.Opponent.HandZone.Count == 1, "pull minion, leave spell");
    });
}

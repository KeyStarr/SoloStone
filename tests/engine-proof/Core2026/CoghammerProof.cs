using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class CoghammerProof
{
    internal static void Run() => Case("CORE_GVG_059", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(friend.HasTaunt && friend.HasDivineShield, "Coghammer grants both keywords");
    });
}

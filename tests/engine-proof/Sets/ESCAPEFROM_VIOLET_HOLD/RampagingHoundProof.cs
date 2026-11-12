using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class RampagingHoundProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_435", Unpinned_RampagingHound);
    private static void Unpinned_RampagingHound(Game g, Controller p, string id)
    {
        ForcedCardProof.ForceAll(g, p, id, false);
        var held = Add(p, id);
        Core2026Proof.Check(held[GameTag.PREPARE] == 1, "pinned Prepare declaration");
    }
}

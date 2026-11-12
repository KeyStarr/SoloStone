using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class MythicalTerrorProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_TTN_866", Unpinned_MythicalTerror);
    private static void Unpinned_MythicalTerror(Game g, Controller p, string id) => ForcedCardProof.ForceAll(g, p, id, true);
}

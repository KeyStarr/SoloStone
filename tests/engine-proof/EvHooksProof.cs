using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class EvHooksProof
{
    [GroupProof("ev-hooks")]
    internal static void Run()
    {
        foreach (string id in new[] { "CATA_EVENT_110", "TLC_EVENT_400", "JAIL_EVENT_100", "JAIL_EVENT_101", "TIME_EVENT_997" })
            Core2026Proof.Check(Cards.FromId(id)?.Implemented == true, "EVENT hook definition loads: " + id);
        EvHooksEdgeProof.Run();
    }
}

using HarmonyLib;
using SabberStoneCore.Auras;
using SabberStoneCore.Tasks.SimpleTasks;

namespace TransportInvariants;

internal static class TransportHooks
{
    internal static void Install()
    {
        var harmony = new Harmony("hs.headless.transport-invariants");
        HistoryShapeHooks.Install(harmony);
        // Same four patches as ClientBridge.Initialize, without loading ClientBridge or any Unity API.
        foreach (var (task, before, after) in new[] {
            (typeof(SummonCopyTask), nameof(CopyTransportBridge.BeforeSummonCopy), nameof(CopyTransportBridge.AfterSummonCopy)),
            (typeof(TransformCopyTask), nameof(CopyTransportBridge.BeforeTransformCopy), nameof(CopyTransportBridge.AfterTransformCopy))
        })
            harmony.Patch(CopyTransportBridge.RequiredProcess(task),
                prefix: new HarmonyMethod(typeof(CopyTransportBridge), before), postfix: new HarmonyMethod(typeof(CopyTransportBridge), after));
        foreach (var type in new[] { typeof(Aura), typeof(SwitchingAura) })
            harmony.Patch(AuraTransportBridge.RequiredRemoveInternal(type),
                prefix: new HarmonyMethod(typeof(AuraTransportBridge), nameof(AuraTransportBridge.BeforeRemove)),
                postfix: new HarmonyMethod(typeof(AuraTransportBridge), nameof(AuraTransportBridge.AfterRemove)));
    }
}

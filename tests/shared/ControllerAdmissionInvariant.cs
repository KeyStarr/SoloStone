using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using EngineGame = SabberStoneCore.Model.Game;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineZone = SabberStoneCore.Enums.Zone;

// Observe the actual admission boundary, including transient mismatches later repaired by a card task.
internal static class ControllerAdmissionInvariant
{
    private static readonly ConditionalWeakTable<EngineGame, List<NativeAdmission.Fault>> faults = new();
    internal static int Admissions { get; private set; }

    internal static void Install() => new Harmony("offline.tests.controller-admission").Patch(
        AccessTools.PropertySetter(typeof(EngineEntity), nameof(EngineEntity.Zone)),
        postfix: new HarmonyMethod(typeof(ControllerAdmissionInvariant), nameof(Admitted)));

    private static void Admitted(EngineEntity __instance)
    {
        // Historyless search clones and detached ID-0 cost probes do not maintain transport tags.
        if (__instance.Id <= 0 || !__instance.Game.History || __instance is not IPlayable || __instance.Controller == null
            || __instance.Zone?.Type is not (EngineZone.HAND or EngineZone.DECK or EngineZone.PLAY)) return;
        Admissions++;
        int owner = __instance.Controller.PlayerId, tagged = __instance[GameTag.CONTROLLER];
        if (owner == tagged) return;
        faults.GetOrCreateValue(__instance.Game).Add(new("R5", -1, 0, __instance.Id,
            $"{__instance.Card.Id} enters {__instance.Zone.Type}: Controller={owner}, CONTROLLER={tagged}\n{new StackTrace(1, true)}",
            __instance.Card.Id));
    }

    internal static IReadOnlyList<NativeAdmission.Fault> Faults(EngineGame game) =>
        faults.TryGetValue(game, out var found) ? found : System.Array.Empty<NativeAdmission.Fault>();
}

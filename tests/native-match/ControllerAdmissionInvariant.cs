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

internal static partial class Program
{
    private static void ControllerAdmissionControls()
    {
        var game = new EngineGame(new SabberStoneCore.Config.GameConfig { History = true });
        int admissions = ControllerAdmissionInvariant.Admissions;
        SabberStoneCore.Actions.CostRules.AffordableSpells(game.Player1, new[] { Cards.FromId("CORE_CS2_029") });
        Check(ControllerAdmissionInvariant.Admissions == admissions && ControllerAdmissionInvariant.Faults(game).Count == 0,
            "R5 ignores detached cost probes, which never enter an engine zone");
        foreach (var owner in new[] { game.Player1, game.Player2 })
        foreach (var zone in new SabberStoneCore.Model.Zones.IZone[] { owner.HandZone, owner.DeckZone, owner.BoardZone })
        {
            int before = ControllerAdmissionInvariant.Faults(game).Count;
            var valid = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: zone);
            Check(ControllerAdmissionInvariant.Faults(game).Count == before, "R5 accepts matching controller at " + zone.Type);
            zone.Remove(valid);
            var stolen = EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_182"));
            stolen.Controller = owner; // Deliberately omit CONTROLLER, as in the Broxigar/Llane bugs.
            zone.Add(stolen);
            stolen[GameTag.CONTROLLER] = owner.PlayerId;
            zone.Remove(stolen);
            var faults = ControllerAdmissionInvariant.Faults(game);
            Check(faults.Count == before + 1 && faults.Last().Entity == stolen.Id && faults.Last().Rule == "R5",
                "R5 catches stale controller on " + zone.Type + " admission even if repaired before fixture ends");
        }
        Check(ControllerAdmissionInvariant.Admissions - admissions == 12, "R5 observes both seats and every HAND/DECK/PLAY admission");
        Console.WriteLine("PASS engine controller admission controls: 6 valid and 6 stale transfers, both seats, HAND/DECK/PLAY");
    }
}

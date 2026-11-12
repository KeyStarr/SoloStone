using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using EngineGame = SabberStoneCore.Model.Game;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

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

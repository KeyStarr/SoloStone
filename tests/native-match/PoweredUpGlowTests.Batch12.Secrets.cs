using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b12-secrets", ProofKind.PoweredUp)]
    private static void RunBatch12Secrets()
    {
        Scenario("JAIL_321", (f, card, met) => {
            if (met)
            {
                var coin = (SabberStoneCore.Model.Entities.Spell)SabberStoneCore.Model.Entities.Entity.FromCard(f.Player, Cards.FromId("GAME_005"));
                SabberStoneCore.Actions.Generic.CastSpell(f.Player, f.Game, coin, null, 0);
            }
        }, (f, card) => PlayCardTask.Minion(f.Player, card),
            (f, card) => f.Player.SecretZone.Count > 0);
    }
}

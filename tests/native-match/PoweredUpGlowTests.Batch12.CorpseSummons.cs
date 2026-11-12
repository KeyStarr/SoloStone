using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b12-corpse-summons", ProofKind.PoweredUp)]
    private static void RunBatch12CorpseSummons()
    {
        foreach (string id in new[] { "CATA_465", "CORE_RLK_118" })
        {
            int required = id == "CATA_465" ? 8 : 4;
            Scenario(id, (f, card, met) => {
                f.Player[GameTag.CORPSES] = required + (met ? 3 : 2);
                f.Player[GameTag.CORPSES_SPENT_THIS_GAME] = 3;
            }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
                var minions = f.Player.BoardZone.ToArray();
                bool spent = f.Player[GameTag.CORPSES_SPENT_THIS_GAME] == required + 3;
                Check(minions.Length == (id == "CATA_465" ? 5 : 2)
                    && minions.All(m => m.Card.Id == (id == "CATA_465" ? "CATA_465t" : "RLK_118t3")
                        && (id == "CATA_465" ? m.IsRush : m[GameTag.REBORN] == 1) == spent)
                    && (spent || f.Player[GameTag.CORPSES_SPENT_THIS_GAME] == 3),
                    id + " exact summon count, all-or-nothing payment and bonus keywords");
                return spent;
            });
        }
    }
}

using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b12-corpses", ProofKind.PoweredUp)]
    private static void RunBatch12Corpses()
    {
        foreach (string id in new[] { "RLK_060", "CORE_RLK_506" })
        {
            bool army = id == "RLK_060";
            Scenario(id, (f, card, met) => {
                f.Player[GameTag.CORPSES] = met ? 5 : 4;
                f.Player[GameTag.CORPSES_SPENT_THIS_GAME] = 4;
            }, (f, card) => army ? PlayCardTask.Spell(f.Player, card) : PlayCardTask.Minion(f.Player, card), (f, card) => {
                string tokenId = army ? "RLK_008t" : "RLK_061t";
                var tokens = f.Player.BoardZone.Where(m => m.Card.Id == tokenId).ToArray();
                bool raised = tokens.Length == 1;
                Check(f.Player[GameTag.CORPSES_SPENT_THIS_GAME] == (raised ? 5 : 4)
                    && tokens.All(m => m[GameTag.DISPLAYED_CREATOR] == card.Id && m[(GameTag)2217] == 1
                        && (army ? m.IsRush : m.HasTaunt)), "raise exact token, creator and Corpse debit");
                foreach (var token in tokens)
                {
                    var tags = f.Match.SnapshotVisibleTransportTags(token);
                    Check(tags[(int)(army ? GameTag.RUSH : GameTag.TAUNT)] == 1,
                        "Risen keyword reaches native actor");
                }
                return raised;
            });
            foreach (bool met in new[] { false, true })
            {
                var f = new Fixture(); var card = f.Draw(id);
                f.Player[GameTag.CORPSES] = 1;
                int needed = army ? 1 : 2;
                while (f.Player.BoardZone.FreeSpace > needed - (met ? 0 : 1)) f.Summon("CS2_182", f.Player);
                f.Flush();
                f.Expected(card, met ? 1 : 0, "raise glow counts physical space after source enters");
            }
        }
    }
}

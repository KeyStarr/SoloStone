using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;

namespace TransportInvariants;

// Packet-local oracle: no endpoint engine state can repair an earlier anonymous/hidden board actor.
// Playtest 2026-11-01: an Illusion (EDR_260t, from Illusory Greenwing) drawn on the opponent's turn left an empty board
// slot although engine and client entity lists both held a 4/5 Taunt there. Drawn and summoned in one packet it goes
// HAND -> SETASIDE -> PLAY; the client treats SETASIDE as a null zone and keeps the actor hidden after the move to
// PLAY. So this checks, per packet, that every PLAY minion/location is named and not hidden.
public static class BoardActorInvariant
{
    public static IReadOnlyList<string> Validate(IEnumerable<PowerHistory> packets, bool repairNullToPlayActors = true)
    {
        var client = new ClientModel { RepairNullToPlayActors = repairNullToPlayActors };
        var faults = new List<string>();
        int packet = 0;
        foreach (var history in packets)
        {
            client.Apply(history);
            faults.AddRange(Inspect(client).Select(detail => $"packet={packet} {detail}"));
            packet++;
        }
        return faults;
    }

    internal static IEnumerable<string> Inspect(ClientModel client)
    {
        foreach (var pair in client.Entities)
        {
            var entity = pair.Value;
            if (entity.Get(GameTag.ZONE) != (int)SabberStoneCore.Enums.Zone.PLAY
                || entity.Get(GameTag.CARDTYPE) is 1 or 2 or 6) continue;
            if (string.IsNullOrEmpty(entity.CardId)) yield return $"entity={pair.Key} anonymous PLAY";
            if (entity.Get(GameTag.CARDTYPE) is 4 or 39 && entity.HiddenByNullZone)
                yield return $"entity={pair.Key} card={entity.CardId} hidden board actor after SETASIDE";
        }
    }
}

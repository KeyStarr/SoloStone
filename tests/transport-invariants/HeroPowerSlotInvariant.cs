using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;

namespace TransportInvariants;

public static class HeroPowerSlotInvariant
{
    public static IReadOnlyList<string> Validate(IEnumerable<PowerHistory> packets)
    {
        var client = new ClientModel();
        var faults = new List<string>();
        foreach (var packet in packets)
        {
            client.Apply(packet);
            faults.AddRange(Inspect(client));
        }
        return faults;
    }

    internal static IEnumerable<string> Inspect(ClientModel client)
    {
        foreach (var slot in client.Entities.Where(e => e.Value.CardZone == (int)SabberStoneCore.Enums.Zone.PLAY
            && e.Value.Get(GameTag.CARDTYPE) == (int)CardType.HERO_POWER)
            .GroupBy(e => (e.Value.CardController, Index: e.Value.Get(BasicHeroPowerRules.SecondaryIndex))))
            if (slot.Count() > 1) yield return $"controller={slot.Key.CardController} slot={slot.Key.Index} multiple PLAY powers: {string.Join(",", slot.Select(e => e.Key))}";
    }
}

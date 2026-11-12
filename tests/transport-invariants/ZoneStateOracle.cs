using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Zone = SabberStoneCore.Enums.Zone;

// Shared serialized-client oracle for native regressions and transport qualification.
// Added for the Godfrey burn bug: the client indexes entities by the ENTITY_ID tag, and both burn displays arrived
// with 0, so one ghost card stayed in the client's deck (client 28 vs engine 27). Comparing per-zone entity sets
// catches that class.
public sealed class ZoneStateOracle
{
    private readonly ClientModel client = new();
    public void Apply(PegasusGame.PowerHistory packet)
    {
        using var stream = new MemoryStream();
        packet.Serialize(stream); stream.Position = 0;
        var received = new PegasusGame.PowerHistory(); received.Deserialize(stream);
        client.Apply(received);
    }
    public IEnumerable<string> Compare(Game game, Func<int, int> wireId)
        => Compare(game, client, wireId);

    internal static IEnumerable<string> Compare(Game game, ClientModel client, Func<int, int> wireId)
    {
        var logicalIds = game.IdEntityDic.Values.Select(e => wireId(e.Id)).ToHashSet();
        foreach (var owner in new[] { game.Player1, game.Player2 })
        foreach (var zone in new[] { Zone.DECK, Zone.HAND, Zone.SETASIDE })
        {
            var expected = game.IdEntityDic.Values.Where(e => e.Controller == owner && e.Zone?.Type == zone)
                .Select(e => wireId(e.Id)).OrderBy(id => id).ToArray();
            // SETASIDE also holds disposable displays and retired anonymous actors, never playable engine cards.
            var actual = client.Entities.Where(p => p.Value.Get(GameTag.CONTROLLER) == owner.PlayerId
                && p.Value.Get(GameTag.ZONE) == (int)zone && (zone != Zone.SETASIDE || logicalIds.Contains(p.Key)))
                .Select(p => p.Key).OrderBy(id => id).ToArray();
            if (!expected.SequenceEqual(actual))
                yield return $"player{owner.PlayerId}:{zone} engine={expected.Length} client={actual.Length} missing=[{string.Join(",", expected.Except(actual))}] extra=[{string.Join(",", actual.Except(expected))}]";
            if (zone == Zone.HAND)
                foreach (var card in owner.HandZone)
                    if (client.Entities.TryGetValue(wireId(card.Id), out var shown)
                        && (shown.CardZone != (int)zone || shown.Get(GameTag.ZONE_POSITION) != card.ZonePosition + 1
                            || shown.CardPosition != card.ZonePosition + 1))
                        yield return $"player{owner.PlayerId}:HAND entity={wireId(card.Id)} position={shown.Get(GameTag.ZONE_POSITION)} actorPosition={shown.CardPosition} expected={card.ZonePosition + 1}";
        }
    }
}

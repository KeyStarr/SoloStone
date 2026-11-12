using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class LootHoarderProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_096", (g, p, id) => {
        var source = (Minion)Play(g, p, id); int top = p.DeckZone.TopCard.Id;
        PoolPilotProof.Resume(g, next => {
            Kill(next, (Minion)next.IdEntityDic[source.Id]);
            Core2026Proof.Check(next.Player1.HandZone.Single().Id == top, "death draws exact top card in original/clone/snapshot");
    });
        source = (Minion)Play(g, p, id); source.Silence(); int hand = p.HandZone.Count; Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == hand, "silenced death does not draw");
        source = (Minion)Play(g, p, id); Play(g, p, "EX1_049", source);
        Core2026Proof.Check(source.Zone == p.HandZone && p.HandZone.Count == hand + 1, "bounce does not trigger Deathrattle");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p); source = Summon(p, id); int deck = p.DeckZone.Count; Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "draw burns at full hand");
    }, legacyPoolMember: true);
}

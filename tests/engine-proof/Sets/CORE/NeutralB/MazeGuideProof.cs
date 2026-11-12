using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class MazeGuideProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_REV_308", (g, p, id) => {
        CoreCostProof.Count(g, id);
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = (Minion)Entity.FromCard(p, Cards.FromId(id)); Generic.SummonBlock(g, source, 1, null); var clone = g.CloneExact(history: true);
        Resolve(g, source); Resolve(clone, clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(p.BoardZone.SlotCount == 4 && p.BoardZone.GetSlot(source.ZonePosition + 1).Card.Cost == 2, "random two-cost minion beside source");
        Core2026Proof.Check(p.BoardZone.Select(c => c.Card.Id).SequenceEqual(clone.Player1.BoardZone.Select(c => c.Card.Id)), "seeded summon deterministic");
        StandardSetsProof.ParkBoard(p); source = Summon(p, id);
        LostCityPoolProof.WithPool(g, id, new[] { "CORE_EX1_506" }, () => {
            Resolve(g, source);
            Core2026Proof.Check(p.BoardZone.Count == 2 && p.HandZone.Count == 0
                && p.BoardZone.Single(m => m != source).Card.Id == "CORE_EX1_506", "generated Tidehunter does not execute Battlecry");
        });
        StandardSetsProof.ParkBoard(p);
        left = Summon(p, "CS2_182");
        var leftLocation = (BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990")); p.BoardZone.Add(leftLocation);
        source = Summon(p, id);
        var rightLocation = (BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990")); p.BoardZone.Add(rightLocation);
        right = Summon(p, "CS2_182");
        Resume(g, other => {
            Resolve(other, other.IdEntityDic[source.Id]); var board = other.Player1.BoardZone;
            Core2026Proof.Check(board.SlotCount == 6 && board.GetSlot(0).Id == left.Id && board.GetSlot(1).Id == leftLocation.Id
                && board.GetSlot(2).Id == source.Id && board.GetSlot(3) is Minion generated && generated.Card.Cost == 2
                && generated[GameTag.DISPLAYED_CREATOR] == source.Id && board.GetSlot(4).Id == rightLocation.Id && board.GetSlot(5).Id == right.Id,
                "partly occupied mixed board summons immediately right of Guide, before location");
        });
        StandardSetsProof.ParkBoard(p); source = Summon(p, id);
        p.BoardZone.Add((BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990")));
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Resolve(g, source); Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full mixed board blocks summon");
    });
}

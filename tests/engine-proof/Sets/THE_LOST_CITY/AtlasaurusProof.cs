using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class AtlasaurusProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_431", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play");
        Core2026Proof.Check(source.HasTaunt && source.IsRace(Race.BEAST) && p.BoardZone.SlotCount == 3, "Taunt Beast with no Battlecry summon");
        PoolPilotProof.Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        var output = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id);
        Batch9PoolProof.Generated(g, source, new[] { output }, 1);
        Core2026Proof.Check(output.Card.Cost >= 5 && output.HasTaunt && output.ZonePosition == 1 && p.BoardZone.GetSlot(0) == left && p.BoardZone.GetSlot(2) == right,
            "expensive Taunt replaces dead source between physical neighbors");
        Batch9PoolProof.CleanBoard(g, p);
        Batch9PoolProof.WithPools(g, id, 0, () => {
            source = Summon(p, id); var rng = g.Random.CaptureState(); Kill(g, source);
            Core2026Proof.Check(p.BoardZone.SlotCount == 0 && rng.Equals(g.Random.CaptureState()), "empty pool removes source without summon or RNG");
        });
        Batch9PoolProof.WithPools(g, id, 1, () => {
            source = Summon(p, id); Kill(g, source); Batch9PoolProof.Generated(g, source, p.BoardZone, 1);
        });
        Batch9PoolProof.CleanBoard(g, p); source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 0, "Silence removes Deathrattle");
        p.BoardZone.Add((BoardObject)Entity.FromCard(p, SabberStoneCore.Model.Cards.FromId("CORE_REV_990")));
        left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182");
        PoolPilotProof.Resolve(g, source, death: true);
        output = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id);
        Core2026Proof.Check(output.ZonePosition == source.ZonePosition + 1 && p.BoardZone.GetSlot(output.ZonePosition + 1) == right,
            "copied Deathrattle summons right of live source across physical location slot");
        Batch9PoolProof.CleanBoard(g, p);
        LostCityPoolProof.FullBoard(p); source = (Minion)Add(p, id); long before = g.Random.DrawCount;
        PoolPilotProof.Resolve(g, source, death: true);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && g.Random.DrawCount - before == 1, "copied Deathrattle respects location slot");
        Kill(g, p.BoardZone.Last()); source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Last().HasTaunt, "Deathrattle can fill its freed physical slot");
        End(g);
    });
}

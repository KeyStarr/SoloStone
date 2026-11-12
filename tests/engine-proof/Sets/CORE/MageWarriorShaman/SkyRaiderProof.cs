using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class SkyRaiderProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_DRG_024", (g, p, id) => {
        p.AddHeroAndPower(Cards.FromId("HERO_01"));
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && candidates.All(c => c.Type == CardType.MINION && c.IsRace(Race.PIRATE) && c.Id != id), "complete pool filter");
        var clone = g.CloneExact(history: true);
        var source = Play(g, p, id); Play(clone, clone.Player1, id);
        Generated(g, source, p.HandZone, 1);
        Core2026Proof.Check(p.HandZone.Select(c => c.Card.Id).SequenceEqual(clone.Player1.HandZone.Select(c => c.Card.Id)), "deterministic independent picks across clones");
        HandBoundaries(g, p, id, 1);
    });
}

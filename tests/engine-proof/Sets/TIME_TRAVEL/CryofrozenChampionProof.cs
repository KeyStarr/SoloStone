using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;
using static StandardSetsProof;

internal static class CryofrozenChampionProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_613", (g, p, id) => {
        g = GraftedKnightProof.NewGame(g); p = g.Player1;
        SourceExcluded(g, id);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.IsEmpty, "no generation until death");
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        var generated = p.HandZone.Single();
        Core2026Proof.Check(OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), source.Card.AssetId).Any(c => c.Id == generated.Card.Id)
            && generated.Card.Type == CardType.MINION && generated.Card.Rarity == Rarity.LEGENDARY
            && generated[GameTag.DISPLAYED_CREATOR] == source.Id && generated.Cost == Math.Max(0, generated.Card.Cost - 1), "admitted Legendary with entity discount and creator");
        var ordinary = Add(p, generated.Card.Id);
        Core2026Proof.Check(ordinary.Cost == ordinary.Card.Cost && ordinary.Card.Cost == generated.Card.Cost, "discount does not mutate shared Card or another copy");
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[generated.Id]));
        Core2026Proof.Check(p.UsedMana == Math.Max(0, generated.Card.Cost - 1), "discount survives snapshot and reduces paid cost");
        GraftedKnightProof.ClearMinions(g, p); EmptyHand(p);
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.IsEmpty, "Silence removes Deathrattle");
        Pool(g, id, 0, () => {
            var before = g.Random.CaptureState(); Kill(g, Summon(p, id));
            Core2026Proof.Check(p.HandZone.IsEmpty && g.Random.CaptureState().Equals(before), "empty pool creates no fallback and consumes no RNG");
        });
        Pool(g, id, 1, () => {
            Kill(g, Summon(p, id)); Kill(g, Summon(p, id));
            Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.Select(c => c.Card.Id).Distinct().Count() == 1
                && p.HandZone.All(c => c.Cost == Math.Max(0, c.Card.Cost - 1)), "singleton pool permits duplicate independent discounted entities");
        });
        EmptyHand(p); FillHand(p);
        var costs = p.HandZone.Select(c => c.Cost).ToArray();
        var random = g.Random.DrawCount; Kill(g, Summon(p, id));
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount == random + 1
            && p.HandZone.Select(c => c.Cost).SequenceEqual(costs), "full hand burns generated card without discounting existing card");
        EmptyHand(p); LocationProofs.Location(p); GraftedKnightProof.FullBoardRejects(g, id);
    });
}

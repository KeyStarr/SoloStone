using System.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B15PoolFiltersEMERALDDREAMProof
{
    [GroupProof("b15-pool-filters-emerald-dream-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("EDR_888", Pinned_MalorneCuratedWildGods);
        StandardSetsProof.Case("FIR_920", Unpinned_SmokeKeywordUnion);
    }
    private static void Pinned_MalorneCuratedWildGods(Game g, Controller p, string id)
    {
        var filter = OfflinePoolDeclarations.Cards.Single(d => d.CardId == id).Pools.Single().Filter;
        var ids = new[] { "EDR_031", "EDR_209", "EDR_421", "EDR_430", "EDR_480", "EDR_471", "EDR_527", "EDR_259", "EDR_489", "EDR_895", "EDR_819" };
        Core2026Proof.Check(filter.CardIds.SequenceEqual(ids.OrderBy(x => x, StringComparer.Ordinal)), "pinned exact eleven Wild Gods, no lore/type inference");
        var expected = OfflineCardPools.SampleDistinct(g.CloneExact(history: true), filter, p.HeroClass, Cards.FromId(id).AssetId);
        Play(g, p, id);
        var offers = p.Choice.Choices.ToArray();
        Core2026Proof.Check(offers.Select(i => g.IdEntityDic[i].Card.Id).SequenceEqual(expected.Select(c => c.Id)), "seeded any-class Wild God offer order");
        B15PoolTest.Resume(g, next => PlayerDecisionProof.Answer(next, offers[0]));
        Core2026Proof.Check(p.HandZone.Single().Cost == p.HandZone.Single().Card.Cost, "below four Imbues retains printed Cost");
        StandardSetsProof.ParkBoard(p); PoolPilotProof.EmptyHand(p); p[GameTag.IMBUE] = 4;
        Play(g, p, id); offers = p.Choice.Choices.ToArray(); PoolPilotProof.FillHand(p, 9);
        B15PoolTest.Resume(g, next => PlayerDecisionProof.Answer(next, offers[0]));
        var selected = g.IdEntityDic[offers[0]];
        Core2026Proof.Check(selected.Zone == p.HandZone && selected.Cost == 1, "four Imbues sets selected card to one");
    }
    private static void Unpinned_SmokeKeywordUnion(Game g, Controller p, string id)
    {
        var filter = OfflinePoolDeclarations.Cards.Single(d => d.CardId == id).Pools.Single().Filter;
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count > 3 && candidates.All(c => c.Type == CardType.MINION
            && (c[GameTag.COMBO] > 0 || c[GameTag.BATTLECRY] > 0 || c[GameTag.STEALTH] > 0)), "one eligible keyword union per definition");
        var expected = OfflineCardPools.SampleDistinct(g.CloneExact(history: true), filter, p.HeroClass, Cards.FromId(id).AssetId);
        Play(g, p, id);
        var offers = p.Choice.Choices.ToArray();
        Core2026Proof.Check(offers.Select(i => g.IdEntityDic[i].Card.Id).SequenceEqual(expected.Select(c => c.Id)), "seeded unweighted keyword union order");
        Core2026Proof.Check(offers.All(i => g.IdEntityDic[i][GameTag.DARK_GIFT_ENTITY] != 0 && !DarkGifts.Has(g.IdEntityDic[i])), "every offer has displayed stored gift, no unchosen grant");
        B15PoolTest.Resume(g, next => PlayerDecisionProof.Answer(next, offers[0]));
        Core2026Proof.Check(DarkGifts.Has(g.IdEntityDic[offers[0]]) && offers.Skip(1).All(i => !DarkGifts.Has(g.IdEntityDic[i])), "only chosen offer gets its gift");
        PoolPilotProof.EmptyHand(p);
        B15PoolTest.WithMembers(g, Array.Empty<string>(), () => {
            var before = g.Random.CaptureState(); Play(g, p, id);
            Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && g.Random.CaptureState().Equals(before), "empty keyword union creates no offers or gift draws");
        });
    }
}

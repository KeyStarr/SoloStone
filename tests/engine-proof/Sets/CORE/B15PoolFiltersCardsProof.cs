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

internal static class B15PoolFiltersCOREProof
{
    [GroupProof("b15-pool-filters-core-cards", ProofKind.Cards)]
    internal static void Run() => StandardSetsProof.Case("CORE_WW_374", Unpinned_CorpseFarmZeroAndFullBoard);
    private static void Unpinned_CorpseFarmZeroAndFullBoard(Game original, Controller _, string id)
    {
        foreach (int available in new[] { 0, 1, 8, 11 })
        {
            var g = DeathKnightC1Proof.NewGame(original.Seed); var p = g.Player1;
            CorpseRules.Gain(p, available);
            int spent = Math.Min(8, available);
            var filter = OfflinePoolFilter.Random(type: CardType.MINION).WithCost(spent);
            var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, Cards.FromId(id).AssetId);
            var random = g.Random.Clone(); var expected = candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
            B15PoolTest.Resume(g, next => Play(next, next.Player1, id));
            Core2026Proof.Check(p[GameTag.CORPSES_SPENT_THIS_GAME] == spent && CorpseRules.Available(p) == available - spent,
                "spends min(8,available), including zero");
            Core2026Proof.Check(p.BoardZone.Count == (expected == null ? 0 : 1)
                && (expected == null || p.BoardZone.Single().Card.Id == expected.Id), "seeded exact corpse Cost including zero; no nearest tier");
            Core2026Proof.Check(g.Random.CaptureState().Equals(random.CaptureState()), "one exact-tier RNG pick");
        }
        var full = DeathKnightC1Proof.NewGame(original.Seed); var owner = full.Player1;
        LostCityPoolProof.FullBoard(owner); CorpseRules.Gain(owner, 10);
        var source = Add(owner, id);
        Core2026Proof.Check(!owner.Options().Any(t => t.Source?.Id == source.Id), "ordinary hand play blocked on full board");
        B15PoolTest.Resume(full, next => PoolPilotProof.Resolve(next, next.IdEntityDic[source.Id]));
        Core2026Proof.Check(CorpseRules.Available(owner) == 2 && owner.BoardZone.SlotCount == 7, "forced full-board resolution spends eight without refund");
        var empty = DeathKnightC1Proof.NewGame(original.Seed);
        B15PoolTest.WithMembers(empty, Array.Empty<string>(), () => {
            CorpseRules.Gain(empty.Player1, 8); Play(empty, empty.Player1, id);
            Core2026Proof.Check(CorpseRules.Available(empty.Player1) == 0 && empty.Player1.BoardZone.Count == 0, "empty exact bucket still consumes payment");
        });
    }
}

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

internal static class B15PoolFiltersBEProof
{
    [GroupProof("b15-pool-filters-be-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("BE_298", Unpinned_ScionRuneSlotsAndMulticolour);
        StandardSetsProof.Case("BE_703", Unpinned_DrestagathBurnFallback);
    }
    private static void Unpinned_DrestagathBurnFallback(Game g, Controller p, string id)
    {
        KnownMetaProofFixture.UseBlackEmpire(g);
        var body = (Minion)Play(g, p, id);
        var rows = OfflinePoolAdmission.Evaluate(g.OfflinePools).Cards.Single(c => c.CardId == id).PoolRequirements;
        OfflinePoolAdmission.Evaluate(g.OfflinePools,
            new[] { OfflinePoolDeclarations.Cards.Single(c => c.CardId == id) }).ValidateRegistered();
        Core2026Proof.Check(rows.Single().Count == 4 && !rows.Single().AllowThinExactCost &&
            rows.Single().MinimumRequiredCandidates == 3 && rows.Single().UnimplementedCuratedCardIds.Count == 0,
            "four Old Gods admitted; curated exemption lapsed and minimum three enforced");
        var definition = Cards.FromId("BE_013");
        Core2026Proof.Check(definition[(GameTag)4754] == 1, "pinned real Old God tutor fixture");
        var god = Entity.FromCard(p, definition, zone: p.DeckZone);
        B15PoolTest.Resume(g, next => Kill(next, (Minion)next.IdEntityDic[body.Id]));
        Core2026Proof.Check(god.Zone == p.HandZone && p.HandZone.Count == 1, "tutor draws exact matching real deck entity");
        PoolPilotProof.EmptyHand(p); body = (Minion)Play(g, p, id);
        god = Entity.FromCard(p, definition, zone: p.DeckZone); PoolPilotProof.FillHand(p);
        B15PoolTest.Resume(g, next => Kill(next, (Minion)next.IdEntityDic[body.Id]));
        Core2026Proof.Check(god.Zone == p.GraveyardZone && p.HandZone.Count == 10, "full hand burns matching deck entity without extra award");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.ClassDeck(p);
        body = (Minion)Play(g, p, id);
        int fatigue = p.Hero.Damage; var random = g.Random.Clone();
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass,
            OfflinePoolDeclarations.Cards.Single(d => d.CardId == id).Pools.Single().Filter, Cards.FromId(id).AssetId);
        var expected = candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
        Kill(g, body);
        Core2026Proof.Check(p.HandZone.Count == (expected == null ? 0 : 1) && (expected == null || p.HandZone.Single().Card.Id == expected.Id)
            && p.Hero.Damage == fatigue && g.Random.CaptureState().Equals(random.CaptureState()), "no-match branch samples declared pool, empty pool skips without fatigue or RNG");
        Core2026Proof.Check(candidates.Count == 4 && p.HandZone.Count == 1 && p.Choice == null,
            "four-God fallback generates only an admitted Old God without a decision");
        B15PoolTest.WithMembers(g, Array.Empty<string>(), () => {
            PoolPilotProof.EmptyHand(p); body = (Minion)Play(g, p, id);
            var before = g.Random.CaptureState(); Kill(g, body);
            Core2026Proof.Check(p.HandZone.Count == 0 && g.Random.CaptureState().Equals(before), "empty fixture fallback still skips without RNG");
        });
        Console.WriteLine("Drestagath fixed fallback: 4/4 admitted; minimum three, no curated exemption");
    }
    private static void Unpinned_ScionRuneSlotsAndMulticolour(Game g, Controller p, string id)
    {
        var colours = new[] { GameTag.COST_BLOOD, GameTag.COST_FROST, GameTag.COST_UNHOLY };
        var pools = OfflinePoolDeclarations.Cards.Single(d => d.CardId == id).Pools;
        var random = g.Random.Clone();
        var expected = new List<Card>();
        foreach (var pool in pools)
        {
            var candidates = OfflineCardPools.Candidates(g, p.HeroClass, pool.Filter, Cards.FromId(id).AssetId)
                .Where(c => colours.Sum(tag => c[tag]) <= 2 && expected.All(prior => prior.Id != c.Id)).ToArray();
            if (candidates.Length != 0) expected.Add(candidates[random.Next(candidates.Length)]);
        }
        var body = (Minion)Play(g, p, id);
        var offers = p.Choice.Choices.ToArray();
        Core2026Proof.Check(offers.Select(i => g.IdEntityDic[i].Card.Id).SequenceEqual(expected.Select(c => c.Id)), "seeded Blood/Frost/Unholy slots without duplicate weighting");
        Core2026Proof.Check(g.Random.CaptureState().Equals(random.CaptureState()), "Scion exactly one RNG draw per nonempty category");
        foreach (int offer in offers)
        {
            var branch = g.CloneExact(history: true);
            B15PoolTest.Resume(branch, next => PlayerDecisionProof.Answer(next, offer));
            Core2026Proof.Check(branch.HistoryFacts.Query.Discoveries().Count() == 1, "Scion selection records one real Discover");
            var selected = branch.IdEntityDic[offer]; var scion = (Minion)branch.IdEntityDic[body.Id];
            Core2026Proof.Check(selected.Zone == branch.Player1.HandZone && scion.HasLifeSteal == (selected.Card[GameTag.COST_BLOOD] > 0)
                && (scion[GameTag.REBORN] > 0) == (selected.Card[GameTag.COST_FROST] > 0)
                && scion.IsRush == (selected.Card[GameTag.COST_UNHOLY] > 0), "every chosen rune colour grants corresponding keyword");
            scion.Silence();
            Core2026Proof.Check(!scion.HasLifeSteal && scion[GameTag.REBORN] == 0 && !scion.IsRush, "Silence clears rune grants");
        }
        PoolPilotProof.FillHand(p);
        B15PoolTest.Resume(g, next => PlayerDecisionProof.Answer(next, offers[0]));
        Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[offers[0]].Zone == p.GraveyardZone, "full hand burns selection while granting keywords");
        var empty = GiftedDiscoverProof.New(g.Seed, CardClass.ROGUE);
        B15PoolTest.WithMembers(empty, Array.Empty<string>(), () => {
            var before = empty.Random.CaptureState(); Play(empty, empty.Player1, id);
            Core2026Proof.Check(empty.Player1.Choice == null && empty.Random.CaptureState().Equals(before), "empty rune slots create no decision or RNG");
        });
    }
}

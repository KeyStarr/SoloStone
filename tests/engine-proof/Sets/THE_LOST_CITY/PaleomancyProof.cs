using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static LostCityPoolProof;

internal static class PaleomancyProof
{
    // PINNED-RULE-ONLY: NOTES/rules-evidence/corpse-spend-automatic.yaml (tier C Corpse wiki; tier E Ginx/Out of Games)
    // and NOTES/rules-evidence/tlc_ed-tlc434-paleomancy.yaml. Spend 5 automatically when affordable, otherwise normal Discover.
    // Supplied builder rule: keep offers in offer order; overflow uses generic hand rules. Retail hand/wire order remains open.
    internal static void Run() => DeathKnightC1Proof.Case("TLC_434", (g, p, id) => {
        // Catch keep-all paths that enroll only the first definition (or only cards that fit in hand).
        foreach (var members in new[] {
            new[] { "CORE_EX1_012", "TIME_614", "CATA_469" },
            new[] { "CORE_EX1_012", "CORE_EX1_059", "TIME_614" }
        })
        foreach (int hand in new[] { 0, 9, 10 })
        {
            var fresh = DeathKnightC1Proof.NewGame(g.Seed); var owner = fresh.Player1;
            PoolPilotProof.FillHand(owner, hand); owner[GameTag.CORPSES] = 5;
            Core2026Proof.Check(!fresh.HistoryFacts.HasConsumers && members.Count(x => Cards.FromId(x).ConsumesHistory) == 1,
                "only second/third offer Liferender can enroll this fresh game");
            PoolPilotProof.Resume(fresh, next => WithPool(next, id, members, () => {
                var source = Entity.FromCard(next.Player1, Cards.FromId(id)); PoolPilotProof.Resolve(next, source);
                var discovery = next.HistoryFacts.Query.Discoveries().Single();
                Core2026Proof.Check(discovery.KeepAll && discovery.SelectedDefinitions.Select(d => d.DbfId)
                    .SequenceEqual(members.Select(x => Cards.FromId(x).AssetId)), "keep-all records every offered definition once in order");
                Core2026Proof.Check(next.HistoryFacts.HasConsumers && HsSim.Canonical.State(next).Contains("|history-facts-v1:"),
                    "second/third awarded history consumer enables canonical history, including overflow");
                var liferender = next.IdEntityDic.Values.Single(e => e.Card.Id == "TIME_614");
                Core2026Proof.Check(hand == 0 ? liferender.Zone == next.Player1.HandZone : liferender.Zone == next.Player1.GraveyardZone,
                    "history consumer enters hand or burns in actual offer order");
            }));
        }
        var pool = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), Cards.FromId(id).AssetId);
        Core2026Proof.Check(pool.Count >= 3 && pool.All(c => c.Type == CardType.MINION && c.IsRace(Race.UNDEAD)), "at least three admitted controller/neutral Undead");
        PoolPilotProof.SourceExcluded(g, id); Empty(g, p, id);
        foreach (int corpses in new[] { 0, 4 })
        {
            p[GameTag.CORPSES] = corpses; p[GameTag.CORPSES_SPENT_THIS_GAME] = 0;
            var source = Play(g, p, id); var offers = Offers(g, p, source, c => c.IsRace(Race.UNDEAD));
            Core2026Proof.Check(CorpseRules.Available(p) == corpses && p.HandZone.Count == 0, "insufficient Corpses remain unspent while normal Discover waits");
            int selected = offers[1]; PoolPilotProof.Resume(g, next => {
                int discoveries = next.HistoryFacts.Query.Discoveries().Count();
                Answer(next, selected); var owner = next.Player1;
                Core2026Proof.Check(owner.HandZone.Single().Id == selected && CorpseRules.Available(owner) == corpses, "normal Discover keeps only selected entity without spending");
                var discovery = next.HistoryFacts.Query.Discoveries().Last();
                Core2026Proof.Check(next.HistoryFacts.Query.Discoveries().Count() == discoveries + 1 && !discovery.KeepAll
                    && discovery.SelectionIds.SequenceEqual(new[] { selected }) && discovery.SelectedDefinitions.Single().DbfId == next.IdEntityDic[selected].Card.AssetId,
                    "normal Discover records exactly the selected offer");
            }); PoolPilotProof.EmptyHand(p);
        }
        foreach (int corpses in new[] { 5, 6 })
        foreach (int hand in new[] { 0, 8, 9 })
        {
            p[GameTag.CORPSES] = corpses; p[GameTag.CORPSES_SPENT_THIS_GAME] = 0;
            PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, hand);
            var before = g.CloneExact(history: true); var expected = OfflineCardPools.SampleDistinct(before, Filter(id), p.HeroClass, Cards.FromId(id).AssetId).Select(c => c.Id).ToArray();
            PoolPilotProof.Resume(g, next => {
                int discoveries = next.HistoryFacts.Query.Discoveries().Count();
                var owner = next.Player1; var source = Play(next, owner, id);
                var generated = next.IdEntityDic.Values.Where(e => e[GameTag.DISPLAYED_CREATOR] == source.Id && e.Card.Type == CardType.MINION).OrderBy(e => e.Id).ToArray();
                Core2026Proof.Check(owner.Choice == null && !next.Resolution.HasSuspendedEffect && CorpseRules.Available(owner) == corpses - 5
                    && owner[GameTag.CORPSES_SPENT_THIS_GAME] == 5, "affordable clause spends exactly five automatically with no prompt");
                Core2026Proof.Check(generated.Select(c => c.Card.Id).SequenceEqual(expected) && generated.All(c => c.Card.IsRace(Race.UNDEAD)), "same distinct offline offers, in deterministic offer order");
                var discovery = next.HistoryFacts.Query.Discoveries().Last();
                Core2026Proof.Check(next.HistoryFacts.Query.Discoveries().Count() == discoveries + 1 && discovery.KeepAll
                    && discovery.SelectedDefinitions.Select(d => d.DbfId).SequenceEqual(generated.Select(e => e.Card.AssetId)),
                    "automatic Discover records all three definitions in one keep-all fact");
                Core2026Proof.Check(owner.HandZone.Skip(hand).Select(c => c.Id).SequenceEqual(generated.Take(10-hand).Select(c => c.Id))
                    && generated.Skip(10-hand).All(c => c.Zone == owner.GraveyardZone) && owner.HandZone.Count == Math.Min(10, hand+3), "offer order preserved; overflow burns remaining offers");
            });
        }
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p);
        p[GameTag.CORPSES] = 5; p[GameTag.CORPSES_SPENT_THIS_GAME] = 0;
        var automatic = Entity.FromCard(p, Cards.FromId(id)); PoolPilotProof.Resolve(g, automatic);
        var burned = g.IdEntityDic.Values.Where(e => e[GameTag.DISPLAYED_CREATOR] == automatic.Id).ToArray();
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Choice == null && CorpseRules.Available(p) == 0
            && burned.Length == 3 && burned.All(e => e.Zone == p.GraveyardZone), "zero hand space still spends and burns all offers");
        PoolPilotProof.EmptyHand(p); p[GameTag.CORPSES] = 4; p[GameTag.CORPSES_SPENT_THIS_GAME] = 0;
        Play(g, p, id); int chosen = p.Choice.Choices.First(); PoolPilotProof.FillHand(p); Answer(g, chosen);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[chosen].Zone == p.GraveyardZone && CorpseRules.Available(p) == 4, "full hand normal Discover burns choice without corpse spend");
    });
}

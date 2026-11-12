using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SpitefulChefProof
{
    // STILL-UNPINNED: data/rules-evidence/cost-sampling/chef-mana-threshold.yaml.
    // Tier C Manathirst analogy + tier E Chef preview; permanent maximum crystals include spent/locked, exclude temporary.
    internal static void Run() => StandardSetsProof.Case("JAIL_507", Unpinned_SpitefulChef);
    private static void Unpinned_SpitefulChef(Game g, Controller p, string id)
    {
        var declarations = OfflinePoolDeclarations.Cards.Single(d => d.CardId == id);
        Core2026Proof.Check(declarations.Pools.Select(r => r.Filter).SequenceEqual(new[] { 2, 6 }.Select(cost =>
            OfflinePoolFilter.Random(type: CardType.MINION, minCost: cost, maxCost: cost, keyword: GameTag.TAUNT))),
            "two declared any-class printed-cost Taunt pools");
        foreach (var state in new[] { (Mana: 9, Spent: 0, Locked: 0, Temporary: 1, Cost: 2),
            (Mana: 10, Spent: 6, Locked: 0, Temporary: 0, Cost: 6),
            (Mana: 10, Spent: 0, Locked: 5, Temporary: 0, Cost: 6),
            (Mana: 8, Spent: 0, Locked: 0, Temporary: 2, Cost: 2) })
        {
            ParkBoard(p); PoolPilotProof.EmptyHand(p);
            var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_120");
            var source = Add(p, id);
            p.BaseMana = state.Mana; p.UsedMana = state.Spent; p.OverloadLocked = state.Locked; p.TemporaryMana = state.Temporary;
            var filter = OfflinePoolFilter.Random(type: CardType.MINION, minCost: state.Cost, maxCost: state.Cost, keyword: GameTag.TAUNT);
            var pool = OfflineCardPools.Candidates(g, p.HeroClass, filter, source.Card.AssetId);
            g.Random.SetSeed(g.Seed);
            string expected = pool[new Util.DeepCloneableRandom(g.Seed).Next(pool.Count)].Id;
            PoolPilotProof.Resume(g, next => Core2026Proof.Check(next.Process(PlayCardTask.Any(next.Player1,
                next.IdEntityDic[source.Id], zonePosition: 2)), "Chef accepted with specified Mana state"));
            var output = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id);
            Core2026Proof.Check(source.Card.ATK == 3 && source.Card.Health == 2 && source.Card.Cost == 3
                && output.Card.Id == expected && output.Card.Cost == state.Cost && output.Card.Type == CardType.MINION
                && output.HasTaunt && output.Card.Id != id && output.AttackDamage == output.Card.ATK && output.Health == output.Card.Health,
                "seeded eligible summon, exact stats, threshold uses permanent maximum");
            Core2026Proof.Check(p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { location.Id, left.Id, source.Id, output.Id, right.Id }),
                "summon immediately right of living Chef, locations occupy physical slots");
            p.OverloadLocked = 0; p.TemporaryMana = 0;
        }
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        LocationProofs.Location(p); while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1
            && p.BoardZone.All(m => m[GameTag.DISPLAYED_CREATOR] == 0), "Chef takes seventh physical slot; no overflow summon");
        ParkBoard(p); Summon(p, id);
        Core2026Proof.Check(p.BoardZone.Count == 1, "ordinary summon does not execute Battlecry");
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        var fullHandSource = Add(p, id); PoolPilotProof.FillHand(p);
        PlayEntity(g, p, fullHandSource);
        Core2026Proof.Check(p.HandZone.Count == 9 && p.BoardZone.Count == 2 && p.BoardZone.Last().HasTaunt,
            "full hand does not block board generation or receive the summoned card");
    }
}

using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LifeCycleProof
{
    // STILL-UNPINNED: data/rules-evidence/discover-pools/life-cycle-{death-order,empty-cost}.yaml.
    // Tier C Life Cycle Notes + owner assumption: printed Cost/controller/slot snapshot, normal deaths before replacement, empty bucket fizzles.
    internal static void Run() => StandardSetsProof.Case("TLC_235", Unpinned_LifeCycle);
    private static void Unpinned_LifeCycle(Game g, Controller p, string id)
    {
        var declaration = OfflinePoolDeclarations.Cards.Single(d => d.CardId == id).Pools.Single();
        Core2026Proof.Check(declaration.Filter == OfflinePoolFilter.Random(type: CardType.MINION)
            && declaration.CostBinding == SabberStoneCore.Tasks.SimpleTasks.PoolCostBinding.StackNumber,
            "declared dynamic any-class same-cost minion pool");
        foreach (bool opponent in new[] { false, true })
        {
            var owner = opponent ? p.Opponent : p;
            ParkBoard(owner); PoolPilotProof.EmptyHand(p);
            var location = LocationProofs.Location(owner); var left = Summon(owner, "CS2_182");
            var target = Summon(owner, "CS2_120"); var right = Summon(owner, "CS2_200");
            target.Cost = 8; target.AttackDamage = 9; target.Damage = 1;
            TargetRules(g, p, id, opponent ? target : Summon(p.Opponent, "CS2_120"), opponent ? Summon(p, "CS2_120") : target, false);
            PoolPilotProof.EmptyHand(p);
            var pool = OfflineCardPools.Candidates(g, owner.HeroClass, OfflinePoolFilter.Random(type: CardType.MINION, minCost: 2, maxCost: 2), Cards.FromId(id).AssetId);
            g.Random.SetSeed(g.Seed);
            string expected = pool[new Util.DeepCloneableRandom(g.Seed).Next(pool.Count)].Id;
            var source = Add(p, id);
            PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id], (ICharacter)next.IdEntityDic[target.Id]));
            var output = owner.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id);
            Core2026Proof.Check(target.Zone == owner.GraveyardZone && output.Card.Id == expected && output.Card.Cost == 2
                && output.Controller == owner && output.AttackDamage == output.Card.ATK && output.Health == output.Card.Health,
                "destroy actual target; seeded fresh printed-cost replacement for original controller");
            Core2026Proof.Check(owner.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { location.Id, left.Id, output.Id, right.Id }),
                "same physical slot, preserving neighboring location and minions: " + string.Join(",", owner.BoardZone.Slots.Select(e => e.Id + ":" + e.Card.Id))
                    + " expected=" + string.Join(",", new[] { location.Id, left.Id, output.Id, right.Id }));
            ParkBoard(p); ParkBoard(p.Opponent);
        }
        var egg = Summon(p, "TLC_237"); LocationProofs.Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, id, egg);
        Core2026Proof.Check(egg.Zone == p.GraveyardZone && p.BoardZone.SlotCount == 7
            && p.BoardZone.Count(m => m.Card.Id == "TLC_237t") == 1
            && p.BoardZone.All(m => m[GameTag.DISPLAYED_CREATOR] != p.LastCardPlayed), "Deathrattle fills freed slot before replacement fails");
        ParkBoard(p);
        var golem = Summon(p, "EX1_556"); var spell = Play(g, p, id, golem);
        Core2026Proof.Check(p.BoardZone.Any(m => m.Card.Id == "skele21")
            && p.BoardZone.Any(m => m[GameTag.DISPLAYED_CREATOR] == spell.Id && m.Card.Cost == 3), "normal Deathrattle and replacement both resolve when capacity permits");
        var token = p.BoardZone.Single(m => m.Card.Id == "skele21");
        var replacement = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == spell.Id);
        var timeline = g.PowerHistory.Full.ToArray();
        int tokenCreated = Array.FindIndex(timeline, e => e is PowerHistoryFullEntity full && full.Entity.Id == token.Id);
        int replacementCreated = Array.FindIndex(timeline, e => e is PowerHistoryFullEntity full && full.Entity.Id == replacement.Id);
        Core2026Proof.Check(tokenCreated >= 0 && replacementCreated > tokenCreated,
            "Deathrattle summon must actually resolve before replacement creation, not merely produce the same final board");
        ParkBoard(p);
        var zero = Summon(p, "CS2_231"); spell = Play(g, p, id, zero);
        Core2026Proof.Check(zero.Zone == p.GraveyardZone && p.BoardZone.Single().Card.Id == "CORE_GIL_531"
            && p.BoardZone.Single().Card.Cost == 0, "zero printed Cost is not treated as an empty pool");
        ParkBoard(p);
        var missing = Summon(p, "EX1_620"); // Molten Giant, printed 20; no admitted 20-Cost minions.
        var before = g.Random.CaptureState(); Play(g, p, id, missing);
        Core2026Proof.Check(missing.Zone == p.GraveyardZone && p.BoardZone.IsEmpty && g.Random.CaptureState().Equals(before),
            "empty same-cost bucket still destroys without substitute, refund or RNG");
        PoolPilotProof.EmptyHand(p); var fullHandTarget = Summon(p, "CS2_120");
        var fullHandSource = Add(p, id); PoolPilotProof.FillHand(p);
        PlayEntity(g, p, fullHandSource, fullHandTarget);
        Core2026Proof.Check(fullHandTarget.Zone == p.GraveyardZone && p.HandZone.Count == 9
            && p.BoardZone.Single().Card.Cost == 2, "full hand does not interfere with the board replacement");
    }
}

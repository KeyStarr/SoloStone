using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B14K1NeutralAProof
{
    [GroupProof("b14-k1-neutral-a-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("BE_999", Unpinned_PowerTiger);
        B14K1EmeraldDreamProof.Run();
        B14K1TimeTravelProof.Run();
    }

    // data/rules-evidence/forced-attack/{forced-budget,forced-readiness}.yaml: PINNED-RULE-ONLY, tiers A/C.
    // forced-zero-cant-attack.yaml: owner-approved working_assumption, tiers C/E; forced combat ignores readiness.
    private static void Unpinned_PowerTiger(Game g, Controller p, string id)
    {
        bool nonFirst = false, heroPicked = false;
        foreach (bool reverse in new[] { false, true })
        foreach (int seed in new[] { 1, 2, 3 })
        {
            ForcedCardProof.Clean(g);
            PoolPilotProof.EmptyHand(p);
            var source = (Minion)Play(g, p, id);
            source.BaseHealth = 100;
            var first = Summon(p.Opponent, "CS2_182"); first.BaseHealth = 100;
            // Duplicate definitions remain distinct random candidates; physical order differs from age.
            var second = (Minion)Entity.FromCard(p.Opponent, Cards.FromId("CS2_182"));
            SabberStoneCore.Actions.Generic.SummonBlock(g, second, reverse ? 0 : -1, null);
            second.BaseHealth = 100;
            var dormant = Summon(p.Opponent, "TIME_046");
            var location = LocationProofs.Location(p.Opponent);
            int cursor = g.HistoryFacts.Count;
            g.SetRandomSeed(seed);
            var oracle = g.CloneExact(history: true);
            int[] pool = p.Opponent.BoardZone.Where(m => !m.Untouchable).Select(m => m.Id).Append(p.Opponent.Hero.Id).ToArray();
            int expected = pool[oracle.Random.Next(pool.Length)];
            nonFirst |= expected != pool[0]; heroPicked |= expected == p.Opponent.Hero.Id;
            PoolPilotProof.Resume(g, next => {
                End(next);
                var attack = next.HistoryFacts.Query.AttacksStarted().Single(f => f.Sequence > cursor);
                var tiger = (Minion)next.IdEntityDic[source.Id];
                Core2026Proof.Check(attack.AttackerId == source.Id && attack.TargetId == expected
                    && tiger.NumAttacksThisTurn == 0, "seeded opponent-turn forced attack preserves ordinary budget; physical order differs from age");
                Core2026Proof.Check(((Minion)next.IdEntityDic[dormant.Id]).Damage == 0
                    && ((BoardObject)next.IdEntityDic[location.Id]).Durability == location.Durability,
                    "Dormant and location are excluded");
            });
            cursor = g.HistoryFacts.Count;
            End(g);
            Core2026Proof.Check(g.HistoryFacts.Query.AttacksStarted().Count(f => f.Sequence > cursor && f.AttackerId == source.Id) == 1,
                "also attacks at owner's start, not at end");
            source.Silence(); cursor = g.HistoryFacts.Count; End(g); End(g);
            Core2026Proof.Check(!g.HistoryFacts.Query.AttacksStarted().Any(f => f.Sequence > cursor && f.AttackerId == source.Id),
                "Silence removes both turn-start subscriptions");
        }
        Core2026Proof.Check(nonFirst && heroPicked, "seed coverage includes nonfirst enemy and hero");
        ForcedCardProof.Clean(g);
        var zero = Summon(p, id); zero.AttackDamage = 0; zero.IsFrozen = true; zero[GameTag.CANT_ATTACK] = 1;
        zero.NumAttacksThisTurn = 1;
        var enemy = Summon(p.Opponent, "CS2_182");
        bool retaliates = false;
        foreach (int seed in new[] { 1, 2, 3 })
        {
            g.SetRandomSeed(seed);
            if (g.CloneExact(history: true).Random.Next(2) == 0) { retaliates = true; break; }
        }
        Core2026Proof.Check(retaliates, "zero-Attack fixture selects minion for retaliation");
        int before = g.HistoryFacts.Count; End(g);
        Core2026Proof.Check(g.HistoryFacts.Query.AttacksStarted().Count(f => f.Sequence > before && f.AttackerId == zero.Id) == 1
            && zero.NumAttacksThisTurn == 0 && enemy.Damage == 0 && zero.Damage == 4,
            "Unpinned_PowerTiger: zero Attack, Frozen, Can't attack and spent allowance do not block forced combat");
        ForcedCardProof.Clean(g);
        var live = Summon(p, id); before = g.HistoryFacts.Count; End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage >= 6
            && g.HistoryFacts.Query.AttacksStarted().Single(f => f.Sequence > before).TargetId == p.Opponent.Hero.Id,
            "empty enemy minion board still attacks hero");
        Kill(g, live); before = g.HistoryFacts.Count; End(g); End(g);
        Core2026Proof.Check(!g.HistoryFacts.Query.AttacksStarted().Any(f => f.Sequence > before), "departure removes trigger");
        var held = Add(p, id); before = g.HistoryFacts.Count; End(g); End(g);
        Core2026Proof.Check(held.Zone == p.HandZone && !g.HistoryFacts.Query.AttacksStarted().Any(f => f.Sequence > before),
            "hand source cannot trigger");
    }
}

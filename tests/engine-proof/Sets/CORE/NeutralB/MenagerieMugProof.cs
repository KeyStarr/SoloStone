using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;

internal static class MenagerieMugProof
{
    // Tier C multi-type notes; owner assumptions: minion-types/menagerie-type-matching.yaml, mug-random-selection.yaml.
    internal static void Run() => StandardSetsProof.Case("CORE_WON_141", Unpinned_MenagerieMugMatching);
    private static void Unpinned_MenagerieMugMatching(Game g, Controller p, string id)
    {
        var older = Summon(p, "CS2_171"); var newer = Summon(p, "CS2_168");
        p.BoardZone.Add(p.BoardZone.Remove(newer), 0);
        Core2026Proof.Check(older.OrderOfPlay < newer.OrderOfPlay && newer.ZonePosition < older.ZonePosition, "newer recipient left of older recipient");
        Play(g, p, id);
        var grants = g.IdEntityDic.Values.OfType<Enchantment>().Where(e => e.Card.Id == "WON_141e")
            .OrderBy(e => e.Id).Select(e => e.Target.Id);
        Core2026Proof.Check(grants.SequenceEqual(new[] { older.Id, newer.Id }), "Mug emits buffs in play order despite reversed board order");
        StandardSetsProof.ParkBoard(p); EmptyHand(p);
        var none = Summon(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(none.AttackDamage == 4 && none.Health == 5, "untyped minions excluded");
        StandardSetsProof.ParkBoard(p);
        var a = Summon(p, "TLC_243"); var b = Summon(p, "TLC_243"); var beast = Summon(p, "CS2_171");
        Play(g, p, id);
        Core2026Proof.Check(a.AttackDamage == a.Card.ATK + 1 && b.AttackDamage == b.Card.ATK + 1
            && beast.AttackDamage == beast.Card.ATK + 1, "dual recipients assigned separate Elemental and Dragon roles");
        for (int reverse = 0; reverse < 2; reverse++)
        {
            StandardSetsProof.ParkBoard(p); EmptyHand(p);
            a = Summon(p, "TLC_243"); b = Summon(p, "CS2_033"); var dragon = Summon(p, "EX1_561"); beast = Summon(p, "CS2_171");
            if (reverse == 1) p.BoardZone.Add(p.BoardZone.Remove(a), 3);
            var ordered = p.BoardZone.ToArray();
            // Exactly three valid size-three recipient sets. Count each once, even when a dual type has alternatives.
            var sets = new[] { new[] { a.Id, b.Id, beast.Id }, new[] { a.Id, dragon.Id, beast.Id }, new[] { b.Id, dragon.Id, beast.Id } }
                .OrderBy(set => set.Sum(entity => 1 << Array.FindIndex(ordered, m => m.Id == entity))).ToArray();
            var rng = g.Random.Clone(); var expected = sets[rng.Next(3)];
            Resume(g, next => {
                Play(next, next.Player1, id);
                foreach (var m in ordered)
                {
                    var actual = (Minion)next.IdEntityDic[m.Id];
                    int buff = expected.Contains(m.Id) ? 1 : 0;
                    Core2026Proof.Check(actual.AttackDamage == m.Card.ATK + buff && actual.Health == m.Card.Health + buff, $"uniform seeded maximum subset: seed={g.Seed} reverse={reverse} expected={string.Join(',', expected)} actual={string.Join(',', next.Player1.BoardZone.Select(x => x.Id + ":" + x.AttackDamage))} draws={next.Random.DrawCount}/{g.Random.DrawCount}");
                }
                Core2026Proof.Check(next.Player1.BoardZone.Single(m => m.Card.Id == id).AttackDamage == 3, "untyped source excluded");
            });
        }
        StandardSetsProof.ParkBoard(p); EmptyHand(p);
        var all = Summon(p, "GIL_681"); a = Summon(p, "CS2_171"); b = Summon(p, "CS2_171");
        var rngAll = g.Random.Clone(); int selectedAll = rngAll.Next(2);
        var enemy = Summon(p.Opponent, "CS2_168");
        Play(g, p, id);
        Core2026Proof.Check(all.AttackDamage == all.Card.ATK + 1 && a.AttackDamage == a.Card.ATK + (selectedAll == 0 ? 1 : 0)
            && b.AttackDamage == b.Card.ATK + (selectedAll == 1 ? 1 : 0), "All supplies unused role; seeded duplicate Beast recipient");
        Core2026Proof.Check(enemy.AttackDamage == enemy.Card.ATK, "enemy never selected");
        StandardSetsProof.ParkBoard(p); EmptyHand(p); LocationProofs.Location(p);
        var distinct = new[] { Summon(p, "CS2_171"), Summon(p, "CS2_168"), Summon(p, "CS2_033"), Summon(p, "EX1_561") };
        var rngCap = g.Random.Clone(); int excluded = new[] { 3, 2, 1, 0 }[rngCap.Next(4)];
        Play(g, p, id);
        for (int i = 0; i < 4; i++) Core2026Proof.Check(distinct[i].AttackDamage == distinct[i].Card.ATK + (i == excluded ? 0 : 1), "Mug caps at three even with four distinct types");
        for (int reverse = 0; reverse < 2; reverse++)
        {
            StandardSetsProof.ParkBoard(p); EmptyHand(p);
            var weighted = new[] { Summon(p, "TLC_243"), Summon(p, "GIL_681"), Summon(p, "CS2_171"), Summon(p, "CS2_168") };
            if (reverse == 1) p.BoardZone.Add(p.BoardZone.Remove(weighted[0]), 3);
            var ordered = p.BoardZone.ToArray(); var rng = g.Random.Clone(); int omitted = new[] { 3, 2, 1, 0 }[rng.Next(4)];
            Play(g, p, id);
            for (int i = 0; i < 4; i++) Core2026Proof.Check(ordered[i].AttackDamage == ordered[i].Card.ATK + (i == omitted ? 0 : 1),
                "uniform recipient sets despite unequal dual/All assignment counts, reordered board");
        }
        StandardSetsProof.ParkBoard(p); EmptyHand(p);
        var allRoles = new[] { Summon(p, "GIL_681"), Summon(p, "GIL_681"), Summon(p, "CS2_171") }; Play(g, p, id);
        Core2026Proof.Check(allRoles.All(m => m.AttackDamage == m.Card.ATK + 1 && m.Health == m.Card.Health + 1), "each All gets independent unused role and one buff");
    }
}

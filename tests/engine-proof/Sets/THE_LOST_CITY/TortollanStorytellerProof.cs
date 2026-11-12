using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;

internal static class TortollanStorytellerProof
{
    // Tier C multi-type/turn rules; owner assumptions: minion-types/menagerie-type-matching.yaml, storyteller-end-turn.yaml.
    internal static void Run() => StandardSetsProof.Case("TLC_254", Unpinned_TortollanStorytellerEndTurn);
    private static void Unpinned_TortollanStorytellerEndTurn(Game g, Controller p, string id)
    {
        Play(g, p, id);
        var older = Summon(p, "CS2_171"); var newer = Summon(p, "CS2_168");
        p.BoardZone.Add(p.BoardZone.Remove(newer), 0);
        Core2026Proof.Check(older.OrderOfPlay < newer.OrderOfPlay && newer.ZonePosition < older.ZonePosition, "newer recipient left of older recipient");
        End(g);
        var grants = g.IdEntityDic.Values.OfType<Enchantment>().Where(e => e.Card.Id == "TLC_254e")
            .OrderBy(e => e.Id).Select(e => e.Target.Id);
        Core2026Proof.Check(grants.SequenceEqual(new[] { older.Id, newer.Id }), "Storyteller emits buffs in play order despite reversed board order");
        End(g); StandardSetsProof.ParkBoard(p); EmptyHand(p);
        var source = (Minion)Play(g, p, id);
        var emptyRng = g.Random.CaptureState(); End(g); End(g);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 2 && emptyRng.Equals(g.Random.CaptureState()), "no typed recipient fizzles without RNG");
        var untyped = Summon(p, "CS2_182"); var a = Summon(p, "TLC_243"); var b = Summon(p, "TLC_243"); var beast = Summon(p, "CS2_171");
        var enemy = Summon(p.Opponent, "CS2_168");
        Resume(g, next => {
            End(next);
            foreach (var m in new[] { a, b, beast }) Core2026Proof.Check(((Minion)next.IdEntityDic[m.Id]).AttackDamage == m.Card.ATK + 1, "maximum distinct role set gets one buff each");
            Core2026Proof.Check(((Minion)next.IdEntityDic[untyped.Id]).AttackDamage == 4 && ((Minion)next.IdEntityDic[source.Id]).AttackDamage == 1
                && ((Minion)next.IdEntityDic[enemy.Id]).AttackDamage == enemy.Card.ATK, "untyped and enemy excluded");
            End(next);
            Core2026Proof.Check(((Minion)next.IdEntityDic[beast.Id]).AttackDamage == beast.Card.ATK + 1, "opponent end cannot retrigger; buff persists");
        });
        source.Silence(); End(g); End(g);
        Core2026Proof.Check(beast.AttackDamage == beast.Card.ATK + 1, "silence stops source trigger");
        Kill(g, source); End(g); End(g);
        Core2026Proof.Check(beast.AttackDamage == beast.Card.ATK + 1, "source leaves without lingering effect");
        for (int reverse = 0; reverse < 2; reverse++)
        {
            StandardSetsProof.ParkBoard(p); EmptyHand(p);
            source = (Minion)Play(g, p, id);
            var tied = new[] { Summon(p, "CS2_171"), Summon(p, "CS2_171"), Summon(p, "CS2_171") };
            if (reverse == 1) p.BoardZone.Add(p.BoardZone.Remove(tied[0]), 3);
            var ordered = p.BoardZone.Where(m => m != source).ToArray(); var rng = g.Random.Clone(); int expected = ordered[rng.Next(3)].Id;
            Resume(g, next => {
                End(next);
                foreach (var m in tied) Core2026Proof.Check(((Minion)next.IdEntityDic[m.Id]).AttackDamage == m.Card.ATK + (m.Id == expected ? 1 : 0), $"seeded representative: seed={g.Seed} reverse={reverse} expected={expected} actual={string.Join(',', next.Player1.BoardZone.Select(x => x.Id + ":" + x.AttackDamage))} draws={next.Random.DrawCount}/{g.Random.DrawCount}");
                End(next);
            });
        }
        StandardSetsProof.ParkBoard(p); EmptyHand(p);
        Play(g, p, id); Play(g, p, id); beast = Summon(p, "CS2_171"); End(g);
        Core2026Proof.Check(beast.AttackDamage == beast.Card.ATK + 2 && beast.Health == beast.Card.Health + 2, "multiple sources each grant once"); End(g);
        StandardSetsProof.ParkBoard(p); EmptyHand(p); Play(g, p, id);
        var distinct = new[] { Summon(p, "CS2_171"), Summon(p, "CS2_168"), Summon(p, "CS2_033"), Summon(p, "EX1_561") };
        End(g);
        Core2026Proof.Check(distinct.All(m => m.AttackDamage == m.Card.ATK + 1 && m.Health == m.Card.Health + 1), "Storyteller has no three-recipient cap"); End(g);
    }
}

using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;

internal static class ScalehideKodoProof
{
    // Tier C random tags/general rule; owner assumption: tie-break/attack-extreme-ties.yaml.
    internal static void Run() => StandardSetsProof.Case("TLC_454", Unpinned_ScalehideKodoExtremes);
    private static void Unpinned_ScalehideKodoExtremes(Game g, Controller p, string id)
    {
        Play(g, p, id); Core2026Proof.Check(p.Opponent.BoardZone.IsEmpty, "empty enemy pool fizzles");
        var low = Summon(p.Opponent, "CS2_168"); var high = Summon(p.Opponent, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(low.Zone == p.Opponent.GraveyardZone && high.Zone == p.Opponent.BoardZone, "no Kindred destroys unique lowest");
        StandardSetsProof.ParkBoard(p); Play(g, p, "CS2_171"); End(g); End(g);
        low = Summon(p.Opponent, "CS2_168");
        Play(g, p, id); Core2026Proof.Check(high.Zone == p.Opponent.GraveyardZone && low.Zone == p.Opponent.BoardZone, "previous own-turn Beast activates highest");
        // Same tied entities in reversed board order: assert seeded selection, not always-first.
        for (int reverse = 0; reverse < 2; reverse++)
        {
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent); EmptyHand(p);
            var first = Summon(p.Opponent, "CS2_182"); var second = Summon(p.Opponent, "CS2_182"); var third = Summon(p.Opponent, "CS2_182");
            if (reverse == 1) p.Opponent.BoardZone.Add(p.Opponent.BoardZone.Remove(first), 2);
            var tied = p.Opponent.BoardZone.ToArray(); var rng = g.Random.Clone(); int selected = rng.Next(3);
            Resume(g, next => {
                Play(next, next.Player1, id);
                Core2026Proof.Check(tied.Where((m, index) => index == selected).All(m => next.IdEntityDic[m.Id].Zone == next.Player2.GraveyardZone)
                    && next.Player2.BoardZone.Count == 2, $"seeded uniform tied entity destroyed: seed={g.Seed} reverse={reverse} expected={tied[selected].Id} remaining={string.Join(',', next.Player2.BoardZone.Select(m => m.Id))} draws={next.Random.DrawCount}/{g.Random.DrawCount}");
            });
        }
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        End(g); End(g); Play(g, p, "CS2_182"); End(g); End(g);
        low = Summon(p.Opponent, "CS2_168"); high = Summon(p.Opponent, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(low.Zone == p.Opponent.GraveyardZone && high.Zone == p.Opponent.BoardZone, "untyped prior play does not activate Kindred");
    }
}

using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ConfrontTheTolvirProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_560", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 0, "empty play history summons nothing");
        Summon(p, "CS2_171"); // Summoning is not playing.
        Play(g, p, "CS1_042"); Play(g, p, "CS2_171"); Play(g, p, "CS2_171"); Play(g, p, "CS2_120");
        Play(g, p, "CS2_005");
        var living = g.CloneExact(history: true); Play(living, living.Player1, id);
        Core2026Proof.Check(living.Player1.BoardZone.Count(m => m.Card.Id == "CS1_042") == 2
            && living.Player1.BoardZone.Count(m => m.Card.Id == "CS2_171") == 4, "played minions qualify while still alive");
        ClearBoard(g, p);
        End(g); Play(g, p.Opponent, "CS2_168"); End(g);
        var discounted = Add(p, "CS2_120"); discounted.Cost = 1; PlayEntity(g, p, discounted); Kill(g, (Minion)discounted);
        var reducedOne = Add(p, "CS2_168"); reducedOne.Cost = 0; PlayEntity(g, p, reducedOne); Kill(g, (Minion)reducedOne);
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, id);
        Core2026Proof.Check(clone.Player1.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { "CS1_042", "CS2_171", "CS2_171", "CS2_168" })
            && p.BoardZone.Count == 0, "printed 1-Cost plays, duplicates and order retained; spell, summoned, opponent and discounted higher-cost cards excluded");
        var left = Summon(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { "CS2_182", "CS1_042", "CS2_171", "CS2_171", "CS2_168" }), "summons append in play order");
        Core2026Proof.Check(p.BoardZone[1].AttackDamage == 1 && p.BoardZone[1].Health == 2 && p.BoardZone[4].AttackDamage == 2 && p.BoardZone[4].Health == 1, "fresh base stats");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone[5].Card.Id == "CS1_042" && p.BoardZone[6].Card.Id == "CS2_171", "partial board respects cap and play order");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7, "full board no summons");
    });
}

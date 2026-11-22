using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SlimeEmProof
{
    internal static void Run()
    {
        // One Case per card: the empty-side pin runs on a clone of each seed's fresh game.
        StandardSetsProof.Case("CAP_805", (g, p, id) => {
            var fresh = g.CloneExact(history: true);
            Unpinned_EmptySideStillGetsEctoplasm(fresh, fresh.Player1, id);
            Shared(g, p, id);
        });
    }

    // #139. UNPINNED (no retail trace): card text "Each player gets a 3-Cost spell" is unconditional, so a player with no minions
    // still gets an Ectoplasm, and casting it resummons nothing. Lean from the literal text (rules research 2026-11-19).
    private static void Unpinned_EmptySideStillGetsEctoplasm(Game g, Controller p, string id)
    {
        Summon(p, "CS2_182");
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 0, "the opponent has no minions");
        Play(g, p, id);
        var theirs = p.Opponent.HandZone.Where(c => c.Card.Id == "CAP_805t").ToArray();
        Core2026Proof.Check(theirs.Length == 1 && theirs[0].Cost == 3, "Unpinned lean: the empty side still gets a 3-Cost Ectoplasm");
        End(g); var opponent = p.Opponent; opponent.UsedMana = 0;
        PlayEntity(g, opponent, theirs[0]);
        Core2026Proof.Check(theirs[0].Zone.Type == Zone.GRAVEYARD && opponent.BoardZone.Count == 0 && p.BoardZone.Count == 0,
            "Unpinned lean: the empty side's Ectoplasm is castable and resummons nothing");
        var ours = p.HandZone.Single(c => c.Card.Id == "CAP_805t");
        Core2026Proof.Check(ours.Cost == 3, "the side with minions gets its own Ectoplasm");
    }

    private static void Shared(Game g, Controller p, string id) {
        var first = Summon(p, "CS2_182"); first.AttackDamage = 12;
        Summon(p, "FP1_002"); Summon(p.Opponent, "CS2_231");
        Play(g, p, id); var ours = p.HandZone.Single(c => c.Card.Id == "CAP_805t"); var theirs = p.Opponent.HandZone.Single(c => c.Card.Id == "CAP_805t");
        Core2026Proof.Check(first.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count == 2 && p.Opponent.BoardZone.Count == 0, "destroy resolves Deathrattles before giving resurrection spells");
        Core2026Proof.Check(ours.Cost == 3 && theirs.Cost == 3, "exact 3-Cost Ectoplasm for each side");
        var clone = g.CloneExact(history: true); PlayEntity(clone, clone.Player1, (IPlayable)clone.IdEntityDic[ours.Id]);
        Core2026Proof.Check(clone.Player1.BoardZone.Count == 4 && clone.Player1.BoardZone.Count(m => m.Card.Id == "CS2_182" && m.AttackDamage == 4 && m.Health == 5) == 1
            && clone.Player1.BoardZone.Count(m => m.Card.Id == "FP1_002") == 1 && p.BoardZone.Count == 2, "clone retains captured identities; resummons printed stats, excludes deathrattle children");
        Play(g, p, id); var second = p.HandZone.Where(c => c.Card.Id == "CAP_805t").OrderBy(c => c.Id).Last();
        PlayEntity(g, p, ours); Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.Any(m => m.Card.Id == "CS2_182"), "first cast retains its own captured list");
        PlayEntity(g, p, second); Core2026Proof.Check(p.BoardZone.Count == 4 && p.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 2, "second cast captures only its own slimed minions");
        End(g); PlayEntity(g, p.Opponent, theirs);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 1 && p.Opponent.BoardZone[0].Card.Id == "CS2_231", "each player gets their own captured minions"); End(g);
        Play(g, p, id); var capped = p.HandZone.Where(c => c.Card.Id == "CAP_805t").OrderBy(c => c.Id).Last();
        while (p.BoardZone.Count < 6) Summon(p, "CS2_182"); PlayEntity(g, p, capped);
        Core2026Proof.Check(p.BoardZone.Count == 7, "resurrection respects board cap");
    }
}

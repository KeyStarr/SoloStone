using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RavenousFlockProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_232", (g, p, id) => {
        Play(g, p, id); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 0, "no immediate summons");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(clone.Player1.BoardZone.Count == 0, "opponent turn start does not trigger"); End(clone);
        Core2026Proof.Check(clone.Player1.BoardZone.Count == 6 && p.BoardZone.Count == 0, "stacked delayed triggers clone independently");
        End(g); Core2026Proof.Check(p.BoardZone.Count == 0, "own end/opponent start does not trigger"); End(g);
        var tokens = p.BoardZone.ToArray();
        Core2026Proof.Check(tokens.Length == 6 && tokens.All(m => m.Card.Id == "TLC_237t" && m.AttackDamage == 2 && m.Health == 1)
            && tokens.Select(m => m.Id).SequenceEqual(tokens.Select(m => m.Id).OrderBy(x => x)), "two casts create six exact Hatchlings in creation order");
        Core2026Proof.Check(!p.AppliedEnchantments.Any(e => e.Card.Id == "TLC_232e"), "one-shot controller enchant removed");
        ClearBoard(g, p); End(g); End(g); Core2026Proof.Check(p.BoardZone.Count == 0, "does not repeat next turn");
        for (int occupied = 5; occupied <= 7; occupied++)
        {
            ClearBoard(g, p);
            for (int i = 0; i < occupied; i++) Summon(p, "CS2_182").Silence();
            Play(g, p, id); End(g); End(g);
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "TLC_237t") == 7 - occupied,
                $"{occupied} occupied slots truncate summons; minion Silence does not cancel controller effect");
            Core2026Proof.Check(!p.AppliedEnchantments.Any(e => e.Card.Id == "TLC_232e"),
                $"{occupied} occupied slots still remove one-shot enchantment");
            ClearBoard(g, p); End(g); End(g);
            Core2026Proof.Check(p.BoardZone.Count == 0, "truncated effect never repeats");
        }
    });
}

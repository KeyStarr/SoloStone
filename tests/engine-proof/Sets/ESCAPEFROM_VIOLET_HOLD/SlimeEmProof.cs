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
    internal static void Run() => StandardSetsProof.Case("CAP_805", (g, p, id) => {
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
    });
}

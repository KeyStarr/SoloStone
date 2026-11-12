using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BurstingShotProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_909", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var a = Summon(p.Opponent, "CS2_182"); var b = Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(a.Damage == 2 && b.Damage == 2 && p.Opponent.Hero.Damage == 2 && friend.Damage == 0 && p.Hero.Damage == 0, "three distinct enemies each hit once; friendly characters excluded");
        ClearBoard(g, p.Opponent); Summon(p, "EX1_012"); p.Opponent.Hero.HasDivineShield = true;
        Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && !p.Opponent.Hero.HasDivineShield, "only enemy hero selected once; shield absorbs");
        Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Damage == 5, "Spell Damage applies once with fewer than three enemies");
        for (int i = 0; i < 4; i++) Summon(p.Opponent, "CS2_182");
        int before = p.Opponent.Hero.Damage; Play(g, p, id);
        Core2026Proof.Check(p.Opponent.BoardZone.Count(m => m.Damage == 3) + (p.Opponent.Hero.Damage > before ? 1 : 0) == 3, "larger pool sampled without replacement");
    });
}

using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TheColdDarkVoidProof
{
    internal static void Run() => StandardSetsProof.Case("BE_284", (g, p, id) => {
        var spell = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "no minion means spell unplayable");
        p.HandZone.Remove(spell); p.SetasideZone.Add(spell);
        var farLeft = Summon(p.Opponent, "CS2_182");
        var left = Summon(p.Opponent, "CS2_182");
        var center = Summon(p.Opponent, "CS2_171");
        var right = Summon(p.Opponent, "CS2_182");
        var farRight = Summon(p.Opponent, "CS2_182");
        var friendly = Summon(p, "CS2_182");
        TargetRules(g, p, id, center, friendly, false);
        Play(g, p, id, center);
        Core2026Proof.Check(center.Zone == p.Opponent.GraveyardZone && left.Damage == 2 && right.Damage == 2,
            "lethal center retains original two neighbors");
        Core2026Proof.Check(farLeft.Damage == 0 && farRight.Damage == 0 && friendly.Damage == 0 && p.Opponent.Hero.Damage == 0,
            "non-neighbors, other side and heroes untouched");
        ClearBoard(g, p.Opponent);
        left = Summon(p.Opponent, "CS2_182"); right = Summon(p.Opponent, "CS2_182");
        Summon(p, "EX1_012");
        Play(g, p, id, left);
        Core2026Proof.Check(left.Zone == p.Opponent.GraveyardZone && right.Damage == 3, "edge target and both amounts scale with Spell Damage");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        center = Summon(p, "CS2_182");
        Play(g, p, id, center);
        Core2026Proof.Check(center.Health == 1, "single friendly target takes exactly four");
        ClearBoard(g, p);
        left = Summon(p.Opponent, "CS2_182"); center = Summon(p.Opponent, "CS2_182"); right = Summon(p.Opponent, "CS2_182");
        left[GameTag.DORMANT] = 1;
        Play(g, p, id, center);
        Core2026Proof.Check(left.Damage == 0 && center.Damage == 4 && right.Damage == 2, "Dormant neighbor occupies position but takes no damage");
        left[GameTag.DORMANT] = 0;
    });
}

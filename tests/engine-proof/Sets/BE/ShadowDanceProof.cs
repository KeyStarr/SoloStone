using SabberStoneCore.Enums;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ShadowDanceProof
{
    internal static void Run() => StandardSetsProof.Case("BE_071", (g, p, id) => {
        var healthy = Summon(p.Opponent, "CS2_182");
        var damaged = Summon(p.Opponent, "CS2_182"); damaged.Damage = 1;
        var friendly = Summon(p, "CS2_182");
        var dormant = Summon(p.Opponent, "CS2_182"); dormant[GameTag.DORMANT] = 1;
        var stealth = Summon(p.Opponent, "CS2_182"); stealth.HasStealth = true;
        Play(g, p, id);
        Core2026Proof.Check(healthy.Damage == 1 && damaged.Damage == 1 && stealth.Damage == 1, "hits undamaged enemies, including Stealth; skips damaged minions");
        Core2026Proof.Check(friendly.Damage == 0 && p.Hero.Damage == 0 && dormant.Damage == 0 && p.Opponent.Hero.Damage == 1, "enemy hero included; friendly and Dormant excluded");
        var next = Summon(p.Opponent, "CS2_182"); Summon(p, "EX1_012");
        Play(g, p, id);
        Core2026Proof.Check(next.Damage == 2 && healthy.Damage == 1 && p.Opponent.Hero.Damage == 1, "Spell Damage applies; previously damaged enemies excluded");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        p.Opponent.Hero.Damage = 0; p.Opponent.Hero.Armor = 3;
        Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Armor == 2 && p.Opponent.Hero.Damage == 0, "armor does not count as damage");
        p.Opponent.Hero.HasDivineShield = true;
        Play(g, p, id); Core2026Proof.Check(!p.Opponent.Hero.HasDivineShield && p.Opponent.Hero.Armor == 2, "undamaged shielded hero absorbs damage");
    });
}

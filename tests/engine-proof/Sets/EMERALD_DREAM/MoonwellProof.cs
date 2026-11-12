using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamBatch3Proof;

internal static class MoonwellProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_476", (g, p, id) => {
        var ally = Summon(p, "CS2_182"); ally.Damage = 4;
        var healthy = Summon(p, "CS2_182"); p.Hero.Damage = 10;
        var enemy = Summon(p.Opponent, "CS2_182");
        var shield = Summon(p.Opponent, "CS2_182"); shield.HasDivineShield = true;
        var lethal = Summon(p.Opponent, "CS2_171");
        Play(g, p, id);
        Core2026Proof.Check(ally.Damage == 0 && healthy.Damage == 0 && p.Hero.Damage == 6, "heals every friendly character up to maximum");
        Core2026Proof.Check(enemy.Damage == 4 && shield.Damage == 0 && !shield.HasDivineShield
            && lethal.Zone == p.Opponent.GraveyardZone && p.Opponent.Hero.Damage == 4, "damages every enemy, respecting shields and lethal");
        ClearBoard(g, p.Opponent); Summon(p, "EX1_012"); p.Hero.Damage = 6;
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 9 && p.Hero.Damage == 2, "Spell Damage increases damage only; empty enemy board legal");
        p.Opponent.Hero.HasDivineShield = true; Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 9 && !p.Opponent.Hero.HasDivineShield && p.Hero.Damage == 0,
            "hero Divine Shield absorbs damage then drops; healing still resolves");
    });
}

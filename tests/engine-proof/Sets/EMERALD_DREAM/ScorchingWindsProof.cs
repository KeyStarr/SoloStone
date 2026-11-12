using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ScorchingWindsProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_910", (g, p, id) => {
        var target = Summon(p.Opponent, "CS2_182"); var friend = Summon(p, "CS2_182");
        var nature = Add(p, "EX1_169"); Play(g, p, id, target);
        Core2026Proof.Check(target.Damage == 3 && nature.Zone == p.HandZone, "no Fire spell means only first three damage");
        var fire = Add(p, "CS2_029"); p.Opponent.Hero.HasDivineShield = true;
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(fire.Zone == p.GraveyardZone && p.Opponent.Hero.Damage == 3 && !p.Opponent.Hero.HasDivineShield, "first packet breaks shield, discarded legacy Fire enables second packet");
        fire = Add(p, "CS2_029"); Summon(p, "EX1_012"); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 11 && fire.Zone == p.GraveyardZone, "Spell Damage applies to both packets");
        var f1 = Add(p, "CS2_029"); var f2 = Add(p, "CS2_032");
        Play(g, p, id, target);
        Core2026Proof.Check(target.Zone == p.Opponent.GraveyardZone && new[] { f1, f2 }.Count(x => x.Zone == p.GraveyardZone) == 1, "discard still occurs after first packet lethal; exactly one Fire discarded");
        var spell = Add(p, id);
        Core2026Proof.Check(p.Options().Any(x => x.Source?.Id == spell.Id && x.Target?.Id == p.Hero.Id) && p.Options().Any(x => x.Source?.Id == spell.Id && x.Target?.Id == p.Opponent.Hero.Id), "either hero is legal damage target");
    });
}

using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RiftcleaverProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_ULD_165", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 7 && source.Health == 5 && source.IsRace(Race.DEMON) && p.Hero.Damage == 0,
            "printed 7/5 Demon can play without target on empty board");
        ClearBoard(g, p);
        var target = Summon(p.Opponent, "CS2_182"); target.Damage = 2; p.Hero.Armor = 2;
        Play(g, p, id, target);
        Core2026Proof.Check(target.Zone == p.Opponent.GraveyardZone && p.Hero.Armor == 0 && p.Hero.Damage == 1,
            "destroy damaged minion and deal its current three Health through armor");
        var friendly = Summon(p, "CS2_182"); target = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, target, friendly, false);
        friendly.HasDivineShield = true; p.Hero.HasDivineShield = true;
        Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.Zone == p.GraveyardZone && !p.Hero.HasDivineShield && p.Hero.Damage == 1,
            "destroy bypasses minion shield; hero shield absorbs self damage");
        Play(g, p, id, target);
        Core2026Proof.Check(target.Zone == p.Opponent.GraveyardZone && p.Hero.Damage == 6, "next destruction damages hero after shield consumed");
    });
}

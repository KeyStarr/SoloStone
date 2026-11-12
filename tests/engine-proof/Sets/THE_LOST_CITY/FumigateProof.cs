using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FumigateProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_901", (g, p, id) => {
        var target = Summon(p, "CORE_LOE_039"); var mech = Summon(p, "EX1_556");
        var beast = Summon(p.Opponent, "CS2_171"); var enemyDual = Summon(p.Opponent, "CORE_LOE_039");
        var none = Summon(p, "CS2_182"); var held = (Minion)Add(p, "CORE_LOE_039");
        TargetRules(g, p, id, beast, target, false); Play(g, p, id, target);
        Core2026Proof.Check(target.Damage == 3 && enemyDual.Damage == 3 && mech.Zone == p.GraveyardZone && beast.Zone == p.Opponent.GraveyardZone, "both types on both sides; dual overlap takes one hit");
        Core2026Proof.Check(none.Damage == 0 && held.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "unrelated, hand and heroes excluded");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); none = Summon(p, "CS2_182"); var other = Summon(p.Opponent, "CS2_182");
        Summon(p, "EX1_012"); Play(g, p, id, none);
        Core2026Proof.Check(none.Damage == 4 && other.Damage == 0, "Spell Damage applies; typeless target damaged alone");
    });
}

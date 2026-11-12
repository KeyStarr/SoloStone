using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class OverheatProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_906", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var nonspell = Add(p, "CS2_182"); var fire = Add(p, "CS2_029");
        Play(g, p, id);
        Core2026Proof.Check(friend.AttackDamage == 5 && friend.Health == 6 && enemy.AttackDamage == 4 && nonspell.Zone == p.HandZone && fire.Zone == p.HandZone, "base +1/+1 with no Nature spell; other cards retained");
        var nature = Add(p, "EX1_169"); Play(g, p, id);
        Core2026Proof.Check(friend.AttackDamage == 7 && friend.Health == 8 && nature.Zone == p.GraveyardZone && fire.Zone == p.HandZone, "discard legacy Nature spell for second +1/+1");
        var n1 = Add(p, "EX1_169"); var n2 = Add(p, "CS2_013"); Play(g, p, id);
        Core2026Proof.Check(new[] { n1, n2 }.Count(x => x.Zone == p.GraveyardZone) == 1 && friend.AttackDamage == 9, "exactly one of multiple Nature spells discarded");
        Play(g, p, "BE_228", friend); Core2026Proof.Check(friend.AttackDamage == 4 && friend.Health == 5, "Silence removes both buffs");
        ClearBoard(g, p); var held = p.HandZone.First(x => x.Card.Id == "EX1_169" || x.Card.Id == "CS2_013"); Play(g, p, id);
        Core2026Proof.Check(held.Zone == p.GraveyardZone && p.BoardZone.Count == 0, "empty board still discards matching spell");
    });
}

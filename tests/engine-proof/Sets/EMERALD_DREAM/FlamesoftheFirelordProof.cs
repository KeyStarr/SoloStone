using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FlamesoftheFirelordProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_923", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var seven = Add(p, "CS2_186"); Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 4 && friend.Damage == 0 && p.Opponent.Hero.Damage == 0 && seven.Cost == 7, "seven-cost card does not satisfy eight; only enemy minions hit");
        Kill(g, enemy); enemy = Summon(p.Opponent, "EX1_563"); var eight = Add(p, "EX1_298");
        Play(g, p, id); Core2026Proof.Check(enemy.Zone == p.Opponent.BoardZone && enemy.Damage == 8 && enemy.Health == 4
            && eight.Zone == p.HandZone, "holding eight-cost card deals exactly eight damage to surviving target");
        Kill(g, enemy); enemy = Summon(p.Opponent, "EX1_563"); Summon(p, "EX1_012");
        Play(g, p, id); Core2026Proof.Check(enemy.Zone == p.Opponent.BoardZone && enemy.Damage == 9 && enemy.Health == 3,
            "upgraded eight plus Spell Damage deals exactly nine damage to surviving target");
        ((Playable)eight).Cost = 7; Kill(g, enemy); enemy = Summon(p.Opponent, "CS2_200");
        Play(g, p, id); Core2026Proof.Check(enemy.Damage == 5 && enemy.Health == 2, "current held COST used; base four plus Spell Damage");
        ClearBoard(g, p.Opponent); Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Damage == 0, "empty enemy board legal and hero never hit");
        var a = Summon(p.Opponent, "CS2_182"); var b = Summon(p.Opponent, "CS2_182"); a.HasDivineShield = true; b.HasDivineShield = true;
        Play(g, p, id); Core2026Proof.Check(new[] { a, b }.Count(x => !x.HasDivineShield) == 1 && a.Damage == 0 && b.Damage == 0, "random target chosen once; shield absorbs");
    });
}

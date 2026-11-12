using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SilithidQueenProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_903", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRush && source.IsRace(Race.BEAST) && p.Hero.AttackDamage == 0, "printed Rush Beast; no Kindred bonus");
        ClearBoard(g, p); Play(g, p, "CS2_171"); End(g); End(g); ClearBoard(g, p);
        Play(g, p, id); Core2026Proof.Check(p.Hero.AttackDamage == 5 && p.Hero[GameTag.ATK] == 5, "Kindred grants visible +5 hero Attack");
        Play(g, p, id); Core2026Proof.Check(p.Hero.AttackDamage == 10, "Kindred attack bonuses stack");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(clone.Player1.Hero.AttackDamage == 0 && p.Hero.AttackDamage == 10, "clone expiry isolated");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage == 0 && p.Opponent.Hero.AttackDamage == 0, "bonus expires at turn end");
        End(g); End(g); End(g); ClearBoard(g, p); Play(g, p, "CS2_171"); Play(g, p, id);
        Core2026Proof.Check(p.Hero.AttackDamage == 0, "current-turn Beast does not satisfy Kindred");
    });
}

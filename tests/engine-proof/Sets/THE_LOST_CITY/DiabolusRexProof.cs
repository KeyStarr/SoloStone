using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DiabolusRexProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_138", (g, p, id) => {
        var left = Summon(p.Opponent, "CS2_186"); var middle = Summon(p.Opponent, "CS2_186"); var right = Summon(p.Opponent, "CS2_186");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.DEMON) && source.IsRace(Race.BEAST) && left.Damage + middle.Damage + right.Damage == 0, "dual types; no Kindred leaves enemies unharmed");
        ClearBoard(g, p); Play(g, p, "CS2_171"); End(g); End(g);
        Play(g, p, id);
        Core2026Proof.Check(left.Damage == 6 && right.Damage == 6 && middle.Damage == 0 && p.Opponent.Hero.Damage == 0, "last-turn Beast activates six damage only to both outer minions");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        var only = Summon(p.Opponent, "CS2_186"); only.BaseHealth = 20;
        Play(g, p, id); Core2026Proof.Check(only.Damage == 6, "single enemy is selected once");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 0, "Kindred with empty enemy board never targets hero");
        ClearBoard(g, p); End(g); End(g); End(g); Play(g, p.Opponent, "CS2_171"); End(g);
        only = Summon(p.Opponent, "CS2_186"); Play(g, p, "CS2_171"); Play(g, p, id);
        Core2026Proof.Check(only.Damage == 0, "current-turn and opponent Beast do not activate Kindred");
    });
}

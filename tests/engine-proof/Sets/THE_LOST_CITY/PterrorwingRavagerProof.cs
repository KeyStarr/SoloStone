using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PterrorwingRavagerProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_366", (g, p, id) => {
        var source = Add(p, id);
        Core2026Proof.Check(source.Cost == 6 && source[GameTag.COST] == 6 && ((Minion)source).IsRush, "printed cost visible without Kindred");
        Play(g, p, "CS2_171"); Core2026Proof.Check(source.Cost == 6, "current-turn Beast insufficient"); End(g); End(g);
        Core2026Proof.Check(source.Cost == 4 && source[GameTag.COST] == 4, "previous-turn Beast makes hand cost four, client tag agrees");
        var clone = g.Clone(); PlayEntity(clone, clone.Player1, clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(source.Zone == p.HandZone && source.Cost == 4, "clone play does not change original hand aura");
        End(g); Play(g, p.Opponent, "CS2_171"); End(g);
        Core2026Proof.Check(source.Cost == 6, "empty own previous turn removes discount despite opponent Beast");
        Play(g, p, "CORE_LOE_039"); End(g); End(g);
        Core2026Proof.Check(source.Cost == 4, "dual-type Beast activates discount"); PlayEntity(g, p, source);
        var minion = (Minion)source; minion.Silence();
        Core2026Proof.Check(minion.Cost == 6 && minion.IsSilenced, "hand cost effect removed on play and remains absent after Silence");
    });
}

using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AvianaElunesChosenProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_895", (g, p, id) => {
        var aviana = (Minion)Play(g, p, id);
        var old = Add(p, "CS2_029");
        Kill(g, aviana);
        for (int i = 0; i < 2; i++) { End(g); Core2026Proof.Check(old.Cost == 4, "opponent start not counted"); End(g); Core2026Proof.Check(old.Cost == 4, "first two owner starts do not grant discount"); }
        var clone = g.Clone(); End(clone); End(clone);
        Core2026Proof.Check(((IPlayable)clone.IdEntityDic[old.Id]).Cost == 1 && old.Cost == 4, "third start and clone isolation");
        End(g); End(g);
        Core2026Proof.Check(old.Cost == 1 && old[GameTag.COST] == 1, "existing hand cost visible as one");
        var fresh = Add(p, "CS2_182"); var zero = Add(p, "CS2_231");
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(fresh.Cost == 1 && zero.Cost == 1, "future and zero-cost cards set to one");
        Core2026Proof.Check(Add(p.Opponent, "CS2_029").Cost == 4, "opponent unaffected");
        End(g); End(g); Core2026Proof.Check(old.Cost == 1, "permanent player effect survives source death and turns");
    });
}

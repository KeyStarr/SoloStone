using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TitanicThrasherProof
{
    internal static void Run() => StandardSetsProof.Case("BE_733", (g, p, id) => {
        var source = (Minion)Add(p, id); Core2026Proof.Check(source.Cost == 8 && source.IsRush && source.HasWindfury, "printed keywords and empty enemy board cost");
        var friendly = Summon(p, "CS2_182"); Core2026Proof.Check(source.Cost == 8, "friendly board excluded");
        var enemy = Summon(p.Opponent, "CS2_182"); Core2026Proof.Check(source.Cost == 7, "one enemy discount");
        var clone = g.Clone(); Summon(clone.Player2, "CS2_182"); Core2026Proof.Check(clone.IdEntityDic[source.Id].Cost == 6 && source.Cost == 7, "adaptive cost clone isolated");
        for (int i = 1; i < 7; i++) Summon(p.Opponent, "CS2_182"); Core2026Proof.Check(source.Cost == 1, "seven enemies boundary");
        Kill(g, enemy); Core2026Proof.Check(source.Cost == 2, "cost increases immediately as enemy leaves");
        PlayEntity(g, p, source); Core2026Proof.Check(source.AttackDamage == 6 && source.Health == 6 && source.IsRush && source.HasWindfury, "played stats and keywords");
        Play(g, p, "BE_228", source); Core2026Proof.Check(source.IsSilenced && !source.HasWindfury && source.OngoingEffect == null, "silence removes Windfury and no hand cost effect remains (printed Rush has the accepted legacy limitation)");
    });
}

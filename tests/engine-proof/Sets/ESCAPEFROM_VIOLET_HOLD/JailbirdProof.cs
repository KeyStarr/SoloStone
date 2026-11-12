using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class JailbirdProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_453", (g, p, id) => {

        var first = Add(p, id); var second = Add(p, id); var enemy = Add(p.Opponent, id);
        var source = PrepareProof.Add(p, 1); p.BaseMana = 1;
        var clone = g.CloneExact(); Core2026Proof.Check(clone.Process(PrepareCardTask.Any(clone.Player1, clone.IdEntityDic[source.Id])), "clone Prepare");
        Core2026Proof.Check(clone.IdEntityDic[first.Id].Cost == 3 && first.Cost == 5, "clone held trigger independent");
        Core2026Proof.Check(g.Process(PrepareCardTask.Any(p, source)), "one-cost Prepare");
        Core2026Proof.Check(first.Cost == 3 && second.Cost == 3 && first[GameTag.COST] == 3 && enemy.Cost == 5, "all friendly held copies use nominal two discount");
        End(g); End(g); Core2026Proof.Check(first.Cost == 3, "discount permanent");
        PlayEntity(g, p, first); Core2026Proof.Check(((Minion)first).HasTaunt, "printed Taunt");
        source = PrepareProof.Add(p, 7); p.BaseMana = 10; p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PrepareCardTask.Any(p, source)) && second.Cost == 0, "discount floors at zero");
        Core2026Proof.Check(first.AppliedEnchantments == null || first.AppliedEnchantments.Count(e => e.Card.Id == "JAIL_453e") == 1, "board Jailbird no longer listens");
        ((Minion)first).Silence(); Generic.ReturnToHandBlock(p, (Minion)first);
        Core2026Proof.Check(first.Cost == 5, "bounce resets discount");
    });
}

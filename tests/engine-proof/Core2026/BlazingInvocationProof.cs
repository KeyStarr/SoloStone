using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class BlazingInvocationProof
{
    internal static void Run() => Case("CORE_GIL_836", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.Choice != null, "Invocation offers Discover");
        Core2026Proof.Check(p.Choice.Choices.All(key => g.IdEntityDic[key].Card[GameTag.BATTLECRY] == 1), "only Battlecry minions offered");
        int key = p.Choice.Choices[0]; int cost = g.IdEntityDic[key].Card.Cost;
        CoreCostProof.Answer(g, key);
        Core2026Proof.Check(p.HandZone.Single().Cost == Math.Max(0, cost - 1), "chosen minion costs 1 less");
        Core2026Proof.Check(cost == 0 || g.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>()
            .Any(change => change.EntityId == key && change.Tag == GameTag.COST && change.Value == cost - 1), "Discover discount reaches transport");
    });
}

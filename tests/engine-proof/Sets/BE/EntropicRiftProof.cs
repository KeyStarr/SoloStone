using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class EntropicRiftProof
{
    internal static void Run() => StandardSetsProof.Case("BE_220", (g, p, id) => {
        HandBoundaries(g, p, id, 2);
        var source = Add(p, id);
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        Generated(g, source, p.HandZone, 2);
    });
}

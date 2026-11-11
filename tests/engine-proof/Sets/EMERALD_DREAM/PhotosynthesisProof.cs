using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class PhotosynthesisProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_848", (g, p, id) => {
        HandBoundaries(g, p, id, 3);
        p.Hero.Damage = 10;
        var source = Add(p, id);
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id], next.Player1.Hero));
        Core2026Proof.Check(p.Hero.Damage == 4, "heals target for six");
        Generated(g, source, p.HandZone, 3);
        EmptyHand(p); p.Opponent.Hero.Damage = 2;
        Pool(g, id, 0, () => {
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 0 && p.HandZone.Count == 0, "empty pool still heals, capped at max Health");
        });
        var minion = Summon(p, "CS2_200"); minion.Damage = 5;
        Play(g, p, id, minion);
        Core2026Proof.Check(minion.Damage == 0, "can heal a minion");
    });
}

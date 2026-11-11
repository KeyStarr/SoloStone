using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz (identity and current text).
internal static class BlazingInvocationProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_GIL_836", (g, p, id) => {
        CorePoolProof.CrossClass(g, id);
        var existing = Add(p, "CS2_200"); Play(g, p, id);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count == 3 && p.Choice.Choices.All(key => g.IdEntityDic[key].Card.Type == CardType.MINION && g.IdEntityDic[key].Card[GameTag.BATTLECRY] == 1), "three Battlecry minion offers");
        int key = p.Choice.Choices[0]; int cost = g.IdEntityDic[key].Card.Cost;
        PoolPilotProof.Resume(g, next => {
            CoreCostProof.Answer(next, key); var chosen = next.IdEntityDic[key];
            Core2026Proof.Check(chosen.Zone == next.Player1.HandZone && chosen.Cost == Math.Max(0, cost - 1) && next.IdEntityDic[existing.Id].Cost == 6, "only chosen minion discounted, floor zero, across clone/snapshot");
            End(next); End(next); Core2026Proof.Check(chosen.Cost == Math.Max(0, cost - 1), "discount persists next turn");
        });
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        Play(g, p, id); key = p.Choice.Choices[0]; while (!p.HandZone.IsFull) Add(p, "CS2_182");
        CoreCostProof.Answer(g, key);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[key].Zone == p.GraveyardZone, "full hand burns chosen card without overflow");
        PoolPilotProof.EmptyHand(p); Play(g, p, id); key = p.Choice.Choices[0];
        g.IdEntityDic[key].Cost = 0;
        PoolPilotProof.Resume(g, next => {
            CoreCostProof.Answer(next, key);
            Core2026Proof.Check(next.IdEntityDic[key].Zone == next.Player1.HandZone && next.IdEntityDic[key].Cost == 0,
                "discounted zero-Cost offer stays at zero after another -1, including clone/snapshot");
        });
    }, legacyPoolMember: true);
}

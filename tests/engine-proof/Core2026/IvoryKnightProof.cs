using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class IvoryKnightProof
{
    internal static void Run() => Case("CORE_KAR_057", (g, p, id) => {
        p.Hero.Damage = 20; Play(g, p, id);
        int key = p.Choice.Choices[0]; int cost = g.IdEntityDic[key].Card.Cost;
        CoreCostProof.Answer(g, key);
        Core2026Proof.Check(p.HandZone.Single().Card.Type == CardType.SPELL && p.Hero.Damage == 20 - cost, "Ivory heals chosen spell Cost");
    });
}

using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class TheCuratorProof
{
    internal static void Run() => Case("CORE_KAR_061", (g, p, id) => {
        foreach (string card in new[] { "CS2_172", "EX1_043", "EX1_506" }) Entity.FromCard(p, Cards.FromId(card), zone: p.DeckZone);
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 3 && p.HandZone.Any(c => c.Card.IsRace(Race.BEAST)) && p.HandZone.Any(c => c.Card.IsRace(Race.DRAGON)) && p.HandZone.Any(c => c.Card.IsRace(Race.MURLOC)), "Curator draws each tribe");
    });
}

using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class GreaterHealingPotionProof
{
    internal static void Run() => Case("CORE_CFM_604", (g, p, id) => {
        p.Hero.Damage = 20; int deck = p.DeckZone.Count;
        Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 8 && p.HandZone.Count == 1 && p.DeckZone.Count == deck - 1, "heal 12 and draw");
    });
}

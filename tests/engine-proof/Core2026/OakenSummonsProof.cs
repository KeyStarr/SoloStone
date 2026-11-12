using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class OakenSummonsProof
{
    internal static void Run() => Case("CORE_LOOT_309", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.Hero.Armor == 6 && p.BoardZone.Single().Card.Cost <= 4 && p.DeckZone.Count == 25, "Oaken Summons armor and deck recruit");
    });
}

using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class AssassinateProof
{
    internal static void Run() => Case("CORE_CS2_076", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "Assassinate destroys enemy minion");
    });
}

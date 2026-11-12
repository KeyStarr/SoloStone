using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class DefiasRingleaderProof
{
    internal static void Run() => Case("CORE_EX1_131", (g, p, id) => {
        Play(g, p, "GAME_005"); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Any(m => m.Card.Id == "EX1_131t" && m.AttackDamage == 2 && m.Health == 1), "Combo summons Bandit");
    });
}

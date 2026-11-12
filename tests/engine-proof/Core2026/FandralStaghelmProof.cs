using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class FandralStaghelmProof
{
    internal static void Run() => Case("CORE_OG_044", (g, p, id) => {
        Play(g, p, id); Play(g, p, "EX1_160");
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.Any(m => m.Card.Id == id && m.AttackDamage == 4 && m.Health == 7) && p.BoardZone.Any(m => m.Card.Id == "EX1_160t"), "Fandral summons Panther and buffs existing minions");
    });
}

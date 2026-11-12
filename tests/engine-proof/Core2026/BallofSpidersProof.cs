using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class BallofSpidersProof
{
    internal static void Run() => Case("CORE_AT_062", (g, p, id) => {
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 3 && p.BoardZone.All(m => m.AttackDamage == 1 && m.Health == 1), "three 1/1 Webspinners");
        foreach (var spider in p.BoardZone.ToArray()) Kill(g, spider);
        Core2026Proof.Check(p.HandZone.Count == 3 && p.HandZone.All(c => c.Card.IsRace(Race.BEAST)), "each Webspinner gets a Beast on death");
    });
}

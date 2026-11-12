using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SiphonSoulProof
{
    internal static void Run() => Case("CORE_EX1_309", (g, p, id) => {
        p.Hero.Damage = 7; var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.Hero.Damage == 4, "Siphon destroys and heals 3");
    });
}

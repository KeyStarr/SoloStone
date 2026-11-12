using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class DrainSoulProof
{
    internal static void Run() => Case("CORE_ICC_055", (g, p, id) => {
        p.Hero.Damage = 7; var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Damage == 3 && p.Hero.Damage == 4, "Drain Soul deals and lifesteals 3");
    });
}

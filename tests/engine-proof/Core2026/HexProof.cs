using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HexProof
{
    internal static void Run() => Case("CORE_EX1_246", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.HasDivineShield = true; Play(g, p, id, enemy);
        var frog = p.Opponent.BoardZone.Single(); Core2026Proof.Check(frog.AttackDamage == 0 && frog.Health == 1 && frog.HasTaunt && !frog.HasDivineShield, "Hex creates 0/1 Taunt Frog");
    });
}

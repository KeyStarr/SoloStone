using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class MortalCoilProof
{
    internal static void Run() => Case("CORE_EX1_302", (g, p, id) => {
        var enemy = Summon(p.Opponent, "EX1_506a"); Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == 1, "Coil kill draws");
        var survivor = Summon(p.Opponent, "CS2_182"); int hand = p.HandZone.Count;
        Play(g, p, id, survivor); Core2026Proof.Check(survivor.Damage == 1 && p.HandZone.Count == hand, "survivor gives no draw");
    });
}

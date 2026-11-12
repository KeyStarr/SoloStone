using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HammerofWrathProof
{
    internal static void Run() => Case("CORE_CS2_094", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); int deck = p.DeckZone.Count;
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 3 && p.HandZone.Count == 1 && p.DeckZone.Count == deck - 1, "Hammer deals 3 and draws");
    });
}

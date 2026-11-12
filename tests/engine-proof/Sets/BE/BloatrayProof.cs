using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BloatrayProof
{
    internal static void Run() => StandardSetsProof.Case("BE_193", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        var enemy = Summon(p.Opponent, "CS2_182"); Attack(g, source, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "Poisonous kills high Health minion");
        source = (Minion)Play(g, p, id); source.Poisonous = false; var survivor = Summon(p.Opponent, "CS2_182");
        source.NumAttacksThisTurn = 0; Attack(g, source, survivor);
        Core2026Proof.Check(survivor.Health == 4, "without Poisonous only attack damage");
    });
}

using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class UltragigasaurProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_248", (g, p, id) => {
        var source = Add(p, id);
        Core2026Proof.Check(source.Cost == 11 && !p.Options().Any(t => t.Source?.Id == source.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD), "printed eleven cost cannot be paid with ten Mana");
        source.Cost = 10; PlayEntity(g, p, source);
        var minion = (Minion)source;
        Core2026Proof.Check(minion.IsRace(Race.BEAST) && minion.AttackDamage == 14 && minion.Health == 28 && minion.Card.Text == "", "discounted vanilla Beast enters with exact 14/28 stats");
        minion.Silence();
        Core2026Proof.Check(minion.AttackDamage == 14 && minion.Health == 28, "Silence preserves vanilla stats");
        var enemy = Summon(p.Opponent, "CS2_182"); Attack(g, minion, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && minion.Health == 24, "vanilla combat uses printed Attack and Health");
    });
}

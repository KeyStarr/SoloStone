using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WhelpoftheBronzeProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_056", (g, p, id) => {
        p.Hero.Damage = 10; var source = (Minion)Play(g, p, id);
        var enemy = Summon(p.Opponent, "CS2_182"); Attack(g, source, enemy);
        Core2026Proof.Check(source.Health == 1 && !source.HasDivineShield && p.Hero.Damage == 6, "shield absorbs retaliation and Lifesteal heals");
        source.NumAttacksThisTurn = 0; Attack(g, source, enemy);
        Core2026Proof.Check(source.Zone.Type == Zone.GRAVEYARD && p.Hero.Damage == 2, "second hit has no shield");
    });
}

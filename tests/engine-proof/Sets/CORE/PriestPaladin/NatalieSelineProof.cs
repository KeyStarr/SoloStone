using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class NatalieSelineProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_198", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Health == 1, "no target leaves base Health"); Kill(g, source);
        var target = Summon(p.Opponent, "CS2_182"); target.Damage = 2; target.HasDivineShield = true;
        source = (Minion)Play(g, p, id, target);
        Core2026Proof.Check(target.Zone.Type == Zone.GRAVEYARD && source.Health == 4 && source.Damage == 0, "gains current three Health, destroys through shield");
        source.Silence(); Core2026Proof.Check(source.Health == 1, "gained Health removed by Silence"); Kill(g, source);
        target = Summon(p, "CS2_182");
        Generic.AddEnchantmentBlock(g, Cards.FromId("BT_292e"), p.Hero, target); target.Damage = 1;
        source = (Minion)Play(g, p, id, target);
        Core2026Proof.Check(target.Zone.Type == Zone.GRAVEYARD && source.Health == 6, "friendly enchanted target contributes remaining Health");
        var enemy = Summon(p.Opponent, "CS2_182"); TargetRules(g, p, id, enemy, source, false);
        var card = p.HandZone.Last();
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == card.Id && t.Target?.Id == card.Id), "battlecry cannot target itself in hand");
    });
}

using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LohTheLivingLegendProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_257", (g, p, id) => {
        var small = Add(p, "CS2_171"); var large = Add(p, "CS2_200"); var spell = Add(p, "CS2_029"); var enemy = Add(p.Opponent, "CS2_171");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(small.Cost == 5 && large.Cost == 5 && small[GameTag.COST] == 5 && spell.Cost == 4 && enemy.Cost == 1, "all friendly minions cost five; spells and enemy unchanged");
        var clone = g.CloneExact(history: true); var future = Add(clone.Player1, "CS2_182"); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(future.Cost == 5 && p.HandZone.Count == 3, "clone future minion receives independent permanent aura");
        Play(g, p, "BE_228", source); Core2026Proof.Check(small.Cost == 5, "Silence cannot remove controller aura"); Kill(g, source); End(g); End(g);
        future = Add(p, "CS2_182"); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(future.Cost == 5 && small.Cost == 5 && p.AppliedEnchantments.Any(e => e.Card.Id == "TLC_257e1"), "controller enchant persists after source death and turns");
        PlayEntity(g, p, small); Core2026Proof.Check(p.UsedMana == 5, "five mana actually paid");
    });
}

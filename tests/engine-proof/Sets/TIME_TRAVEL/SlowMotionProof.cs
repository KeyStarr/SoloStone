using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SlowMotionProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_716", (g, p, id) => {
        var own = Add(p, "CS2_008"); var minion = Add(p.Opponent, "CS2_182");
        var spell = Add(p.Opponent, "CS2_008"); var weapon = Add(p.Opponent, "JAIL_376");
        Play(g, p, id);
        Core2026Proof.Check(own.Cost == 0, "caster hand unaffected");
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(clone.IdEntityDic[minion.Id].Cost == 5 && minion.Zone == p.Opponent.HandZone, "clone retains next-turn tax");
        End(g);
        Core2026Proof.Check(minion.Cost == 5 && spell.Cost == 1 && weapon.Cost == 2, "all opponent card types taxed next turn");
        Core2026Proof.Check(p.Opponent.AppliedEnchantments.Any(e => e.Card.Id == "TIME_716e3"), "exact controller enchantment");
        var fresh = Add(p.Opponent, "CS2_008"); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(fresh.Cost == 1 && minion[GameTag.COST] == 5, "new hand cards taxed; COST tag exposes derived cost");
        PlayEntity(g, p.Opponent, spell, p.Hero);
        Core2026Proof.Check(p.Opponent.UsedMana == 1 && p.Hero.Damage == 1, "taxed current cost paid");
        End(clone);
        Core2026Proof.Check(clone.IdEntityDic[minion.Id].Cost == 4 && minion.Cost == 5, "clone tax expiry isolated");
        End(g);
        Core2026Proof.Check(minion.Cost == 4 && fresh.Cost == 0 && weapon.Cost == 1 && !p.Opponent.AppliedEnchantments.Any(e => e.Card.Id == "TIME_716e3"), "tax and controller enchantment expire after opponent turn");
        Play(g, p, id); Play(g, p, id); End(g);
        Core2026Proof.Check(minion.Cost == 6 && weapon.Cost == 3, "two copies stack for next opponent turn");
        End(g);
        Core2026Proof.Check(minion.Cost == 4 && weapon.Cost == 1, "stacked taxes expire together");
    });
}

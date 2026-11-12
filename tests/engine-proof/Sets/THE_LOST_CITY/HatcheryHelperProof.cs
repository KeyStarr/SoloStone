using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HatcheryHelperProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_233", (g, p, id) => {
        var zero = Summon(p, "TLC_237"); var two = Summon(p, "CS2_120"); var three = Summon(p, "CS2_172"); var enemy = Summon(p.Opponent, "CS2_120"); var held = (Minion)Add(p, "CS2_120");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(zero.AttackDamage == 1 && zero.Health == 3 && zero.HasTaunt && two.AttackDamage == 3 && two.Health == 4 && two.HasTaunt, "0 and 2 Attack included and gain +1/+1 Taunt");
        Core2026Proof.Check(two.AppliedEnchantments.Any(e => e.Card.Id == "TLC_233e") && source.AttackDamage == 2 && source.Health == 3 && !source.HasTaunt && three.AttackDamage == 3 && !three.HasTaunt && enemy.AttackDamage == 2 && held.AttackDamage == 2, "self, 3 Attack, enemy and hand excluded");
        Play(g, p, "BE_228", two); Core2026Proof.Check(two.AttackDamage == 2 && two.Health == 3 && !two.HasTaunt, "silence removes granted stats and Taunt");
    });
}

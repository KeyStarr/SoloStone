using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_PicklockProof
{
    internal static void Run() => Unpinned_CostMemory();
    // Owner-approved working assumptions: docs/design/costmemory.md.
    private static void Unpinned_CostMemory() => StandardSetsProof.Case("JAIL_501", (g, p, id) => {
        p.BaseMana = 6;
        var target = Summon(p.Opponent, "CS2_200"); target.Health = 9;
        var source = (Minion)Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(source.Cost == 6 && source.AttackDamage == 6 && source.Health == 6, "synchronized six-Mana hand numbers");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, target)), "six-Mana Picklock play");
        Core2026Proof.Check(p.RemainingMana == 0 && source.AttackDamage == 6 && source.Health == 6 && target.Damage == 6,
            "payment freezes stats and damage");
        p.BaseMana = 10; p.UsedMana = 0; g.AuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 6 && source.Health == 6, "lock survives later Mana");
        ParkBoard(p);
        p.BaseMana = 0; p.UsedMana = 0;
        source = (Minion)Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(source.Cost == 1 && source.Health == 1 && !source.IsPlayableByPlayer,
            "minimum one at zero Mana and unaffordable without discount");
        Effects.ReduceCost(1).ApplyTo(source);
        source.AttackDamage += 2; source.BaseHealth += 3;
        var clone = g.CloneExact(true); var restored = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var branch in new[] { clone, restored })
        {
            var actor = (Minion)branch.IdEntityDic[source.Id];
            Core2026Proof.Check(branch.Process(PlayCardTask.Any(branch.Player1, actor, branch.IdEntityDic[target.Id] as Minion)),
                "zero-payment discounted Picklock");
            Core2026Proof.Check(actor.AttackDamage == 3 && actor.Health == 4 && actor[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] == 0,
                "hand buffs retained separately from Mana-derived base");
            Generic.ReturnToHandBlock(branch.Player1, actor); branch.AuraUpdate();
            Core2026Proof.Check(actor.Cost == 1 && actor.AttackDamage == 1 && actor.Health == 1,
                "bounce clears lock/buffs and resumes dynamic base");
        }
        p.Opponent.BaseMana = 3;
        var copy = Generic.Copy(p.Opponent, p.Hero, source, Zone.HAND);
        Core2026Proof.Check(copy.Cost == 2 && source.Cost == 0, "hand copy retains independent dynamic Cost modifier");
        copy.Cost = 0; p.Opponent.BaseMana = 5; g.AuraUpdate();
        Core2026Proof.Check(copy.Cost == 0, "explicit Cost SET overrides changing dynamic base");
    });
}

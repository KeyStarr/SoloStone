using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_MarchToWarProof
{
    internal static void Run() => Unpinned_CostMemory();
    // Owner-approved working assumptions: docs/design/costmemory.md.
    private static void Unpinned_CostMemory() => StandardSetsProof.Case("BE_142", (g, p, id) => {
        p.BaseMana = 6;
        var source = Add(p, id);
        Core2026Proof.Check(source.Cost == 6, "dynamic Cost before payment");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source)), "six-Mana March play");
        var soldier = p.BoardZone.Single();
        Core2026Proof.Check(p.RemainingMana == 0 && soldier.Card.Id == "BE_142t" && soldier.AttackDamage == 6
            && soldier.Health == 6 && soldier.HasDivineShield, "prepayment Soldier numbers and shield");
        p.BaseMana = 10; p.UsedMana = 0;
        Core2026Proof.Check(soldier.AttackDamage == 6 && soldier.Health == 6, "later Mana does not change Soldier");
        ParkBoard(p);
        p.BaseMana = 6; p.UsedMana = 0;
        source = Add(p, id); Effects.ReduceCost(3).ApplyTo(source);
        var branches = new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()) };
        foreach (var branch in branches)
        {
            var actor = branch.IdEntityDic[source.Id];
            Core2026Proof.Check(branch.Process(PlayCardTask.Any(branch.Player1, actor)), "resumed discounted March");
            var token = branch.Player1.BoardZone.Single();
            Core2026Proof.Check(token.AttackDamage == 6 && token.Health == 6 && token.Card.Cost == 1
                && branch.Player1.RemainingMana == 3, "discount changes payment, not locked Soldier numbers");
        }
        Core2026Proof.Check(source.Zone == p.HandZone, "resumed March leaves original untouched");
    });
}

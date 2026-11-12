using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BlowtorchSaboteurProof
{
    private static void UsePower(Game g, Controller owner)
    {
        owner.BaseMana = 10; owner.UsedMana = 0;
        int cost = owner.Hero.HeroPower.Cost;
        Core2026Proof.Check(g.Process(owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER)), "power accepted");
        Core2026Proof.Check(owner.UsedMana == cost, "tax paid before consumption");
    }

    internal static void Run() => StandardSetsProof.Case("CORE_DRG_403", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 3, "printed 3/3");
        Core2026Proof.Check(Cards.FromId("DRG_403e").AssetId == 56227 && Cards.FromId("DRG_403e").Text == "Costs (2) more.", "exact pinned tax enchantment identity and text");
        var power = p.Opponent.Hero.HeroPower;
        Core2026Proof.Check(power.Cost == 4 && power[GameTag.COST] == 4 && p.Hero.HeroPower.Cost == 2, "tax adds two to opponent power and exposes COST");
        source.Silence(); Kill(g, source);
        UsePower(g, p);
        Core2026Proof.Check(power.Cost == 4, "caster power, source Silence and source death do not consume tax");
        foreach (var branch in new[] { g.Clone(), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            End(branch); UsePower(branch, branch.Player2);
            Core2026Proof.Check(branch.Player2.Hero.HeroPower.Cost == 2 && power.Cost == 4, "clone/snapshot consumes tax independently");
        }
        End(g); p.Opponent.UsedMana = 7;
        Core2026Proof.Check(!g.Process(HeroPowerTask.Any(p.Opponent, p.Hero)) && power.Cost == 4, "unaffordable power does not consume tax");
        End(g); End(g);
        Core2026Proof.Check(power.Cost == 4, "unused tax persists across turn ends");
        new ReplaceHeroPower(Cards.FromId("CS2_034_H1")).Process(g, p.Opponent, p.Opponent.Hero, null);
        g.DeathProcessingAndAuraUpdate();
        power = p.Opponent.Hero.HeroPower;
        Core2026Proof.Check(power.Cost == 4 && power[GameTag.COST] == 4, "replacement power inherits unconsumed controller tax");
        UsePower(g, p.Opponent);
        Core2026Proof.Check(power.Cost == 2 && !p.Opponent.AppliedEnchantments.Any(e => e.Card.Id == "DRG_403e"), "successful power consumes exact enchantment");
        End(g); Play(g, p, id); Play(g, p, id); End(g);
        Core2026Proof.Check(power.Cost == 6, "two Battlecries stack to plus four");
        UsePower(g, p.Opponent);
        Core2026Proof.Check(power.Cost == 2, "next use consumes all stacked taxes");
        End(g); End(g); UsePower(g, p.Opponent);
        Core2026Proof.Check(p.Opponent.UsedMana == 2, "later use has normal cost");
    });
}

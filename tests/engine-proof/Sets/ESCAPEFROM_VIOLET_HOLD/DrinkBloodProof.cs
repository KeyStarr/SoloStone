using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DrinkBloodProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_441", (g, p, id) => {

        var own = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, own, false);
        p.Hero.Damage = 10; var power = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);
        Core2026Proof.Check(g.Process(power) && p.Hero.HeroPower.IsExhausted && p.HeroPowerActivationsThisTurn == 1, "hero power used through core action");
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Health == 2 && p.Hero.Damage == 7 && !p.Hero.HeroPower.IsExhausted && p.HeroPowerActivationsThisTurn == 0, "three damage Lifesteals and fully refreshes hero power");
        power = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);
        Core2026Proof.Check(g.Process(power), "refreshed hero power usable again");
        own.HasDivineShield = true; Play(g, p, id, own);
        Core2026Proof.Check(!own.HasDivineShield && own.Health == 5 && p.Hero.Damage == 7 && !p.Hero.HeroPower.IsExhausted, "prevented damage gives no Lifesteal but still refreshes");
        Summon(p, "EX1_012"); Play(g, p, id, own);
        Core2026Proof.Check(own.Health == 1 && p.Hero.Damage == 3, "spell damage increases damage and Lifesteal");
    });
}

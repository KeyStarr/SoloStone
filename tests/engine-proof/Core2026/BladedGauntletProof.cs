using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class BladedGauntletProof
{
    internal static void Run() => Case("CORE_LOOT_044", (g, p, id) => {
        p.Hero.Armor = 5; Play(g, p, id); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.AttackDamage == 5 && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target is Hero), "Gauntlet Attack equals Armor and cannot attack hero");
        p.Hero.Armor = 8; g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(p.Hero.AttackDamage == 8, "Gauntlet follows Armor changes");
    });
}

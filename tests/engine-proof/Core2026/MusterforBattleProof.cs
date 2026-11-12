using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class MusterforBattleProof
{
    internal static void Run() => Case("CORE_GVG_061", (g, p, id) => {
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 3 && p.BoardZone.All(m => m.Card.Id == "CS2_101t" && m.AttackDamage == 1 && m.Health == 1) && p.Hero.Weapon.AttackDamage == 1 && p.Hero.Weapon.Durability == 4, "Muster summons three Recruits and equips 1/4");
    });
}

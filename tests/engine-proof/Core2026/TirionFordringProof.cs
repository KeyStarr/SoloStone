using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class TirionFordringProof
{
    internal static void Run() => Case("CORE_EX1_383", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasTaunt && source.HasDivineShield && source.AttackDamage == 8 && source.Health == 8, "current Tirion 8/8 with keywords");
        Kill(g, source); Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 5 && p.Hero.Weapon.Durability == 3, "Tirion equips Ashbringer");
    });
}

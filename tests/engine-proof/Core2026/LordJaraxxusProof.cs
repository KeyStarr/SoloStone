using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class LordJaraxxusProof
{
    internal static void Run() => Case("CORE_EX1_323", (g, p, id) => {
        p.Hero.Damage = 11; p.Hero.Armor = 3; Play(g, p, id);
        Core2026Proof.Check(p.Hero.Card.Id == id && p.Hero.Health == 19 && p.Hero.Armor == 8 && p.Hero.Weapon.AttackDamage == 3 && p.Hero.Weapon.Durability == 8, "hero retains health, adds 5 Armor, equips 3/8");
        Core2026Proof.Check(p.Hero.HeroPower.Card.Id == "EX1_tk33", "Jaraxxus uses Inferno");
        p.UsedMana = 0; Core2026Proof.Check(g.Process(HeroPowerTask.Any(p)), "Inferno accepted");
        Core2026Proof.Check(p.BoardZone.Single().AttackDamage == 6 && p.BoardZone.Single().Health == 6, "Inferno summons 6/6");
    });
}

using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using SabberStoneCore.Tasks.PlayerTasks;
internal static class AldrachiWarbladesProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_921", (g, p, id) => {
        var source = Play(g, p, id);
        Core2026Proof.Check(p.Hero.Weapon?.Card.Id == id && p.Hero.Weapon.AttackDamage == 2
            && p.Hero.Weapon.Durability == 2 && p.Hero.HasLifeSteal, "printed 2/2 Lifesteal weapon equipped");
        p.Hero.Damage = 10; p.Opponent.Hero.Armor = 5;
        Resume(g, next => Core2026Proof.Check(next.Process(HeroAttackTask.Any(next.Player1, next.Player2.Hero)), "hero attack accepted"));
        Core2026Proof.Check(p.Hero.Damage == 8 && p.Opponent.Hero.Armor == 3 && p.Opponent.Hero.Damage == 0
            && p.Hero.Weapon.Durability == 1, "Lean_LifestealArmorDamage: weapon heals two Armor damage, spends one durability");
        End(g); End(g); p.Hero.Damage = 1; p.Opponent.Hero.Armor = 1;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "second hero attack accepted");
        Core2026Proof.Check(p.Hero.Damage == 0 && p.Opponent.Hero.Armor == 0 && p.Opponent.Hero.Damage == 1
            && p.Hero.Weapon == null && source.Zone == p.GraveyardZone && !p.Hero.HasLifeSteal,
            "Lean_LifestealArmorDamage: mixed hit caps healing, final durability removes weapon and Lifesteal");
    });
}

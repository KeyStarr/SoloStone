using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class CorruptedAshbringerProof
{
    internal static void Run() => DeathKnightC2Proof.Case("RLK_067", (g, p, id) => {
        p.Hero.Damage = 15; Play(g, p, id);
        Core2026Proof.Check(p.Hero.Weapon.Card.Id == id && p.Hero.Weapon.AttackDamage == 5 && p.Hero.Weapon.Durability == 2
            && p.Hero.Weapon.HasLifeSteal, "6 mana 5/2 Lifesteal weapon");
        DeathKnightC2Proof.Continue(g, copy => {
            var cp = copy.Player1;
            Core2026Proof.Check(copy.Process(HeroAttackTask.Any(cp, cp.Opponent.Hero)), "native weapon attack");
            Core2026Proof.Check(cp.Opponent.Hero.Damage == 5 && cp.Hero.Damage == 10 && cp.Hero.Weapon.Durability == 1,
                "weapon damage heals and consumes durability");
            End(copy); End(copy);
            var shield = Summon(cp.Opponent, "CS2_051"); shield.HasDivineShield = true;
            Core2026Proof.Check(copy.Process(HeroAttackTask.Any(cp, shield)), "legal attack into Divine Shield");
            Core2026Proof.Check(cp.Hero.Damage == 10 && cp.Hero.Weapon == null, "prevented damage heals zero; last durability breaks");
        });
        Play(g, p, id); Play(g, p, "CS2_106");
        Core2026Proof.Check(p.Hero.Weapon.Card.Id == "CS2_106" && !p.Hero.Weapon.HasLifeSteal, "replacement does not inherit Lifesteal");
    });
}

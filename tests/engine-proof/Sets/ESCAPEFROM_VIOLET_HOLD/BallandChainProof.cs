using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BallandChainProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_376", (g, p, id) => {
        var damaged = Summon(p, "CS2_182"); damaged.Damage = 1;
        var healthy = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 1;
        Play(g, p, id); Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 1 && p.Hero.Weapon.Durability == 2 && damaged.AttackDamage == 4, "1/2 weapon; no equip buff");
        var attack = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Opponent.Hero.Id);
        Core2026Proof.Check(g.Process(attack) && p.Hero.Weapon.Durability == 1 && damaged.AttackDamage == 4, "first swing does not trigger deathrattle");
        End(g); End(g);
        attack = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Opponent.Hero.Id);
        Core2026Proof.Check(g.Process(attack), "breaking swing accepted");
        Core2026Proof.Check(p.Hero.Weapon == null && damaged.AttackDamage == 5 && damaged.Health == 6 && damaged.Damage == 1 && healthy.AttackDamage == 4 && healthy.Health == 5 && enemy.AttackDamage == 4 && enemy.Health == 4, "break buffs only damaged friendly minions +1/+2");
        Core2026Proof.Check(damaged.AppliedEnchantments.Any(e => e.Card.Id == "JAIL_376e"), "pinned buff enchantment");
        Play(g, p, id); Play(g, p, "CS2_082");
        Core2026Proof.Check(damaged.AttackDamage == 6 && damaged.Health == 8 && p.Hero.Weapon.Card.Id == "CS2_082" && healthy.AttackDamage == 4, "replacement also triggers deathrattle exactly once");
        Play(g, p, "BE_228", damaged);
        Core2026Proof.Check(damaged.AttackDamage == 4 && damaged.Health == 5, "silence removes weapon-granted buff");
        ClearBoard(g, p); Play(g, p, id); Play(g, p, "CS2_082");
        Core2026Proof.Check(p.BoardZone.Count == 0 && enemy.Health == 4, "no friendly minions is harmless");
    });
}

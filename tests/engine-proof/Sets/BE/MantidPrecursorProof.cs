using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static SilenceKeywordsProof;

internal static class MantidPrecursorProof
{
    internal static void Run() => StandardSetsProof.Case("BE_169", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182");
        var unarmed = (Minion)Play(g, p, id);
        Core2026Proof.Check(unarmed.AttackDamage == 3 && unarmed.Health == 3 && !unarmed.IsRush && !CanAttack(unarmed, enemy), "without weapon remains 3/3 and exhausted");
        AttackOptionsProof.Lifecycle(g, unarmed, enemy, rush: true, keyword: false);
        End(g); Play(g, p.Opponent, "CS2_106"); End(g);
        var enemyWeapon = (Minion)Play(g, p, id);
        Core2026Proof.Check(!enemyWeapon.IsRush && enemyWeapon.AttackDamage == 3, "enemy weapon does not satisfy Battlecry");
        Play(g, p, "CS2_106");
        Core2026Proof.Check(!unarmed.IsRush && unarmed.AttackDamage == 3, "equipping weapon later does not trigger old Mantid");
        var armed = (Minion)Play(g, p, id);
        Core2026Proof.Check(armed.AttackDamage == 4 && armed.Health == 4 && armed.IsRush && armed.AppliedEnchantments.Any(e => e.Card.Id == "BE_169e"), "weapon grants pinned enchantment +1/+1 and Rush");
        Core2026Proof.Check(CanAttack(armed, enemy) && !CanAttack(armed, p.Opponent.Hero), "granted Rush permits only minion attacks immediately");
        AttackOptionsProof.Lifecycle(g, armed, enemy, rush: true,
            grantOnPreviousTurnMinion: (copy, target) => {
                target.Silence();
                target.Card.Power.PowerTask.Process(copy, target.Controller, target, null, new SabberStoneCore.Model.TaskStack());
                Core2026Proof.Check(target.AttackDamage == 4 && target.Health == 4, "Battlecry grants stats to previous-turn Mantid");
            });
        Play(g, p, "BE_228", armed);
        Core2026Proof.Check(armed.IsSilenced && armed.AttackDamage == 3 && armed.Health == 3 && !armed.IsRush && !armed.AttackableByRush
            && !CanAttack(armed, enemy) && !CanAttack(armed, p.Opponent.Hero), "silence removes buffs and Rush and restores summoning sickness");
        var persistent = (Minion)Play(g, p, id);
        p.Hero.Weapon.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Weapon == null && persistent.AttackDamage == 4 && persistent.Health == 4 && persistent.IsRush, "weapon loss does not revoke Battlecry buff");
        End(g); End(g);
        Core2026Proof.Check(CanAttack(armed, enemy) && CanAttack(armed, p.Opponent.Hero), "silenced Mantid ready next turn");
        Core2026Proof.Check(CanAttack(persistent, p.Opponent.Hero), "granted Rush hero restriction ends next turn");
    });
}

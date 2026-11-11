using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class EmboldeningBladeProof
{
    internal static void Run() => StandardSetsProof.Case("MEND_803", (g, p, id) => {
        var common = g.CloneExact(history: true);
        BrashBattlemasterProof.VerifyRecruitBuff(common, common.Player1, id, 1, 1, (game, owner) => {
            Play(game, owner, id); Play(game, owner, "CS2_082");
        });
        var recruit = Summon(p, "CS2_101t"); var held = (Minion)Add(p, "CS2_101t");
        var summonedWeapon = (Weapon)Add(p, id);
        p.HandZone.Remove(summonedWeapon); p.SetasideZone.Add(summonedWeapon);
        Core2026Proof.Check(recruit.Health == 1 && recruit.AttackDamage == 1, "weapon in hand or setaside grants no buff");
        var blade = (Weapon)Play(g, p, id);
        Core2026Proof.Check(blade.AttackDamage == 3 && blade.Durability == 2 && p.Hero.Weapon == blade,
            "printed 3/2 weapon equipped");
        Core2026Proof.Check(recruit.AttackDamage == 2 && recruit.Health == 2 && held.AttackDamage == 2 && held.Health == 2
            && p.AppliedEnchantments.Any(e => e.Card.Id == "MEND_803e")
            && recruit.AppliedEnchantments.Any(e => e.Card.Id == "MEND_803e2"), "Battlecry applies pinned +1/+1 controller aura");
        recruit.TakeDamage(p.Opponent.Hero, 1);
        var clone = g.CloneExact(history: true);
        var copy = (Minion)clone.IdEntityDic[recruit.Id]; copy.Silence(); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(copy.BaseHealth == 2 && copy.Damage == 1 && copy.Health == 1 && !recruit.IsSilenced,
            "clone Silence retains aura damage without changing original");
        recruit.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(recruit.AttackDamage == 2 && recruit.BaseHealth == 2 && recruit.Damage == 1 && recruit.Health == 1,
            "Silence preserves Blade's external +1/+1 and damage");
        Play(g, p, "CS2_082");
        Core2026Proof.Check(blade.Zone.Type == Zone.GRAVEYARD && recruit.BaseHealth == 2 && recruit.Damage == 1,
            "replacing weapon retains earned buff and damage");
        End(g); End(g);
        var future = Summon(p, "CS2_101t");
        Core2026Proof.Check(future.AttackDamage == 2 && future.Health == 2, "future Recruits retain buff after source departure and turn boundary");
        Play(g, p, id);
        Core2026Proof.Check(future.AttackDamage == 3 && future.Health == 3 && recruit.Damage == 1,
            "separate weapon Battlecries stack without healing existing damage");
    });
}

using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static DarkGiftConsumerProof;

internal static class CinderswordProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_922", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 1 && p.Hero.Weapon.Durability == 2, "no gift: printed weapon");
        InvalidHoldings(p); Play(g, p, id);
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 1, "only owner's held minion with attached gift counts");
        Clear(p); Gift(p); var sword = (Weapon)Play(g, p, id);
        Core2026Proof.Check(sword.AttackDamage == 4 && sword.Durability == 2 && p.Hero.AttackDamage == 4 &&
            sword.AppliedEnchantments.Single().Card.Id == "FIR_922e", "Battlecry buffs weapon, not just hero");
        Core2026Proof.Check(Cards.FromId("FIR_922e").AssetId == 122048 && Cards.FromId("FIR_922e").Text == "+3 Attack.", "exact attachment");
        PoolPilotProof.Resume(g, next => {
            End(next); End(next);
            var weapon = next.Player1.Hero.Weapon;
            Core2026Proof.Check(weapon.Id == sword.Id && weapon.AttackDamage == 4 && weapon.Durability == 2 && next.Player1.Hero.AttackDamage == 4,
                "weapon attack permanent across turns and clone/snapshot");
        });
    });
}

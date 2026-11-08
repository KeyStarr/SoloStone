using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BladedGauntlet
    {
        internal const int DbfId = 129222;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Aura = new MultiAura(
                new AdaptiveEffect(GameTag.ATK, EffectOperator.ADD, owner => {
                    // AdaptiveEffect targets the hero: update the weapon directly and give the hero no extra aura Attack.
                    ((Weapon)owner).AttackDamage = owner.Controller.Hero.Armor;
                    return 0;
                }),
                new Aura(AuraType.HERO, new Effect(GameTag.CANNOT_ATTACK_HEROES, EffectOperator.SET, 1))) };
        }
    }
}

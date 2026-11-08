using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HandOfInfinity
    {
        [StandardCard("END_012", 121151)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Aura = new Aura(AuraType.HERO, new Effect(GameTag.CANNOT_ATTACK_HEROES, EffectOperator.SET, 1)),
                PowerTask = new CustomTask((g, c, s, t, stack) => InfinityRules.GrantWeapon((Weapon)s))
            };
        }

        [StandardCard("END_012e", 121152)]
        internal static void ConfigureSetter(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
    }
}

using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SmallTimeBuccaneer
    {
        internal const int DbfId = 127089;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Same adaptive Attack aura as the legacy CFM_325 printing.
            card.Power = new Power { Aura = new AdaptiveEffect(GameTag.ATK, EffectOperator.ADD,
                p => p.Controller.Hero.Weapon != null ? 2 : 0) };
        }
    }
}

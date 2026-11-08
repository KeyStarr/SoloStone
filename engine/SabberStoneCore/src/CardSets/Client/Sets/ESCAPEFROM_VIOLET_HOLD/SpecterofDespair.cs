using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SpecterofDespair
    {
        internal const int DbfId = 126898;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Client tag 2525 carries Can't attack; adapt through the existing removable effect.
            card.Power = new Power { Aura = new AdaptiveEffect(new SelfCondition(_ => true), GameTag.CANT_ATTACK) };
        }
    }
}

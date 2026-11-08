using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CaptiveNathrezim
    {
        internal const int DbfId = 127155;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new Aura(AuraType.HANDS, Effects.AddCost(2)) { Condition = SelfCondition.IsMinion } };
        }
    }
}

using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UndercoverCultist
    {
        internal const int DbfId = 119781;
        [StandardCard("TLC_101", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new Aura(AuraType.SELF, "TLC_101e") {
                Condition = SelfCondition.IsDamaged, Restless = true
            } };
        }

        [StandardCard("TLC_101e", 119782)]
        internal static void ConfigureEnrage(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3)) };
        }
    }
}

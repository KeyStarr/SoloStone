using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MawmanCultist
    {
        internal const int DbfId = 130283;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Same both-hand aura as Mana Wraith (EX1_616), restricted to spells.
            card.Power = new Power { Aura = new Aura(AuraType.HANDS, Effects.AddCost(1)) {
                Condition = SelfCondition.IsSpell
            } };
        }
    }
}

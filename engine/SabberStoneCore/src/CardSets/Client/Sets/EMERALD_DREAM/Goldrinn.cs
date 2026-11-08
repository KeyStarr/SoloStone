using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Goldrinn
    {
        internal const int DbfId = 114254;
        [StandardCard("EDR_480", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[CombatRules.DoubleRace] = (int)Race.BEAST;
            card.Power = new Power { Aura = new Aura(AuraType.BOARD,"EDR_480e") {
                Condition = new SelfCondition(m => m.Card.IsRace(Race.BEAST)) } };
        }
        [StandardCard("EDR_480e", 122477)]
        internal static void ConfigureFerocity(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant() };
        }
    }
}

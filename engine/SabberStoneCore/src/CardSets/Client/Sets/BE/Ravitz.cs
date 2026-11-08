using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Ravitz
    {
        internal const int DbfId = 130651;
        [StandardCard("BE_230", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new MultiAura(
                new Aura(AuraType.BOARD, new Effect(GameTag.RUSH, EffectOperator.SET, 1)) {
                    Restless = true, Condition = new SelfCondition(m => m.Controller.BoardZone.FirstOrDefault() == m) },
                new Aura(AuraType.BOARD, new Effect(GameTag.IMMUNE_WHILE_ATTACKING, EffectOperator.ADD, 1)) {
                    Restless = true, Condition = new SelfCondition(m => m.Controller.BoardZone.LastOrDefault() == m) }) };
        }
    }
}

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
                // Left- and right-most skip Untouchable minions, as Flash Flood and Diabolus Rex do (owner ruling 2026-11-18; no retail source found).
                new Aura(AuraType.BOARD, new Effect(GameTag.RUSH, EffectOperator.SET, 1)) {
                    Restless = true, Condition = new SelfCondition(m => m.Controller.BoardZone.FirstInteractable == m) },
                new Aura(AuraType.BOARD, new Effect(GameTag.IMMUNE_WHILE_ATTACKING, EffectOperator.ADD, 1)) {
                    Restless = true, Condition = new SelfCondition(m => m.Controller.BoardZone.LastInteractable == m) }) };
        }
    }
}

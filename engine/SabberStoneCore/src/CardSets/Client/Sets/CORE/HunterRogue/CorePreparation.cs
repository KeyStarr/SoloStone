using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CorePreparation
    {
        internal const int DbfId = 69623;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
        }

        internal static void ConfigureDiscount(Card card)
        {
            // Tier A: Blizzard's May 22, 2019 balance update; pinned enchantment still says (2).
            card.Implemented = true;
            card.Tags[GameTag.CANT_BE_SILENCED] = 1;
            card.Power = new Power {
                Aura = new Aura(AuraType.HAND, Effects.ReduceCost(2)) {
                    Condition = SelfCondition.IsSpell,
                    RemoveTrigger = (TriggerType.CAST_SPELL, null)
                }
            };
        }
    }
}

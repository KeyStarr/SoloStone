using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FoxyFraud
    {
        internal const int DbfId = 120460;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("DMF_511e", EntityType.CONTROLLER) };
        }

        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true;
            // The pinned enchantment has TAG_ONE_TURN_EFFECT; Game.MainCleanup expires it.
            card.Power = new Power {
                Aura = new Aura(AuraType.HAND, Effects.ReduceCost(2)) { Condition = SelfCondition.IsComboCard },
                Trigger = new Trigger(TriggerType.PLAY_CARD) {
                    TriggerSource = TriggerSource.FRIENDLY,
                    Condition = SelfCondition.IsComboCard,
                    SingleTask = RemoveEnchantmentTask.Task
                }
            };
        }
    }
}

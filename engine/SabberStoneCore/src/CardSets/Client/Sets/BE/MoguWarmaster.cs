using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MoguWarmaster
    {
        internal const int DbfId = 132535;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_CARD) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(e => e.Card.Class == CardClass.NEUTRAL),
                SingleTask = new AddEnchantmentTask("BE_008e", EntityType.CONTROLLER)
            } };
        }

        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true;
            // The pinned enchantment's TAG_ONE_TURN_EFFECT handles end-of-turn expiry (Game.MainCleanup).
            card.Power = new Power {
                Aura = new Aura(AuraType.HAND, Effects.ReduceCost(1)) { Condition = SelfCondition.IsSpell },
                Trigger = new Trigger(TriggerType.CAST_SPELL) {
                    TriggerSource = TriggerSource.FRIENDLY,
                    SingleTask = RemoveEnchantmentTask.Task
                }
            };
        }
    }
}

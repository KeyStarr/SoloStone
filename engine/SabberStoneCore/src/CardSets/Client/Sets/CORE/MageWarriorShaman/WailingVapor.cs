using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WailingVapor
    {
        internal const int DbfId = 120881;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                TriggerSource = TriggerSource.MINIONS,
                Condition = SelfCondition.IsRace(Race.ELEMENTAL),
                SingleTask = new AddEnchantmentTask("WC_042e", EntityType.SOURCE)
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1)) };
        }
    }
}

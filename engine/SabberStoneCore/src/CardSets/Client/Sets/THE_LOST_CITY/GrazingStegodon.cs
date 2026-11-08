using SabberStoneCore.Enums;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GrazingStegodon
    {
        internal const int DbfId = 117600;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new MultiTrigger(Grow(TriggerActivation.PLAY), Grow(TriggerActivation.HAND), Grow(TriggerActivation.DECK)) };
        }

        private static Trigger Grow(TriggerActivation activation) => new Trigger(TriggerType.TURN_END) {
            TriggerActivation = activation, SingleTask = new AddEnchantmentTask("TLC_827e", EntityType.SOURCE)
        };

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1)) };
        }
    }
}

using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BlackMarketOverseer
    {
        internal const int DbfId = 126422;
        // "Whenever you play": the on-play step before the Battlecry; summons and opponent plays do not qualify.
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.PLAY_MINION) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(p => p is Minion minion && minion.HasDeathrattle),
                SingleTask = new AddEnchantmentTask("JAIL_880e", EntityType.TARGET)
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Rush) };
        }
    }
}

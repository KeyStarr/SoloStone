using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BarkshieldSentinel
    {
        internal const int DbfId = 114346;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.INSPIRE) {
                    TriggerSource = TriggerSource.FRIENDLY,
                    SingleTask = new AddEnchantmentTask("EDR_470e", EntityType.SOURCE)
                }
            };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(2)) };
        }
    }
}

using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CityDefenses
    {
        internal const int DbfId = 117917;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new SummonTask("TLC_622t", 2) };
        }

        internal static void ConfigureSecurity(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TAKE_DAMAGE) {
                TriggerSource = TriggerSource.SELF,
                SingleTask = new AddEnchantmentTask("TLC_622e", EntityType.SOURCE)
            } };
        }

        internal static void ConfigureAttack(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1)) };
        }
    }
}

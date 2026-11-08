using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GorishiWasp
    {
        internal const int DbfId = 117869;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TAKE_DAMAGE) {
                TriggerSource = TriggerSource.SELF,
                SingleTask = new AddCardTo("TLC_630t", EntityType.HAND)
            } };
        }
    }
}

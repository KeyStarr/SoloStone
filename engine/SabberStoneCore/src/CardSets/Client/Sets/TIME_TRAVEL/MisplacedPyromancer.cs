using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MisplacedPyromancer
    {
        internal const int DbfId = 121670;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.SHATTER) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new DamageTask(2, EntityType.OP_MINIONS)
            } };
        }
    }
}

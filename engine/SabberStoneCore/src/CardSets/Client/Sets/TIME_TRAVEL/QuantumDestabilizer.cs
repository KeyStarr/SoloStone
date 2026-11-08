using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class QuantumDestabilizer
    {
        internal const int DbfId = 120976;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Character.TakeDamage reads EventNumber again after the PREDAMAGE tasks.
            card.Power = new Power { Trigger = new Trigger(TriggerType.PREDAMAGE) {
                TriggerSource = TriggerSource.SELF,
                FastExecution = true,
                SingleTask = new CustomTask((g, c, s, t, stack) => g.CurrentEventData.EventNumber = SabberStoneCore.Actions.DamageArithmetic.Double(g.CurrentEventData.EventNumber, 1))
            } };
        }
    }
}

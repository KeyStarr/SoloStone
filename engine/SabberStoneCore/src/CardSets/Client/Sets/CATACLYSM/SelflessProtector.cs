using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SelflessProtector
    {
        internal const int DbfId = 123122;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Character.TakeDamage consumes EventNumber after PREDAMAGE (Character.cs:356).
            card.Power = new Power { Trigger = new Trigger(TriggerType.PREDAMAGE) {
                TriggerSource = TriggerSource.SELF,
                Condition = new SelfCondition(p => p.Game.CurrentEventData.EventNumber > 0),
                FastExecution = true,
                SingleTask = new CustomTask((g, c, s, t, stack) => { g.CurrentEventData.EventNumber = SabberStoneCore.Actions.DamageArithmetic.Add(g.CurrentEventData.EventNumber, 1); })
            } };
        }

    }
}

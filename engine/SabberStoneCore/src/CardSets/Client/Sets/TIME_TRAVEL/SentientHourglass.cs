using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SentientHourglass
    {
        internal const int DbfId = 120947;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TAKE_DAMAGE) {
                TriggerSource = TriggerSource.SELF,
                Condition = new SelfCondition(me => me is Minion m && m.Health > 0 && !m.ToBeDestroyed),
                SingleTask = new SwapAttackHealthTask(EntityType.SOURCE, "TIME_050e")
            } };
        }

        internal static void ConfigureSwap(Card card) { card.Implemented = true; card.Power = new Power(); }
    }
}

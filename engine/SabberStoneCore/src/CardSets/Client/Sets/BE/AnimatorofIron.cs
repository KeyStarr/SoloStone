using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AnimatorofIron
    {
        internal const int DbfId = 130279;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.ARMOR) {
                TriggerSource = TriggerSource.HERO,
                Condition = new SelfCondition(p => p.Game.CurrentEventData.EventNumber > 0),
                SingleTask = new DrawTask(1)
            } };
        }
    }
}

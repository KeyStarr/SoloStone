using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AcolyteOfDeath
    {
        internal const int DbfId = 111318;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.DEATH) {
                TriggerSource = TriggerSource.MINIONS,
                Condition = SelfCondition.IsRace(Race.UNDEAD),
                OrderDeathByPlay = true,
                SingleTask = new DrawTask()
            } };
        }
    }
}

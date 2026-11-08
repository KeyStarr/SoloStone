using System;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EnduringRoach
    {
        internal const int DbfId = 121106;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.INSPIRE) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g, c, s, t, stack) =>
                    new SetControllerGameTagTask(GameTag.RESOURCES_USED, Math.Max(0, c.UsedMana - 2)).Process(g, c, s, t, stack))
            } };
        }
    }
}

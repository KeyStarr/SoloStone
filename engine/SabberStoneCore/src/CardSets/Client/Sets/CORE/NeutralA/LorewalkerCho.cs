using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LorewalkerCho
    {
        internal const int DbfId = 120883;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Tags[(GameTag)858] = 1135;
            card.Tags[(GameTag)1413] = 1135;
            card.Power = new Power
            {
                Trigger = new Trigger(TriggerType.CAST_SPELL)
                {
                    // Construct the task per cast so cloned triggers share no mutable StateTaskList.
                    SingleTask = new CustomTask((g, c, s, t, stack) =>
                        new CopyTask(EntityType.TARGET, Zone.HAND, toOpponent: t.Controller == c)
                            .Process(g, c, s, t, new TaskStack()))
                }
            };
        }
    }
}

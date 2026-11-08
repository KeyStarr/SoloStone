using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Stormbinder
    {
        internal const int DbfId = 122642;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // The client OVERLOAD tag is a boolean marker; Generic.OverloadBlock reads this amount.
            card.Overload = 3;
            card.Power = new Power { DeathrattleTask = ComplexTask.Create(
                new SetControllerGameTagTask(GameTag.OVERLOAD_LOCKED, 0),
                new SetControllerGameTagTask(GameTag.OVERLOAD_OWED, 0)) };
        }
    }
}

using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreBallOfSpiders
    {
        internal const int DbfId = 111364;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            // Use the pinned CORE Webspinner so the original FP1_011 power stays unchanged.
            card.Power = new Power { PowerTask = new SummonTask("CORE_FP1_011", 3) };
        }
    }
}

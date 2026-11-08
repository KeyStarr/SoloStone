using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ImmortalizedInStone
    {
        internal const int DbfId = 116713;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new SummonTask("TSC_076t3"), new SummonTask("TSC_076t2"), new SummonTask("TSC_076t")) };
        }
    }
}

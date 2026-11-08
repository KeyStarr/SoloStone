using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TamePet
    {
        internal const int DbfId = 123486;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                    CompanionRules.Upgrade(c, s, 1, DbfId);
                    new DrawTask(1).Process(g, c, s, t, stack);
                }) };
        }
    }
}

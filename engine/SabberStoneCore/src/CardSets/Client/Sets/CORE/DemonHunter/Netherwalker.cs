using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Netherwalker
    {
        internal const int DbfId = 129167;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CORE_BT_321/v1",
                new DiscoverThenContinueTask(CoreCostPools.Demon(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen != null && Generic.RemoveFromZone(c, chosen))
                        Generic.AddHandPhase(c, chosen);
                })) };
        }
    }
}

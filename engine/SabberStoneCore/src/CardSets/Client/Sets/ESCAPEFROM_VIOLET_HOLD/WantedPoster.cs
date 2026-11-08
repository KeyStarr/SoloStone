using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WantedPoster
    {
        internal const int DbfId = 129888;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/CAP_407/v1",
                new DiscoverThenContinueTask(EscapeFromVioletHoldPools.ExpensiveMinion()),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.LastOrDefault();
                    if (chosen == null || !Generic.RemoveFromZone(c, chosen)) return;
                    PrepareRules.Grant(chosen);
                    Generic.AddHandPhase(c, chosen);
                })) };
        }
    }
}

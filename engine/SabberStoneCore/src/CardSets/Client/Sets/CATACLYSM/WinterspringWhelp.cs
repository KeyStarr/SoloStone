using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WinterspringWhelp
    {
        internal const int DbfId = 123398;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_484/v1",
                new DiscoverThenContinueTask(OfflinePoolDeclarations.AnyClassOneCostSpell(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen != null && Generic.RemoveFromZone(c, chosen))
                        Generic.AddHandPhase(c, chosen);
                })) };
        }
    }
}

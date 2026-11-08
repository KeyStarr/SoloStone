using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CreatureofMadness
    {
        internal const int DbfId = 113973;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 3, maxCost: 3);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/EDR_105/v1",
                new DiscoverWithDarkGiftThenContinueTask(Filter, recipeDbfId: DbfId), new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    Generic.RemoveFromZone(c, chosen);
                    Generic.AddHandPhase(c, chosen);
                    DarkGifts.ResolveSweetDreams(chosen);
                })) };
        }
    }
}

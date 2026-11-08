using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AvantGardening
    {
        internal const int DbfId = 115086;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.DEATHRATTLE);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/EDR_488/v1",
                new DiscoverWithDarkGiftThenContinueTask(Filter, recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null || !Generic.RemoveFromZone(c, chosen)) return;
                    Generic.AddHandPhase(c, chosen);
                    DarkGifts.ResolveSweetDreams(chosen);
                })) };
        }
    }
}

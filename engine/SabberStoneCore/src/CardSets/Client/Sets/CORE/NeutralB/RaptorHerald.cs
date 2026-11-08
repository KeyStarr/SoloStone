using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RaptorHerald
    {
        internal const int DbfId = 131356;
        internal const int OriginalDbfId = 119798;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.BEAST);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/" + card.Id + "/v1",
                new DiscoverWithDarkGiftThenContinueTask(Filter, recipeDbfId: card.AssetId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) { KindredRules.BeginMatchedClause((SabberStoneCore.Model.Entities.IPlayable)s); return; }
                    Generic.RemoveFromZone(c, chosen);
                    if (!Generic.AddHandPhase(c, chosen)) { KindredRules.BeginMatchedClause((SabberStoneCore.Model.Entities.IPlayable)s); return; }
                    DarkGifts.ResolveSweetDreams(chosen);
                    int repeats = KindredRules.BeginMatchedClause((SabberStoneCore.Model.Entities.IPlayable)s);
                    for (int i = 0; i < repeats; i++) Effects.ReduceCost(1).ApplyTo(chosen);
                })) };
        }
    }
}

using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Cremate
    {
        internal const int DbfId = 115624;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Discover(type: CardType.MINION);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/FIR_900/v1",
                new DiscoverWithDarkGiftThenContinueTask(Filter, recipeDbfId: DbfId), new CustomTask((g, c, s, t, stack) => {
                var chosen = stack.Playables.SingleOrDefault();
                if (chosen == null) return;
                Generic.RemoveFromZone(c, chosen);
                if (!Generic.AddHandPhase(c, chosen)) return;
                // D7 / dg11b-sweet-dreams-sequence: finish gift relocation before the discount.
                DarkGifts.ResolveSweetDreams(chosen);
                Effects.ReduceCost(2).ApplyTo(chosen);
            })) };
        }
    }
}

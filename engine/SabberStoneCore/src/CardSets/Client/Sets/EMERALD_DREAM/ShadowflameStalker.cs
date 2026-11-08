using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShadowflameStalker
    {
        internal const int DbfId = 115651;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.DEMON);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/FIR_924/v1",
                new DiscoverWithDarkGiftThenContinueTask(Filter, recipeDbfId: DbfId), new CustomTask((g, c, s, t, stack) => {
                var chosen = stack.Playables.SingleOrDefault();
                if (chosen == null) return;
                Generic.RemoveFromZone(c, chosen);
                Generic.AddHandPhase(c, chosen);
                // dg11b-sweet-dreams-sequence / dg9-copy-retention: copy after gift movement.
                // Deck-to-hand retains the gift too; copying never gives/relocates it again.
                DarkGifts.ResolveSweetDreams(chosen);
                Generic.Copy(c, s, chosen, Zone.HAND);
            })) };
        }
    }
}

using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Jumpscare
    {
        internal const int DbfId = 114337;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 5, race: Race.DEMON);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/EDR_882/v1",
                new DiscoverWithDarkGiftThenContinueTask(Filter, recipeDbfId: DbfId), new CustomTask((g, c, s, t, stack) => {
                var chosen = stack.Playables.SingleOrDefault();
                if (chosen == null) return;
                int executor = g.IdEntityDic[chosen[GameTag.DARK_GIFT_ENTITY]][GameTag.CREATOR];
                // D9 / dg10-jumpscare-shuffled-offers: only chosen offer ever receives a gift.
                var others = c.SetasideZone.Where(p => p.Id != chosen.Id && p is Model.Entities.Minion &&
                    p[GameTag.DARK_GIFT_ENTITY] != 0 &&
                    g.IdEntityDic[p[GameTag.DARK_GIFT_ENTITY]][GameTag.CREATOR] == executor).ToArray();
                Generic.RemoveFromZone(c, chosen);
                Generic.AddHandPhase(c, chosen);
                DarkGifts.ResolveSweetDreams(chosen);
                foreach (var other in others)
                {
                    if (c.DeckZone.IsFull) break;
                    Generic.RemoveFromZone(c, other);
                    Generic.ShuffleIntoDeck(c, s, other);
                }
            })) };
        }
    }
}

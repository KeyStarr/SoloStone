using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MerchantOfLegend
    {
        internal const int DbfId = 117691;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/TLC_514/v1",
                new CustomTask((g, c, s, t, stack) => { stack.Number = c.SetasideZone.Select(p => p.Id).DefaultIfEmpty().Max(); }),
                new DiscoverThenContinueTask(TheLostCityPools.LegendaryMinionDiscover(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    var others = c.SetasideZone.Where(p => p.Id > stack.Number && p.Id != chosen.Id &&
                        p[GameTag.DISPLAYED_CREATOR] == s.Id).ToArray();
                    c.SetasideZone.Remove(chosen);
                    Generic.AddHandPhase.Invoke(c, chosen);
                    using var historyShuffle = g.Facts.Shuffle(c.Id);
                    foreach (var other in others)
                    {
                        if (c.DeckZone.IsFull) break;
                        c.SetasideZone.Remove(other);
                        Generic.ShuffleIntoDeck(c, s, other);
                    }
                    stack.Playables.Clear();
                })) };
        }
    }
}

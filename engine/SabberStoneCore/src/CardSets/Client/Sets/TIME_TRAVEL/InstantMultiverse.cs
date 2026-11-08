using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class InstantMultiverse
    {
        internal const int DbfId = 119688;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Export OVERLOAD is a keyword marker; the printed amount is three.
            card.Overload = 3;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int remaining = 12;
                // Unpinned_TwelveManaFill: choose any affordable printed cost, including zero.
                while (remaining > 0 && !c.BoardZone.IsFull)
                {
                    var chosen = ctx.PickRandomCard(TimeTravelPools.BudgetMinion(remaining), recipeDbfId: DbfId);
                    if (chosen == null || ctx.Summon(chosen.Id) == 0) break;
                    remaining -= chosen.Cost;
                }
            }) };
        }
    }
}

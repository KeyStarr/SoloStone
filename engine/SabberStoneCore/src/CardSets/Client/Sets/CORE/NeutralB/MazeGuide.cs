using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MazeGuide
    {
        internal const int DbfId = 118076;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var chosen = ctx.PickRandomCard(CoreCostPools.TwoCostMinion(), recipeDbfId: DbfId);
                if (chosen == null || c.BoardZone.IsFull) return;
                int position = Enumerable.Range(0, c.BoardZone.SlotCount).FirstOrDefault(i => c.BoardZone.GetSlot(i) == s);
                ctx.Summon(chosen.Id, s.Zone == c.BoardZone ? position + 1 : c.BoardZone.SlotCount);
            }) };
        }
    }
}

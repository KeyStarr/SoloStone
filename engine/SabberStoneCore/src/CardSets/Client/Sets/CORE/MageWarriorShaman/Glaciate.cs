using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Glaciate
    {
        internal const int DbfId = 127473;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/CORE_AV_107/v1",
                new DiscoverThenContinueTask(CoreCostPools.EightCostMinion(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault() as Minion;
                    if (chosen != null && !c.BoardZone.IsFull)
                    {
                        Generic.RemoveFromZone(c, chosen);
                        Generic.SummonBlock(g, chosen, c.BoardZone.SlotCount, s);
                        g.Facts.Freeze(chosen,s);
                    }
                    stack.Playables.Clear();
                })) };
        }
    }
}

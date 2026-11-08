using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UndefeatedChampion
    {
        internal const int DbfId = 119783;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int slots = c.Opponent.BoardZone.FreeSpace;
                for (int i = 0; i < slots && !c.Opponent.BoardZone.IsFull; i++)
                {
                    var chosen = ctx.PickRandomCard(TimeTravelPools.OneCostMinion(), recipeDbfId: DbfId);
                    if (chosen == null) break;
                    var generated = Entity.FromCard(c.Opponent, chosen);
                    generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                    new SummonOpTask().Process(g, c, s, null, new TaskStack { Playables = new[] { generated } });
                }
            }) };
        }
    }
}

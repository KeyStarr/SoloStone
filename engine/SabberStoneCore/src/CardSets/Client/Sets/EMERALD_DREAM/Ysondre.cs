using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Ysondre
    {
        internal const int DbfId = 114340;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id);
                int count = ctx.History.Deaths().Count(f => f.DbfId == DbfId);
                int position = EffectContext.DeathrattlePosition(c, s);
                for (int i = 0; i < count; i++)
                {
                    var dragon = ctx.PickRandomCard(EmeraldDreamPools.Dragon(), recipeDbfId: DbfId);
                    if (dragon != null && ctx.Summon(dragon.Id, position) != 0) position++;
                }
            }) };
        }
    }
}

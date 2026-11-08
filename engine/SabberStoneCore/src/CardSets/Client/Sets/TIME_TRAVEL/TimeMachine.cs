using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TimeMachine
    {
        internal const int DbfId = 120506;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Random(keyword: (GameTag)3934);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var rewind = ctx.PickRandomCard(Filter, recipeDbfId: DbfId);
                if (rewind != null) ctx.AddToHand(rewind.Id);
            }) };
        }
    }
}

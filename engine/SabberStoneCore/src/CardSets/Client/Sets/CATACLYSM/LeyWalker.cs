using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LeyWalker
    {
        internal const int DbfId = 123496;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) =>
                LeylineRules.Upgrade(c, (IPlayable)s, cost: 1)),
                // Lean: coordinator D3, fixed closed printed family, not a collection-wide pool.
                DeathrattleTask = EffectContext.Task(ctx => ctx.AddToHand(ctx.SampleDistinct(LeylineRules.Family(), 1)[0].Id)) };
        }
    }
}

using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TroubledDouble
    {
        internal const int DbfId = 120790;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { ComboTask = EffectContext.Task(ctx => ctx.RunLegacy(new SummonCopyTask(EntityType.SOURCE, side: SummonSide.RIGHT))) };
        }
    }
}

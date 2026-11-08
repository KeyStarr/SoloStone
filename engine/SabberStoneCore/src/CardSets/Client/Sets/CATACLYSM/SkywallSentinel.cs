using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SkywallSentinel
    {
        internal const int DbfId = 122953;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {  PowerTask = EffectContext.Task(ctx => {
                    ctx.Herald();
                    return TaskState.COMPLETE;
                }) };
        }
    }
}

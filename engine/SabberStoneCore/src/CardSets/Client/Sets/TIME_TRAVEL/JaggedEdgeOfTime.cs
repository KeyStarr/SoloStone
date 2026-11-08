using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class JaggedEdgeOfTime
    {
        internal const int DbfId = 121274;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                ctx.Imbue();
                return TaskState.COMPLETE;
            }) };
        }
    }
}

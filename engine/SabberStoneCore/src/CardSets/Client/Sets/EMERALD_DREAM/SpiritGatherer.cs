using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SpiritGatherer
    {
        internal const int DbfId = 116462;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                // Lean_ConsumerClauseOrder: imbue-16, full-hand generation still continues.
                ctx.AddToHand("EDR_851t");
                ctx.Imbue();
                return TaskState.COMPLETE;
            }) };
        }
    }
}

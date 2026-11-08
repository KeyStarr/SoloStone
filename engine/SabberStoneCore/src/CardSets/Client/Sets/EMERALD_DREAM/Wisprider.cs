using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Wisprider
    {
        internal const int DbfId = 114292;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                // Lean_Wisprider: imbue-13 D2; targeted/choice/passive power trigger is a no-op.
                ctx.Imbue();
                ctx.TriggerCurrentHeroPower();
                return TaskState.COMPLETE;
            }) };
        }
    }
}

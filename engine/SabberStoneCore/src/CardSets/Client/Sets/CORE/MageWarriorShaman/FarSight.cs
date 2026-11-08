using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Tasks;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FarSight
    {
        internal const int DbfId = 76293;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                // An overdraw is burned, not drawn into hand, and must not receive the discount.
                var drawn = ctx.DrawEntity();
                if (drawn != null) ctx.GiveBuff(drawn.Id, "CS2_053e");
                return TaskState.COMPLETE;
            }) };
        }
    }
}

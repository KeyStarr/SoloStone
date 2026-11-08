using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreJeweledMacaw
    {
        internal const int DbfId = 97376;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                var chosen = ctx.PickRandomCard(CoreCostPools.Beast(), recipeDbfId: DbfId);
                if (chosen != null) ctx.AddToHand(chosen.Id);
                return TaskState.COMPLETE;
            }) };
        }
    }
}

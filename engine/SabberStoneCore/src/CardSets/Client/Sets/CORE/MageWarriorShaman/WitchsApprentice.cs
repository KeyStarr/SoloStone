using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WitchsApprentice
    {
        internal const int DbfId = 130790;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                var chosen = ctx.PickRandomCard(CoreCostPools.ShamanSpell(), recipeDbfId: DbfId);
                if (chosen != null) ctx.AddToHand(chosen.Id);
                return TaskState.COMPLETE;
            }) };
        }
    }
}

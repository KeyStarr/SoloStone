using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GnawingGreenfin
    {
        internal const int DbfId = 116256;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                var chosen = ctx.PickRandomCard(EmeraldDreamPools.Murloc(), recipeDbfId: DbfId);
                if (chosen != null) ctx.AddToHand(chosen.Id);
                return TaskState.COMPLETE;
            }) };
        }
    }
}

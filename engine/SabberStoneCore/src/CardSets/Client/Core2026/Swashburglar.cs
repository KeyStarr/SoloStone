using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class Swashburglar
    {
        internal const int DbfId = 69742;
        internal static void Configure(Card card)
        {
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                var chosen = ctx.PickRandomCard(Sets.CoreCostPools.OtherClassCard(), recipeDbfId: DbfId);
                if (chosen != null) ctx.AddToHand(chosen.Id);
                return TaskState.COMPLETE;
            })  };
        }
    }
}

using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BlindHarrower
    {
        internal const int DbfId = 130785;
        private static TaskState Grant(EffectContext ctx)
        {
            // Picked even into a full hand (the generated card burns), as other random-generation modules do.
            var chosen = ctx.PickRandomCard(BEPools.OutcastCard(), recipeDbfId: DbfId);
            return chosen == null ? TaskState.COMPLETE : ctx.AddToHand(chosen.Id);
        }
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Outcast is not a Battlecry (no BATTLECRY tag): Brann does not repeat it; summons never qualify.
            card.Power = new Power {
                PowerTask = EffectContext.Task(ctx => ctx.PlayedFromEdge ? Grant(ctx) : TaskState.COMPLETE),
                DeathrattleTask = EffectContext.Task(Grant)
            };
        }
    }
}

using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Jackpot
    {
        internal const int DbfId = 127470;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                for (int i = 0; i < 2; i++)
                {
                    var chosen = ctx.PickRandomCard(CoreCostPools.OtherClassExpensiveSpell(), recipeDbfId: DbfId);
                    if (chosen != null) ctx.AddToHand(chosen.Id);
                }
                return TaskState.COMPLETE;
            }) };
        }
    }
}

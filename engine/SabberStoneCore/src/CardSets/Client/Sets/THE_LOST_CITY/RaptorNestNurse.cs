using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RaptorNestNurse
    {
        internal const int DbfId = 118766;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = EffectContext.Task(ctx => {
                    var chosen = ctx.PickRandomCard(TheLostCityPools.OneCost(CardType.MINION), recipeDbfId: DbfId);
                    if (chosen != null) ctx.AddToHand(chosen.Id);
                    return TaskState.COMPLETE;
                }),
                DeathrattleTask = EffectContext.Task(ctx => {
                    var chosen = ctx.PickRandomCard(TheLostCityPools.OneCost(CardType.SPELL), recipeDbfId: DbfId);
                    if (chosen != null) ctx.AddToHand(chosen.Id);
                    return TaskState.COMPLETE;
                })
            };
        }
    }
}

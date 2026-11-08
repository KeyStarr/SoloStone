using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GuardDuty
    {
        internal const int DbfId = 118730;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                foreach (int cost in new[] { 6, 4, 2 })
                {
                    var chosen = ctx.PickRandomCard(TheLostCityPools.TauntAtCost(cost), recipeDbfId: DbfId);
                    if (chosen != null) ctx.Summon(chosen.Id);
                }
                return TaskState.COMPLETE;
            }) };
        }
    }
}

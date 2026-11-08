using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TwilightMender
    {
        internal const int DbfId = 117312;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = EffectContext.Task(ctx => {
                foreach (int school in new[] { 5, 6 })
                {
                    var chosen = ctx.PickRandomCard(TheLostCityPools.SpellSchool(school), recipeDbfId: DbfId);
                    if (chosen != null) ctx.AddToHand(chosen.Id);
                }
                return TaskState.COMPLETE;
            }) };
        }
    }
}

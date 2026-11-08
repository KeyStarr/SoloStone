using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CeremonialClash
    {
        internal const int DbfId = 123001;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                for (int cost = 3; cost >= 1; cost--)
                {
                    var chosen = ctx.PickRandomCard(CataclysmPools.CostMinion(cost), recipeDbfId: DbfId);
                    if (chosen != null) ctx.Summon(chosen.Id);
                }
                return TaskState.COMPLETE;
            }) };
        }
    }
}

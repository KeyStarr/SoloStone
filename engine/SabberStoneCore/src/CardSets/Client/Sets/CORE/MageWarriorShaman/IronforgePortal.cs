using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class IronforgePortal
    {
        internal const int DbfId = 127592;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new ArmorTask(4), EffectContext.Task(ctx => {
                var chosen = ctx.PickRandomCard(CoreCostPools.FourCostMinion(), recipeDbfId: DbfId);
                if (chosen != null) ctx.Summon(chosen.Id);
                return TaskState.COMPLETE;
            })) };
        }
    }
}

using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SilvermoonPortal
    {
        internal const int DbfId = 111367;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new AddEnchantmentTask("KAR_077e", EntityType.TARGET), EffectContext.Task(ctx => {
                    var chosen = ctx.PickRandomCard(CoreCostPools.TwoCostMinion(), recipeDbfId: DbfId);
                    if (chosen != null) ctx.Summon(chosen.Id);
                    return TaskState.COMPLETE;
                })) };
        }
    }
}

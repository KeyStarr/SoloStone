using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class BlazingInvocation
    {
        internal const int DbfId = 120572;
        internal static void Configure(Card card)
        {
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/CORE_GIL_836/v1",
                new DiscoverThenContinueTask(Sets.CoreCostPools.BattlecryMinion(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    new ApplyEffectTask(EntityType.TARGET, Effects.ReduceCost(1)).Process(g, c, s, chosen, stack);
                    if (Generic.RemoveFromZone(c, chosen)) Generic.AddHandPhase(c, chosen);
                })) };
        }
    }
}

using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Atlasaurus
    {
        internal const int DbfId = 118656;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = EffectContext.Task(ctx => {
                var chosen = ctx.PickRandomCard(TheLostCityPools.ExpensiveTaunt(), recipeDbfId: DbfId);
                if (chosen != null) ctx.RunLegacy(new SummonTask(chosen.Id, SummonSide.RIGHT));
                return TaskState.COMPLETE;
            }) };
        }
    }
}

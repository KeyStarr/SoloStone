using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Lifebloom
    {
        internal const int DbfId = 123485;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                ctx.RunLegacy(new HealTask(8, EntityType.FRIENDS));
                for (int i = 0; i < 2; i++)
                {
                    var chosen = ctx.PickRandomCard(CataclysmPools.CostMinion(8), recipeDbfId: DbfId);
                    if (chosen != null) ctx.Summon(chosen.Id);
                }
                return TaskState.COMPLETE;
            }) };
        }
    }
}

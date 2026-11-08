using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WardofEarth
    {
        internal const int DbfId = 110889;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                ctx.RunLegacy(new ArmorTask(5));
                var chosen = ctx.PickRandomCard(EmeraldDreamPools.FiveCostMinion(), recipeDbfId: DbfId);
                if (chosen != null)
                {
                    int summoned = ctx.Summon(chosen.Id);
                    if (summoned != 0) ctx.RunLegacy(new SetGameTagTask(GameTag.TAUNT, 1, EntityType.TARGET), summoned);
                }
                return TaskState.COMPLETE;
            }) };
        }
    }
}

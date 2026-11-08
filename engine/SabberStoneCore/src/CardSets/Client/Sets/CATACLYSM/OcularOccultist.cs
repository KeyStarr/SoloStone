using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class OcularOccultist
    {
        internal const int DbfId = 122710;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_490/v1",
                new SelectEntityThenContinueTask(),
                EffectContext.Task(ctx => ctx.LastChosenEntityId == 0 ? TaskState.COMPLETE :
                    ctx.RunLegacy(new DiscardTask(EntityType.TARGET), ctx.LastChosenEntityId))) };
        }
    }
}

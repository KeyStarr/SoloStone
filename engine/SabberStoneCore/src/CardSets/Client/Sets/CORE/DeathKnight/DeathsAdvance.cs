using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DeathsAdvance
    {
        internal const int DbfId = 127412;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new SetGameTagTask(GameTag.FROZEN, 1, EntityType.TARGET),
                EffectContext.Task(ctx => {
                    ctx.Discover(OfflinePoolDeclarations.DeathsAdvanceSpell(), recipeDbfId: DbfId);
                    return TaskState.COMPLETE;
                })) };
        }
    }
}

using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ColdSnap
    {
        internal const int DbfId = 126356;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 } });
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                ctx.RunLegacy(ComplexTask.Freeze(EntityType.TARGET));
                var chosen = ctx.PickRandomCard(OfflinePoolDeclarations.FrostSpell(), recipeDbfId: DbfId);
                if (chosen != null) ctx.AddToHand(chosen.Id);
                return TaskState.COMPLETE;
            }) };
        }
    }
}

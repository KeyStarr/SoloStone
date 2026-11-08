using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Photosynthesis
    {
        internal const int DbfId = 114914;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                if (ctx.TargetId is int target) ctx.Heal(target, 6);
                for (int i = 0; i < 3; i++)
                {
                    var chosen = ctx.PickRandomCard(OfflinePoolDeclarations.DruidSpell(), recipeDbfId: DbfId);
                    if (chosen != null) ctx.AddToHand(chosen.Id);
                }
                return TaskState.COMPLETE;
            }) };
        }
    }
}

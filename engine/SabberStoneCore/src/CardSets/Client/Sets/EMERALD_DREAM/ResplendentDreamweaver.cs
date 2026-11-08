using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ResplendentDreamweaver
    {
        internal const int DbfId = 114076;
        private static bool ImbuedTwice(IPlayable source) => ImbueRules.Count(source.Controller) >= 2;

        [StandardCard("EDR_860", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(ImbuedTwice);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.TargetingAvailabilityPredicate = (controller, _) => ImbueRules.Count(controller) >= 2;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                if (ctx.ImbueCount >= 2 && ctx.TargetId is int target) ctx.Damage(target, 4);
                return TaskState.COMPLETE;
            }) };
        }
    }
}

using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WoundPrey
    {
        internal const int DbfId = 117039;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                if (ctx.TargetId is int target) ctx.Damage(target, 1, spellDamage: true);
                ctx.Summon("BAR_035t");
                return TaskState.COMPLETE;
            }) };
        }
    }
}

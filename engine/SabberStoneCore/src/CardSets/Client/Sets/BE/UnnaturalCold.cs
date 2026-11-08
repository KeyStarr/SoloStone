using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;
using SabberStoneCore.Enums;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UnnaturalCold
    {
        internal const int DbfId = 131059;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 } });
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => ctx.RunLegacy(ComplexTask.Create(ComplexTask.Freeze(EntityType.TARGET), new ArmorTask(3), new DrawTask(1)))) };
        }
    }
}

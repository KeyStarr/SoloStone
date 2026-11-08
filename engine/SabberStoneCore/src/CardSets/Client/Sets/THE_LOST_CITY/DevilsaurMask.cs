using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;
using SabberStoneCore.Enums;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DevilsaurMask
    {
        internal const int DbfId = 118408;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                if (ctx.TargetId is int target) ctx.GiveBuff(target, "DINO_403e");
                return TaskState.COMPLETE;
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(8), Effects.SetMaxHealth(8), Effects.Charge) };
        }
    }
}

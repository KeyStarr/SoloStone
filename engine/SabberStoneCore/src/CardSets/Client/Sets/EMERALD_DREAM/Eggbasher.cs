using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Eggbasher
    {
        internal const int DbfId = 116293;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                if (ctx.TargetId is int target) {
                    ctx.Damage(target, 1);
                    ctx.GiveBuff(target, "EDR_468e1");
                }
                return TaskState.COMPLETE;
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(4)) };
        }
    }
}

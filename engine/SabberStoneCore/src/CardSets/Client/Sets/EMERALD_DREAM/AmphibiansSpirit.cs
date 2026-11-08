using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AmphibiansSpirit
    {
        internal const int DbfId = 115052;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => ctx.TargetId is int target ? ctx.GiveBuff(target, "EDR_261e") : TaskState.COMPLETE) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power {
                Enchant = new Enchant(Effects.AttackHealth_N(2)),
                DeathrattleTask = EffectContext.Task(ctx => ctx.RunLegacy(ComplexTask.Create(
                    new RandomTask(1, EntityType.MINIONS), new AddEnchantmentTask("EDR_261e", EntityType.STACK))))
            };
        }
    }
}

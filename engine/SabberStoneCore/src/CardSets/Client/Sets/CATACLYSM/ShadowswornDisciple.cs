using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShadowswornDisciple
    {
        internal const int DbfId = 122643;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new HealTask(3, EntityType.HERO), PowerTask = EffectContext.Task(ctx => {
                    ctx.Herald();
                    return TaskState.COMPLETE;
                }) };
        }
    }
}

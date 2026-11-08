using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AfflictedDevastator
    {
        internal const int DbfId = 114243;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = EffectContext.Task(ctx => ctx.RunLegacy(new DamageTask(3, EntityType.MINIONS_NOSOURCE))),
                DeathrattleTask = EffectContext.Task(ctx => ctx.RunLegacy(new DamageTask(3, EntityType.OP_MINIONS)))
            };
        }
    }
}

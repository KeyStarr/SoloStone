using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SahketSapper
    {
        internal const int DbfId = 129343;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = ComplexTask.Create(
                new IncludeTask(EntityType.OP_MINIONS),
                new FilterStackTask(SelfCondition.IsNotDead),
                new RandomTask(1, EntityType.STACK),
                // Destroy in play when the hand is full so Deathrattles and death counters run.
                new ConditionTask(EntityType.SOURCE, SelfCondition.IsOpHandFull),
                new FlagTask(true, new DestroyTask(EntityType.STACK)),
                new FlagTask(false, new ReturnHandTask(EntityType.STACK))) };
        }
    }
}

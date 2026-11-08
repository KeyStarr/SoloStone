using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CuriousExplorer
    {
        internal const int DbfId = 118223;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = ComplexTask.Create(
                new IncludeTask(EntityType.OP_HAND),
                new FilterStackTask(SelfCondition.IsMinion),
                new RandomTask(1, EntityType.STACK),
                new ApplyEffectTask(EntityType.STACK, Effects.ReduceCost(2))) };
        }
    }
}

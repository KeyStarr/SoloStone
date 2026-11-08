using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShadowDance
    {
        internal const int DbfId = 129340;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new IncludeTask(EntityType.ENEMIES),
                new FilterStackTask(SelfCondition.IsUndamaged),
                new DamageTask(1, EntityType.STACK, true)) };
        }
    }
}

using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShadowWordRuin
    {
        internal const int DbfId = 69616;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new IncludeTask(EntityType.ALLMINIONS),
                new FilterStackTask(new SelfCondition(p => ((Minion)p).AttackDamage >= 5)), new DestroyTask(EntityType.STACK)) };
        }
    }
}

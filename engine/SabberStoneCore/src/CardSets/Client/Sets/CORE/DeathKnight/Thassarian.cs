using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Thassarian
    {
        internal const int DbfId = 86731;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 2),
                DeathrattleTask = ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 2)
            };
        }
    }
}

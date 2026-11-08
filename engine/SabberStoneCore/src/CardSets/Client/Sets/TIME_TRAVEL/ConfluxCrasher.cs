using SabberStoneCore.Enchants;
using SabberStoneCore.Tasks;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ConfluxCrasher
    {
        internal const int DbfId = 119317;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 7) };
        }
    }
}

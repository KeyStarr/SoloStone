using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AstralRain
    {
        internal const int DbfId = 129811;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = SplitHits.Inline(5,
                ComplexTask.DamageRandomTargets(1, EntityType.OP_MINIONS, 2, true), deathProcessing: true) };
        }
    }
}

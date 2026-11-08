using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BonechillStegodon
    {
        internal const int DbfId = 116376;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = ComplexTask.Create(
                new RandomTask(3, EntityType.ENEMIES), new DamageTask(6, EntityType.STACK)) };
        }
    }
}

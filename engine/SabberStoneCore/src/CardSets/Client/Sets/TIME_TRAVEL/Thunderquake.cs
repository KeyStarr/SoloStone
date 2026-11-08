using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Thunderquake
    {
        internal const int DbfId = 120176;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new DamageTask(1, EntityType.ALLMINIONS, spellDmg: true),
                new AddCardTo("TIME_218", EntityType.HAND)) };
        }
    }
}

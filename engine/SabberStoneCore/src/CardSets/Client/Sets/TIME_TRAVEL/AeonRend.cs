using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AeonRend
    {
        internal const int DbfId = 120922;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var targets = new TaskStack();
                if (new RandomTask(2, EntityType.ENEMIES).Process(g, c, s, null, targets) == TaskState.STOP) return;
                new DamageTask(4, EntityType.STACK, spellDmg: true).Process(g, c, s, null, targets);
            }) };
        }
    }
}

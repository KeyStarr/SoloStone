using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MadBomber
    {
        internal const int DbfId = 69662;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Tags[(GameTag)858] = 762;
            card.Power = new Power { PowerTask = SplitHits.Inline(3,
                new CustomTask((g, c, s, t, stack) => {
                    ComplexTask.DamageRandomTargets(1, EntityType.ALL_NOSOURCE, 1).Process(g, c, s, t, stack);
                    // Shield-loss reactions also finish before the next hit; deaths still wait.
                    g.ProcessTasks();
                }), deathProcessing: false) };
        }
    }
}

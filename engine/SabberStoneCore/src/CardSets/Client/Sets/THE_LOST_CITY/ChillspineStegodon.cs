using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChillspineStegodon
    {
        internal const int DbfId = 118501;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                stack = stack ?? new TaskStack();
                if (new RandomTask(2, EntityType.OP_MINIONS).Process(g, c, s, t, stack) == TaskState.STOP)
                {
                    // An empty selection still activates the matched Kindred clause.
                    KindredRules.BeginMatchedClause((IPlayable)s);
                    return;
                }
                new DamageTask(2, EntityType.STACK).Process(g, c, s, t, stack);
                KindredRules.Clause(new SetGameTagTask(GameTag.FROZEN, 1, EntityType.STACK)).Process(g, c, s, t, stack);
            }) };
        }
    }
}

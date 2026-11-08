using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TranquilTreant
    {
        internal const int DbfId = 116292;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = EffectContext.Task(ctx => ctx.RunLegacy(ComplexTask.Create(new ManaCrystalEmptyTask(1), new ManaCrystalEmptyTask(1, opponent: true)))) };
        }
    }
}

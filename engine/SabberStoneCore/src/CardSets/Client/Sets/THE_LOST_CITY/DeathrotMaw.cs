using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DeathrotMaw
    {
        internal const int DbfId = 119789;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = EffectContext.Task(ctx => {
                var chosen = ctx.SampleDistinct(new[] { "TLC_446t2", "TLC_446t3", "TLC_446t4" }, 1)[0];
                return ctx.RunLegacy(new SummonTask(chosen, SummonSide.RIGHT));
            }) };
        }
    }
}

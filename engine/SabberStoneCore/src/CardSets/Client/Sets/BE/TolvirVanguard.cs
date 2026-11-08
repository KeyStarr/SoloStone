using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TolvirVanguard
    {
        internal const int DbfId = 130567;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => ctx.RunLegacy(new ArmorTask(3))), DeathrattleTask = EffectContext.Task(ctx => ctx.RunLegacy(new ArmorTask(3))) };
        }
    }
}

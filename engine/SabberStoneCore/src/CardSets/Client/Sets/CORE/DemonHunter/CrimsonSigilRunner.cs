using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CrimsonSigilRunner
    {
        internal const int DbfId = 69683;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Outcast is not a Battlecry: no BATTLECRY tag, so Brann's replay gate stays closed.
            card.Power = new Power { PowerTask = EffectContext.Task(ctx =>
                ctx.PlayedFromEdge ? ctx.RunLegacy(new DrawTask()) : TaskState.COMPLETE) };
        }
    }
}

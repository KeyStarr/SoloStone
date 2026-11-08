using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DemolitionRenovator
    {
        internal const int DbfId = 115971;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                BattlecryLocationTarget = LocationTargetKind.EnemyLocation,
                PowerTask = EffectContext.Task(context => context.TargetId.HasValue
                    ? context.Destroy(context.TargetId.Value) : TaskState.COMPLETE)
            };
        }
    }
}

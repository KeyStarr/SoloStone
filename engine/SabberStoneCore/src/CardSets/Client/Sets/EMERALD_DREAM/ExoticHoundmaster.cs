using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ExoticHoundmaster
    {
        internal const int DbfId = 114018;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                // Lean_ConsumerClauseOrder: imbue-16, draw failure does not suppress Imbue.
                ctx.DrawWhere(entity => entity.Card.IsRace(Race.BEAST));
                ctx.Imbue();
                return TaskState.COMPLETE;
            }) };
        }
    }
}

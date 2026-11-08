using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Finality
    {
        internal const int DbfId = 121271;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                ctx.DrawWhere(candidate => candidate.Card.IsRace(Race.UNDEAD));
                // Lean_ imbue-16: a missing/burned draw does not suppress either Imbue.
                ctx.Imbue(2);
                return TaskState.COMPLETE;
            }) };
        }
    }
}

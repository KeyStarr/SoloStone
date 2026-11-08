using SabberStoneCore.Enchants;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TickAndTock
    {
        internal const int DbfId = 121160;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = EffectContext.Task(ctx => ctx.DrawUntilHandFull()),
                DeathrattleTask = EffectContext.Task(ctx => ctx.EmptyOpponentHand())
            };
        }
    }
}

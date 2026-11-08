using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Techysaurus
    {
        internal const int DbfId = 118485;
        // Rule: count own committed foreign-origin hand plays, including countered spells; automatic casts are not plays (R1/R2).
        // A fair view's erased origins (an opponent's past plays seen from a search sample) count as unknown, not foreign.
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { Aura = new AdaptiveCostEffect(p => p.Game.HistoryFacts.Query.Plays()
                .Count(f => f.ControllerId == p.Controller.Id && f.OriginKnown && !f.StartedInDeck)) };
        }
    }
}

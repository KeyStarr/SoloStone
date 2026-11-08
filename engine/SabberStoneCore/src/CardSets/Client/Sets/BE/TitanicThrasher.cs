using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TitanicThrasher
    {
        internal const int DbfId = 130769;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new AdaptiveCostEffect(p => p.Controller.Opponent.BoardZone.Count) };
        }
    }
}

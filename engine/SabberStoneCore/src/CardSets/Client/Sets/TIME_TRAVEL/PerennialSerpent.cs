using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PerennialSerpent
    {
        internal const int DbfId = 120181;
        [StandardCard("TIME_022", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new AdaptiveCostEffect(Discount) };
        }

        private static int Discount(IPlayable source) => source.Controller.BoardZone
            .Concat(source.Controller.Opponent.BoardZone).Any(m => m.IsDormant) ? 4 : 0;
    }
}

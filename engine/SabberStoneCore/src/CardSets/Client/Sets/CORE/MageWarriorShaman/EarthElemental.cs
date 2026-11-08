using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EarthElemental
    {
        internal const int DbfId = 69627;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // The pinned OVERLOAD tag is a marker; the payment block reads this amount.
            card.Overload = 2;
        }
    }
}

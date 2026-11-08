using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Hex
    {
        internal const int DbfId = 69551;
        internal static void Configure(Card card)
        {
            // Retain the existing legacy/rebalanced power and random-pool membership.
            card.Implemented = true;
        }
    }
}

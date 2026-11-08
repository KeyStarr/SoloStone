using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RavagingGhoul
    {
        internal const int DbfId = 120573;
        internal static void Configure(Card card)
        {
            // Retain the existing legacy/rebalanced power and random-pool membership.
            card.Implemented = true;
        }
    }
}

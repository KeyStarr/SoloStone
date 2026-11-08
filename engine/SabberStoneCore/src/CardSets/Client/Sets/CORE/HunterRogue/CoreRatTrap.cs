using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreRatTrap
    {
        internal const int DbfId = 111363;
        internal static void Configure(Card card)
        {
            // Retain the already configured legacy/rebalanced power and legacy pool membership.
            card.Implemented = true;
        }
    }
}

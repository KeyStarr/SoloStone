using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreQuickShot
    {
        internal const int DbfId = 69691;
        internal static void Configure(Card card)
        {
            // Retain the already configured legacy/rebalanced power and legacy pool membership.
            card.Implemented = true;
        }
    }
}

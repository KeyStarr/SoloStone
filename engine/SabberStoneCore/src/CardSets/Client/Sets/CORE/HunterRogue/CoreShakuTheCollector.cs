using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreShakuTheCollector
    {
        internal const int DbfId = 120463;
        internal static void Configure(Card card)
        {
            // Retain the already configured legacy/rebalanced power and legacy pool membership.
            card.Implemented = true;
        }
    }
}

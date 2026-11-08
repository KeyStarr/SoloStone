using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreHellfire
    {
        internal const int DbfId = 69519;
        internal static void Configure(Card card)
        {
            // Retain the existing reprint/rebalanced power and legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)203] = 1;
            card.Tags[(GameTag)858] = 950;
            card.Tags[(GameTag)1635] = 2;
        }
    }
}

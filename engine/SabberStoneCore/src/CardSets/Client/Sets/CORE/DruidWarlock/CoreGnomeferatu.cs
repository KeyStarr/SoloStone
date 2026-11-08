using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreGnomeferatu
    {
        internal const int DbfId = 95710;
        internal static void Configure(Card card)
        {
            // Retain the existing reprint/rebalanced power and legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)200] = 11;
            card.Tags[(GameTag)858] = 42670;
            card.Tags[(GameTag)4533] = 1;
        }
    }
}

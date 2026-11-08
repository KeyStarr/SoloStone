using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreMarkOfTheWild
    {
        internal const int DbfId = 69511;
        internal static void Configure(Card card)
        {
            // Retain the existing reprint/rebalanced power and legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)203] = 3;
            card.Tags[(GameTag)858] = 213;
            card.Tags[(GameTag)1635] = 4;
        }
    }
}

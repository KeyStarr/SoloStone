using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Innervate
    {
        internal const int DbfId = 69550;
        internal static void Configure(Card card)
        {
            // Retain the existing reprint/rebalanced power and legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)203] = 3;
            card.Tags[(GameTag)858] = 254;
            card.Tags[(GameTag)1635] = 4;
            card.Tags[(GameTag)2589] = 1;
        }
    }
}

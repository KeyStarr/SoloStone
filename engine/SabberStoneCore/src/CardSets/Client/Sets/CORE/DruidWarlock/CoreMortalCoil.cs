using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreMortalCoil
    {
        internal const int DbfId = 69553;
        internal static void Configure(Card card)
        {
            // Retain the existing reprint/rebalanced power and legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)203] = 1;
            card.Tags[(GameTag)858] = 1092;
            card.Tags[(GameTag)1635] = 6;
        }
    }
}

using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SavageStriker
    {
        internal const int DbfId = 126889;
        internal static void Configure(Card card)
        {
            // Retain the existing reprint/rebalanced power and legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 49984;
            card.Tags[(GameTag)1413] = 49984;
        }
    }
}

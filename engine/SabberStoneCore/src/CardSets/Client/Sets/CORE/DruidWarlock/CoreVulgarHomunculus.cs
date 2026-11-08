using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreVulgarHomunculus
    {
        internal const int DbfId = 117060;
        internal static void Configure(Card card)
        {
            // Retain the existing reprint/rebalanced power and legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 43121;
            card.Tags[(GameTag)1413] = 43121;
            card.Tags[(GameTag)1965] = 1;
            card.Tags[(GameTag)4533] = 1;
            card.Tags[(GameTag)4547] = 1;
        }
    }
}

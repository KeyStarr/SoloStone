using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FlashHeal
    {
        internal const int DbfId = 69707;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 2582;
            card.Tags[(GameTag)1635] = 5;
        }
    }
}

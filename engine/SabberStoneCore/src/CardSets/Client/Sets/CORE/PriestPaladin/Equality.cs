using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Equality
    {
        internal const int DbfId = 69614;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 756;
            card.Tags[(GameTag)1635] = 5;
        }
    }
}

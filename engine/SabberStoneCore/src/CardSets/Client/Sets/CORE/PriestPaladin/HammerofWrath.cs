using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HammerofWrath
    {
        internal const int DbfId = 102790;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 250;
            card.Tags[(GameTag)1635] = 5;
        }
    }
}

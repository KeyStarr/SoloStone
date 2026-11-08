using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PowerWordShield
    {
        internal const int DbfId = 120786;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 613;
            card.Tags[(GameTag)1413] = 613;
            card.Tags[(GameTag)1635] = 5;
        }
    }
}

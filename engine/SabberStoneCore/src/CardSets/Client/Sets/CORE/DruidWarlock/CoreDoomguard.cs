using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreDoomguard
    {
        internal const int DbfId = 111457;
        internal static void Configure(Card card)
        {
            // Retain the existing reprint/rebalanced power and legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 631;
            card.Tags[(GameTag)4533] = 1;
        }
    }
}

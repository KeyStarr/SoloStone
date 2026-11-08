using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArgentProtector
    {
        internal const int DbfId = 69611;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 1022;
        }
    }
}

using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HolySmite
    {
        internal const int DbfId = 69509;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Text = "Deal $3 damage\nto a minion.";
            card.Tags[(GameTag)858] = 279;
            card.Tags[(GameTag)1635] = 5;
        }
    }
}

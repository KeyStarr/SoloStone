using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Consecration
    {
        internal const int DbfId = 69532;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Text = "Deal $2 damage to all enemies.";
            card.Tags[(GameTag)858] = 476;
            card.Tags[(GameTag)1635] = 5;
        }
    }
}

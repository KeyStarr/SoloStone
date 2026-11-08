using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GreaterHealingPotion
    {
        internal const int DbfId = 120459;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Text = "Restore #12 Health to a friendly character. Draw a card.";
            card.Tags[(GameTag)858] = 40375;
            card.Tags[(GameTag)1413] = 40375;
            card.Tags[(GameTag)1635] = 5;
        }
    }
}

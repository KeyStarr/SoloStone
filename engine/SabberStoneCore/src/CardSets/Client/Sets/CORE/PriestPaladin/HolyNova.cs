using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HolyNova
    {
        internal const int DbfId = 69508;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Text = "Deal $2 damage to all enemy minions. Restore #2 Health to all friendly characters.";
            card.Tags[(GameTag)858] = 841;
            card.Tags[(GameTag)1635] = 5;
        }
    }
}

using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShadowAscendant
    {
        internal const int DbfId = 95746;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Text = "[x]At the end of your turn,&\ngive another random\nfriendly minion +1/+1.";
            card.Tags[(GameTag)858] = 42574;
            card.Tags[(GameTag)4533] = 1;
            card.Tags[(GameTag)4577] = 1;
        }
    }
}

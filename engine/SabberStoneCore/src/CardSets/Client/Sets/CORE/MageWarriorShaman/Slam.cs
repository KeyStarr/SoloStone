using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Slam
    {
        internal const int DbfId = 69638;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Text = "Deal $2 damage to a minion. If it survives, draw a card.";
            card.Tags[(GameTag)201] = 3;
            card.Tags[(GameTag)251] = 1;
            card.Tags[(GameTag)858] = 1074;
        }
    }
}

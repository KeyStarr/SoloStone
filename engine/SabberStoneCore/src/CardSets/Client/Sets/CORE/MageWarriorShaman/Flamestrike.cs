using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Flamestrike
    {
        internal const int DbfId = 69502;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Text = "Deal $5 damage to all enemy minions.";
            card.Tags[(GameTag)201] = 3;
            card.Tags[(GameTag)251] = 1;
            card.Tags[(GameTag)268] = 2;
            card.Tags[(GameTag)858] = 1004;
        }
    }
}

using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Fireball
    {
        internal const int DbfId = 69501;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Text = "Deal $6 damage.";
            card.Tags[(GameTag)201] = 3;
            card.Tags[(GameTag)251] = 3;
            card.Tags[(GameTag)268] = 2;
            card.Tags[(GameTag)858] = 315;
        }
    }
}

using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SenjinShieldmasta
    {
        internal const int DbfId = 69542;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Tags[(GameTag)251] = 1;
            card.Tags[(GameTag)268] = 2;
            card.Tags[(GameTag)858] = 635;
        }
    }
}

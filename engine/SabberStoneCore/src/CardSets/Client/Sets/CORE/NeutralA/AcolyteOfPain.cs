using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AcolyteOfPain
    {
        internal const int DbfId = 76316;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Text = "Whenever this minion takes damage,& draw a_card.";
            card.Tags[(GameTag)858] = 1659;
            card.Tags[(GameTag)4533] = 1;
        }
    }
}

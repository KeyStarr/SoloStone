using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class VoodooDoctor
    {
        internal const int DbfId = 69547;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Text = "<b>Battlecry:</b> Restore #2_Health.";
            card.Tags[(GameTag)251] = 6;
            card.Tags[(GameTag)858] = 132;
        }
    }
}

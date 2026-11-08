using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PlatedBeetle
    {
        internal const int DbfId = 76323;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Text = "<b>Deathrattle:</b>& Gain 3 Armor.";
            card.Tags[(GameTag)858] = 45930;
            card.Tags[(GameTag)4533] = 1;
        }
    }
}

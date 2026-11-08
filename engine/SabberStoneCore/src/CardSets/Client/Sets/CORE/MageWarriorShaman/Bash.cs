using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Bash
    {
        internal const int DbfId = 76304;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Text = "Deal $3 damage.\nGain 3 Armor.";
            card.Tags[(GameTag)858] = 2729;
        }
    }
}

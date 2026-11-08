using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Blizzard
    {
        internal const int DbfId = 76273;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Text = "Deal $2 damage to all enemy minions and <b>Freeze</b> them.";
            card.Tags[(GameTag)201] = 3;
            card.Tags[(GameTag)251] = 1;
            card.Tags[(GameTag)268] = 2;
            card.Tags[(GameTag)858] = 457;
        }
    }
}

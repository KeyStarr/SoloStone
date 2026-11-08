using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ObsidianStatue
    {
        internal const int DbfId = 95732;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Text = "[x]<b><b>Taunt</b>, Lifesteal</b>\n<b>Deathrattle:</b>& Destroy a\n random enemy minion.";
            card.Tags[(GameTag)858] = 42598;
            card.Tags[(GameTag)4533] = 1;
        }
    }
}

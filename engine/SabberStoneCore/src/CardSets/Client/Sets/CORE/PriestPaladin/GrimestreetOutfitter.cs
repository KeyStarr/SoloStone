using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GrimestreetOutfitter
    {
        internal const int DbfId = 102196;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Text = "<b>Battlecry:</b>& Give all minions in your hand +1/+1.";
            card.Tags[(GameTag)858] = 40567;
            card.Tags[(GameTag)4533] = 1;
        }
    }
}

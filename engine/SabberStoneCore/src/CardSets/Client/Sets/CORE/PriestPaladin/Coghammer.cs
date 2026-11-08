using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Coghammer
    {
        internal const int DbfId = 130820;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Text = "<b>Battlecry:</b>& Give a random friendly minion <b>Divine Shield</b> and <b>Taunt</b>.";
            card.Tags[(GameTag)858] = 2027;
            card.Tags[(GameTag)1413] = 2027;
            card.Tags[(GameTag)4533] = 1;
            card.Tags[(GameTag)4546] = 1;
        }
    }
}

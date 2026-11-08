using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TirionFordring
    {
        internal const int DbfId = 69613;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Text = "<b>Divine Shield</b>, <b>Taunt</b> <b>Deathrattle:</b>& Equip a 5/3_Ashbringer.";
            card.Tags[(GameTag)858] = 890;
            card.Tags[(GameTag)1452] = 1730;
            card.Tags[(GameTag)2589] = 1;
            card.Tags[(GameTag)4533] = 1;
        }
    }
}

using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ConjuredBookkeeper
    {
        internal const int DbfId = 117535;
        internal static void Configure(Card card)
        {
            // Preserve existing legacy pool membership, as in corelegacy2.
            card.Implemented = true;
            card.Text = "<b>Deathrattle:</b> Draw a spell. <b>Kindred:</b>& Summon a copy of this.";
            card.Tags[(GameTag)2589] = 1;
            card.Tags[(GameTag)4533] = 1;
            card.Tags[(GameTag)4548] = 1;
            card.Tags[(GameTag)4930] = 1;
        }
    }
}

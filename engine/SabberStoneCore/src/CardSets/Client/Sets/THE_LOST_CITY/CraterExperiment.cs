using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CraterExperiment
    {
        internal const int DbfId = 119695;
        internal static void Configure(Card card)
        {
            // Preserve existing legacy pool membership, as in corelegacy2.
            card.Implemented = true;
            card.Tags[(GameTag)1824] = 1;
            card.Tags[(GameTag)4548] = 1;
        }
    }
}

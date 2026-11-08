using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArcaneIntellect
    {
        [StandardCard("CORE_CS2_023", 69500)]
        internal static void Configure(Card card)
        {
            // Retain the existing DrawTask and frozen legacy pool membership; correct current-printing metadata.
            card.Implemented = true;
            card.Tags[(GameTag)201] = 3;
            card.Tags[(GameTag)251] = 1;
            card.Tags[(GameTag)268] = 2;
            card.Tags[(GameTag)858] = 555;
        }
    }
}

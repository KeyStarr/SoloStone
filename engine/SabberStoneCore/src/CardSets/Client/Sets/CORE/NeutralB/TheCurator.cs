using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheCurator
    {
        internal const int DbfId = 120180;
        internal static void Configure(Card card)
        {
            // Retain the existing power and legacy pool membership (owner decision, 2026-10-21).
            card.Implemented = true;
        }
    }
}

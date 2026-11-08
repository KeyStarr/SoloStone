using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TarCreeper
    {
        internal const int DbfId = 76319;
        internal static void Configure(Card card)
        {
            // Retain the existing power and legacy pool membership (owner decision, 2026-10-21).
            card.Implemented = true;
        }
    }
}

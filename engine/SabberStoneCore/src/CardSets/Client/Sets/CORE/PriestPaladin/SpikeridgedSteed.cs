using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SpikeridgedSteed
    {
        internal const int DbfId = 111369;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
        }
    }
}

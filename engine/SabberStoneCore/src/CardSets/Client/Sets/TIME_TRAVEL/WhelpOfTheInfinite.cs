using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WhelpOfTheInfinite
    {
        internal const int DbfId = 120856;
        [StandardCard("TIME_045", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Pinned presentation metadata correction; replay migration audited in docs/reports/b14-k2-corrections.md.
            card.Tags[(GameTag)2355] = 1;
            card.Tags[(GameTag)2589] = 1;
        }
    }
}

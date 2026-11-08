using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SlumberingSprite
    {
        [StandardCard("EDR_469", 114344)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            // Starts Dormant is a definition keyword; the existing id-scoped entry/awakening lifecycle supplies runtime DORMANT.
            card.Tags.Remove(GameTag.DORMANT);
            card.Tags[(GameTag)1652] = 1;
        }
    }
}

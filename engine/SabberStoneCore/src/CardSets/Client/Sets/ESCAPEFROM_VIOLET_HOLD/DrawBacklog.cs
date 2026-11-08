using SabberStoneCore.Model;
using SabberStoneCore.Enchants;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DrawBacklog
    {
        // Start of Game admission belongs to Godfrey's group; this is the shared retained-draw host.
        [StandardCard("JAIL_509e", 126701)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Power = new Power();
            card.ExcludeFromLegacyRandomPools = true;
        }
    }
}

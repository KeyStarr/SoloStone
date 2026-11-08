using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DisguisedDetective
    {
        [StandardCard("JAIL_452", 126627)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Overload = 2;
            card.Power = new Power();
        }
        [StandardCard("JAIL_452a", 130022)]
        [StandardCard("JAIL_452b", 130023)]
        internal static void Side(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
    }
}

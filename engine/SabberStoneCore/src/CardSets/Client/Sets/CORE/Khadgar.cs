using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Khadgar
    {
        [StandardCard("CORE_DAL_575", 126975)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
            // Legacy alias shares the facility, without becoming a second Standard collectible.
            var alias = Cards.FromId("DAL_575");
            alias.Power = card.Power;
            alias.Implemented = true;
        }
    }
}

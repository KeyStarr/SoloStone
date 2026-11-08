using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Raincaller
    {
        [StandardCard("CATA_487", 123419)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Positive resolved spell damage is dispatched by StatDamageRules; state lives in exported actors.
        }
        [StandardCard("CATA_487e", 126249)]
        internal static void ConfigureAttack(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2)) };
        }
        [StandardCard("CATA_487e1", 131270)]
        internal static void ConfigureLock(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}

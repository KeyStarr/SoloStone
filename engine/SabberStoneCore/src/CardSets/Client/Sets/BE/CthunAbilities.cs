using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CthunAbilities
    {
        internal static void Configure(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
        internal static void ConfigureDomain(Card card)
        {
            Configure(card);
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(3)) };
        }
    }
}

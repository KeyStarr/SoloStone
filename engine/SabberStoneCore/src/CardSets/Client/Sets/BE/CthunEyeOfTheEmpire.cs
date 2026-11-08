using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CthunEyeOfTheEmpire
    {
        [StandardCard("BE_013",132339)]
        internal static void Configure(Card card) { _ = CthunRules.OnBoardRecipe; card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
        [StandardCard("BE_013t6",134968)]
        internal static void Glare(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
        [StandardCard("BE_013t2e2",132341)]
        internal static void Domain(Card card) {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(6),Effects.Health_N(9)) };
        }
    }
}

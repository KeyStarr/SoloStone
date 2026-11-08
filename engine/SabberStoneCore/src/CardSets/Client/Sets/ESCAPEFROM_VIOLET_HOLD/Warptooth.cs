using SabberStoneCore.Actions;
using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Warptooth
    {
        [StandardCard("JAIL_421", 125803)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            SwapRules.DeclareDamageSummon(card, 4);
        }
    }
}

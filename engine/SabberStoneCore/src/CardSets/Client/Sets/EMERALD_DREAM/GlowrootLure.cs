using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GlowrootLure
    {
        internal const int DbfId = 114920;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                // Actions/HeroPower.cs increments this controller tag after every successful use.
                Aura = new AdaptiveCostEffect(p => p.Controller.NumTimesHeroPowerUsedThisGame)
            };
        }
    }
}

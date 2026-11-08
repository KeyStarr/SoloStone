using SabberStoneCore.Actions;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ToreththeUnbreaking
    {
        [StandardCard("EDR_258", 114544)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[DeathContextRules.ShieldAura] = 1;
        }
    }
}

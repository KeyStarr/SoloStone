using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ScrappyDefender
    {
        internal const int DbfId = 126104;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new AdaptiveEffect(GameTag.ATK, EffectOperator.ADD,
                p => p.Controller.DeckZone.Count >= 25 ? 5 : 0) };
        }

    }
}

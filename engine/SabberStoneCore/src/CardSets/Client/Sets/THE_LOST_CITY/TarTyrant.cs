using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TarTyrant
    {
        internal const int DbfId = 117733;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new AdaptiveEffect(GameTag.ATK, EffectOperator.ADD,
                p => p.Controller == p.Game.CurrentOpponent ? 6 : 0) };
        }
    }
}

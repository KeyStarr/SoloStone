using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SolitaryPrisoner
    {
        internal const int DbfId = 127994;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(NoOtherMinions);
            card.Power = new Power { Aura = new AdaptiveCostEffect(p => 2, EffectOperator.SET,
                new SelfCondition(NoOtherMinions)) };
        }
        private static bool NoOtherMinions(IPlayable source) =>
            !source.Controller.BoardZone.Any(p => p.Id != source.Id)
                && !source.Controller.Opponent.BoardZone.Any(p => p.Id != source.Id);

    }
}

using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GladesongSiren
    {
        internal const int DbfId = 117563;
        // Rule: completed Player casts of both Holy (5) and Shadow (6) this turn; countered/minion recasts do not qualify (R1 lean).
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CastHolyAndShadowThisTurn);
            card.ConsumesHistory = true;
            card.Power = new Power { Aura = new AdaptiveCostEffect(p => 1, EffectOperator.SET, new SelfCondition(CastHolyAndShadowThisTurn)) };
        }

        private static bool CastHolyAndShadowThisTurn(IPlayable source)
        {
            var history = source.Game.HistoryFacts.Query;
            var casts = history.Casts(history.ThisTurn).Where(f => f.ControllerId == source.Controller.Id
                && f.EntityId != source.Id && f.CasterKind == HistoryCastKind.Player
                && f.Outcome == HistoryCastOutcome.Completed).ToArray();
            return casts.Any(f => f.SpellSchool == 5) && casts.Any(f => f.SpellSchool == 6);
        }
    }
}

using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RavenousFelfisher
    {
        internal const int DbfId = 122775;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            // Rule: completed own Player/Repeat Fel casts count; countered, Effect and Drawn casts do not (R1).
            card.Power = new Power { Aura = new AdaptiveCostEffect(p => p.Game.HistoryFacts.Query.Casts()
                .Count(f => f.ControllerId == p.Controller.Id && f.SpellSchool == 7
                    && f.Outcome == HistoryCastOutcome.Completed
                    && (f.CasterKind == HistoryCastKind.Player || f.CasterKind == HistoryCastKind.Repeat))) };
        }
    }
}

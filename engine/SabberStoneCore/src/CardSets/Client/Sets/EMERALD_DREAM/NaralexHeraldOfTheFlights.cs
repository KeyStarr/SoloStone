using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NaralexHeraldOfTheFlights
    {
        [StandardCard("EDR_844", 114849)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.Power = new Power { Aura = new Aura(AuraType.HAND, Effects.SetCost(1)) {
                Restless = true,
                Condition = new SelfCondition(p => p.Card.IsRace(Race.DRAGON)
                    && !p.Game.HistoryFacts.Query.Plays().Any(f => f.ControllerId == p.Controller.Id && f.Turn == p.Game.Turn
                        && f.Definition.ReadDefinition().IsRace(Race.DRAGON)))
            } };
        }
    }
}

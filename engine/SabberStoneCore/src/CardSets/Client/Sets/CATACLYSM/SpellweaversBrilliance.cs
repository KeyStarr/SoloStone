using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SpellweaversBrilliance
    {
        internal const int DbfId = 122454;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            // Rule: own direct spell damage this global turn includes every victim, armor and uncapped overkill (R5).
            card.Power = new Power {
                Aura = new AdaptiveCostEffect(p => p.Game.HistoryFacts.Query.SpellDamageAmount(
                    p.Controller.Id, p.Game.HistoryFacts.Query.ThisTurn)),
                PowerTask = new SummonTask("CATA_452t")
            };
        }
    }
}

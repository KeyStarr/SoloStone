using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class KronaKeeperOfEons
    {
        internal const int DbfId = 120762;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var cardInDeck in c.DeckZone.Take(5))
                    CostRules.AddModifier((Playable)cardInDeck, EffectOperator.SET, 1);
            }) };
        }
    }
}

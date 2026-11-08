using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NozdormuTheEternal
    {
        [StandardCard("CS3_035", 66870)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            StartOfGameRules.Declare(card, s => {
                s.Controller[NativeTailRules.NozdormuEnabled] = 1;
                s.Controller.Opponent[NativeTailRules.NozdormuEnabled] = 1;
            }, c => c.Opponent.DeckCards.Any(other => other.Id == "CS3_035"));
        }
    }
}

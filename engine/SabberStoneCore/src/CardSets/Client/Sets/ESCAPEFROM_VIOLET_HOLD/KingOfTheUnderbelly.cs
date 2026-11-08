using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class KingOfTheUnderbelly
    {
        [StandardCard("JAIL_831", 125998)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/JAIL_831/v1",
                new DiscoverThenContinueTask((g,c,s,k) => DeckbuildingRules.Remaining(c, DeckbuildingRules.Underbelly)
                    .Select(p => p.Card).Distinct().ToArray()),
                new CustomTask((g,c,s,t,k) => DeckbuildingRules.Receive(c, k.Playables.FirstOrDefault()))) };
        }
    }
}

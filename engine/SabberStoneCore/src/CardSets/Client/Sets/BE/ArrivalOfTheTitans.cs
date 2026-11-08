using SabberStoneCore.Enchants;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArrivalOfTheTitans
    {
        [StandardCard("BE_EVENT_101", 132671)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                ArrivalRules.Resolve(g, c, (IPlayable)s, CardType.SPELL, 6, "BE_EVENT_101e");
            }) };
        }

        [StandardCard("BE_EVENT_101e", 132672)]
        internal static void ConfigureWatch(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}

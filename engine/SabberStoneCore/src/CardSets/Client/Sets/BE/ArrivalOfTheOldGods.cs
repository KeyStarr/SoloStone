using SabberStoneCore.Enchants;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArrivalOfTheOldGods
    {
        [StandardCard("BE_EVENT_100", 132669)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                ArrivalRules.Resolve(g, c, (IPlayable)s, CardType.MINION, 5, "BE_EVENT_100e");
            }) };
        }

        [StandardCard("BE_EVENT_100e", 134807)]
        internal static void ConfigureWatch(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}

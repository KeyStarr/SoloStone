using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Tankgineer
    {
        // deathrattle-copy/paladin-tankgineer-summon-identity: owner-approved identity assumption.
        [StandardCard("TIME_017", 119965)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new SummonTask("GVG_079", SummonSide.DEATHRATTLE) };
        }
    }
}

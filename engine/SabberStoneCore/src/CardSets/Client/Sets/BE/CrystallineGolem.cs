using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CrystallineGolem
    {
        internal const int DbfId = 133415;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Game.ResolveReborns restores a fresh one-Health copy after deathrattles.
            card.Power = new Power { DeathrattleTask = new HealTask(3, EntityType.HERO) };
        }
    }
}

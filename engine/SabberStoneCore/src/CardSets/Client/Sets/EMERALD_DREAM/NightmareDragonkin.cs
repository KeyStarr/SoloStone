using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NightmareDragonkin
    {
        internal const int DbfId = 114496;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var right = c.HandZone.LastOrDefault();
                if (right != null) new ApplyEffectTask(EntityType.TARGET, Effects.ReduceCost(2)).Process(g, c, s, right, stack);
            }) };
        }
    }
}

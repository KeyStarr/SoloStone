using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BlackpawsWhip
    {
        internal const int DbfId = 125988;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Aura = new AdaptiveCostEffect(p => p.Controller.HandZone.Count(c => IsCoin(c.Card))),
                DeathrattleTask = new DrawTask()
            };
        }

        private static bool IsCoin(Card card)
            // These two legacy XML definitions predate the pinned client's Coin tag 2088.
            => card[(GameTag)2088] == 1 || card.AssetId == 1746 || card.AssetId == 58596;
    }
}

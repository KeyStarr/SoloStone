using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CrystalTusk
    {
        internal const int DbfId = 118484;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    var leftmost = c.HandZone.OrderBy(p => p.ZonePosition).FirstOrDefault();
                    if (leftmost != null) new MoveToDeck(EntityType.TARGET).Process(g, c, s, leftmost, stack);
                }),
                DeathrattleTask = new DrawTask(2)
            };
        }
    }
}

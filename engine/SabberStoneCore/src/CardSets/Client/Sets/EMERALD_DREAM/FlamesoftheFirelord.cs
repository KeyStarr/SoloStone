using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FlamesoftheFirelord
    {
        internal const int DbfId = 115650;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HoldingEightCostCard);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int damage = HoldingEightCostCard((IPlayable)s) ? 8 : 4;
                ComplexTask.Create(new RandomTask(1, EntityType.OP_MINIONS), new DamageTask(damage, EntityType.STACK, true))
                    .Process(g, c, s, null, stack);
            }) };
        }

        private static bool HoldingEightCostCard(IPlayable source) => source.Controller.HandZone
            .Any(p => p.Id != source.Id && p.Cost >= 8);
    }
}

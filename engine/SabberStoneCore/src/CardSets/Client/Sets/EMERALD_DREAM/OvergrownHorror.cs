using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class OvergrownHorror
    {
        internal const int DbfId = 118250;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var minion in c.HandZone.Where(p => p is Minion && DarkGifts.Has(p)))
                    new ApplyEffectTask(EntityType.TARGET, Effects.ReduceCost(2)).Process(g, c, s, minion, stack);
            }) };
        }
    }
}

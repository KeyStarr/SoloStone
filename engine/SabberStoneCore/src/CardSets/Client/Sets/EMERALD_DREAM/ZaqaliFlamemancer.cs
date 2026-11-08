using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ZaqaliFlamemancer
    {
        internal const int DbfId = 115654;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(OtherHandCostsAreUnique);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!OtherHandCostsAreUnique((IPlayable)s)) return;
                var hand = c.HandZone.Where(p => p.Id != s.Id).ToArray();
                foreach (var held in hand)
                    new ApplyEffectTask(EntityType.TARGET, Effects.ReduceCost(2)).Process(g, c, s, held, stack);
            }) };
        }

        private static bool OtherHandCostsAreUnique(IPlayable source)
        {
            var costs = source.Controller.HandZone.Where(p => p.Id != source.Id).Select(p => p.Cost).ToArray();
            return costs.Distinct().Count() == costs.Length;
        }
    }
}

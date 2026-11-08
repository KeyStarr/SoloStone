using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StoryOfTheWaygate
    {
        internal const int DbfId = 118265;
        // Rule: physical initial-deck membership survives same-entity transformations; generated copies are foreign (R2).
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var history = new EffectContext(g, c.Id, s.Id).History;
                foreach (var held in c.HandZone.Where(p => !history.StartedInDeck(p.Id, c.Id)).ToArray())
                    new ApplyEffectTask(EntityType.TARGET, Effects.ReduceCost(1)).Process(g, c, s, held, stack);
            }) };
        }
    }
}

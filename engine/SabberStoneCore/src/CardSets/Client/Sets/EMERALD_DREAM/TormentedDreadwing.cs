using System.Linq;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TormentedDreadwing
    {
        internal const int DbfId = 116673;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var drawStack = new TaskStack();
                ComplexTask.DrawFromDeck(2, SelfCondition.IsRace(Race.DRAGON)).Process(g, c, s, null, drawStack);
                foreach (var drawn in drawStack.Playables.Where(p => p.Zone == c.HandZone))
                    new ApplyEffectTask(EntityType.TARGET, Effects.ReduceCost(1)).Process(g, c, s, drawn, stack);
            }) };
        }
    }
}

using System.Linq;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GnomeMuncher
    {
        internal const int DbfId = 80819;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var targets = new TaskStack();
                    new IncludeTask(EntityType.ENEMIES).Process(g, c, s, null, targets);
                    new FilterStackTask(SelfCondition.IsNotDead).Process(g, c, s, null, targets);
                    if (targets.Playables.Count == 0) return;
                    int health = targets.Playables.Min(p => ((ICharacter)p).Health);
                    targets.Playables = targets.Playables.Where(p => ((ICharacter)p).Health == health).ToList();
                    // 25.6 fixed hero priority on ties: https://hearthstone.blizzard.com/en-gb/news/23921496/25-6-patch-notes
                    new RandomTask(1, EntityType.STACK).Process(g, c, s, null, targets);
                    new AttackTask(EntityType.SOURCE, EntityType.STACK).Process(g, c, s, null, targets);
                })
            } };
        }
    }
}

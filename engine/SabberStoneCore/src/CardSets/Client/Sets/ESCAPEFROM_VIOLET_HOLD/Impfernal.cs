using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Impfernal
    {
        [StandardCard("JAIL_398", 125918)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = StateTaskList.Chain(
                new CustomTask((g, c, s, t, stack) => {
                    var type = EntityType.ALL;
                    stack.Playables = IncludeTask.GetEntities(type, c, s, t, null).Where(p => p != s).ToList();
                }),
                new DamageTask(3, EntityType.STACK)) };
        }
    }
}

using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PressurePlate
    {
        internal const int DbfId = 126890;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Cat Trick's AFTER_CAST phase follows spell resolution and death processing.
            // Check remaining targets in the task, not pre-cast trigger validation.
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_CAST) {
                TriggerSource = TriggerSource.ENEMY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (!c.Opponent.BoardZone.Any(m => !m.ToBeDestroyed && !m.Untouchable)) return;
                    ComplexTask.Secret(ComplexTask.DestroyRandomTargets(1, EntityType.OP_MINIONS))
                        .Process(g, c, s, t, stack);
                })
            } };
        }
    }
}

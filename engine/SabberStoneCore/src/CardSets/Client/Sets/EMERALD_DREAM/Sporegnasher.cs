using SabberStoneCore.Actions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Sporegnasher
    {
        internal const int DbfId = 115652;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                stack = stack ?? new TaskStack();
                if (new RandomTask(1, EntityType.OP_MINIONS).Process(g, c, s, t, stack) == TaskState.STOP) return;
                var target = (Minion)stack.Playables[0];
                Generic.DamageCharFunc((IPlayable)s, target, 1, false);
            }) };
        }
    }
}

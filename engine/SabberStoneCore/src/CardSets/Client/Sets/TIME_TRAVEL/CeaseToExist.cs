using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CeaseToExist
    {
        internal const int DbfId = 120492;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var targets = new TaskStack();
                if (new RandomTask(1, EntityType.OP_MINIONS).Process(g, c, s, null, targets) == TaskState.STOP) return;
                var ctx = new EffectContext(g, c.Id, s.Id, stack: targets);
                ctx.RunLegacy(new SilenceTask(EntityType.STACK));
                ctx.RunLegacy(new DestroyTask(EntityType.STACK));
            }) };
        }
    }
}

using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ExperimentalAnimation
    {
        internal const int DbfId = 123546;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, source, target, stack) => {
                var ctx = new EffectContext(g, c.Id, source.Id, stack: stack);
                ctx.Herald();
                ctx.RunLegacy(new DamageTask(4, EntityType.OP_MINIONS, true));
            }) };
        }
    }
}

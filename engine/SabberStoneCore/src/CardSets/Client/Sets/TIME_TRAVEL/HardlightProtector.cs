using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HardlightProtector
    {
        internal const int DbfId = 119963;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => ctx.RunLegacy(ComplexTask.Create(
                new HealTask(3, EntityType.HERO), new SetGameTagTask(GameTag.DIVINE_SHIELD, 1, EntityType.HERO)))) };
        }
    }
}

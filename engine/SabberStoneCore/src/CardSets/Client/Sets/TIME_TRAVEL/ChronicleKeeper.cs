using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChronicleKeeper
    {
        internal const int DbfId = 120979;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(TimeTravelHandConditions.HoldingAnotherDragon);
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => ctx.RunLegacy(ComplexTask.Conditional(new SelfCondition(TimeTravelHandConditions.HoldingAnotherDragon),
                ComplexTask.Create(new SetGameTagTask(GameTag.TAUNT, 1, EntityType.SOURCE),
                    new SetGameTagTask(GameTag.DIVINE_SHIELD, 1, EntityType.SOURCE))))) };
        }
    }
}

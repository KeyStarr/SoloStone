using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BroodKeeper
    {
        internal const int DbfId = 114219;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(EmeraldDreamConditions.HoldingDragon);
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => ctx.RunLegacy(ComplexTask.Conditional(new SelfCondition(EmeraldDreamConditions.HoldingDragon),
                new WeaponTask("EDR_457t")))) };
        }
    }
}

using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WrathspikeBrute
    {
        internal const int DbfId = 129168;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.SELF_AS_TARGET,
                // Mortally wounded defenders retaliate before the attack's death-processing phase.
                SingleTask = new DamageTask(1, EntityType.ENEMIES)
            } };
        }
    }
}

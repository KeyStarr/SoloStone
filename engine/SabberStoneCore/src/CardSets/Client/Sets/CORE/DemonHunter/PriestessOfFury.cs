using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PriestessOfFury
    {
        internal const int DbfId = 120878;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                // Finish all missiles in this trigger; death cleanup belongs to the enclosing phase.
                SingleTask = ComplexTask.Repeat(ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), 6)
            } };
        }
    }
}

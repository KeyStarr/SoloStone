using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Dragonbane
    {
        internal const int DbfId = 102191;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Inspire runs after HeroPowerBlock resolves the power and processes deaths.
            card.Power = new Power { Trigger = new Trigger(TriggerType.INSPIRE) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 5)
            } };
        }
    }
}

using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SigilofCinder
    {
        internal const int DbfId = 115626;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Rule: one next-owner-start trigger; current Spell Damage increases the total number of one-damage missiles.
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                RemoveAfterTriggered = true,
                SingleTask = SplitHits.Inline(6, ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), deathProcessing: false, spellDmg: true)
            } };
        }
    }
}

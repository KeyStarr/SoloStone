using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MagmaHound
    {
        internal const int DbfId = 115751;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Wind-up Burglebot uses this same survival/defender condition after combat.
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.SELF,
                Condition = new SelfCondition(p => !p.ToBeDestroyed && p.Game.CurrentEventData?.EventTarget is Minion),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    SplitHits.Inline(((Minion)s).AttackDamage, ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), deathProcessing: false)
                        .Process(g, c, s, t, stack);
                })
            } };
        }
    }
}

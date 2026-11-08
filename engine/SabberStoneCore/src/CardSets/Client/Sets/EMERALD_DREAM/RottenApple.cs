using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RottenApple
    {
        internal const int DbfId = 115025;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Rule: two owner-end ticks beginning on the casting turn; delayed damage uses the original spell and current Spell Damage.
            card.Power = new Power { PowerTask = ComplexTask.Create(new HealTask(12, EntityType.HERO),
                new CustomTask((g, c, s, t, stack) => Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_482e"), (IPlayable)s, c, 2))) };
        }

        internal static void ConfigureTimer(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var timer = (Enchantment)s;
                    new DamageTask(3, EntityType.HERO, true).Process(g, c, timer.Creator, t, stack);
                    if (--timer[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0) timer.Remove();
                })
            } };
        }
    }
}

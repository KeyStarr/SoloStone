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
    internal static class FracturedPower
    {
        internal const int DbfId = 115033;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Rule: grant two full crystals at the second subsequent owner start, independently of the source spell.
            card.Power = new Power { PowerTask = ComplexTask.Create(new ManaCrystalEmptyTask(-1),
                new CustomTask((g, c, s, t, stack) => {
                    c.UsedMana = System.Math.Max(0, c.UsedMana);
                    Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_483e"), (IPlayable)s, c, 2);
                })) };
        }

        internal static void ConfigureTimer(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (--s[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0) return;
                    new ManaCrystalFullTask(2).Process(g, c, s, t, stack);
                    ((Enchantment)s).Remove();
                })
            } };
        }
    }
}

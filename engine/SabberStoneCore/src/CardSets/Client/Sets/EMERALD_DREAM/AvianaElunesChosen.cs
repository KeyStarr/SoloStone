using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AvianaElunesChosen
    {
        internal const int DbfId = 115264;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Rule: third subsequent owner start grants a permanent player-bound set-cost aura, surviving Aviana's death.
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) =>
                Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_895t"), (IPlayable)s, c, 3)) };
        }

        internal static void ConfigureCycle(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (--s[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0) return;
                    Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_895e"), (IPlayable)s, c);
                    ((Enchantment)s).Remove();
                })
            } };
        }

        internal static void ConfigureFullMoon(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Aura = new Aura(AuraType.HAND, Effects.SetCost(1)) };
        }
    }
}

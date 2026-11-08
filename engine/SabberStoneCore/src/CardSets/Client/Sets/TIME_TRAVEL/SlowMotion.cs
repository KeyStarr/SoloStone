using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SlowMotion
    {
        internal const int DbfId = 120829;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("TIME_716e3", EntityType.OP_CONTROLLER) };
        }

        internal static void ConfigureTax(Card card)
        {
            card.Implemented = true;
            // Loatheb's controller enchantment survives the caster turn and expires after the opponent turn.
            card.Power = new Power { Aura = new Aura(AuraType.OP_HAND, Effects.AddCost(1)) {
                RemoveTrigger = (TriggerType.TURN_END, SelfCondition.IsOpTurn)
            } };
        }
    }
}

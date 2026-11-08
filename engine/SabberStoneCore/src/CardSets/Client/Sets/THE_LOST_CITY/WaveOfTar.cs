using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WaveOfTar
    {
        internal const int DbfId = 117604;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = new DamageTask(2, EntityType.OP_MINIONS, true),
                Aura = new Aura(AuraType.OP_HAND, "TLC_439e2") {
                    Condition = SelfCondition.IsMinion,
                    RemoveTrigger = (TriggerType.TURN_END, SelfCondition.IsOpTurn)
                }
            };
        }

        internal static void ConfigureTax(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AddCost(2)) };
        }
    }
}
